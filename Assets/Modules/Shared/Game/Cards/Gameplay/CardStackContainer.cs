using System;
using System.Collections;
using System.Collections.Generic;
using CardsChaos.Cards.Album;
using PrimeTween;
using RoboRyanTron.SearchableEnum;
using UnityEngine;
using UnityEngine.Rendering;
using Vesolovsky.Game.Services.Hud;
using Vesolovsky.Game.Views.GameplayHud;
using Zenject;

namespace CardsChaos.Cards
{
    /// <summary>
    /// A fixed grid of vertical card stacks. The component owns only the physical layout; cards
    /// themselves remain ordinary <see cref="Card"/> objects and can be picked back up normally.
    /// Occupancy is derived from the current children, so authored test cards and cards removed at
    /// runtime cannot leave a stale slot count behind.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CardsChaos/Card Stack Container")]
    public sealed class CardStackContainer : MonoBehaviour
    {
        private const string GhostMaterialResourcePath = "M_Card_PlacementGhost";
        private const float OcclusionTolerance = 0.01f;

        // An automatic placement is chosen without a ray. The distance only ever feeds the cursor's
        // occlusion test, so a target built this way carries none.
        private const float AutoPlacementRayDistance = 0f;

        private static readonly int FrontTexId = Shader.PropertyToID("_FrontTex");
        private static readonly int BackTexId = Shader.PropertyToID("_BackTex");
        private static readonly int FrontTexStId = Shader.PropertyToID("_FrontTex_ST");
        private static readonly int BackTexStId = Shader.PropertyToID("_BackTex_ST");
        private static readonly int GhostTintId = Shader.PropertyToID("_GhostTint");

        // Tried in order as multiples of the sweep the tuning asked for: the same bend the other
        // way, then wider each way. Shared and static because they are the same for every box.
        private static readonly float[] ArcAlternatives = { -1f, 1.5f, -1.5f, 2.25f, -2.25f };

        // One buffer for the look-ahead sweeps. They all run inside a single call, one after
        // another, so there is nothing to keep between them.
        private static readonly RaycastHit[] ObstacleHits = new RaycastHit[8];

        private static readonly List<CardStackContainer> Instances =
            new List<CardStackContainer>();
        private static readonly Dictionary<CardRef, int> StoredCardCounts =
            new Dictionary<CardRef, int>();

        private static bool _storedCardCountsDirty = true;

        /// <summary>Raised when a physical card enters or leaves any duplicate container.</summary>
        public static event Action ContentsChanged;

        [Header("Identity")]
        [Tooltip("Stable id written into the world save. It must be unique among card containers.")]
        [SerializeField] private string containerId = "duplicate-container";

        [Header("Grid")]
        [SerializeField, Min(1)] private int columns = 6;
        [SerializeField, Min(1)] private int rows = 3;

        [Tooltip("Local X/Z centre of the whole grid.")]
        [SerializeField] private Vector2 gridCenter = new Vector2(-0.0007f, -0.0018f);

        [SerializeField, Min(0.0001f)] private float columnSpacing = 0.0456f;
        [SerializeField, Min(0.0001f)] private float rowSpacing = 0.06555f;

        [Header("Stacks")]
        [Tooltip("Local Y centre of the first card in an empty slot.")]
        [SerializeField] private float baseCenterY = 0.0186f;

        [Tooltip("Local centre-to-centre rise. It exceeds the card collider thickness slightly, " +
                 "so neighbouring static colliders never overlap.")]
        [SerializeField, Min(0.0001f)] private float stackStep = 0.00245f;

        [Tooltip("The highest legal card centre, kept below the container rim.")]
        [SerializeField] private float maxCenterY = 0.102f;

        [SerializeField, Min(1)] private int maxCardsPerSlot = 32;

        [Header("Interaction")]
        [Tooltip("Maximum camera-to-container pointing distance in world metres.")]
        [SerializeField, Min(0.1f)] private float interactionDistance = 2f;

        [SerializeField] private Color ghostTint = new Color(0.2f, 0.9f, 1f, 0.5f);

        [Header("Placement Animation")]
        [SerializeField, Min(0f)] private float placementDuration = 0.45f;

        [Tooltip("Sideways bend of the flight path in container-local units. The Bezier path " +
                 "reaches half this offset at its widest point.")]
        [SerializeField, Min(0f)] private float placementSideArc = 0.06f;

        [Tooltip("Temporary in-plane roll at the middle of the flight; zero again on landing.")]
        [SerializeField, Range(0f, 20f)] private float placementRollDegrees = 9f;

        [Tooltip("Small temporary pitch layered over the rotation toward the flat slot pose.")]
        [SerializeField, Range(0f, 12f)] private float placementPitchDegrees = 4f;

        [Header("Flourish (a card that files itself)")]
        [Tooltip("A card the player did not aim crosses the whole room, so its flight is timed and " +
                 "bent by how far it has to go rather than by the fixed numbers above. All lengths " +
                 "here are container-local: the box is scaled up, so one local unit is more than a " +
                 "metre of room.")]
        [SerializeField, Min(0f)] private float flourishDuration = 0.85f;

        [Tooltip("Added to the duration for each local unit of travel.")]
        [SerializeField, Min(0f)] private float flourishSecondsPerUnit = 0.18f;

        [Tooltip("However far the card comes from, it never floats for longer than this.")]
        [SerializeField, Min(0.1f)] private float flourishMaxDuration = 2f;

