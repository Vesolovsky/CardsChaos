using UnityEngine;

namespace Vesolovsky.Core.UISystem.UIComponents
{
    /// <summary>
    /// Holds a UI container at a supported aspect ratio whatever the screen's aspect happens to be.
    ///
    /// The UI is laid out once, for 16:9. On any other aspect the Canvas Scaler hands the canvas
    /// extra width (ultrawide) or extra height (16:10, 4:3), and hand-anchored panels either drift
    /// apart or overflow their masks. This component takes the largest rect of a supported aspect
    /// that fits inside its parent and centres itself in it, so everything parented under it sees
    /// exactly the canvas it was designed against.
    ///
    /// Paired with a Canvas Scaler on Scale With Screen Size / Expand at 1920x1080 the frame works
    /// out to exactly 1920x1080 canvas units on every aspect ratio: Expand guarantees the canvas is
    /// never smaller than the reference in either axis, and the frame trims whichever axis overshot.
    ///
    /// Keep full-screen elements - darkening, faders, click blockers - outside the frame. They are
    /// meant to cover the whole screen, and the darkened room showing through around the frame is
    /// what makes the pillarboxing read as a deliberate border rather than as a missing chunk of UI.
    ///
    /// <see cref="minAspect"/> and <see cref="maxAspect"/> bracket the aspects that are used as they
    /// are. With both at 16:9 the frame is strictly 16:9 everywhere. Lowering <see cref="minAspect"/>
    /// to 1.6 lets 16:10 screens fill their full height instead of leaving a strip above and below.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Vesolovsky/UI/Aspect Frame")]
    public class AspectFrame : MonoBehaviour
    {
        [Header("Supported aspects (width / height)")]
        [Tooltip("The widest aspect used as-is; anything wider is pillarboxed down to it. " +
                 "16:9 = 1.7778.")]
        [SerializeField] private float maxAspect = 16f / 9f;

        [Tooltip("The narrowest aspect used as-is; anything narrower is letterboxed up to it. " +
                 "Leave at 16:9 to pin the layout exactly, or set to 1.6 to let 16:10 screens use " +
                 "their full height.")]
        [SerializeField] private float minAspect = 16f / 9f;

        [Header("Inset")]
        [Tooltip("Trimmed off the frame after fitting - x is the total horizontal trim, y the total " +
                 "vertical one. Use it to keep a margin the framed panel used to carry in its own " +
                 "size delta, which this component overwrites.")]
        [SerializeField] private Vector2 padding;

        private RectTransform _rect;
        private RectTransform _parent;
        private Vector2 _lastAvailable = NeverMeasured;

        private static readonly Vector2 NeverMeasured = new Vector2(float.NaN, float.NaN);

        private RectTransform Rect
        {
            get
            {
                if (_rect == null) _rect = (RectTransform)transform;
                return _rect;
            }
        }

        private RectTransform Parent
        {
            get
            {
                if (_parent == null) _parent = transform.parent as RectTransform;
                return _parent;
            }
        }

        private void OnEnable() => Invalidate();

        private void OnTransformParentChanged()
        {
            _parent = null;
            Invalidate();
        }

        private void OnRectTransformDimensionsChange() => Invalidate();

        private void OnValidate() => Invalidate();

        private void Update() => Fit();

        /// <summary>
        /// Forces the next <see cref="Fit"/> to recompute even if the space available has not moved.
        /// </summary>
        private void Invalidate() => _lastAvailable = NeverMeasured;

        /// <summary>
        /// Sizes and centres the frame inside its parent. Cheap, and a no-op unless the parent has
        /// actually changed size - the rect is only written to when something moved, so this does
        /// not dirty the scene on every editor repaint.
        /// </summary>
        private void Fit()
        {
            RectTransform parent = Parent;

            // No parent to measure against - the prefab is open in isolation, or the frame was put
            // on a view root. Nothing sensible to fit into, so leave the rect alone.
            if (parent == null)
                return;

            Vector2 available = parent.rect.size;
            if (available.x <= 0f || available.y <= 0f)
                return;

            if (available == _lastAvailable)
                return;

            _lastAvailable = available;

            float low = Mathf.Max(0.01f, Mathf.Min(minAspect, maxAspect));
            float high = Mathf.Max(low, maxAspect);
            float availableAspect = available.x / available.y;
            float aspect = Mathf.Clamp(availableAspect, low, high);

            // Trim whichever axis overshoots the supported aspect and keep the other one whole.
            Vector2 size = availableAspect > aspect
                ? new Vector2(available.y * aspect, available.y)
                : new Vector2(available.x, available.x / aspect);

            size = Vector2.Max(size - padding, Vector2.zero);

            Vector2 centre = new Vector2(0.5f, 0.5f);
            if (Rect.anchorMin != centre) Rect.anchorMin = centre;
            if (Rect.anchorMax != centre) Rect.anchorMax = centre;
            if (Rect.pivot != centre) Rect.pivot = centre;
            if (Rect.anchoredPosition != Vector2.zero) Rect.anchoredPosition = Vector2.zero;
            if (Rect.sizeDelta != size) Rect.sizeDelta = size;
        }
    }
}
