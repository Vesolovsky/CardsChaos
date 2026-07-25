using CardsChaos.Cards.Album;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace Vesolovsky.Game.Views.Album
{
    /// <summary>
    /// Opens a card up close, the way clicking one in hand does out in the room - but here it is
    /// a panel over the album rather than a card lifted off the table.
    ///
    /// This is a sub-mode of the album, not a view of its own: the album is already the modal
    /// thing holding the room still, and the inspect is one more layer over it. So there is no
    /// load and no context - the panel is part of the album prefab, switched on and off.
    /// </summary>
    public interface IAlbumCardInspector
    {
        bool IsOpen { get; }

        /// <summary>Opens the close-up on whatever card a source is holding.</summary>
        void Inspect(IAlbumCardSource source);
    }

    /// <summary>
    /// The close-up itself. It knows how to show one card and turn it over, and nothing about who
    /// asked or what happens next; the album drives it.
    ///
    /// The card is flat, so it is turned over by squashing its width to nothing and back rather
    /// than by spinning it about its axis. A width-zero swap is what keeps the back the right way
    /// round - a real rotation past ninety degrees shows the front's mirror image, and no card
    /// back should read backwards. It also sits better on a flat card than a pseudo-3D turn that
    /// only ever looks like a squash on a flat canvas anyway.
    /// </summary>
    [AddComponentMenu("CardsChaos/Album/Card Inspector")]
    public class AlbumCardInspector : MonoBehaviour, IAlbumCardInspector
    {
        [Tooltip("The whole overlay. Faded in and out, and made to block raycasts so the album " +
                 "behind it is untouchable while a card is open. Needs a full-screen backdrop " +
                 "graphic under the card, or clicks fall straight through to the album.")]
        [SerializeField] private CanvasGroup group;

        [Tooltip("The card. This is the only thing that turns - centre and size it in the prefab.")]
        [SerializeField] private Image cardImage;

        [Header("Flip")]
        [Tooltip("How sharply the card turns over. Higher snaps; lower lets the turn read.")]
        [SerializeField] private float flipSpeed = 12f;

        [Header("Open / close")]
        [SerializeField] private float fadeDuration = 0.15f;

        [Tooltip("The card springs to full size as it opens, for a bit of pop.")]
        [SerializeField, Range(0.5f, 1f)] private float openScaleFrom = 0.9f;

        [Tooltip("How quickly the pop settles. Shares the flip's units.")]
        [SerializeField] private float openScaleSpeed = 16f;

        private Sprite _front;
        private Sprite _back;
        private CardArtworkResolver _artwork;
        private Camera _uiCamera;

        // Facing runs 0 (front) to 1 (back); the card's width is the cosine of it, so it pinches
        // to an edge at the half-way point where the sprite is swapped.
        private bool _showingBack;
        private float _facing;
        private float _openScale;
        private int _openedFrame = -1;
        private Tween _fadeTween;

        public bool IsOpen { get; private set; }

        /// <summary>
        /// True on the very frame the close-up opened. The opening click is still being reported
        /// that frame, and the album has to know not to read it again as a flip.
        /// </summary>
        public bool JustOpened => Time.frameCount == _openedFrame;

        public void Initialize(CardArtworkResolver artwork)
        {
            _artwork = artwork;

            // The camera the card's screen rectangle is measured against - null under an overlay
            // canvas, which is what the containment test wants there.
            Canvas canvas = cardImage.canvas;
            _uiCamera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.rootCanvas.worldCamera
                : null;

            // Starts closed and, above all, not blocking - an overlay left blocking raycasts over
            // the whole album would swallow every click meant for the cards behind it.
            IsOpen = false;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        /// <summary>
        /// Whether the cursor is over the open card, so the album can tell a flip (on the card)
        /// from a leave (off it). The card's counterpart to the world inspector's raycast.
        /// </summary>
        public bool IsPointerOverCard(Vector2 screenPosition)
        {
            return IsOpen && RectTransformUtility.RectangleContainsScreenPoint(
                cardImage.rectTransform, screenPosition, _uiCamera);
        }

        public void Inspect(IAlbumCardSource source)
        {
            if (source == null)
                return;

            Sprite front = source.Artwork;
            if (front == null)
                return;

            _front = front;
            _back = _artwork.ResolveBack(source.Card);

            _showingBack = false;
            _facing = 0f;
            _openScale = openScaleFrom;
            cardImage.sprite = _front;
            ApplyTransform();

            _openedFrame = Time.frameCount;
            IsOpen = true;

            group.blocksRaycasts = true;
            group.interactable = true;

            Fade(1f);
        }

        /// <summary>Turns the card over. Ignored on the opening frame - see <see cref="JustOpened"/>.</summary>
        public void Flip()
        {
            if (!IsOpen || JustOpened || _back == null)
                return;

            _showingBack = !_showingBack;
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;

            // Dropped the instant it closes, so a fading-out overlay is already click-through and
            // the card underneath the cursor can be picked up straight away.
            group.blocksRaycasts = false;
            group.interactable = false;

            Fade(0f);
        }

        private void Update()
        {
            if (!IsOpen)
                return;

            float t = Time.deltaTime;

            // Framerate-independent approach, the same shape the room's inspector eases with.
            _facing = Mathf.Lerp(_facing, _showingBack ? 1f : 0f, 1f - Mathf.Exp(-flipSpeed * t));
            _openScale = Mathf.Lerp(_openScale, 1f, 1f - Mathf.Exp(-openScaleSpeed * t));

            // The sprite swaps at the pinch, where the card is edge-on and the change is hidden.
            Sprite facing = _facing < 0.5f ? _front : _back;
            if (facing != null && cardImage.sprite != facing)
                cardImage.sprite = facing;

            ApplyTransform();
        }

        private void ApplyTransform()
        {
            // Width is the cosine of the turn: full at either face, zero at the edge. Never
            // negative, so the back is drawn the right way round rather than mirrored.
            float width = Mathf.Abs(Mathf.Cos(_facing * Mathf.PI));
            cardImage.rectTransform.localScale = new Vector3(_openScale * width, _openScale, 1f);
        }

        private void Fade(float alpha)
        {
            if (_fadeTween.isAlive)
                _fadeTween.Stop();

            _fadeTween = Tween.Alpha(group, alpha, fadeDuration);
        }

        private void OnDestroy()
        {
            if (_fadeTween.isAlive)
                _fadeTween.Stop();
        }
    }
}