        [Tooltip("How far the path bows out to the side, as a fraction of the distance travelled. " +
                 "The bend is level: the card sinks from wherever it was thrown to the slot in a " +
                 "straight descent and does all its curving sideways, because a player looking " +
                 "down at the floor would never see a card that arced overhead. The Bezier path " +
                 "reaches half this offset at its widest point.")]
        [SerializeField, Range(0f, 1f)] private float flourishBow = 0.3f;

        [SerializeField, Min(0f)] private float flourishMinBow = 0.15f;
        [SerializeField, Min(0f)] private float flourishMaxBow = 1.5f;

        [Tooltip("Full turns about the box's upright axis on the way in. Whole turns land the card " +
                 "square with the stack; a half turn would leave its face upside down.")]
        [SerializeField, Range(0f, 3f)] private int flourishTurns = 1;

        [Tooltip("Temporary roll and pitch at the middle of the flight; zero again on landing.")]
        [SerializeField, Range(0f, 30f)] private float flourishRollDegrees = 14f;

        [SerializeField, Range(0f, 20f)] private float flourishPitchDegrees = 7f;

        [Tooltip("Ease-in-out reads as floating; the card lifts away slowly and settles slowly.")]
        [SerializeField, SearchableEnum] private Ease flourishEase = Ease.InOutSine;

        [Tooltip("What the card tries not to sail through on its way out. Other cards are never " +
                 "treated as obstacles whatever this says - the floor is covered in them and the " +
                 "card is meant to pass over. Defaults to everything but Ignore Raycast.")]
        [SerializeField] private LayerMask flourishObstacles = ~(1 << 2);

        [Tooltip("Radius of the sweep that looks ahead, in metres of room - about half a card.")]
        [SerializeField, Min(0f)] private float flourishClearance = 0.04f;

        [Tooltip("How much of the flight is looked at, measured from the throw. Only the first " +
                 "stretch is worth bending: past that the card is away across the room and the " +
                 "player has looked back at the floor. Zero turns the check off entirely.")]
        [SerializeField, Range(0f, 1f)] private float flourishCheckedPart = 0.45f;

        [Tooltip("How many sweeps that stretch is broken into.")]
        [SerializeField, Range(2, 12)] private int flourishCheckSteps = 5;

        private readonly List<Card> _childCards = new List<Card>();
        private readonly List<PlacementReservation> _reservations =
            new List<PlacementReservation>();

        private int[] _stackCounts;
        private float[] _highestLocalY;
        private Card[] _topCards;

        // Aiming at a box asks for its slot heights every frame, and working them out means walking
        // every card in it - hundreds of them once the collection is under way. The answer only
        // moves when the box's contents do, and the box is told when that happens, so the walk runs
        // on a change rather than on a look.
        private bool _stacksDirty = true;
        private Material _ghostMaterial;
        private MaterialPropertyBlock _ghostProperties;
        private MaterialPropertyBlock _sourceProperties;

        private Card _ghostCard;
        private SlotTarget _ghostTarget;
        private Camera _ghostCamera;
        private int _ghostFrame = -1;
        private IHudHints _hudHints;
        private IDuplicateCards _duplicates;

        public enum StoreRejection
        {
            None,
            InvalidCard,
            AlreadyStored,

            /// <summary>
            /// The card was only ever authored once, so there is no spare copy of it - putting this
            /// one in a box would leave its album slot with nothing left to fill it.
            /// </summary>
            NotADuplicate,
        }

        /// <summary>How a card travels the last stretch into its slot.</summary>
        public enum PlacementFlight
        {
            /// <summary>Already there - a debug fill has no time to spend on flights.</summary>
            Instant,

            /// <summary>The short hop from a hand held over the box, aimed by the player.</summary>
            Placed,

            /// <summary>
            /// The long one, for a card that files itself from wherever the player happened to be
            /// standing: swept out to the side, turning, and timed by the distance it has to cross.
            /// </summary>
            Flourish,
        }

        /// <summary>
        /// The three passes an automatic placement walks, in order of preference. Only the first
        /// one looks at the card being stored; the other two are plain "where is there room".
        /// </summary>
        private enum AutoPlacementPass
        {
            /// <summary>A stack already showing a card of the same set, with room for one more.</summary>
            SameSetOnTop,

            /// <summary>An untouched slot, so a new set starts its own stack.</summary>
            EmptySlot,

            /// <summary>Anywhere at all that still takes a card.</summary>
            AnyRoom,
        }

        private readonly struct PlacementReservation
        {
            public readonly Card Card;
            public readonly int SlotIndex;
            public readonly Vector3 LocalPosition;
            public readonly Quaternion LocalRotation;

            public PlacementReservation(
                Card card,
                int slotIndex,
                Vector3 localPosition,
                Quaternion localRotation)
            {
                Card = card;
                SlotIndex = slotIndex;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
            }
        }

        public string ContainerId => containerId;

        /// <summary>
        /// How many cards fit when every stack is full - the grid times whichever runs out first,
        /// the per-stack limit or the height under the rim. What the duplicate quota has to stay
        /// under, since every duplicate in the game is meant to end up in one box.
        /// </summary>
        public int Capacity
        {
            get
            {
                int byHeight = stackStep > 0f
                    ? Mathf.FloorToInt((maxCenterY - baseCenterY) / stackStep) + 1
                    : maxCardsPerSlot;

                return Mathf.Max(1, columns) * Mathf.Max(1, rows) *
                       Mathf.Max(0, Mathf.Min(maxCardsPerSlot, byHeight));
            }
        }

