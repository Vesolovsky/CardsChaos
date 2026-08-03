using PrimeTween;
using RoboRyanTron.SearchableEnum;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vesolovsky.Core.UISystem.UIComponents
{
    public class PointerHoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("The image that lights up. Its starting alpha is the lit alpha; it is dimmed to " +
                 "zero at start.")]
        [SerializeField] private Image highlight;

        [Tooltip("Faster in than out, so the button answers at once and settles gently.")]
        [SerializeField] private float fadeInDuration = 0.15f;
        [SerializeField] private float fadeOutDuration = 0.25f;
        [SerializeField, SearchableEnum] private Ease fadeEase = Ease.OutQuad;

        private float _litAlpha;
        private Tween _tween;

        private void Awake()
        {
            if (highlight == null)
            {
                Debug.LogError($"[{nameof(PointerHoverHighlight)}] No highlight graphic assigned.", this);
                return;
            }

            _litAlpha = highlight.color.a;
            SetAlpha(0f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            FadeTo(_litAlpha, fadeInDuration);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            FadeTo(0f, fadeOutDuration);
        }

        private void OnDisable()
        {
            // Snapped off rather than left mid-fade: a button hidden while lit would come back lit.
            if (_tween.isAlive)
                _tween.Stop();

            if (highlight != null)
                SetAlpha(0f);
        }

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
