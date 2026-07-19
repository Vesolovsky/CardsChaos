using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vesolovsky.Core.UISystem.UIComponents
{
    /// <summary>
    /// Makes mouse-wheel scrolling feel identical to drag scrolling on a ScrollRect.
    ///
    /// By default, <see cref="ScrollRect.OnScroll"/> moves content directly (no inertia),
    /// producing a discrete "jump" per wheel tick. This component:
    ///   1. Zeroes <see cref="ScrollRect.scrollSensitivity"/> so the built-in handler
    ///      produces no movement.
    ///   2. Intercepts the same scroll event and adds the delta to
    ///      <see cref="ScrollRect.velocity"/>, which the ScrollRect then decelerates
    ///      smoothly via its inertia system — exactly like a drag release.
    ///
    /// Attach next to a <see cref="ScrollRect"/> (e.g. on the dropdown Template GO).
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    [AddComponentMenu("Vesolovsky/UI/Smooth Scroll")]
    public class SmoothScroll : MonoBehaviour, IScrollHandler
    {
        [Tooltip("How much velocity is added per scroll tick. " +
                 "Tune to match the feel of drag scrolling.")]
        [SerializeField] private float scrollSpeed = 400f;

        private ScrollRect _scrollRect;

        private void Awake()
        {
            _scrollRect = GetComponent<ScrollRect>();

            // Disable the default discrete-jump scroll so our velocity path is the only one.
            _scrollRect.scrollSensitivity = 0f;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!_scrollRect.IsActive()) return;

            _scrollRect.velocity -= Vector2.up * eventData.scrollDelta.y * scrollSpeed;
        }
    }
}
