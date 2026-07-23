using System.Collections.Generic;
using PrimeTween;
using RoboRyanTron.SearchableEnum;
using UnityEngine;
using UnityEngine.Serialization;

namespace CardsChaos.Cards
{
    public enum CardHandLayout
    {
        /// <summary>Squared up in a corner, one card on top of the next. The resting state.</summary>
        Pile,

        /// <summary>Spread out to be read. Costs most of the bottom of the screen.</summary>
        Fan,
    }

    /// <summary>
    /// Holds picked up cards, either squared into a corner pile or fanned out for reading.
    ///
    /// Index 0 is the top of the pile and the leftmost card of the fan; new cards enter there.
    /// The selection is kept as a reference rather than an index because the order changes under
    /// it - cycling the pile moves every card by one slot, and a selection tied to a slot would
    /// appear to jump to a different card each notch of the wheel.
    /// </summary>
    [AddComponentMenu("CardsChaos/Card Hand")]
    public class CardHand : MonoBehaviour
    {
        [Header("Anchors")]
        [Tooltip("Where the pile rests. Its +Z must point away from the viewer.")]
        [SerializeField] private Transform pileAnchor;

        [FormerlySerializedAs("anchor")]
        [Tooltip("Where the fan sits once TAB opens it. Same +Z rule as the pile.")]
        [SerializeField] private Transform fanAnchor;

        [Tooltip("Where a card sits while being inspected. Same +Z rule again; " +
                 "the closer it is to the camera the larger the card reads.")]
        [SerializeField] private Transform inspectAnchor;

        [Header("Pile Layout")]
        [Tooltip("How far each card is nudged along from the one below it. Enough that every " +
                 "card in the pile shows an edge, not so much that the pile becomes a fan.")]
        [SerializeField] private Vector2 pileStep = new Vector2(0.0022f, 0.0016f);

        [Tooltip("Degrees each card is turned from the one below it, so the stack looks put " +
                 "down by hand rather than machined.")]
        [SerializeField] private float pileAngleStep = 1.1f;

        [SerializeField] private float pileDepthStep = 0.0008f;

        [Header("Fan Layout")]
        [SerializeField] private int slotCount = 10;
        [SerializeField] private float arcRadius = 0.25f;
        [SerializeField] private float anglePerCard = 9f;
        [SerializeField] private float maxFanAngle = 40f;
        [SerializeField] private float depthStep = 0.004f;

        [Header("Selection")]
        [SerializeField] private float selectedLift = 0.015f;
        [SerializeField] private float selectedPull = 0.01f;

        [Header("Animation")]
        [SerializeField] private float moveDuration = 0.35f;
        [SerializeField, SearchableEnum] private Ease moveEase = Ease.OutQuint;

        [Header("Cycling")]
        [Tooltip("How long a card takes to travel from one end of the pile to the other.")]
        [SerializeField] private float cycleDuration = 0.4f;

        [Tooltip("How far the travelling card swings clear of the stack on its way round, in " +
                 "the anchor's own space. Mostly sideways, so the card passes beside the pile " +
                 "rather than through it.")]
        [SerializeField] private Vector3 cycleArc = new Vector3(0.05f, 0.015f, -0.02f);

        [SerializeField, SearchableEnum] private Ease cycleEase = Ease.InOutQuad;

        [Header("Throw")]
        [SerializeField] private float throwSpeed = 1.2f;
        [SerializeField] private float throwLift = 0.35f;
        [SerializeField] private float throwSpin = 6f;

        private readonly List<Card> _cards = new List<Card>();
        private Card _selected;
        private CardHandLayout _layout = CardHandLayout.Pile;

        public bool IsFull => _cards.Count >= slotCount;

        public CardHandLayout Layout => _layout;

        public Card SelectedCard => _selected;

        private Transform ActiveAnchor => _layout == CardHandLayout.Fan ? fanAnchor : pileAnchor;

