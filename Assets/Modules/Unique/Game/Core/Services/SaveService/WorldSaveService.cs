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
    /// Cards are authored into the scene, so a fresh load always starts with every card resting on
    /// the floor. Apply reshapes that authored starting point into the saved one: it repositions
    /// the floor cards, lifts the held ones into the hand, and removes the ones now filed away.
    /// Cards are keyed by set-and-number, which the game guarantees is unique per physical card.
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
                world.GroundCards.Add(new SavedGroundCard
                {
                    SetId = setId,
                    Number = number,
                    Position = new SaveVector3(t.position),
                    Rotation = new SaveQuaternion(t.rotation),
                });
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
            Dictionary<CardRef, Card> byRef = MapSceneCards();

            RemoveFiledCards(byRef, album);

            var held = new List<Card>();
            if (world.HeldCards != null)
            {
                Vector3 handPosition = _hand != null ? _hand.transform.position : Vector3.zero;
                Quaternion handRotation = _hand != null ? _hand.transform.rotation : Quaternion.identity;

                foreach (SavedCard saved in world.HeldCards)
                {
                    Card card = TakeOrCreate(byRef, saved.SetId, saved.Number, handPosition, handRotation);
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

                    Card card = TakeOrCreate(byRef, saved.SetId, saved.Number, position, rotation);
                    if (card != null)
                        PlaceOnGround(card, position, rotation);
                }
            }

            // Whatever is left in byRef is a card the save never mentioned - new content added since
            // the save was written. Left where the scene authored it.

            ApplyPlayerPose(world);
        }

        private Dictionary<CardRef, Card> MapSceneCards()
        {
            var byRef = new Dictionary<CardRef, Card>();

            foreach (Card card in UnityEngine.Object.FindObjectsByType<Card>(FindObjectsSortMode.None))
            {
                if (card == null || !TryReadIdentity(card, out string setId, out int number))
                    continue;

                var key = new CardRef(setId, number);
                if (!byRef.TryAdd(key, card))
                {
                    Debug.LogWarning(
                        $"[{nameof(WorldSaveService)}] Two scene cards are {key}; the save assumes " +
                        "each card is unique, so the duplicate is left where it was authored.", card);
                }
            }

            return byRef;
        }

        private static void RemoveFiledCards(Dictionary<CardRef, Card> byRef, List<AlbumPlacement> album)
        {
            if (album == null)
                return;

            foreach (AlbumPlacement placement in album)
            {
                if (placement == null || string.IsNullOrEmpty(placement.CardSetId))
                    continue;

                var key = new CardRef(placement.CardSetId, placement.CardNumber);
                if (!byRef.TryGetValue(key, out Card card))
                    continue;

                byRef.Remove(key);
                if (card != null)
                    UnityEngine.Object.Destroy(card.gameObject);
            }
        }

        private Card TakeOrCreate(
            Dictionary<CardRef, Card> byRef, string setId, int number, Vector3 position, Quaternion rotation)
        {
            var key = new CardRef(setId, number);
            if (byRef.TryGetValue(key, out Card existing))
            {
                byRef.Remove(key);
                return existing;
            }

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

        private static void PlaceOnGround(Card card, Vector3 position, Quaternion rotation)
        {
            card.StopAnimation();
            card.transform.SetPositionAndRotation(position, rotation);
            card.FreezeInPlace();
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