        [Inject]
        private void Construct(
            [InjectOptional] IHudHints hudHints,
            [InjectOptional] IDuplicateCards duplicates)
        {
            _hudHints = hudHints;
            _duplicates = duplicates;
        }

        public readonly struct SlotTarget
        {
            public readonly int SlotIndex;
            public readonly Vector3 LocalPosition;
            public readonly Quaternion LocalRotation;
            public readonly float RayDistance;

            // A default-initialized struct has SlotIndex == 0, but it was never produced by a
            // successful slot query, and a slot the cursor found but cannot take another card is
            // built with -1. Carried as a field rather than derived from the ray distance, because
            // an automatic placement has no ray behind it at all.
            public readonly bool IsValid;

            public SlotTarget(
                int slotIndex,
                Vector3 localPosition,
                Quaternion localRotation,
                float rayDistance)
            {
                SlotIndex = slotIndex;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                RayDistance = rayDistance;
                IsValid = slotIndex >= 0;
            }
        }

        private void OnEnable()
        {
            if (!Instances.Contains(this))
                Instances.Add(this);

            _storedCardCountsDirty = true;
            MarkStacksDirty();
            EnsureStackBuffers();
        }

        private void OnDisable()
        {
            Instances.Remove(this);
            _storedCardCountsDirty = true;
            MarkStacksDirty();
            _ghostFrame = -1;

            // A disabled container no longer advances its completion coroutines. Finish any cards
            // it already accepted so none can be stranded mid-air with a phantom reservation.
            for (int i = 0; i < _reservations.Count; i++)
                SnapToReservation(_reservations[i]);

            _reservations.Clear();

            if (Application.isPlaying)
                ContentsChanged?.Invoke();
        }

        private void OnTransformChildrenChanged()
        {
            _storedCardCountsDirty = true;
            MarkStacksDirty();

            if (!Application.isPlaying)
                return;

            ContentsChanged?.Invoke();
        }

        private void OnValidate()
        {
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);
            columnSpacing = Mathf.Max(0.0001f, columnSpacing);
            rowSpacing = Mathf.Max(0.0001f, rowSpacing);
            stackStep = Mathf.Max(0.0001f, stackStep);
            maxCardsPerSlot = Mathf.Max(1, maxCardsPerSlot);
            interactionDistance = Mathf.Max(0.1f, interactionDistance);
            MarkStacksDirty();
            placementDuration = Mathf.Max(0f, placementDuration);
            placementSideArc = Mathf.Max(0f, placementSideArc);
            placementRollDegrees = Mathf.Clamp(placementRollDegrees, 0f, 20f);
            placementPitchDegrees = Mathf.Clamp(placementPitchDegrees, 0f, 12f);

            if (maxCenterY < baseCenterY)
                maxCenterY = baseCenterY;

            EnsureStackBuffers();
        }

        /// <summary>
        /// Finds the closest visible container slot under a ray. The ordinary physics hit is used
        /// only as an occlusion guard: the open interior deliberately has no extra collider, so it
        /// cannot block picking stored cards back up when the hand is empty.
        /// </summary>
        public static bool TryFindTarget(
            Ray ray,
            float obstructionDistance,
            CardStackContainer obstructionOwner,
            out CardStackContainer container,
            out SlotTarget target)
        {
            container = null;
            target = default;
            float bestDistance = float.PositiveInfinity;
            SlotTarget bestTarget = default;

            for (int i = Instances.Count - 1; i >= 0; i--)
            {
                CardStackContainer candidate = Instances[i];
                if (candidate == null)
                {
                    Instances.RemoveAt(i);
                    continue;
                }

                if (!candidate.TryGetPointedTarget(ray, out SlotTarget pointedTarget,
                        out float distance))
                    continue;

                bool obstructionBelongsToCandidate = obstructionOwner == candidate;
                if (!obstructionBelongsToCandidate &&
                    distance > obstructionDistance + OcclusionTolerance)
                {
                    continue;
                }

                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                container = candidate;
                bestTarget = pointedTarget;
            }

            if (container == null)
                return false;

            target = bestTarget;
            return true;
        }

        public static bool IsStored(Card card)
        {
            return card != null && card.GetComponentInParent<CardStackContainer>() != null;
        }

        /// <summary>
        /// How many physical copies of each card are in the boxes right now. Rebuilt only after a
        /// container's children actually changed, so the collection tally can be recomputed off the
        /// containers alone rather than by sweeping every card in the room.
        /// </summary>
        public static IReadOnlyDictionary<CardRef, int> StoredCards
        {
            get
            {
                RefreshStoredCardCounts();
                return StoredCardCounts;
            }
        }

        /// <summary>
        /// Picks where a card should land when nobody is aiming: the top of a stack that already
        /// shows a card of the same set and still has room, otherwise the first empty slot,
        /// otherwise the first slot anywhere that still takes a card. Containers are searched in
        /// scene order and slots in grid order, so the boxes fill predictably rather than by
        /// whichever container happened to register first.
        /// </summary>
        public static bool TryFindAutoPlacement(
            Card card,
            out CardStackContainer container,
            out SlotTarget target)
        {
            container = null;
            target = default;

            CardRef cardRef = CardRef.From(card != null ? card.Identity : null);
            if (!cardRef.IsValid)
                return false;

            return TryFindAutoPlacement(cardRef.SetId, AutoPlacementPass.SameSetOnTop, out container, out target)
                   || TryFindAutoPlacement(null, AutoPlacementPass.EmptySlot, out container, out target)
                   || TryFindAutoPlacement(null, AutoPlacementPass.AnyRoom, out container, out target);
        }

