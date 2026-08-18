using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardsChaos.Cards
{
    /// <summary>
    /// What brought a house of cards down. The house itself does not care - the collapse is the same
    /// physics either way - but it is the only place that knows, so it reports it and anything that
    /// wants to tell a pickup from a spell from a thrown card reads it off the collapse.
    /// </summary>
    public enum HouseCollapseCause
    {
        /// <summary>A member was lifted out of the house, by hand or into a container.</summary>
        PickedUp,

        /// <summary>A member was raised out of the house by the Levitate skill.</summary>
        Levitate,

        /// <summary>A card thrown across the room hit the house hard enough to topple it.</summary>
        StruckByCard,
    }

    /// <summary>
    /// A built house of cards - a fixed set of cards standing in one authored arrangement.
    ///
    /// A house is only ever its arrangement. This component remembers which cards belong to it and
    /// the local pose each was placed at, and does exactly one thing at runtime: the first time a
    /// member is lifted off the floor while the house is still whole, it drops the rest into flight
    /// so the whole thing comes down under real physics. After that it is spent.
    ///
    /// "Still whole" is read straight from those stored poses - every member still sitting where it
    /// was authored - never from tilt or contacts, which would be brittle. That also makes a
    /// collapse permanent across saves for free: the save restores each card's pose, so a house
    /// saved intact loads intact (and can still come down) while one saved collapsed loads with its
    /// cards scattered, which no longer match their authored poses and so never come down again.
    ///
    /// The cards are authored by <see cref="CardEditor.CardHouseBuilder"/>; the collapse is fired
    /// from <see cref="CardHand.PickUp"/> the instant a member is taken, and from
    /// <see cref="OnStruck"/> when a card thrown across the room hits the house hard enough.
    /// </summary>
    [AddComponentMenu("CardsChaos/Card House")]
    public class CardHouse : MonoBehaviour
    {
        [System.Serializable]
        private struct Member
        {
            public Card Card;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
        }

        // A member that has drifted more than this from its authored local pose counts as moved,
        // which is all it takes for the house to read as no longer whole. Loose enough to ride out
        // the float round-trip through a save, tight enough that a card lifted into the hand or one
        // fallen from a collapse never reads as still in place.
        private const float PositionTolerance = 0.01f; // metres
        private const float RotationTolerance = 8f;     // degrees

        // The collapse is real physics, tuned to stay calm. The cards are authored just touching, so
        // waking them all at once would have the solver shove the overlaps apart in one frame - the
        // "pop" that read as jitter. A low depenetration speed eases those overlaps apart instead.
        // (Collision detection stays ContinuousDynamic, as BeginFlight sets it - a thin card at
        // Discrete tunnels through a thin table the house may be standing on.)
        private const float DepenetrationSpeed = 0.1f; // metres/second, caps how hard overlaps push apart

        // No launch impulse: a slick, unsupported leaning card comes down on its own under gravity,
        // and a linear shove was exactly the unnatural "flick" we were chasing out. Only a whisper of
        // random spin remains, so a perfectly symmetric tent cannot balance for a beat before it goes.
        private const float ToppleSpin = 0.4f; // radians/second, random

        // Heavy drag for the length of the collapse, so the whole thing comes down slowly and
        // deliberately - the suspense - while every velocity stays low, which is also what keeps a
        // card from punching through the table. Gravity still carries each card all the way to flat;
        // the drag only slows the journey there. Removed with the body once the card settles.
        private const float FallDrag = 10f;    // linear drag while collapsing
        private const float TumbleDrag = 2.5f; // angular drag, so the tumble is slow too

        // The real fix for cards freezing half-toppled: while collapsing they are given a slick
        // surface. A near-frictionless card cannot hold a lean - it slides down until it lies flat -
        // so every card settles within a few degrees of the table instead of catching on the heap.
        // Minimum combine makes the card's low friction win over the table's, whatever that is.
        private const float CollapseFriction = 0.1f;
        private static PhysicMaterial _slick;

        // How fast a card has to be travelling when it hits the house to bring it down. A thrown
        // card leaves the hand at over a metre a second and picks up more on the way, while a card
        // bedding in against the house as it settles, or one nudged along the table, stays well
        // under - so a real throw topples the house and a card coming to rest beside it does not.
        private const float ToppleImpactSpeed = 0.8f; // metres/second

        // A shove given to the one card that was hit, along the throw and level with the floor. The
        // rest of the house comes down under gravity alone, as it does on a pickup; this is what
        // makes the difference read as knocked over rather than as the house choosing that moment
        // to fall. Small, because the collapse is meant to be slow and watchable either way.
        private const float ToppleKick = 0.35f; // metres/second

        /// <summary>
        /// Raised the moment a standing house actually comes down, with the house and what brought
        /// it down. Fires once per house, ever - a spent house never collapses again.
        ///
        /// Static because the listeners are services with no reference to any particular house: the
        /// houses are authored into the scene and there is nothing that owns the set of them. The
        /// same shape as <see cref="CardStackContainer.ContentsChanged"/>, and with the same duty on
        /// a listener to unsubscribe when it is disposed.
        /// </summary>
        public static event Action<CardHouse, HouseCollapseCause> Collapsed;

        [SerializeField] private List<Member> _members = new List<Member>();

        // Runtime latch. Deliberately not serialized: on a fresh load it starts false, and the pose
        // check in OnMemberPickedUp is what stops an already-collapsed house from acting again.
        [System.NonSerialized] private bool _spent;

        /// <summary>
        /// Records the cards and the local poses they were just placed at. The cards must already be
        /// parented under this house and sitting at their final poses. Editor authoring only.
        /// </summary>
        public void Configure(IReadOnlyList<Card> cards)
        {
            _members.Clear();
            if (cards == null)
                return;

            foreach (Card card in cards)
            {
                if (card == null)
                    continue;

                Transform t = card.transform;
                _members.Add(new Member
                {
                    Card = card,
                    LocalPosition = t.localPosition,
                    LocalRotation = t.localRotation,
                });

                card.House = this;
            }
        }

        // The member list is serialized, but Card.House is a runtime-only back-reference that is
        // not - so re-bind it on load, before anything can be picked up.
        private void Awake()
        {
            foreach (Member member in _members)
            {
                if (member.Card != null)
                    member.Card.House = this;
            }
        }

        /// <summary>
        /// Called the instant a member is picked up, before it is reparented into the hand. If the
        /// house is still whole the other members are released into flight; either way the house is
        /// spent afterwards and never fires again.
        ///
        /// <paramref name="cause"/> is how the card left the house - a plain lift by default, or
        /// <see cref="HouseCollapseCause.Levitate"/> when a spell raised it. It changes nothing about
        /// the collapse; it is only carried through to <see cref="Collapsed"/>.
        /// </summary>
        public void OnMemberPickedUp(Card taken, HouseCollapseCause cause = HouseCollapseCause.PickedUp)
        {
            if (_spent)
                return;

            _spent = true;

            // Already broken earlier this session, or loaded already collapsed - the cards are no
            // longer in their authored arrangement, so this is just an ordinary floor pickup.
            if (IsWhole())
                Collapse(taken, cause);
        }

        /// <summary>
        /// Called when a card in flight hits one of this house's members, with the blow as a
        /// velocity - which way it came and how fast. A glancing touch leaves the house standing
        /// and, unlike a pickup, does not spend it: a card that merely rolls up against the house
        /// must not quietly disarm the collapse. A hard enough hit brings the whole thing down.
        /// </summary>
        public void OnStruck(Card struck, Vector3 blow)
        {
            if (_spent)
                return;

            if (blow.magnitude < ToppleImpactSpeed)
                return;

            _spent = true;

            // Already broken, or loaded already collapsed - the cards no longer stand in their
            // authored arrangement, so this was just one card knocking into another on the floor.
            if (!IsWhole())
                return;

            Collapse(null, HouseCollapseCause.StruckByCard);

            // The struck card now has a body of its own (Collapse gave it one), so the blow can be
            // passed on to it - the contact itself was against a static collider and pushed nothing.
            if (struck != null && struck.TryGetComponent(out Rigidbody body) && !body.isKinematic)
                body.velocity += blow.normalized * ToppleKick;
        }

        /// <summary>
        /// Called once by the world restore, after every card is back where the save left it.
        ///
        /// A house saved intact loads intact and is left armed. One saved already down loads with
        /// its members scattered, and this is where it is retired: they are cut free of the house
        /// root and forget it, exactly as <see cref="Collapse"/> left them last session, so a
        /// collapsed house comes back as a heap of ordinary cards rather than as a house that only
        /// looks broken.
        /// </summary>
        public void SettleAfterRestore()
        {
            if (_spent || IsWhole())
                return;

            _spent = true;

            foreach (Member member in _members)
            {
                Card card = member.Card;
                if (card == null)
                    continue;

                card.House = null;

                // Only what is still sitting under the house root is cut loose. A member the save
                // put back into the player's hand, or filed into a duplicate box, has already been
                // parented where it belongs, and pulling it out of there would undo the restore.
                if (card.transform.IsChildOf(transform))
                    card.transform.SetParent(null, worldPositionStays: true);
            }
        }

        private bool IsWhole()
        {
            foreach (Member member in _members)
            {
                Card card = member.Card;
                if (card == null || card.IsHeld)
                    return false;

                Transform t = card.transform;
                if ((t.localPosition - member.LocalPosition).sqrMagnitude >
                    PositionTolerance * PositionTolerance)
                    return false;

                if (Quaternion.Angle(t.localRotation, member.LocalRotation) > RotationTolerance)
                    return false;
            }

            return _members.Count > 0;
        }

        private void Collapse(Card taken, HouseCollapseCause cause)
        {
            foreach (Member member in _members)
            {
                Card card = member.Card;
                if (card == null || card == taken || card.IsHeld)
                    continue;

                card.House = null;

                // Cut loose from the house root so the fallen cards live on as ordinary scene cards,
                // the way a thrown card does.
                card.transform.SetParent(null, worldPositionStays: true);

                // Slick surface so the card cannot rest half-toppled - it slides down to flat.
                if (card.TryGetComponent(out BoxCollider box))
                    box.sharedMaterial = Slick();

                card.BeginFlight();
                Rigidbody body = card.EnsureBody();

                // Set after BeginFlight/EnsureBody, which restore the shared flight values. Collision
                // stays BeginFlight's ContinuousDynamic so a card cannot tunnel through the table; the
                // rest keeps the fall slow and soft. No launch impulse - gravity and the slick surface
                // bring every unsupported card down on their own, only a little spin to break symmetry.
                body.maxDepenetrationVelocity = DepenetrationSpeed;
                body.drag = FallDrag;
                body.angularDrag = TumbleDrag;
                body.angularVelocity += UnityEngine.Random.insideUnitSphere * ToppleSpin;
                body.WakeUp();
            }

            if (taken != null)
                taken.House = null;

            // Announced after every member is in flight, so a listener that looks at the room sees
            // the collapse already under way rather than a house still standing.
            Collapsed?.Invoke(this, cause);
        }

        /// <summary>
        /// Debug helper: stands the house back up after a collapse - every member returned to its
        /// authored pose, reset to plain resting, and the house re-armed - so a collapse can be tried
        /// again without leaving Play mode. A member filed away to the album (destroyed) cannot come
        /// back. Driven from the editor-only <see cref="CardHouseDebugRestore"/>.
        /// </summary>
        public void RestoreStanding(CardHand hand)
        {
            foreach (Member member in _members)
            {
                Card card = member.Card;
                if (card == null)
                    continue;

                // A member still in hand has to be lifted out of it before it can go back home.
                if (card.IsHeld)
                    hand?.TryRemove(card);

                card.ResetToFrozen();

                Transform t = card.transform;
                t.SetParent(transform, worldPositionStays: false);
                t.localPosition = member.LocalPosition;
                t.localRotation = member.LocalRotation;
                t.localScale = Vector3.one;

                card.House = this;
            }

            _spent = false;
        }

        // Built once and shared by every collapsing card. Minimum combine so the low value wins over
        // whatever the table and the other cards use, and the leaning contacts actually let go.
        private static PhysicMaterial Slick()
        {
            if (_slick == null)
            {
                _slick = new PhysicMaterial("CardHouseCollapse")
                {
                    dynamicFriction = CollapseFriction,
                    staticFriction = CollapseFriction,
                    bounciness = 0f,
                    frictionCombine = PhysicMaterialCombine.Minimum,
                    bounceCombine = PhysicMaterialCombine.Minimum,
                };
            }

            return _slick;
        }
    }
}
