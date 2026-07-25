using System.Collections.Generic;
using CardsChaos.Cards;
using PrimeTween;
using RoboRyanTron.SearchableEnum;
using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Vesolovsky.Game.Views.Album
{
    /// <summary>
    /// The player's hand, held in the album the way a hand of cards is held - spread just wide
    /// enough that every card shows an edge, the last one face up on top of the rest.
    ///
    /// Order is the whole point of the spread, and the wheel is how the player changes it: one
    /// notch carries the card at one end round to the other, exactly as thumbing through a stack
    /// does in the room. It is not scrolling - nothing moves out of view - so there is no scroll
    /// rect here and no position to remember.
    ///
    /// It is a view of <see cref="CardHand"/>, never a copy: the hand stays the one place a card
    /// is or is not in the player's possession, and this reconciles itself against it. That is
    /// also why the wheel turns the real hand rather than reordering these copies - the order
    /// belongs to the hand, and anything done only here would be undone by the next refresh.
    /// </summary>
    [AddComponentMenu("CardsChaos/Album/Hand Pile")]
    public class AlbumHandFan : MonoBehaviour, IDropHandler, IScrollHandler
    {
        [Tooltip("What the cards are parented to. The fan is centred on this rect, so it stays " +
                 "put as cards come and go rather than growing off to one side.")]
        [SerializeField] private RectTransform container;

        [SerializeField] private AlbumHandCard cardPrefab;

        [Header("Fan")]
        [Tooltip("Horizontal gap between neighbouring cards, in pixels. This is the number that " +
                 "decides how much of each card behind stays visible, so it wants to be a good " +
                 "deal smaller than the card is wide but never zero.")]
        [SerializeField] private float spacing = 34f;

        [Tooltip("Degrees between neighbouring cards. Small - this is a hand held close to the " +
                 "chest, not a spread laid out on a table.")]
        [SerializeField] private float anglePerCard = 3f;

        [Tooltip("How far the middle of the fan bows above its ends, in pixels. Zero lays the " +
                 "cards along a straight line.")]
        [SerializeField] private float arcHeight = 12f;

        [Header("Animation")]
        [SerializeField] private float settleDuration = 0.22f;
        [SerializeField, SearchableEnum] private Ease settleEase = Ease.OutQuint;

        [Header("Wheel")]
        [Tooltip("How long the card carried by the wheel takes to travel from one end to the " +
                 "other. Longer than a settle, so the journey reads.")]
        [SerializeField] private float travelDuration = 0.4f;

        [Tooltip("How far the travelling card bows up over the fan on its way across, in pixels, " +
                 "so it passes above the others rather than through them.")]
        [SerializeField] private float travelArc = 60f;

        [SerializeField, SearchableEnum] private Ease travelEase = Ease.InOutQuad;

        // Matches the room's table, so the wheel has the same dead spot everywhere.
        private const float ScrollDeadzone = 0.01f;

        private readonly List<AlbumHandCard> _cards = new List<AlbumHandCard>();

        private DiContainer _container;
        private AlbumDragController _drag;
        private IAlbumCardInspector _inspector;
        private CardHand _hand;

        // The card the wheel just carried across, to be arced rather than slid on the next
        // reconcile. Cleared once used.
        private Card _pendingTraveller;

        [Inject]
        private void Inject(DiContainer container) => _container = container;

        public void Initialize(AlbumDragController drag, IAlbumCardInspector inspector, CardHand hand)
        {
            _drag = drag;
            _inspector = inspector;

            if (_hand != null)
            {
                _hand.Changed -= Refresh;
                _hand.Cycled -= OnCycled;
            }

            _hand = hand;
            _hand.Changed += Refresh;
            _hand.Cycled += OnCycled;

            Reconcile(immediately: true);
        }

        // The hand raises Cycled before Changed, so the traveller is noted here and used by the
        // reconcile that Changed triggers a moment later.
        private void OnCycled(Card traveller) => _pendingTraveller = traveller;

        /// <summary>Brings the pile back in line with the hand, animating the difference.</summary>
        public void Refresh() => Reconcile(immediately: false);

        /// <summary>
        /// A card was let go of over the pile. Taking one back out of the album is the same
        /// gesture as putting it in, run backwards, so this is the other end of that move.
        ///
        /// Needs something with a raycast target underneath it to fire at all - an Image on the
        /// pile's own rect, transparent if need be.
        /// </summary>
        public void OnDrop(PointerEventData eventData) => _drag.TryDropOnPile();

        /// <summary>
        /// One notch of the wheel carries the card at one end of the fan round to the other.
        ///
        /// Read the way a stack of paper is thumbed through, and the same way round as the room's
        /// table: pushing the wheel away sends the leftmost card to the far right. Cards are
        /// reached this way rather than by hovering, because a hand of cards is read by fanning
        /// it, not by pointing at it.
        ///
        /// Fires from anywhere over the hand, cards included - they handle no scroll of their
        /// own, so the event walks up to here.
        /// </summary>
        public void OnScroll(PointerEventData eventData)
        {
            float scroll = eventData.scrollDelta.y;

            if (_hand == null || Mathf.Abs(scroll) < ScrollDeadzone)
                return;

            // Turns the real hand, not this copy of it. The refresh that follows is what
            // rearranges the fan.
            _hand.Cycle(scroll > 0f ? 1 : -1);
        }

        /// <summary>
        /// Brings the pile back in line with the hand.
        ///
        /// Reconciled rather than rebuilt: a card that was already on the pile keeps its object,
        /// so it slides to its new place instead of blinking out and back in a frame later. That
        /// matters most on the move that prompted the refresh - filing a card makes every card
        /// behind it shuffle up one.
        /// </summary>
        private void Reconcile(bool immediately)
        {
            if (_hand == null)
                return;

            IReadOnlyList<Card> handCards = _hand.Cards;

            DropCardsNoLongerHeld(handCards);
            SortToMatch(handCards);
            Layout(immediately);
        }

        private void DropCardsNoLongerHeld(IReadOnlyList<Card> handCards)
        {
            for (int i = _cards.Count - 1; i >= 0; i--)
            {
                AlbumHandCard card = _cards[i];

                // The world card can also have been destroyed outright - that is what filing one
                // into the album does to it - so a null check is not paranoia here.
                if (card.WorldCard != null && Contains(handCards, card.WorldCard))
                    continue;

                _cards.RemoveAt(i);
                Destroy(card.gameObject);
            }
        }

        private void SortToMatch(IReadOnlyList<Card> handCards)
        {
            for (int i = 0; i < handCards.Count; i++)
            {
                Card worldCard = handCards[i];
                int existing = IndexOf(worldCard);

                if (existing < 0)
                {
                    _cards.Insert(i, CreateCard(worldCard));
                    continue;
                }

                if (existing != i)
                {
                    AlbumHandCard card = _cards[existing];
                    _cards.RemoveAt(existing);
                    _cards.Insert(i, card);
                }
            }
        }

        /// <summary>
        /// Spreads the hand out. Index 0 - the top of the hand, where new and just-handled cards
        /// go - sits at the right end and on top, fully readable; the rest fan away to the left,
        /// each drawn under the card to its right.
        ///
        /// The spread is centred on the container rather than grown from one edge, so filing a
        /// card closes the hand around the middle instead of dragging the whole fan sideways.
        /// </summary>
        private void Layout(bool immediately)
        {
            Card traveller = _pendingTraveller;
            _pendingTraveller = null;

            float last = Mathf.Max(1, _cards.Count - 1);

            for (int i = 0; i < _cards.Count; i++)
            {
                // +0.5 at the right end (index 0, the top), -0.5 at the left, 0 in the middle.
                float offset = _cards.Count > 1 ? 0.5f - i / last : 0f;

                // A parabola through the two ends, peaking in the middle - squaring the offset is
                // what makes it a bow rather than a ramp. The bow is measured above the ends, so
                // with one card there are no ends for it to rise from and it stays put.
                float arc = _cards.Count > 1 ? arcHeight * (1f - 4f * offset * offset) : 0f;

                var position = new Vector2(spacing * offset * last, arc);
                float angle = -anglePerCard * offset * last;

                if (!immediately && _cards[i].WorldCard == traveller)
                    _cards[i].ArcTo(position, angle, new Vector2(0f, travelArc), travelDuration, travelEase);
                else
                    _cards[i].MoveTo(position, angle, immediately ? 0f : settleDuration, settleEase);
            }

            // Index 0 is the top of the hand and must draw over the rest. Walking from the back of
            // the fan forward, each card takes the top in turn, so index 0 ends up drawn last.
            for (int i = _cards.Count - 1; i >= 0; i--)
                _cards[i].transform.SetAsLastSibling();
        }

        private AlbumHandCard CreateCard(Card worldCard)
        {
            // Through the container rather than Object.Instantiate: the card prefab is free to
            // grow a VButton or anything else with an [Inject], and a plain instantiate would
            // leave those half-built in a way that only shows up on the first click.
            AlbumHandCard card = _container.InstantiatePrefabForComponent<AlbumHandCard>(
                cardPrefab, container);

            card.Initialize(_drag, _inspector, worldCard);
            return card;
        }

        private int IndexOf(Card worldCard)
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i].WorldCard == worldCard)
                    return i;
            }

            return -1;
        }

        private static bool Contains(IReadOnlyList<Card> cards, Card card)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == card)
                    return true;
            }

            return false;
        }

        private void OnDestroy()
        {
            if (_hand != null)
            {
                _hand.Changed -= Refresh;
                _hand.Cycled -= OnCycled;
            }
        }
    }
}