        private static bool TryFindAutoPlacement(
            string setId,
            AutoPlacementPass pass,
            out CardStackContainer container,
            out SlotTarget target)
        {
            container = null;
            target = default;

            for (int i = 0; i < Instances.Count; i++)
            {
                CardStackContainer candidate = Instances[i];
                if (candidate == null || !candidate.isActiveAndEnabled)
                    continue;

                if (!candidate.TryFindAutoSlot(setId, pass, out target))
                    continue;

                container = candidate;
                return true;
            }

            return false;
        }

        private bool TryFindAutoSlot(string setId, AutoPlacementPass pass, out SlotTarget target)
        {
            target = default;
            EnsureStackBuffers();
            RefreshStacks();

            for (int slotIndex = 0; slotIndex < _stackCounts.Length; slotIndex++)
            {
                switch (pass)
                {
                    case AutoPlacementPass.SameSetOnTop:
                        Card top = _topCards[slotIndex];
                        if (top == null || top.Identity == null || top.Identity.SetId != setId)
                            continue;

                        break;

                    case AutoPlacementPass.EmptySlot:
                        if (_stackCounts[slotIndex] > 0)
                            continue;

                        break;
                }

                // The slot may still be full or over the rim; that is exactly what this rejects,
                // and the search simply moves on to the next one.
                if (TryBuildTargetFromCurrentStacks(slotIndex, AutoPlacementRayDistance, out target))
                    return true;
            }

            return false;
        }

        public static bool TryFindById(string id, out CardStackContainer container)
        {
            container = null;
            if (string.IsNullOrEmpty(id))
                return false;

            foreach (CardStackContainer candidate in Instances)
            {
                if (candidate == null || candidate.containerId != id)
                    continue;

                if (container != null && container != candidate)
                {
                    Debug.LogError(
                        $"[{nameof(CardStackContainer)}] More than one container uses id '{id}'.",
                        candidate);
                    return false;
                }

                container = candidate;
            }

            return container != null;
        }

        public bool CanStore(Card card, out StoreRejection rejection)
        {
            CardRef cardRef = CardRef.From(card != null ? card.Identity : null);
            if (!cardRef.IsValid)
            {
                rejection = StoreRejection.InvalidCard;
                return false;
            }

            // The box is for spare copies. A card the room only holds once has to end up in the
            // album, and a box would be a dead end for it - nothing could ever fill its slot again.
            if (_duplicates != null && !_duplicates.HasDuplicate(cardRef))
            {
                rejection = StoreRejection.NotADuplicate;
                return false;
            }

            if (IsCardRefStored(cardRef, card))
            {
                rejection = StoreRejection.AlreadyStored;
                return false;
            }

            rejection = StoreRejection.None;
            return true;
        }

        private static bool IsCardRefStored(CardRef wanted, Card except)
        {
            RefreshStoredCardCounts();
            if (!StoredCardCounts.TryGetValue(wanted, out int count))
                return false;

            if (except != null && CardRef.From(except.Identity) == wanted)
            {
                CardStackContainer owner = except.GetComponentInParent<CardStackContainer>();
                if (owner != null && owner.isActiveAndEnabled && Instances.Contains(owner))
                    count--;
            }

            return count > 0;
        }

        private static void RefreshStoredCardCounts()
        {
            if (!_storedCardCountsDirty)
                return;

            StoredCardCounts.Clear();
            for (int i = Instances.Count - 1; i >= 0; i--)
            {
                CardStackContainer container = Instances[i];
                if (container == null)
                {
                    Instances.RemoveAt(i);
                    continue;
                }

                container._childCards.Clear();
                container.GetComponentsInChildren(
                    includeInactive: true,
                    result: container._childCards);

                foreach (Card stored in container._childCards)
                {
                    CardRef cardRef = CardRef.From(stored != null ? stored.Identity : null);
                    if (!cardRef.IsValid)
                        continue;

                    StoredCardCounts.TryGetValue(cardRef, out int count);
                    StoredCardCounts[cardRef] = count + 1;
                }
            }

            _storedCardCountsDirty = false;
        }

