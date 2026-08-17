using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using CardsChaos.Cards;
using CardsChaos.Cards.Album;
using UnityEngine;
using Vesolovsky.Core.Services;
using Vesolovsky.Core.Services.Save;
using Vesolovsky.Game.Services.Album;
using Vesolovsky.Game.Services.Skills;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Save
{
    /// <summary>
    /// Bridges the live room and the save. On load it rebuilds the world from the save - player
    /// pose, cards in hand, cards on the floor, skill cooldowns - and as a save contributor it
    /// writes that same state back just before every write.
    ///
    /// Cards are authored into the scene, so a fresh load starts with every copy resting in its
    /// authored place. Apply reshapes that starting point into the saved one: equal set-and-number
    /// cards live in per-identity pools, so duplicates are consumed as separate physical instances
    /// rather than collapsing in a dictionary. Container membership is restored explicitly.
    /// </summary>
    public class WorldSaveService : IInitializable, ITickable, IDisposable, ISaveContributor
    {
        // Below these a frame's drift is not worth marking the save dirty for.
        private const float MoveEpsilonSqr = 0.0001f;
        private const float TurnEpsilonDegrees = 0.5f;

        private readonly ISaveService<GameSave> _saveService;
        private readonly ISaveCoordinator _saveCoordinator;
        private readonly ICameraService _cameraService;
        private readonly ICameraHeading _cameraHeading;
        private readonly CardHand _hand;
        private readonly ICardCatalog _catalog;
        private readonly ICardFactory _cardFactory;
        private readonly ISkillService _skills;

        private readonly CancellationTokenSource _applyCts = new CancellationTokenSource();

        private Vector3 _lastMarkedPosition;
        private Quaternion _lastMarkedRotation = Quaternion.identity;

        // False until the loaded room has been applied. Guards capture and movement-dirtiness so
        // neither runs while the live scene is still the authored starting state.
        private bool _worldReady;

        [Inject]
        public WorldSaveService(
            ISaveService<GameSave> saveService,
            ISaveCoordinator saveCoordinator,
            ICameraService cameraService,
            ICameraHeading cameraHeading,
            CardHand hand,
            ICardCatalog catalog,
            ICardFactory cardFactory,
            [InjectOptional] ISkillService skills)
        {
            _saveService = saveService;
            _saveCoordinator = saveCoordinator;
            _cameraService = cameraService;
            _cameraHeading = cameraHeading;
            _hand = hand;
            _catalog = catalog;
            _cardFactory = cardFactory;
            _skills = skills;
        }

        public void Initialize()
        {
            _saveCoordinator.AddContributor(this);

            if (_hand != null)
                _hand.Changed += OnHandChanged;

            // The save loads asynchronously (GameSaveService is an IAsyncInitializable) and is not
            // in yet at Zenject init time - CurrentSave is still null. Reading it here would apply
            // nothing and leave the authored scene, so defer the apply until the save has landed,
            // the same lazy-read-after-load rule CollectionProgress and LocalCardAlbum follow.
            ApplyWhenLoaded(_applyCts.Token).Forget();
        }

        public void Dispose()
        {
            _applyCts.Cancel();
            _applyCts.Dispose();

            _saveCoordinator?.RemoveContributor(this);

            if (_hand != null)
                _hand.Changed -= OnHandChanged;
        }

        private async UniTask ApplyWhenLoaded(CancellationToken token)
        {
            bool canceled = await UniTask
                .WaitUntil(() => _saveService?.CurrentSave != null, cancellationToken: token)
                .SuppressCancellationThrow();

            if (canceled)
                return;

            GameSave save = _saveService.CurrentSave;

            // Runs after the camera controllers' own Initialize (execution order -20), so the saved
            // pose is the final word over the authored one. By now the authored cards have started
            // and frozen themselves; apply repositions and repurposes them from there.
            if (save.World != null)
                ApplyWorld(save.World, save.Album);

            RestoreCooldowns(save.SkillCooldowns);

            // Measure movement-dirtiness from wherever the camera ended up, restored or authored.
            Camera camera = _cameraService?.MainCamera;
            if (camera != null)
            {
                _lastMarkedPosition = camera.transform.position;
                _lastMarkedRotation = camera.transform.rotation;
            }

            // Only now is the live room the saved room, so capture and movement-dirty may run.
            _worldReady = true;
        }

        // Walking the room does not otherwise touch the save, so nudge it dirty when the camera has
        // actually moved. That is what lets autosave notice a stroll that never picked up a card.
        public void Tick()
        {
            if (!_worldReady)
                return;

            Camera camera = _cameraService?.MainCamera;
            if (camera == null || _saveCoordinator == null)
                return;

            Transform t = camera.transform;
            bool moved = (t.position - _lastMarkedPosition).sqrMagnitude > MoveEpsilonSqr;
            bool turned = Quaternion.Angle(t.rotation, _lastMarkedRotation) > TurnEpsilonDegrees;
            if (!moved && !turned)
                return;

            _lastMarkedPosition = t.position;
            _lastMarkedRotation = t.rotation;
            _saveCoordinator.MarkDirty();
        }

        public void CaptureForSave()
        {
            // Before the loaded room is applied, the live scene is still the authored starting
            // state; capturing it would overwrite the good save. The correct World and cooldowns
            // are already sitting in CurrentSave from the load, so leaving them be is right here.
            if (!_worldReady)
                return;

            GameSave save = _saveService?.CurrentSave;
            if (save == null)
                return;

            save.World = CaptureWorld();
            save.SkillCooldowns = CaptureCooldowns();
        }

        private WorldState CaptureWorld()
        {
            var world = new WorldState
            {
                HandLayout = (_hand != null ? _hand.Layout : CardHandLayout.Pile).ToString(),
                HeldCards = new List<SavedCard>(),
                GroundCards = new List<SavedGroundCard>(),
            };

            Camera camera = _cameraService?.MainCamera;
            if (camera != null)
            {
                world.PlayerPosition = new SaveVector3(camera.transform.position);
                world.PlayerRotation = new SaveQuaternion(camera.transform.rotation);
            }

            if (_hand != null)
            {
                foreach (Card card in _hand.Cards)
                {
                    if (TryReadIdentity(card, out string setId, out int number))
                        world.HeldCards.Add(new SavedCard { SetId = setId, Number = number });
                }
            }

            // Every resting card in the room. Held cards report IsHeld and are captured above; filed
            // cards no longer exist as objects. A card mid-throw is caught at its current pose and
            // will simply be frozen there on load.
            foreach (Card card in UnityEngine.Object.FindObjectsByType<Card>(FindObjectsSortMode.None))
            {
                if (card == null || card.IsHeld || !TryReadIdentity(card, out string setId, out int number))
                    continue;

                Transform t = card.transform;
                var saved = new SavedGroundCard
                {
                    SetId = setId,
                    Number = number,
                    Position = new SaveVector3(t.position),
                    Rotation = new SaveQuaternion(t.rotation),
                };

                CardStackContainer container = card.GetComponentInParent<CardStackContainer>();
                if (container != null && container.TryGetSavedPlacement(
                        card, out int slot, out Vector3 localPosition,
                        out Quaternion localRotation))
                {
                    saved.ContainerId = container.ContainerId;
                    saved.ContainerSlot = slot;
                    saved.ContainerLocalPosition = new SaveVector3(localPosition);
                    saved.ContainerLocalRotation = new SaveQuaternion(localRotation);
                }

                world.GroundCards.Add(saved);
            }

            return world;
        }

        private List<SkillCooldownState> CaptureCooldowns()
        {
            var cooldowns = new List<SkillCooldownState>();
            if (_skills == null)
                return cooldowns;

            foreach (SkillCooldownSnapshot cd in _skills.GetActiveCooldowns())
            {
                cooldowns.Add(new SkillCooldownState
                {
                    SkillId = cd.Id.ToString(),
                    Remaining = cd.Remaining,
                    Total = cd.Total,
                });
            }

            return cooldowns;
        }

        private void ApplyWorld(WorldState world, List<AlbumPlacement> album)
        {
            Dictionary<CardRef, List<Card>> byRef = MapSceneCards();

            RemoveFiledCards(byRef, album);

            var held = new List<Card>();
            if (world.HeldCards != null)
            {
                Vector3 handPosition = _hand != null ? _hand.transform.position : Vector3.zero;
                Quaternion handRotation = _hand != null ? _hand.transform.rotation : Quaternion.identity;

                foreach (SavedCard saved in world.HeldCards)
                {
                    Card card = TakeOrCreate(
                        byRef, saved.SetId, saved.Number, handPosition, handRotation,
                        preferredContainerId: null,
                        preferOutsideContainers: true);
                    if (card != null)
                        held.Add(card);
                }
            }

            _hand?.Restore(held, ParseLayout(world.HandLayout));

            if (world.GroundCards != null)
            {
                foreach (SavedGroundCard saved in world.GroundCards)
                {
                    Vector3 position = saved.Position.ToVector3();
                    Quaternion rotation = saved.Rotation.ToQuaternion();

                    Card card = TakeOrCreate(
                        byRef, saved.SetId, saved.Number, position, rotation, saved.ContainerId,
                        preferOutsideContainers: false);
                    if (card == null)
                        continue;

                    if (!string.IsNullOrEmpty(saved.ContainerId) &&
                        CardStackContainer.TryFindById(saved.ContainerId, out CardStackContainer container) &&
                        container.RestoreCard(
                            card,
                            saved.ContainerSlot,
                            saved.ContainerLocalPosition.ToVector3(),
                            saved.ContainerLocalRotation.ToQuaternion()))
                    {
                        continue;
                    }

                    // Saves written before container metadata existed still carry the exact world
                    // pose. If the nearest authored copy is already a child of a container at that
                    // same pose, preserve that membership as a lossless best-effort migration.
                    CardStackContainer legacyContainer =
                        card.GetComponentInParent<CardStackContainer>();
                    if (string.IsNullOrEmpty(saved.ContainerId) && legacyContainer != null &&
                        (card.transform.position - position).sqrMagnitude <= 0.0004f &&
                        legacyContainer.TryGetSavedPlacement(
                            card, out int legacySlot, out _, out _) &&
                        legacyContainer.RestoreCard(
                            card,
                            legacySlot,
                            legacyContainer.transform.InverseTransformPoint(position),
                            Quaternion.Inverse(legacyContainer.transform.rotation) * rotation))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(saved.ContainerId))
                    {
                        Debug.LogWarning(
                            $"[{nameof(WorldSaveService)}] Could not restore {saved.SetId}#" +
                            $"{saved.Number} into container '{saved.ContainerId}' slot " +
                            $"{saved.ContainerSlot}; placed it in the room instead.");
                    }

                    PlaceOnGround(card, position, rotation);
                }
            }

            // Whatever is left in byRef is a card the save never mentioned - new content added since
            // the save was written. Left where the scene authored it.

            SettleHouses();

            ApplyPlayerPose(world);
        }

        private Dictionary<CardRef, List<Card>> MapSceneCards()
        {
            var byRef = new Dictionary<CardRef, List<Card>>();

            foreach (Card card in UnityEngine.Object.FindObjectsByType<Card>(FindObjectsSortMode.None))
            {
                if (card == null || !TryReadIdentity(card, out string setId, out int number))
                    continue;

                var key = new CardRef(setId, number);
                if (!byRef.TryGetValue(key, out List<Card> copies))
                    byRef[key] = copies = new List<Card>();

                copies.Add(card);
            }

            return byRef;
        }

        private static void RemoveFiledCards(
            Dictionary<CardRef, List<Card>> byRef,
            List<AlbumPlacement> album)
        {
            if (album == null)
                return;

            foreach (AlbumPlacement placement in album)
            {
                if (placement == null || string.IsNullOrEmpty(placement.CardSetId))
                    continue;

                var key = new CardRef(placement.CardSetId, placement.CardNumber);
                Card card = TakeCandidate(byRef, key, Vector3.zero, preferredContainerId: null,
                    preferOutsideContainers: true);
                if (card == null)
                    continue;

                UnityEngine.Object.Destroy(card.gameObject);
            }
        }

        private Card TakeOrCreate(
            Dictionary<CardRef, List<Card>> byRef,
            string setId,
            int number,
            Vector3 position,
            Quaternion rotation,
            string preferredContainerId,
            bool preferOutsideContainers)
        {
            var key = new CardRef(setId, number);
            Card existing = TakeCandidate(
                byRef,
                key,
                position,
                preferredContainerId,
                preferOutsideContainers);
            if (existing != null)
                return existing;

            // Not authored in this scene (e.g. content added after the save, or a trimmed scene) -
            // build it from the catalog prefab so the save is still honoured.
            Card prefab = ResolvePrefab(key);
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[{nameof(WorldSaveService)}] Save names {key} but the catalog cannot build it.");

                return null;
            }

            return _cardFactory.Create(prefab, position, rotation);
        }

        private static Card TakeCandidate(
            Dictionary<CardRef, List<Card>> byRef,
            CardRef key,
            Vector3 wantedPosition,
            string preferredContainerId,
            bool preferOutsideContainers)
        {
            if (!byRef.TryGetValue(key, out List<Card> copies) || copies.Count == 0)
                return null;

            int bestIndex = -1;
            float bestScore = float.PositiveInfinity;

            for (int i = 0; i < copies.Count; i++)
            {
                Card candidate = copies[i];
                if (candidate == null)
                    continue;

                CardStackContainer container = candidate.GetComponentInParent<CardStackContainer>();
                float preference = 0f;
                if (!string.IsNullOrEmpty(preferredContainerId))
                    preference = container != null && container.ContainerId == preferredContainerId ? 0f : 1000000f;
                else if (preferOutsideContainers && container != null)
                    preference = 1000000f;

                float score = preference + (candidate.transform.position - wantedPosition).sqrMagnitude;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                bestIndex = i;
            }

            if (bestIndex < 0)
                return null;

            Card chosen = copies[bestIndex];
            copies.RemoveAt(bestIndex);
            if (copies.Count == 0)
                byRef.Remove(key);

            return chosen;
        }

        private static void PlaceOnGround(Card card, Vector3 position, Quaternion rotation)
        {
            card.StopAnimation();

            // A card standing in a house of cards keeps its place under that house. The house reads
            // "am I still whole" off each member's pose in its own local space, so cutting the card
            // loose here - as every other ground card is - left every loaded house permanently
            // uncollapsible: its members' local poses were suddenly world poses and could never
            // match again. Restoring the world pose in place re-derives the same local pose the
            // card was saved at, so a house left standing loads standing and still comes down.
            // A house that was already down is cut loose afterwards, by SettleHouses.
            if (card.House == null)
                card.transform.SetParent(null, worldPositionStays: true);

            card.transform.SetPositionAndRotation(position, rotation);
            card.FreezeInPlace();
        }

        /// <summary>
        /// Lets every house of cards look at the room the save just rebuilt and decide whether it is
        /// still standing. Run once the last card is in place, because a house cannot tell until
        /// all of its members are where the save wants them.
        /// </summary>
        private static void SettleHouses()
        {
            foreach (CardHouse house in
                     UnityEngine.Object.FindObjectsByType<CardHouse>(FindObjectsSortMode.None))
            {
                if (house != null)
                    house.SettleAfterRestore();
            }
        }

        private void ApplyPlayerPose(WorldState world)
        {
            Camera camera = _cameraService?.MainCamera;
            if (camera == null)
                return;

            // A never-captured pose is all zeros - an invalid quaternion. Skip it rather than
            // teleport the camera to the origin facing nowhere. (Quaternion.== can't detect this:
            // its dot-product test reads two zero quaternions as unequal, so check the fields.)
            SaveQuaternion r = world.PlayerRotation;
            if (r.X == 0f && r.Y == 0f && r.Z == 0f && r.W == 0f)
                return;

            camera.transform.position = world.PlayerPosition.ToVector3();
            _cameraHeading?.SetHeading(r.ToQuaternion().eulerAngles.y);
        }

        private void RestoreCooldowns(List<SkillCooldownState> cooldowns)
        {
            if (cooldowns == null || _skills == null)
                return;

            foreach (SkillCooldownState cd in cooldowns)
            {
                if (cd == null || string.IsNullOrEmpty(cd.SkillId))
                    continue;

                if (Enum.TryParse(cd.SkillId, out SkillId id))
                    _skills.RestoreCooldown(id, cd.Remaining, cd.Total);
                else
                    Debug.LogWarning($"[{nameof(WorldSaveService)}] Save has an unknown skill '{cd.SkillId}'.");
            }
        }

        private Card ResolvePrefab(CardRef card)
        {
            CardSetDefinition set = _catalog?.FindSet(card.SetId);
            if (set == null || !set.TryGetCard(card.Number, out CardIdentity identity))
                return null;

            return identity.TryGetComponent(out Card prefab) ? prefab : null;
        }

        private void OnHandChanged() => _saveCoordinator?.MarkDirty();

        private static bool TryReadIdentity(Card card, out string setId, out int number)
        {
            setId = null;
            number = 0;

            CardIdentity identity = card != null ? card.Identity : null;
            if (identity == null || string.IsNullOrEmpty(identity.SetId))
                return false;

            setId = identity.SetId;
            number = identity.Number;
            return true;
        }

        private static CardHandLayout ParseLayout(string layout)
        {
            return Enum.TryParse(layout, out CardHandLayout parsed) ? parsed : CardHandLayout.Pile;
        }
    }
}
