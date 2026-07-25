using CardsChaos.Cards;
using CardsChaos.Cards.Album;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Vesolovsky.Game.Views.Album
{
    /// <summary>
    /// One numbered square on a set's page. Empty it shows the set's icon; filled it shows the
    /// card that was put there, which is not necessarily the card that belongs there.
    ///
    /// The slot is both a place to drop a card and a place to pick one back up, because those are
    /// the same gesture in reverse and misfiling is a move the player is allowed to make.
    /// </summary>
    [AddComponentMenu("CardsChaos/Album/Card Slot")]
    public class AlbumCardSlot : MonoBehaviour,
        IDropHandler, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerClickHandler, IAlbumCardSource
    {
        [Tooltip("The slot's own frame, behind both states. Only ever hidden on the padding " +
                 "slots at the end of a short set's last page.")]
        [SerializeField] private Image frame;

        [Tooltip("Shown while the slot is empty. Its inner-shadow child rides along with it, so " +
                 "this one object is the whole empty state.")]
        [SerializeField] private Image setIcon;

        [SerializeField] private Image setIconInnerShadow;

        [Tooltip("Shown once a card is filed here. Carries the card's own inner shadow as a " +
                 "child, so switching this object covers the whole filled state.")]
        [SerializeField] private Image cardImage;

        [Tooltip("The number of the card that belongs in this slot. Part of the empty state: it " +
                 "says which card is still missing, and a filed card carries its own number " +
                 "printed on it.")]
        [SerializeField] private AlbumCardNumber cardNumber;

        [Tooltip("Where the effect for a correctly filed card is spawned. Usually this slot's " +
                 "own transform; give it a separate child to place the effect somewhere else.")]
        [FormerlySerializedAs("impactTarget")]
        [SerializeField] private RectTransform vfxAnchor;

        private AlbumDragController _drag;
        private IAlbumCardInspector _inspector;

        // See AlbumHandCard: a gesture that began a drag out of this slot must not also inspect
        // when it is dropped back onto the same slot.
        private bool _draggedSincePress;

        /// <summary>0-based position on the page. Slot 0 is where card number 1 belongs.</summary>
        public int SlotIndex { get; private set; }

        /// <summary>The set whose page this slot is on - not necessarily the card's set.</summary>
        public string PageSetId { get; private set; }

        public CardRef Card { get; private set; }

        public bool IsEmpty => !Card.IsValid;

        /// <summary>
        /// False on the padding slots at the end of a short set's last page.
        ///
        /// Pages are always built ten slots wide so the grid keeps its five-by-two shape - hand
        /// it seven children and it rearranges itself into four columns - so a set whose card
        /// count is not a round ten ends with slots that stand for no card. They hold the shape
        /// and take nothing.
        /// </summary>
        public bool IsUsable { get; private set; } = true;

        public Sprite Artwork => cardImage.sprite;

        public RectTransform Rect => (RectTransform)transform;

        public RectTransform VfxAnchor => vfxAnchor != null ? vfxAnchor : Rect;

        public void Initialize(
            AlbumDragController drag, IAlbumCardInspector inspector, string pageSetId, int slotIndex,
            CardSetDefinition set)
        {
            _drag = drag;
            _inspector = inspector;
            PageSetId = pageSetId;
            SlotIndex = slotIndex;
            IsUsable = true;
            frame.enabled = true;

            setIcon.sprite = set.Icon;
            setIconInnerShadow.sprite = set.IconInnerShadow;

            // An Image with no sprite draws a white box, which would read as a filled slot.
            setIcon.enabled = set.Icon != null;
            setIconInnerShadow.enabled = set.IconInnerShadow != null;

            // Slots run in number order from zero, so this square is waiting for card index + 1.
            cardNumber.SetNumber(slotIndex + 1);

            Clear();
        }

        /// <summary>
        /// Turns the slot into invisible padding. It keeps its cell in the grid and drops
        /// everything else - see <see cref="IsUsable"/>.
        /// </summary>
        public void MakeUnused()
        {
            IsUsable = false;
            frame.enabled = false;

            Clear();
        }

        public void Fill(CardRef card, Sprite artwork)
        {
            Card = card;
            cardImage.sprite = artwork;

            ShowCard(true);
        }

        public void Clear()
        {
            Card = CardRef.None;
            cardImage.sprite = null;

            ShowCard(false);
        }

        /// <summary>
        /// Switches between the filled and the empty look, without changing what the slot is
        /// recorded as holding.
        ///
        /// That separation is what lets the slot read as empty from the moment a card is lifted
        /// off it rather than only once the card is put down somewhere - the set icon comes back
        /// under the player's hand as they drag, which is the whole point of the icon.
        /// </summary>
        private void ShowCard(bool visible)
        {
            cardImage.gameObject.SetActive(visible);

            // Padding slots have no empty state to fall back to - they are meant to be nothing at
            // all, and only exist to hold their cell in the grid.
            bool empty = !visible && IsUsable;

            setIcon.gameObject.SetActive(empty);
            cardNumber.SetVisible(empty);
        }

        #region Taking a card back out

        public void OnPointerDown(PointerEventData eventData) => _draggedSincePress = false;

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Nothing to pick up, and no drag to start - without this the empty slot would eat
            // the gesture and the player would be dragging a blank.
            if (!IsUsable || IsEmpty || eventData.button != PointerEventData.InputButton.Left)
                return;

            _draggedSincePress = true;
            _drag.Begin(this, eventData);
        }

        public void OnDrag(PointerEventData eventData) => _drag.Move(this, eventData);

        public void OnEndDrag(PointerEventData eventData) => _drag.End(this, eventData);

        /// <summary>
        /// A plain click on a filled slot opens the card up close, the same as clicking one in
        /// hand. Anything that began a drag is not a click - dragging a card out and dropping it
        /// straight back must not inspect it - so the two gestures never collide.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_draggedSincePress && IsUsable && !IsEmpty
                && eventData.button == PointerEventData.InputButton.Left)
            {
                _inspector.Inspect(this);
            }
        }

        void IAlbumCardSource.OnCardLifted() => ShowCard(false);

        void IAlbumCardSource.OnCardReturned() => ShowCard(true);

        void IAlbumCardSource.OnCardTaken() => Clear();

        #endregion

        /// <summary>
        /// A card was let go of over this slot. Only an empty slot answers: the album never
        /// displaces a card, because the displaced one would have nowhere to go.
        /// </summary>
        public void OnDrop(PointerEventData eventData)
        {
            if (!IsUsable || !IsEmpty)
                return;

            _drag.TryDropOn(this);
        }
    }
}