        /// <summary>
        /// Removes the card from the hand and flies it onto the chosen stack. The slot is
        /// revalidated immediately before ownership changes, so a full slot can never make the
        /// hand lose a card.
        ///
        /// A card that is not being held - one taken straight off the floor by a debug fill - is
        /// accepted too; then there is no hand to take it out of and <paramref name="hand"/> may be
        /// null. <paramref name="flight"/> chooses how it travels the last stretch.
        /// </summary>
        public bool TryStore(
            CardHand hand,
            Card card,
            SlotTarget target,
            PlacementFlight flight = PlacementFlight.Placed)
        {
            if (card == null || !target.IsValid)
                return false;

            if (card.IsHeld && hand == null)
                return false;

            if (!CanStore(card, out StoreRejection rejection))
            {
                switch (rejection)
                {
                    case StoreRejection.AlreadyStored:
                        _hudHints?.Raise(HintId.DuplicateAlreadyStored);
                        break;

                    case StoreRejection.NotADuplicate:
                        _hudHints?.Raise(HintId.NotADuplicate);
                        break;
                }

                return false;
            }

            if (!TryBuildTarget(target.SlotIndex, target.RayDistance, out SlotTarget current))
                return false;

            if (card.IsHeld && !hand.TryRemove(card))
                return false;

            // Lifting a card out of a standing house of cards brings the rest of it down, exactly
            // as picking it up by hand would. A card coming from the hand has already done this.
            card.House?.OnMemberPickedUp(card);

            // TryRemove performs the hand bookkeeping and leaves a kinematic held card for its new
            // owner. ResetToFrozen returns its render layer, collider and mip request to the cheap
            // world state without ever launching a physics simulation.
            card.ResetToFrozen();

            Transform cardTransform = card.transform;
            // Preserve the card's current world pose while ownership moves to the container.
            // Container_D is uniformly scaled to 1.5, so Unity also derives the same 2/3 local
            // scale the user's authored test cards use.
            cardTransform.SetParent(transform, worldPositionStays: true);
            cardTransform.SetAsLastSibling();

            var reservation = new PlacementReservation(
                card,
                current.SlotIndex,
                current.LocalPosition,
                current.LocalRotation);
            _reservations.Add(reservation);

            // A card in flight already owns its slot height, so the next card queued into this box
            // stacks on top of it rather than into the same place.
            MarkStacksDirty();

            // Card owns and cancels its PrimeTween handles, so picking it back up mid-flight remains
            // safe. No Rigidbody is created: this is a visual transfer, not another physics object.
            float duration = Application.isPlaying && isActiveAndEnabled
                ? BeginPlacementFlight(card, current, flight)
                : 0f;

            if (duration <= 0f)
            {
                SnapToReservation(reservation);
                RemoveReservation(card);
            }
            else
            {
                StartCoroutine(CompletePlacementAfter(card, duration));
            }

            Physics.SyncTransforms();
            _ghostFrame = -1;
            return true;
        }

        /// <summary>
        /// Starts the card on its way and reports how long it will be in the air, or zero when it
        /// should simply appear in its slot.
        /// </summary>
        private float BeginPlacementFlight(Card card, SlotTarget target, PlacementFlight flight)
        {
            if (flight == PlacementFlight.Instant)
                return 0f;

            Vector3 localTravel = target.LocalPosition - card.transform.localPosition;
            Vector3 planarTravel = Vector3.ProjectOnPlane(localTravel, Vector3.up);
            Vector3 sideDirection = planarTravel.sqrMagnitude > 0.000001f
                ? Vector3.Cross(Vector3.up, planarTravel.normalized)
                : Vector3.right;

            // Which way the card banks: away from the side it is coming in on, so the roll reads as
            // part of the turn rather than fighting it.
            float turnSign = Mathf.Abs(localTravel.x) > 0.0001f ? Mathf.Sign(localTravel.x) : 1f;

            if (flight == PlacementFlight.Placed)
            {
                if (placementDuration <= 0f)
                    return 0f;

                card.FlyTo(
                    target.LocalPosition,
                    target.LocalRotation,
                    sideDirection * placementSideArc,
                    new Vector3(placementPitchDegrees, 0f, -turnSign * placementRollDegrees),
                    placementDuration,
                    Ease.InOutCubic);

                return placementDuration;
            }

            // A card crossing the room needs longer in the air than one dropped from just above the
            // box, and a bend big enough to read from that distance - a fixed arc of a few
            // centimetres would be invisible over several metres and the flight would look like a
            // straight line. Both come off the distance, so a card thrown from the doorway takes a
            // long slow sweep and one thrown from beside the box still lands promptly.
            float distance = localTravel.magnitude;
            float duration = Mathf.Min(
                flourishDuration + distance * flourishSecondsPerUnit,
                flourishMaxDuration);

            // Sideways only. sideDirection is level with the floor, so the control point never
            // lifts the path: the card stays in the plane the player is already looking at.
            float bow = Mathf.Clamp(distance * flourishBow, flourishMinBow, flourishMaxBow);
            Vector3 arc = ChooseClearArc(
                card.transform.localPosition, target.LocalPosition, sideDirection, bow);

            card.FlyTo(
                target.LocalPosition,
                target.LocalRotation,
                arc,
                new Vector3(flourishPitchDegrees, 0f, -turnSign * flourishRollDegrees),
                duration,
                flourishEase,
                flourishTurns);

            return duration;
        }

        /// <summary>
        /// Picks the sweep the card takes out of the player's hands. The one the tuning asks for is
        /// used whenever it is clear; otherwise the same sweep is tried the other way round and
        /// then wider, so a card thrown at an armchair goes round it instead of through it. Every
        /// candidate keeps the flight level and keeps its shape - the move still reads as the move.
        ///
        /// Only the near stretch is looked at, and only once, as the card leaves. Bending the far
        /// end would cost more sweeps for something nobody is watching by then, and the room is
        /// full of furniture no curve could dodge anyway.
        /// </summary>
        private Vector3 ChooseClearArc(Vector3 start, Vector3 end, Vector3 sideDirection, float bow)
        {
            Vector3 preferred = sideDirection * bow;

            if (flourishCheckedPart <= 0f || flourishClearance <= 0f || IsPathClear(start, end, preferred))
                return preferred;

            foreach (float scale in ArcAlternatives)
            {
                Vector3 candidate = sideDirection * (bow * scale);
                if (IsPathClear(start, end, candidate))
                    return candidate;
            }

            // Nothing was clear - thrown at a wall from a foot away there is nowhere to bend to.
            // The card flies anyway rather than the throw being refused: a reward the player cannot
            // rely on is worse than a card that clips a chair.
            return preferred;
        }

