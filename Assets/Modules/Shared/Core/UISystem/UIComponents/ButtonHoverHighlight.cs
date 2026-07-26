using PrimeTween;
using RoboRyanTron.SearchableEnum;
using UnityEngine;
using UnityEngine.UI;

namespace Vesolovsky.Core.UISystem.UIComponents
{
    /// <summary>
    /// Fades a highlight graphic in while the pointer is over a button and out again when it
    /// leaves - the same feel the album's set buttons have, made reusable.
    ///
    /// The highlight's authored alpha is the target it fades up to, read once at start, so the
    /// look is set on the prefab and not in numbers here. It hooks the button's own pointer
    /// events, which only fire while the button is interactable, so a disabled button never lights.
    /// </summary>
    [AddComponentMenu("Vesolovsky/UI/Button Hover Highlight")]
    public class ButtonHoverHighlight : MonoBehaviour
    {
        [Tooltip("The button whose hover this follows.")]
        [SerializeField] private VButton button;

        [Tooltip("The image that lights up. Its starting alpha is the lit alpha; it is dimmed to " +
                 "zero at start.")]
        [SerializeField] private Image highlight;

        [Tooltip("Faster in than out, so the button answers at once and settles gently.")]
        [SerializeField] private float fadeInDuration = 0.15f;
        [SerializeField] private float fadeOutDuration = 0.25f;
        [SerializeField, SearchableEnum] private Ease fadeEase = Ease.OutQuad;

        private float _litAlpha;
        private bool _lastInteractable = true;
        private Tween _tween;

        private void Awake()
        {
            if (highlight == null)
            {
                Debug.LogError($"[{nameof(ButtonHoverHighlight)}] No highlight graphic assigned.", this);
                return;
            }

            _litAlpha = highlight.color.a;
            SetAlpha(0f);
        }

        private void OnEnable()
        {
            if (button == null)
            {
                Debug.LogError($"[{nameof(ButtonHoverHighlight)}] No button assigned.", this);
                return;
            }

            button.PointerEnter += OnPointerEnter;
            button.PointerExit += OnPointerExit;
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.PointerEnter -= OnPointerEnter;
                button.PointerExit -= OnPointerExit;
            }

            // Snapped off rather than left mid-fade: a button hidden while lit would come back lit.
            if (_tween.isAlive)
                _tween.Stop();

            if (highlight != null)
                SetAlpha(0f);
        }

        private void Update()
        {
            if (button == null)
                return;

            if (button.interactable)
            {
                _lastInteractable = true;
                return;
            }

            // A button that goes disabled under the cursor never gets a pointer-exit, so the
            // highlight would stay lit; drop it the moment the button stops being interactable.
            if (_lastInteractable)
            {
                _lastInteractable = false;
                FadeTo(0f, fadeOutDuration);
            }
        }

        private void OnPointerEnter() => FadeTo(_litAlpha, fadeInDuration);

        private void OnPointerExit() => FadeTo(0f, fadeOutDuration);

        private void FadeTo(float alpha, float duration)
        {
            if (highlight == null)
                return;

            if (_tween.isAlive)
                _tween.Stop();

            if (Mathf.Approximately(highlight.color.a, alpha))
                return;

            _tween = Tween.Alpha(highlight, alpha, duration, fadeEase);
        }

        private void SetAlpha(float alpha)
        {
            Color color = highlight.color;
            color.a = alpha;
            highlight.color = color;
        }
    }
}
