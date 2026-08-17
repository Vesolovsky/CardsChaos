using CardsChaos.Cards;
using CardsChaos.Cards.Album;
using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vesolovsky.Game.Views.Album
{
    /// <summary>
    /// One card of the player's hand, drawn flat on the album's pile.
    ///
    /// It stands for a real card out in the room and keeps hold of it, because filing the card
    /// has to take that one out of the hand - the pile here is a view of the hand, not a second
    /// copy of it.
    /// </summary>
    [AddComponentMenu("CardsChaos/Album/Hand Card")]
    public class AlbumHandCard : MonoBehaviour, IPointerDownHandler, IBeginDragHandler,
        IDragHandler, IEndDragHandler, IPointerClickHandler, IAlbumCardSource
    {
        [SerializeField] private Image cardImage;

        [Tooltip("The material a duplicate is drawn with - the same grey twin a misplaced card gets " +
                 "in its slot, M_Card_UI_Gray. Left empty, a duplicate is drawn in full colour.")]
        [SerializeField] private Material duplicateMaterial;

        private AlbumDragController _drag;
        private IAlbumCardInspector _inspector;
        private Tween _positionTween;
        private Tween _rotationTween;

        // The image's own material as the prefab set it up, so a card drawn grey can be given its
        // colour back when its twin leaves the album or the other copy is thrown.
        private Material _cardMaterial;

        // A press that turns into a drag must not also inspect on release. The event system
        // usually suppresses that click itself, but a short drag back to where it started can
        // slip one through - so the intent is tracked here rather than left to chance.
        private bool _draggedSincePress;

        /// <summary>The card out in the room that this one is standing in for.</summary>
        public Card WorldCard { get; private set; }

        public CardRef Card { get; private set; }

        public Sprite Artwork => cardImage.sprite;

        public RectTransform Rect => (RectTransform)transform;

        private void Awake() => _cardMaterial = cardImage.material;

        public void Initialize(AlbumDragController drag, IAlbumCardInspector inspector, Card worldCard)
        {
            _drag = drag;
            _inspector = inspector;
            WorldCard = worldCard;

            // The album draws the hand as its own flat pile, but the grey is the room card's state:
            // following it rather than working the rule out again keeps the two views agreeing even
            // as filing a card changes which copy in hand is the spare.
            worldCard.ShadedChanged += OnWorldCardShadedChanged;
            ApplyShade(worldCard.IsShaded);

            CardIdentity identity = worldCard.Identity;
            if (identity == null)
            {
                Debug.LogError(
                    $"[{nameof(AlbumHandCard)}] '{worldCard.name}' has no {nameof(CardIdentity)}, " +
                    "so the album cannot tell which card it is.", worldCard);

                return;
            }

            Card = CardRef.From(identity);
            cardImage.sprite = identity.Artwork;

            if (identity.Artwork == null)
            {
                Debug.LogError(
                    $"[{nameof(AlbumHandCard)}] {Card} has no artwork sprite. Run " +
                    "Tools/Cards/Build All Card Sets to fill it in.", worldCard);
            }
        }

        /// <summary>
        /// Slides the card to its place in the fan. Called on every reshuffle, so a card that is
        /// already where it belongs is left alone rather than re-tweened.
        /// </summary>
        public void MoveTo(Vector2 anchoredPosition, float angle, float duration, Ease ease)
        {
            StopTweens();

            var rotation = Quaternion.Euler(0f, 0f, angle);

            if (duration <= 0f)
            {
                Rect.anchoredPosition = anchoredPosition;
                Rect.localRotation = rotation;
                return;
            }

            if (Rect.anchoredPosition != anchoredPosition)
                _positionTween = Tween.UIAnchoredPosition(Rect, anchoredPosition, duration, ease);

            if (Rect.localRotation != rotation)
                _rotationTween = Tween.LocalRotation(Rect, rotation, duration, ease);
        }

        /// <summary>
        /// Same as <see cref="MoveTo"/>, but bowed out along the way by <paramref name="arc"/>, so
        /// the card visibly travels over the fan rather than sliding straight through it. Used for
        /// the one card the wheel carries from one end to the other.
        /// </summary>
        public void ArcTo(Vector2 anchoredPosition, float angle, Vector2 arc, float duration, Ease ease)
        {
            StopTweens();

            var rotation = Quaternion.Euler(0f, 0f, angle);

            if (duration <= 0f)
            {
                Rect.anchoredPosition = anchoredPosition;
                Rect.localRotation = rotation;
                return;
            }

            Vector2 start = Rect.anchoredPosition;
            Vector2 control = (start + anchoredPosition) * 0.5f + arc;
            RectTransform rect = Rect;

            // Quadratic bezier, the same curve the room's pile uses to swing a card clear of the
            // stack: the control point is approached, never reached, so the bow reads softer than
            // the offset suggests.
            _positionTween = Tween.Custom(0f, 1f, duration, t =>
            {
                float inverse = 1f - t;
                rect.anchoredPosition = inverse * inverse * start
                                        + 2f * inverse * t * control
                                        + t * t * anchoredPosition;
            }, ease);

            if (Rect.localRotation != rotation)
                _rotationTween = Tween.LocalRotation(Rect, rotation, duration, ease);
        }

        public void OnPointerDown(PointerEventData eventData) => _draggedSincePress = false;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            _draggedSincePress = true;
            _drag.Begin(this, eventData);
        }

        public void OnDrag(PointerEventData eventData) => _drag.Move(this, eventData);

        public void OnEndDrag(PointerEventData eventData) => _drag.End(this, eventData);

        /// <summary>
        /// A plain click - press and release without a drag between them - opens the card up
        /// close. Anything that began a drag is not a click, whatever the event system decides to
        /// raise, so drag-to-file and click-to-inspect never fight over the same gesture.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_draggedSincePress && eventData.button == PointerEventData.InputButton.Left)
                _inspector.Inspect(this);
        }

        void IAlbumCardSource.OnCardLifted() => cardImage.enabled = false;

        void IAlbumCardSource.OnCardReturned() => cardImage.enabled = true;

        // Nothing to do: the pile is rebuilt from the hand, and the hand is what actually lost
        // the card. Showing the image again would make it flash for the frame before the pile
        // notices and destroys this object.
        void IAlbumCardSource.OnCardTaken() { }

        private void OnWorldCardShadedChanged(Card card) => ApplyShade(card.IsShaded);

        private void ApplyShade(bool shaded)
        {
            cardImage.material = shaded && duplicateMaterial != null
                ? duplicateMaterial
                : _cardMaterial;
        }

        private void StopTweens()
        {
            if (_positionTween.isAlive)
                _positionTween.Stop();

            if (_rotationTween.isAlive)
                _rotationTween.Stop();
        }

        private void OnDestroy()
        {
            if (WorldCard != null)
                WorldCard.ShadedChanged -= OnWorldCardShadedChanged;

            StopTweens();
        }
    }
}