        private bool IsPathClear(Vector3 start, Vector3 end, Vector3 arc)
        {
            Vector3 previous = transform.TransformPoint(start);

            for (int step = 1; step <= flourishCheckSteps; step++)
            {
                float t = flourishCheckedPart * step / flourishCheckSteps;
                Vector3 point = transform.TransformPoint(Card.FlightPoint(start, end, arc, t));

                if (IsBlocked(previous, point))
                    return false;

                previous = point;
            }

            return true;
        }

        private bool IsBlocked(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
                return false;

            int count = Physics.SphereCastNonAlloc(
                from,
                flourishClearance,
                delta / distance,
                ObstacleHits,
                distance,
                flourishObstacles,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider hit = ObstacleHits[i].collider;
                if (hit == null)
                    continue;

                // Cards are what the room is made of - the card in flight itself included - and the
                // box is where this is going. Neither is something to steer around. A card's
                // collider sits on the card itself, so this is a lookup rather than a walk upwards.
                if (hit.TryGetComponent(out Card _))
                    continue;

                if (hit.transform.IsChildOf(transform) || transform.IsChildOf(hit.transform))
                    continue;

                return true;
            }

            return false;
        }

        public bool TryGetSavedPlacement(
            Card card,
            out int slotIndex,
            out Vector3 localPosition,
            out Quaternion localRotation)
        {
            slotIndex = -1;
            localPosition = default;
            localRotation = default;

            if (card == null || !card.transform.IsChildOf(transform))
                return false;

            // Autosave can land during the short flight. Persist the reserved landing pose rather
            // than the visual midpoint so reload cannot freeze a card between hand and stack.
            for (int i = 0; i < _reservations.Count; i++)
            {
                PlacementReservation reservation = _reservations[i];
                if (reservation.Card != card)
                    continue;

                slotIndex = reservation.SlotIndex;
                localPosition = reservation.LocalPosition;
                localRotation = reservation.LocalRotation;
                return true;
            }

            localPosition = card.transform.localPosition;
            localRotation = card.transform.localRotation;
            return TryGetNearestSlot(localPosition, out slotIndex);
        }

        public bool RestoreCard(
            Card card,
            int slotIndex,
            Vector3 localPosition,
            Quaternion localRotation)
        {
            EnsureStackBuffers();
            if (card == null || slotIndex < 0 || slotIndex >= _stackCounts.Length)
                return false;

            card.StopAnimation();
            card.ResetToFrozen();
            // A saved container card may be restored from an authored floor copy or a freshly
            // created prefab. Preserve its world scale while parenting, just like TryStore does;
            // Container_D is scaled to 1.5 and a false here would enlarge such a card by 50%.
            card.transform.SetParent(transform, worldPositionStays: true);
            card.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            card.transform.SetAsLastSibling();

            // Reparenting announces itself, but a card already under this box only moves - and a
            // restore is exactly when a whole stack lands at once.
            MarkStacksDirty();
            return true;
        }

        /// <summary>
        /// Queues one translucent, collider-free preview draw for the current frame. The preview
        /// shows where the card would land, not whether it may: a card the box refuses is turned
        /// away on the key press, with a hint saying why.
        /// </summary>
        public void ShowGhost(Card card, SlotTarget target, Camera camera)
        {
            _ghostCard = card;
            _ghostTarget = target;
            _ghostCamera = camera;
            _ghostFrame = Time.frameCount;
        }

        private void LateUpdate()
        {
            if (_ghostFrame != Time.frameCount ||
                _ghostCard == null || !_ghostCard.IsHeld || _ghostCamera == null)
            {
                return;
            }

            if (!EnsureGhostResources())
                return;

            Mesh mesh = _ghostCard.OutlineMesh;
            MeshRenderer source = _ghostCard.OutlineRenderer;
            if (mesh == null || source == null || source.sharedMaterial == null)
                return;

            int layer = gameObject.layer;
            if ((_ghostCamera.cullingMask & (1 << layer)) == 0)
                return;

            Material sourceMaterial = source.sharedMaterial;
            Texture front = sourceMaterial.GetTexture(FrontTexId);
            Texture back = sourceMaterial.GetTexture(BackTexId);

            if (front == null && _ghostCard.Identity != null)
                front = _ghostCard.Identity.ArtworkTexture;
            if (back == null)
                back = front;

            _ghostProperties.Clear();
            _ghostProperties.SetTexture(FrontTexId, front);
            _ghostProperties.SetTexture(BackTexId, back);
            _ghostProperties.SetVector(
                FrontTexStId,
                source.HasPropertyBlock()
                    ? ResolveTextureTransform(source, sourceMaterial, FrontTexStId, "_FrontTex")
                    : TextureTransform(sourceMaterial, "_FrontTex"));
            _ghostProperties.SetVector(
                BackTexStId,
                source.HasPropertyBlock()
                    ? ResolveTextureTransform(source, sourceMaterial, BackTexStId, "_BackTex")
                    : TextureTransform(sourceMaterial, "_BackTex"));
            _ghostProperties.SetColor(GhostTintId, ghostTint);

            Matrix4x4 matrix = Matrix4x4.TRS(
                transform.TransformPoint(_ghostTarget.LocalPosition),
                transform.rotation * _ghostTarget.LocalRotation,
                Vector3.one);

            Graphics.DrawMesh(
                mesh,
                matrix,
                _ghostMaterial,
                layer,
                _ghostCamera,
                0,
                _ghostProperties,
                ShadowCastingMode.Off,
                receiveShadows: false,
                probeAnchor: null,
                lightProbeUsage: LightProbeUsage.Off);
        }

