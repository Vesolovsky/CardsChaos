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
    public class AlbumHandCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IAlbumCardSource
    {
        [SerializeField] private Image cardImage;

        private AlbumDragController _drag;
        private Tween _positionTween;
        private Tween _rotationTween;

        /// <summary>The card out in the room that this one is standing in for.</summary>
        public Card WorldCard { get; private set; }

        public CardRef Card { get; private set; }

        public Sprite Artwork => cardImage.sprite;

        public RectTransform Rect => (RectTransform)transform;

        public void Initialize(AlbumDragController drag, Card worldCard)
        {
            _drag = drag;
            WorldCard = worldCard;

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

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            _drag.Begin(this, eventData);
        }

        public void OnDrag(PointerEventData eventData) => _drag.Move(this, eventData);

        public void OnEndDrag(PointerEventData eventData) => _drag.End(this, eventData);

        void IAlbumCardSource.OnCardLifted() => cardImage.enabled = false;

        void IAlbumCardSource.OnCardReturned() => cardImage.enabled = true;

        // Nothing to do: the pile is rebuilt from the hand, and the hand is what actually lost
        // the card. Showing the image again would make it flash for the frame before the pile
        // notices and destroys this object.
        void IAlbumCardSource.OnCardTaken() { }

        private void StopTweens()
        {
            if (_positionTween.isAlive)
                _positionTween.Stop();

            if (_rotationTween.isAlive)
                _rotationTween.Stop();
        }

        private void OnDestroy() => StopTweens();
    }
}
