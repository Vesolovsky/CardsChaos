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

        private Action<AlbumSetButton> _clicked;
        private Tween _glowTween;

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

        private void OnDestroy()
        {
            if (_glowTween.isAlive)
                _glowTween.Stop();
        }
    }
}