        private bool EnsureGhostResources()
        {
            if (_ghostMaterial == null)
                _ghostMaterial = Resources.Load<Material>(GhostMaterialResourcePath);

            if (_ghostMaterial == null)
            {
                Debug.LogError(
                    $"[{nameof(CardStackContainer)}] Resources/{GhostMaterialResourcePath}.mat " +
                    "is missing; slot placement still works, but its ghost cannot be drawn.",
                    this);
                return false;
            }

            _ghostProperties ??= new MaterialPropertyBlock();
            _sourceProperties ??= new MaterialPropertyBlock();
            return true;
        }

        private static Vector4 TextureTransform(Material material, string propertyName)
        {
            Vector2 scale = material.GetTextureScale(propertyName);
            Vector2 offset = material.GetTextureOffset(propertyName);
            return new Vector4(scale.x, scale.y, offset.x, offset.y);
        }

        private Vector4 ResolveTextureTransform(
            MeshRenderer source,
            Material material,
            int propertyId,
            string propertyName)
        {
            // Card's held property block normally contains no texture transform, but reading the
            // block keeps a future per-card crop compatible with the ghost. A zero vector means the
            // property was not authored, so fall back to the shared material's scale and offset.
            _sourceProperties.Clear();
            source.GetPropertyBlock(_sourceProperties);
            Vector4 value = _sourceProperties.GetVector(propertyId);
            return value == Vector4.zero ? TextureTransform(material, propertyName) : value;
        }

        private bool TryGetPointedTarget(
            Ray ray,
            out SlotTarget target,
            out float distance)
        {
            target = default;
            distance = 0f;
            EnsureStackBuffers();
            RefreshStacks();

            int bestSlotIndex = -1;
            float bestScore = float.PositiveInfinity;
            float bestDistance = float.PositiveInfinity;

            for (int slotIndex = 0; slotIndex < _stackCounts.Length; slotIndex++)
            {
                SlotCoordinates(slotIndex, out float x, out float z);
                float targetY = PointingSurfaceY(slotIndex);
                Vector3 worldCentre = transform.TransformPoint(x, targetY, z);
                var placementPlane = new Plane(transform.up, worldCentre);

                if (!placementPlane.Raycast(ray, out float candidateDistance) ||
                    candidateDistance <= 0f || candidateDistance > interactionDistance)
                {
                    continue;
                }

                Vector3 localHit = transform.InverseTransformPoint(ray.GetPoint(candidateDistance));
                float normalisedX = (localHit.x - x) / (columnSpacing * 0.5f);
                float normalisedZ = (localHit.z - z) / (rowSpacing * 0.5f);
                if (Mathf.Abs(normalisedX) > 1f || Mathf.Abs(normalisedZ) > 1f)
                    continue;

                float score = normalisedX * normalisedX + normalisedZ * normalisedZ;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                bestSlotIndex = slotIndex;
                bestDistance = candidateDistance;
            }

            if (bestSlotIndex < 0)
                return false;

            distance = bestDistance;
            target = TryBuildTargetFromCurrentStacks(
                bestSlotIndex, bestDistance, out SlotTarget validTarget)
                ? validTarget
                : new SlotTarget(-1, default, default, bestDistance);
            return true;
        }

        private bool TryBuildTarget(int slotIndex, float rayDistance, out SlotTarget target)
        {
            target = default;
            EnsureStackBuffers();

            if (slotIndex < 0 || slotIndex >= _stackCounts.Length)
                return false;

            RefreshStacks();
            return TryBuildTargetFromCurrentStacks(slotIndex, rayDistance, out target);
        }

        private bool TryBuildTargetFromCurrentStacks(
            int slotIndex,
            float rayDistance,
            out SlotTarget target)
        {
            target = default;
            int count = _stackCounts[slotIndex];
            if (count >= maxCardsPerSlot)
                return false;

            float nextY = NextStackY(slotIndex);

            if (nextY > maxCenterY + 0.00001f)
                return false;

            SlotCoordinates(slotIndex, out float x, out float z);
            target = new SlotTarget(
                slotIndex,
                new Vector3(x, nextY, z),
                Quaternion.Euler(-90f, 0f, 0f),
                rayDistance);
            return true;
        }

        private float NextStackY(int slotIndex)
        {
            int count = _stackCounts[slotIndex];
            return count == 0
                ? baseCenterY
                : Mathf.Max(
                    baseCenterY + count * stackStep,
                    _highestLocalY[slotIndex] + stackStep);
        }

        private float PointingSurfaceY(int slotIndex)
        {
            float nextY = NextStackY(slotIndex);
            if (_stackCounts[slotIndex] < maxCardsPerSlot && nextY <= maxCenterY)
                return nextY;

            return _stackCounts[slotIndex] > 0
                ? Mathf.Clamp(_highestLocalY[slotIndex], baseCenterY, maxCenterY)
                : baseCenterY;
        }

        private void MarkStacksDirty() => _stacksDirty = true;