        /// <summary>
        /// Takes a card off the floor. Returns false when there is no room - the hand never
        /// throws one away to make space, because the player did not ask for that.
        /// </summary>
        public bool PickUp(Card card)
        {
            if (card == null || card.IsHeld)
                return false;

            Transform anchor = ActiveAnchor;
            if (anchor == null)
            {
                Debug.LogError($"No anchor assigned for the {_layout} layout; cards cannot be " +
                               "picked up until one is.", this);

                return false;
            }

            if (IsFull)
            {
                Debug.LogWarning(
                    $"Hand is full ({_cards.Count}/{slotCount}); '{card.name}' stays on the floor.",
                    this);

                return false;
            }

            card.AttachTo(anchor);
            _cards.Insert(0, card);

            // No selection here on purpose: pointing at a card is what selects it, so claiming
            // the new card would light it up until the cursor happened to move.
            Relayout();
            return true;
        }

        /// <summary>Points the selection at a card the cursor found, or clears it on null.</summary>
        public void Select(Card card)
        {
            SetSelected(card != null && _cards.Contains(card) ? card : null);
        }

        /// <summary>Steps the selection along the hand. Used by the close-up to look at the next card.</summary>
        public void SelectNeighbour(int delta)
        {
            int count = _cards.Count;
            if (count == 0)
                return;

            int index = _selected != null ? _cards.IndexOf(_selected) : -1;

            // No selection yet, so a step in either direction should land on an end card rather
            // than on whatever sits next to slot zero.
            index = index < 0
                ? (delta > 0 ? 0 : count - 1)
                : ((index + delta) % count + count) % count;

            SetSelected(_cards[index]);
        }

        public void ThrowSelected()
        {
            int index = _selected != null ? _cards.IndexOf(_selected) : -1;
            if (index < 0)
                return;

            _selected = null;
            ThrowAt(index);

            // The card behind the one just thrown slides into the empty slot, and that is what
            // the player is looking at next - so clearing out a run of cards never has to go back
            // to the mouse between throws. At the end of the hand there is nothing behind, so the
            // new last card takes over instead.
            if (_cards.Count > 0)
                Claim(_cards[Mathf.Min(index, _cards.Count - 1)]);

            Relayout();
        }

        /// <summary>
        /// Walks the wheel one card through the hand.
        ///
        /// In the pile that means turning the stack over, which brings a different card to the
        /// top - and the top of the pile is what the player is looking at, so it takes the
        /// selection with it. The fan has no stack to turn, so there the selection simply walks
        /// along the spread. Either way one notch means one card, and either way the wheel is a
        /// way to pick a card out without having to point at it.
        /// </summary>
        public void Step(int direction)
        {
            if (direction == 0 || _cards.Count == 0)
                return;

            if (_layout == CardHandLayout.Fan)
            {
                SelectNeighbour(direction);
                return;
            }

            // A single card has nowhere to travel to, but the wheel should still be able to
            // claim it rather than doing nothing at all.
            Card traveller = _cards.Count > 1 ? Rotate(direction) : null;

            // Claimed before the layout is issued, so the arc the traveller gets already includes
            // the lift of being selected. Setting it afterwards would re-issue that slot as a
            // straight move and the card would cut through the stack instead of swinging past it.
            Claim(_cards[0]);
            Relayout(traveller);
        }

        /// <summary>Swaps between the corner pile and the spread out fan.</summary>
        public void ToggleLayout()
        {
            _layout = _layout == CardHandLayout.Pile ? CardHandLayout.Fan : CardHandLayout.Pile;

            Transform anchor = ActiveAnchor;
            if (anchor == null)
                return;

            // Reparented where they stand, so the relayout below tweens them across from wherever
            // the old anchor had them rather than snapping them to the new one first.
            foreach (Card card in _cards)
                card.transform.SetParent(anchor, worldPositionStays: true);

            Relayout();
        }

        /// <summary>
        /// Lifts the selected card out of the hand and onto the inspect anchor. The caller
        /// drives its transform from there, so any running slot tween is cancelled.
        /// </summary>
        public Card PresentForInspect()
        {
            Card card = _selected;
            if (card == null || inspectAnchor == null)
                return null;

            card.StopAnimation();
            card.transform.SetParent(inspectAnchor, worldPositionStays: true);

            return card;
        }

        /// <summary>Drops the inspected card back into the hand.</summary>
        public void ReturnFromInspect(Card card)
        {
            Transform anchor = ActiveAnchor;

            if (card == null || anchor == null)
                return;

            card.transform.SetParent(anchor, worldPositionStays: true);
            Relayout();
        }

