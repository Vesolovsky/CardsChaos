using PrimeTween;
using RoboRyanTron.SearchableEnum;
using UnityEngine;
using UnityEngine.UI;

namespace Vesolovsky.Core.UISystem.UIComponents
{
    /// <summary>
    /// Drives a scroll view from a pair of up/down buttons, each click easing the content along by
    /// a fixed number of pixels.
    ///
    /// The step is in pixels rather than a fraction of the list so a click moves the same distance
    /// whether the list is short or long. Scrolling is clamped at both ends, so a click at the top
    /// or bottom simply does nothing.
    /// </summary>
    [AddComponentMenu("Vesolovsky/UI/Scroll View Buttons")]
    public class ScrollViewButtons : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private VButton upButton;
        [SerializeField] private VButton downButton;

        [Tooltip("How far one click scrolls, in pixels.")]
        [SerializeField] private float step = 200f;

        [Tooltip("How long the scroll takes to ease across. 0 jumps straight there.")]
        [SerializeField] private float duration = 0.25f;

        [SerializeField, SearchableEnum] private Ease ease = Ease.OutQuad;

        private Tween _tween;

        private void OnEnable()
        {
            if (upButton != null)
                upButton.Bind(ScrollUp);

            if (downButton != null)
                downButton.Bind(ScrollDown);
        }

        private void OnDisable()
        {
            if (_tween.isAlive)
                _tween.Stop();
        }

        // Refreshed after layout each frame rather than off onValueChanged: the content is spawned
        // in and sized a frame later, and a scroll rect does not raise a change for that. Setting
        // interactable to the value it already holds is a no-op, so re-asserting it costs nothing.
        private void LateUpdate() => RefreshButtons();

        /// <summary>Greys the buttons out at the ends, and both out when the list all fits.</summary>
        private void RefreshButtons()
        {
            if (scrollRect == null || scrollRect.content == null)
                return;

            float range = ScrollableHeight();
            bool scrollable = range > 1f;

            float position = scrollRect.verticalNormalizedPosition;

            // Distance from each end in pixels, so "at the end" is a pixel, not a fraction that
            // means different things for a short list and a long one.
            float fromTop = (1f - position) * range;
            float fromBottom = position * range;

            if (upButton != null)
                upButton.interactable = scrollable && fromTop > 1f;

            if (downButton != null)
                downButton.interactable = scrollable && fromBottom > 1f;
        }

        // Up reveals the content above - the top of the list - which is the high end of the
        // normalized position; down is the low end.
        private void ScrollUp() => ScrollByPixels(step);

        private void ScrollDown() => ScrollByPixels(-step);

        private void ScrollByPixels(float pixels)
        {
            if (scrollRect == null || scrollRect.content == null)
                return;

            float range = ScrollableHeight();
            if (range <= 0f)
                return;

            float start = scrollRect.verticalNormalizedPosition;
            float target = Mathf.Clamp01(start + pixels / range);

            if (Mathf.Approximately(start, target))
                return;

            // Killed so a click does not fight leftover fling momentum from a drag.
            scrollRect.velocity = Vector2.zero;

            if (_tween.isAlive)
                _tween.Stop();

            if (duration <= 0f)
            {
                scrollRect.verticalNormalizedPosition = target;
                return;
            }

            _tween = Tween.Custom(start, target, duration,
                value => scrollRect.verticalNormalizedPosition = value, ease);
        }

        private float ScrollableHeight()
        {
            RectTransform viewport = scrollRect.viewport != null
                ? scrollRect.viewport
                : (RectTransform)scrollRect.transform;

            return Mathf.Max(0f, scrollRect.content.rect.height - viewport.rect.height);
        }
    }
}