        private void RefreshStacks()
        {
            if (!_stacksDirty)
                return;

            _stacksDirty = false;

            Array.Clear(_stackCounts, 0, _stackCounts.Length);
            Array.Clear(_topCards, 0, _topCards.Length);
            for (int i = 0; i < _highestLocalY.Length; i++)
                _highestLocalY[i] = float.NegativeInfinity;

            PruneReservations();

            _childCards.Clear();
            GetComponentsInChildren(includeInactive: true, result: _childCards);

            foreach (Card card in _childCards)
            {
                if (card == null)
                    continue;

                if (IsReserved(card))
                    continue;

                Vector3 localPosition = transform.InverseTransformPoint(card.transform.position);
                if (!TryGetNearestSlot(localPosition, out int slotIndex))
                    continue;

                _stackCounts[slotIndex]++;
                RaiseTop(slotIndex, localPosition.y, card);
            }

            for (int i = 0; i < _reservations.Count; i++)
            {
                PlacementReservation reservation = _reservations[i];
                _stackCounts[reservation.SlotIndex]++;

                // A card still in flight is already the top of its stack: it has been promised the
                // pose above everything else there, so the next card must stack on top of it.
                RaiseTop(reservation.SlotIndex, reservation.LocalPosition.y, reservation.Card);
            }
        }

        private void RaiseTop(int slotIndex, float localY, Card card)
        {
            if (localY < _highestLocalY[slotIndex])
                return;

            _highestLocalY[slotIndex] = localY;
            _topCards[slotIndex] = card;
        }

        private bool IsReserved(Card card)
        {
            for (int i = 0; i < _reservations.Count; i++)
            {
                if (_reservations[i].Card == card)
                    return true;
            }

            return false;
        }

        private void PruneReservations()
        {
            for (int i = _reservations.Count - 1; i >= 0; i--)
            {
                Card card = _reservations[i].Card;
                if (card == null || !card.transform.IsChildOf(transform))
                {
                    _reservations.RemoveAt(i);
                    MarkStacksDirty();
                }
            }
        }

        private IEnumerator CompletePlacementAfter(Card card, float delay)
        {
            yield return new WaitForSeconds(delay);

            for (int i = _reservations.Count - 1; i >= 0; i--)
            {
                PlacementReservation reservation = _reservations[i];
                if (reservation.Card != card)
                    continue;

                SnapToReservation(reservation);
                _reservations.RemoveAt(i);
                MarkStacksDirty();
                Physics.SyncTransforms();
                yield break;
            }
        }

        private void SnapToReservation(PlacementReservation reservation)
        {
            Card card = reservation.Card;
            if (card == null || !card.transform.IsChildOf(transform))
                return;

            card.StopAnimation();
            card.transform.SetLocalPositionAndRotation(
                reservation.LocalPosition,
                reservation.LocalRotation);

            // The card moved without the child list changing, which is the one way the layout can
            // shift without Unity telling this component about it.
            MarkStacksDirty();
        }

        private void RemoveReservation(Card card)
        {
            for (int i = _reservations.Count - 1; i >= 0; i--)
            {
                if (_reservations[i].Card == card)
                    _reservations.RemoveAt(i);
            }

            MarkStacksDirty();
        }

        private bool TryGetNearestSlot(Vector3 localPoint, out int slotIndex)
        {
            float firstX = gridCenter.x - (columns - 1) * columnSpacing * 0.5f;
            float firstZ = gridCenter.y - (rows - 1) * rowSpacing * 0.5f;

            int column = Mathf.RoundToInt((localPoint.x - firstX) / columnSpacing);
            int row = Mathf.RoundToInt((localPoint.z - firstZ) / rowSpacing);

            if (column < 0 || column >= columns || row < 0 || row >= rows)
            {
                slotIndex = -1;
                return false;
            }

            float slotX = firstX + column * columnSpacing;
            float slotZ = firstZ + row * rowSpacing;
            if (Mathf.Abs(localPoint.x - slotX) > columnSpacing * 0.5f ||
                Mathf.Abs(localPoint.z - slotZ) > rowSpacing * 0.5f)
            {
                slotIndex = -1;
                return false;
            }

            slotIndex = row * columns + column;
            return true;
        }

        private void SlotCoordinates(int slotIndex, out float x, out float z)
        {
            int row = slotIndex / columns;
            int column = slotIndex - row * columns;

            x = gridCenter.x + (column - (columns - 1) * 0.5f) * columnSpacing;
            z = gridCenter.y + (row - (rows - 1) * 0.5f) * rowSpacing;
        }

        private void EnsureStackBuffers()
        {
            int slotCount = Mathf.Max(1, columns) * Mathf.Max(1, rows);
            if (_stackCounts != null && _stackCounts.Length == slotCount)
                return;

            _stackCounts = new int[slotCount];
            _highestLocalY = new float[slotCount];
            _topCards = new Card[slotCount];
            MarkStacksDirty();
        }

        private void OnDrawGizmosSelected()
        {
            int slotCount = Mathf.Max(1, columns) * Mathf.Max(1, rows);
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.75f);
            // A face-up card maps mesh X/Y onto the container's X/Z plane after the -90 pitch.
            Vector3 cardSizeLocal = new Vector3(0.042f, 0.0008f, 0.063f);

            for (int i = 0; i < slotCount; i++)
            {
                SlotCoordinates(i, out float x, out float z);
                Gizmos.DrawWireCube(new Vector3(x, baseCenterY, z), cardSizeLocal);
            }

            Gizmos.matrix = oldMatrix;
        }
    }
}