        /// <summary>
        /// Turns the stack over by one card and hands back the one that made the journey. The
        /// caller lays the hand out afterwards, so the selection can be settled first - the
        /// travelling card is usually the newly selected one, and re-issuing its slot to add the
        /// lift would throw away the arc it was given.
        /// </summary>
        private Card Rotate(int direction)
        {
            // One card has nowhere to go, and none at all has nothing to send.
            if (_cards.Count < 2)
                return null;

            Card traveller;

            if (direction > 0)
            {
                traveller = _cards[0];
                _cards.RemoveAt(0);
                _cards.Add(traveller);
            }
            else
            {
                int last = _cards.Count - 1;
                traveller = _cards[last];
                _cards.RemoveAt(last);
                _cards.Insert(0, traveller);
            }

            return traveller;
        }

        private void SetSelected(Card card)
        {
            if (_selected == card)
                return;

            Card previous = _selected;
            Claim(card);

            // Only the two cards whose lift changed are re-issued. A full relayout here would
            // restart the slot tween on every card in hand every time the cursor crossed one,
            // and the hand would feel like it was wading.
            ReissueSlot(previous);
            ReissueSlot(card);
        }

        /// <summary>
        /// Moves the selection, leaving every slot tween alone.
        ///
        /// No outline goes with it. A card in hand is already picked out by being lifted out of
        /// the pile, and a ring on top of that only competes with the artwork the player is
        /// trying to read. The outline stays a floor-only affordance.
        /// </summary>
        private void Claim(Card card)
        {
            _selected = card;
        }

        private void ReissueSlot(Card card)
        {
            if (card == null)
                return;

            int index = _cards.IndexOf(card);
            if (index < 0)
                return;

            card.MoveTo(SlotPosition(index, card == _selected), SlotRotation(index),
                moveDuration, moveEase);
        }

        private void ThrowAt(int index)
        {
            if (index < 0 || index >= _cards.Count)
                return;

            Transform anchor = ActiveAnchor;
            Card card = _cards[index];
            _cards.RemoveAt(index);

            Vector3 direction = (anchor.forward + anchor.up * throwLift).normalized;
            card.Release(direction * throwSpeed, Random.onUnitSphere * throwSpin);
        }

        /// <param name="traveller">
        /// The one card taking the long way round, if any. Everything else slides straight to
        /// its slot.
        /// </param>
        private void Relayout(Card traveller = null)
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                Card card = _cards[i];
                bool selected = card == _selected;

                Vector3 position = SlotPosition(i, selected);
                Quaternion rotation = SlotRotation(i);

                if (card == traveller)
                    card.ArcTo(position, rotation, cycleArc, cycleDuration, cycleEase);
                else
                    card.MoveTo(position, rotation, moveDuration, moveEase);
            }
        }

        private Vector3 SlotPosition(int index, bool selected)
        {
            Vector3 position = _layout == CardHandLayout.Fan
                ? FanPosition(index)
                : PilePosition(index);

            if (selected)
                position += new Vector3(0f, selectedLift, -selectedPull);

            return position;
        }

        private Quaternion SlotRotation(int index)
        {
            // Face the viewer (the mesh front is +Z), then tilt along the layout.
            float roll = _layout == CardHandLayout.Fan
                ? -FanAngle(index)
                : -pileAngleStep * index;

            return Quaternion.AngleAxis(roll, Vector3.forward)
                   * Quaternion.AngleAxis(180f, Vector3.up);
        }

        private Vector3 PilePosition(int index)
        {
            return new Vector3(
                pileStep.x * index,
                pileStep.y * index,
                -index * pileDepthStep);
        }

        private Vector3 FanPosition(int index)
        {
            float radians = FanAngle(index) * Mathf.Deg2Rad;

            return new Vector3(
                Mathf.Sin(radians) * arcRadius,
                Mathf.Cos(radians) * arcRadius - arcRadius,
                -index * depthStep);
        }

        private float FanAngle(int index)
        {
            int count = _cards.Count;
            if (count <= 1)
                return 0f;

            float totalAngle = Mathf.Min(maxFanAngle, anglePerCard * (count - 1));
            return -totalAngle * 0.5f + totalAngle * index / (count - 1);
        }
    }
}
