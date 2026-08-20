using System;
using PrimeTween;
using RoboRyanTron.SearchableEnum;
using UnityEngine;
using UnityEngine.UI;
using Vesolovsky.Core.UISystem.UIComponents;

namespace Vesolovsky.Game.Views.MainMenu
{
    /// <summary>
    /// One card of the main menu - a button that happens to be shaped like a playing card.
    ///
    /// The card knows two poses and nothing else: where it rests in the fan, and where it sits
    /// when the cursor is on it. Its resting pose is handed to it by <see cref="MainMenuCardFan"/>
    /// rather than read off the transform, so hiding a card (Continue, on a fresh save) can close
    /// the fan back up around the gap without anything here having to be re-authored.
    ///
    /// Expected object layout - the holder is this component, and it is what moves:
    /// <code>
    /// MenuCard            &lt;- this component, no graphic of its own
    ///   Highlight         &lt;- the glow. First, so it is drawn behind the face.
    ///   Face              &lt;- Image + VButton. Its own size; the glow's is deliberately different.
    ///     Icon
    ///     Label
    ///     SubLabel        &lt;- Continue only
    /// </code>
    /// </summary>
    [AddComponentMenu("CardsChaos/Main Menu/Menu Card")]
    public class MainMenuCard : MonoBehaviour
    {
        [Tooltip("Which menu entry this card is. The view binds what a click does by this, so no " +
                 "two cards in one fan should carry the same one.")]
        [SerializeField, SearchableEnum] private MainMenuAction action = MainMenuAction.None;

        [Tooltip("The card face. Both the hover and the click come off this, so it - not the " +
                 "holder - is the object that carries the raycast.")]
        [SerializeField] private VButton button;

        [Tooltip("The glow behind the card, switched on while the card is raised. Keep it as the " +
                 "first child of the holder so it draws under the face, and leave it disabled in " +
                 "the prefab - the hover is what turns it on.")]
        [SerializeField] private GameObject highlight;

        [Tooltip("The card's picture. Switched off on its own when no sprite is assigned, so a " +
                 "card that is all text does not draw an empty white box.")]
        [SerializeField] private Image icon;

        [SerializeField] private VText label;

        [Tooltip("The second line under the label. Only Continue uses it, for the collection " +
                 "progress; leave it empty on every other card.")]
        [SerializeField] private VText subLabel;

        [Header("Hover")]
        [Tooltip("How far the card rises when the cursor lands on it, in pixels.")]
        [SerializeField] private float hoverLift = 48f;

        [Tooltip("How much the card grows while raised. Keep it small - the lift is what reads, " +
                 "the scale is only there to sell it coming towards the player.")]
        [SerializeField] private float hoverScale = 1.05f;

        [Tooltip("Raise the card along its own up-axis rather than straight up the screen. On a " +
                 "fanned card that reads as it being drawn out of the hand; straight up reads as " +
                 "it sliding.")]
        [SerializeField] private bool liftAlongCardUp = true;

        [SerializeField] private float hoverDuration = 0.18f;
        [SerializeField, SearchableEnum] private Ease hoverEase = Ease.OutBack;

        [Tooltip("The way back down. Softer than the way up - a card settling, not snapping.")]
        [SerializeField] private float releaseDuration = 0.22f;

        [SerializeField, SearchableEnum] private Ease releaseEase = Ease.OutQuad;

        /// <summary>The card was clicked. The view decides what that means.</summary>
        public event Action<MainMenuCard> Clicked;

        /// <summary>The cursor arrived. The fan listens, so only ever one card is raised.</summary>
        public event Action<MainMenuCard> HoverEntered;

        public event Action<MainMenuCard> HoverExited;

        private RectTransform _rect;
        private Vector2 _restPosition;
        private float _restAngle;
        private Vector3 _restScale = Vector3.one;

        private Tween _moveTween;
        private Tween _scaleTween;

        private bool _hovered;

        public MainMenuAction Action => action;

        public RectTransform Rect
        {
            get
            {
                EnsureCached();
                return _rect;
            }
        }

        /// <summary>Where the fan put this card. What the hover and the deal-in both animate from.</summary>
        public Vector2 RestPosition
        {
            get
            {
                EnsureCached();
                return _restPosition;
            }
        }

        public float RestAngle
        {
            get
            {
                EnsureCached();
                return _restAngle;
            }
        }

        /// <summary>Whether this card is part of the menu at all - Continue is not, on a fresh save.</summary>
        public bool IsShown => gameObject.activeSelf;

