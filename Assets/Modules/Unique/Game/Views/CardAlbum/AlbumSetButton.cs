using System;
using CardsChaos.Cards;
using PrimeTween;
using RoboRyanTron.SearchableEnum;
using UnityEngine;
using UnityEngine.UI;
using Vesolovsky.Core.UISystem.UIComponents;

namespace Vesolovsky.Game.Views.Album
{
    /// <summary>
    /// One set in the list down the album's left-hand side: its name, its icon, and how far
    /// through it the player is.
    ///
    /// The button knows which set it stands for and nothing about the album beyond that - which
    /// one is open, and what happens on a click, are the view's business.
    /// </summary>
    [AddComponentMenu("CardsChaos/Album/Set Button")]
    public class AlbumSetButton : MonoBehaviour
    {
        [SerializeField] private VButton button;
        [SerializeField] private VText setName;
        [SerializeField] private VText progressText;

        [Tooltip("The set's icon. Always shown.")]
        [SerializeField] private Image icon;

        [Tooltip("The darker copy that sits over the icon to give it depth. Always shown.")]
        [SerializeField] private Image iconInnerShadow;

        [Tooltip("Lights up on the open set and fades out again when another one is opened. " +
                 "This is the only thing marking which set the grid is currently showing.")]
        [SerializeField] private Image innerGlow;

        [Header("Selection glow")]
        [SerializeField, Range(0f, 1f)] private float selectedAlpha = 1f;

        [Tooltip("Faster in than out: the set the player just asked for should answer at once, " +
                 "while the one they left can afford to die down behind them.")]
        [SerializeField] private float fadeInDuration = 0.15f;
        [SerializeField] private float fadeOutDuration = 0.3f;
        [SerializeField, SearchableEnum] private Ease fadeEase = Ease.OutQuad;

        [Header("Hand pulse")]
        [Tooltip("What breathes while the player holds a card from this set - the \"Set sense\" " +
                 "upgrade. Best a parent holding both the icon and its inner shadow, so the two " +
                 "move together. Left empty it falls back to the icon alone.")]
        [SerializeField] private RectTransform pulseTarget;

        [Tooltip("Peak scale of the breath. Meant to be noticed out of the corner of the eye, not " +
                 "to pull the eye off the page - keep it small.")]
        [SerializeField] private Vector3 pulseScale = new Vector3(1.06f, 1.06f, 1f);

        [Tooltip("Seconds for one full breath in-and-out. Shared by every button, and the phase is " +
                 "read off the clock rather than counted from when each one started, so they all " +
                 "breathe together however many join in or drop out.")]
        [SerializeField] private float pulsePeriod = 1.1f;

        [Tooltip("The colour the icon takes while the set is being marked - the album's warm gold. " +
                 "Only the icon; the inner shadow keeps its own colour so the depth survives.")]
        [SerializeField] private Color pulseIconColor = new Color(0.765f, 0.573f, 0.345f, 1f);

        private Action<AlbumSetButton> _clicked;
        private Tween _glowTween;

        private bool _pulsing;

        // The icon's authored colour, captured before anything can tint it so the way back is
        // whatever the prefab was drawn with rather than an assumed white.
        private Color _iconRestColor = Color.white;

        private RectTransform PulseTarget =>
            pulseTarget != null ? pulseTarget
            : icon != null ? icon.rectTransform
            : null;

        /// <summary>The set this button opens. Null until <see cref="Bind"/> has run.</summary>
        public CardSetDefinition Set { get; private set; }

        public void Bind(CardSetDefinition set, Action<AlbumSetButton> clicked)
        {
            Set = set;
            _clicked = clicked;

            setName.SetText(set.SetName);

            ApplyIcon(icon, set.Icon);
            ApplyIcon(iconInnerShadow, set.IconInnerShadow);

            button.Bind(OnClicked);
            SetSelected(false, immediately: true);
        }

        /// <summary>
        /// Updates the "X / Y". Y is fixed by the set, so only the filed count is passed in.
        /// </summary>
        public void SetProgress(int filed)
        {
            if (Set == null)
            {
                Debug.LogError($"[{nameof(AlbumSetButton)}] Progress set before Bind.", this);
                return;
            }

            progressText.SetText($"{filed} / {Set.CardCount}");
        }

        public void SetSelected(bool selected, bool immediately = false)
        {
            float target = selected ? selectedAlpha : 0f;

            if (_glowTween.isAlive)
                _glowTween.Stop();

            if (immediately)
            {
                SetGlowAlpha(target);
                return;
            }

            // Skipped rather than tweened to where it already is: PrimeTween warns about a
            // redundant end value, and rebuilding the set list re-asserts every button's state.
            if (Mathf.Approximately(innerGlow.color.a, target))
                return;

            _glowTween = Tween.Alpha(
                innerGlow, target, selected ? fadeInDuration : fadeOutDuration, fadeEase);
        }

        /// <summary>
        /// Starts or stops the breath that marks a set the player is carrying a card from, and the
        /// gold the icon wears while it does. Stated rather than toggled - the view re-asserts every
        /// button whenever the hand changes - so asking for what is already running is a no-op.
        /// </summary>
        public void SetPulsing(bool pulsing)
        {
            if (_pulsing == pulsing)
                return;

            _pulsing = pulsing;

            if (icon != null)
                icon.color = pulsing ? pulseIconColor : _iconRestColor;

            if (!pulsing)
            {
                RectTransform target = PulseTarget;
                if (target != null)
                    target.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// Drives the breath from the clock rather than from a tween started when this button began
        /// pulsing. That is the whole point: several sets light up at once and more join as the hand
        /// changes, and a tween each would have every one of them breathing on its own beat. Read off
        /// a shared time instead, they are in step by construction - including a button that starts
        /// halfway through a breath, which simply picks the wave up where everyone else is.
        ///
        /// Unscaled, so the album keeps breathing behind anything that stops the game clock.
        /// </summary>
        private void Update()
        {
            if (!_pulsing)
                return;

            RectTransform target = PulseTarget;
            if (target == null)
                return;

            float period = Mathf.Max(0.05f, pulsePeriod);

            // A raised cosine over the period: rests at 1, eases up to the peak at the half-way
            // point and back, which is the same shape a yoyoed InOutSine tween traces.
            float phase = (1f - Mathf.Cos(Time.unscaledTime / period * 2f * Mathf.PI)) * 0.5f;

            target.localScale = Vector3.LerpUnclamped(Vector3.one, pulseScale, phase);
        }

        private void OnClicked() => _clicked?.Invoke(this);

        private void SetGlowAlpha(float alpha)
        {
            Color color = innerGlow.color;
            color.a = alpha;
            innerGlow.color = color;
        }

        /// <summary>
        /// A set whose art has not been drawn yet has no sprite, and an Image with no sprite
        /// draws a solid white box. Switching the graphic off instead leaves an honest gap.
        /// </summary>
        private static void ApplyIcon(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        // Captured here rather than in Bind: the buttons are freshly instantiated each time the list
        // is built, so this is the prefab's own colour and never a tint left over from a past pulse.
        private void Awake()
        {
            if (icon != null)
                _iconRestColor = icon.color;
        }

        private void OnDestroy()
        {
            if (_glowTween.isAlive)
                _glowTween.Stop();
        }
    }
}
