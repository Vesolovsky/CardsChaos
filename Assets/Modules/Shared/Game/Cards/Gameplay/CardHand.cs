using System.Collections.Generic;
using PrimeTween;
using RoboRyanTron.SearchableEnum;
using UnityEngine;

namespace CardsChaos.Cards
{
    /// <summary>
    /// Holds picked up cards fanned out at the bottom of the screen.
    /// Index 0 is the leftmost slot; new cards enter there and become selected.
    /// When full, the rightmost card is thrown out to make room.
    /// </summary>
    [AddComponentMenu("CardsChaos/Card Hand")]
    public class CardHand : MonoBehaviour
    {
        [Header("Anchor")]
        [Tooltip("Cards are parented here. Its +Z must point away from the viewer.")]
        [SerializeField] private Transform anchor;

        [Tooltip("Where a card sits while being inspected. Same +Z rule as the anchor; " +
                 "the closer it is to the camera the larger the card reads.")]
        [SerializeField] private Transform inspectAnchor;

        [Header("Layout")]
        [SerializeField] private int slotCount = 5;
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

        [Header("Throw")]
        [SerializeField] private float throwSpeed = 1.2f;
        [SerializeField] private float throwLift = 0.35f;
        [SerializeField] private float throwSpin = 6f;

        private readonly List<Card> _cards = new List<Card>();
        private int _selectedIndex = -1;

        public bool IsFull => _cards.Count >= slotCount;

        public Card SelectedCard =>
            _selectedIndex >= 0 && _selectedIndex < _cards.Count ? _cards[_selectedIndex] : null;

        /// <summary>
        /// Lifts the selected card out of the fan and onto the inspect anchor. The caller
        /// drives its transform from there, so any running slot tween is cancelled.
        /// </summary>
        public Card PresentForInspect()
        {
            Card card = SelectedCard;
            if (card == null || inspectAnchor == null)
                return null;

            card.StopAnimation();
            card.transform.SetParent(inspectAnchor, worldPositionStays: true);

            return card;
        }

        /// <summary>Drops the inspected card back into the fan.</summary>
        public void ReturnFromInspect(Card card)
        {
            if (card == null)
                return;

            card.transform.SetParent(anchor, worldPositionStays: true);
            Relayout();
        }

        public void PickUp(Card card)
        {
            if (card == null || card.IsHeld || anchor == null)
                return;

            if (IsFull)
                ThrowAt(_cards.Count - 1);

            card.AttachTo(anchor);
            _cards.Insert(0, card);
            _selectedIndex = 0;

            Relayout();
        }

        public void ThrowSelected()
        {
            if (_selectedIndex < 0)
                return;

            ThrowAt(_selectedIndex);
            _selectedIndex = Mathf.Min(_selectedIndex, _cards.Count - 1);

            Relayout();
        }

        public void MoveSelection(int delta)
        {
            int count = _cards.Count;
            if (count == 0)
                return;

            _selectedIndex = ((_selectedIndex + delta) % count + count) % count;

            Relayout();
        }

        private void ThrowAt(int index)
        {
            Card card = _cards[index];
            _cards.RemoveAt(index);

            Vector3 direction = (anchor.forward + anchor.up * throwLift).normalized;
            card.Release(direction * throwSpeed, Random.onUnitSphere * throwSpin);
        }

        private void Relayout()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                bool selected = i == _selectedIndex;

                _cards[i].MoveTo(SlotPosition(i, selected), SlotRotation(i), moveDuration, moveEase);
            }
        }

        private Vector3 SlotPosition(int index, bool selected)
        {
            float radians = SlotAngle(index) * Mathf.Deg2Rad;

            var position = new Vector3(
                Mathf.Sin(radians) * arcRadius,
                Mathf.Cos(radians) * arcRadius - arcRadius,
                -index * depthStep);

            if (selected)
                position += new Vector3(0f, selectedLift, -selectedPull);

            return position;
        }

        private Quaternion SlotRotation(int index)
        {
            // Face the viewer (the mesh front is +Z), then tilt along the fan.
            return Quaternion.AngleAxis(-SlotAngle(index), Vector3.forward)
                   * Quaternion.AngleAxis(180f, Vector3.up);
        }

        private float SlotAngle(int index)
        {
            int count = _cards.Count;
            if (count <= 1)
                return 0f;

            float totalAngle = Mathf.Min(maxFanAngle, anglePerCard * (count - 1));
            return -totalAngle * 0.5f + totalAngle * index / (count - 1);
        }
    }
}