        private void Awake()
        {
            EnsureCached();

            if (button != null)
            {
                button.Bind(OnClicked);
                button.PointerEnter += OnPointerEnter;
                button.PointerExit += OnPointerExit;
            }
            else
            {
                Debug.LogError($"[{nameof(MainMenuCard)}] '{name}' has no {nameof(VButton)}, so it " +
                               "can neither be hovered nor clicked.", this);
            }

            // An Image with no sprite draws a white rectangle, which on a card reads as a
            // missing icon rather than as no icon at all.
            if (icon != null)
                icon.enabled = icon.sprite != null;

            if (highlight != null)
                highlight.SetActive(false);
        }

        public void SetShown(bool shown) => gameObject.SetActive(shown);

        public void SetLabel(string text) => label?.SetText(text);

        /// <summary>
        /// The extra line under the label. Empty text takes the whole object away rather than
        /// leaving a blank gap, so a card without one is laid out as if it never had it.
        /// </summary>
        public void SetSubLabel(string text)
        {
            if (subLabel == null)
                return;

            bool hasText = !string.IsNullOrEmpty(text);
            subLabel.gameObject.SetActive(hasText);

            if (hasText)
                subLabel.SetText(text);
        }

        /// <summary>
        /// Puts the card where the fan says it belongs, and makes that its home: every animation
        /// from here on measures from this pose. Snaps rather than tweens - this is a layout, not
        /// a move the player is meant to see.
        /// </summary>
        public void SetRestPose(Vector2 anchoredPosition, float angle)
        {
            EnsureCached();

            _restPosition = anchoredPosition;
            _restAngle = angle;

            StopTweens();

            _rect.anchoredPosition = anchoredPosition;
            _rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            _rect.localScale = _restScale;

            // A relayout while a card was raised would otherwise leave the glow burning under a
            // card the cursor is no longer on.
            _hovered = false;

            if (highlight != null)
                highlight.SetActive(false);
        }

        /// <summary>
        /// Moves the card without touching where it thinks home is. The deal-in uses it to park
        /// the card off screen before flying it back to its resting pose.
        /// </summary>
        public void SetPose(Vector2 anchoredPosition, float angle)
        {
            EnsureCached();

            StopTweens();

            _rect.anchoredPosition = anchoredPosition;
            _rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>
        /// Raises and lights the card, or lets it back down. Driven by the fan rather than by the
        /// card's own pointer events, because only one card in the fan may ever be up at once.
        /// </summary>
        public void SetHovered(bool hovered)
        {
            EnsureCached();

            if (_hovered == hovered)
                return;

            _hovered = hovered;

            if (highlight != null)
                highlight.SetActive(hovered);

            Vector2 target = hovered ? _restPosition + LiftOffset() : _restPosition;
            Vector3 scale = hovered ? _restScale * hoverScale : _restScale;
            float duration = hovered ? hoverDuration : releaseDuration;
            Ease ease = hovered ? hoverEase : releaseEase;

            StopTweens();

            if (duration <= 0f)
            {
                _rect.anchoredPosition = target;
                _rect.localScale = scale;
                return;
            }

            // Unscaled, like everything else in a menu: the game's clock may well still be stopped
            // behind us - a quit straight out of a paused room leaves it that way - and a menu
            // that answers the cursor only when the world happens to be running is broken.
            _moveTween = Tween.UIAnchoredPosition(_rect, target, duration, ease, useUnscaledTime: true);
            _scaleTween = Tween.Scale(_rect, scale, duration, ease, useUnscaledTime: true);
        }

        /// <summary>
        /// Which way "up" is for this card. Along its own axis by default, so a card at the end of
        /// the fan lifts out along the way it is leaning instead of shearing off the arc.
        /// </summary>
        private Vector2 LiftOffset()
        {
            if (!liftAlongCardUp)
                return new Vector2(0f, hoverLift);

            float radians = _restAngle * Mathf.Deg2Rad;
            return new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians)) * hoverLift;
        }

        /// <summary>
        /// Reads the authored pose once, before anything has had a chance to move the card. Called
        /// from every entry point rather than only from Awake, because the fan lays the cards out
        /// as part of its own Awake and the order between the two is not ours to decide.
        /// </summary>
        private void EnsureCached()
        {
            if (_rect != null)
                return;

            _rect = (RectTransform)transform;
            _restPosition = _rect.anchoredPosition;
            _restAngle = _rect.localEulerAngles.z;
            _restScale = _rect.localScale;
        }

        private void OnClicked() => Clicked?.Invoke(this);

        private void OnPointerEnter() => HoverEntered?.Invoke(this);

        private void OnPointerExit() => HoverExited?.Invoke(this);

        private void StopTweens()
        {
            if (_moveTween.isAlive)
                _moveTween.Stop();

            if (_scaleTween.isAlive)
                _scaleTween.Stop();
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.PointerEnter -= OnPointerEnter;
                button.PointerExit -= OnPointerExit;
                button.Unbind();
            }

            StopTweens();
        }
    }
}
