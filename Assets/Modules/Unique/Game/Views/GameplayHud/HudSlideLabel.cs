using PrimeTween;
using RoboRyanTron.SearchableEnum;
using TMPro;
using UnityEngine;

namespace Vesolovsky.Game.Views.GameplayHud
{
    /// <summary>
    /// The label that lives under a HUD button's mask and slides out from behind it on hover.
    ///
    /// The label is authored parked off to the side where the mask hides it; showing it slides its
    /// left and right edges home to zero, where it clears the mask and reads. Only the horizontal
    /// edges move - the resting offsets are captured once on Awake, so wherever the designer parks
    /// it is exactly where it slides back to.
    /// </summary>
    [AddComponentMenu("CardsChaos/HUD/Hud Slide Label")]
    public class HudSlideLabel : MonoBehaviour
    {
        [Tooltip("The stretched label rect under the mask. Its authored Left/Right is the hidden " +
                 "position; shown, both slide to zero.")]
        [SerializeField] private RectTransform slider;

        [Tooltip("The text to write into. Usually the slider itself or its child.")]
        [SerializeField] private TMP_Text text;

        [Header("Slide")]
        [SerializeField] private float showDuration = 0.25f;
        [SerializeField, SearchableEnum] private Ease showEase = Ease.OutCubic;
        [SerializeField] private float hideDuration = 0.2f;
        [SerializeField, SearchableEnum] private Ease hideEase = Ease.InCubic;

        private Vector2 _hiddenMin;
        private Vector2 _hiddenMax;
        private bool _captured;

        private Tween _tween;

        private void Awake()
        {
            Capture();
            SnapHidden();
        }

        /// <summary>Sets what the label reads. Safe to call while hidden.</summary>
        public void SetText(string value)
        {
            if (text != null)
                text.SetText(value ?? string.Empty);
        }

        /// <summary>Slides the label out from behind the mask.</summary>
        public void Show() => Slide(shown: true);

        /// <summary>Slides the label back behind the mask.</summary>
        public void Hide() => Slide(shown: false);

        private void Slide(bool shown)
        {
            if (slider == null)
                return;

            Capture();

            if (_tween.isAlive)
                _tween.Stop();

            Vector2 fromMin = slider.offsetMin;
            Vector2 fromMax = slider.offsetMax;

            // Shown is Left 0 / Right 0; the vertical offsets are left exactly as authored so only
            // the sideways slide is animated.
            Vector2 toMin = shown ? new Vector2(0f, _hiddenMin.y) : _hiddenMin;
            Vector2 toMax = shown ? new Vector2(0f, _hiddenMax.y) : _hiddenMax;

            float duration = shown ? showDuration : hideDuration;
            Ease ease = shown ? showEase : hideEase;

            _tween = Tween.Custom(0f, 1f, duration, t =>
            {
                slider.offsetMin = Vector2.LerpUnclamped(fromMin, toMin, t);
                slider.offsetMax = Vector2.LerpUnclamped(fromMax, toMax, t);
            }, ease);
        }

        private void Capture()
        {
            if (_captured || slider == null)
                return;

            _hiddenMin = slider.offsetMin;
            _hiddenMax = slider.offsetMax;
            _captured = true;
        }

        private void SnapHidden()
        {
            if (slider == null)
                return;

            slider.offsetMin = _hiddenMin;
            slider.offsetMax = _hiddenMax;
        }

        private void OnDestroy()
        {
            if (_tween.isAlive)
                _tween.Stop();
        }
    }
}
