using PrimeTween;
using RoboRyanTron.SearchableEnum;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vesolovsky.Core.UISystem.UIComponents
{
    /// <summary>
    /// Scales a target transform on hover, gamepad focus, and click.
    ///
    ///   Hover / gamepad focus  → scale up to <see cref="highlightScale"/>
    ///   Press (pointer down)   → scale down to <see cref="pressScale"/> (simulates button press)
    ///   Release (pointer up)   → scale back up to <see cref="highlightScale"/>
    ///   Leave / deselect       → scale back to 1
    ///
    /// Attach next to any Button or VButton — does not interfere with click logic.
    /// If <see cref="scaleTarget"/> is not assigned, falls back to this GameObject's transform.
    /// </summary>
    [AddComponentMenu("Vesolovsky/UI/Hover Scale Effect")]
    public class HoverScaleEffect : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler
    {
        [Tooltip("Transform to scale. Leave empty to use this GameObject's transform.")]
        [SerializeField] private Transform scaleTarget;

        [Header("Hover")]
        [SerializeField] private float highlightScale = 1.08f;
        [SerializeField] private float inDuration  = 0.15f;
        [SerializeField, SearchableEnum] private Ease inEase  = Ease.OutBack;
        [SerializeField] private float outDuration = 0.12f;
        [SerializeField, SearchableEnum] private Ease outEase = Ease.OutCubic;

        [Header("Press")]
        [Tooltip("Scale to dip to on pointer-down (should be < 1 or just below highlightScale).")]
        [SerializeField] private float pressScale = 0.95f;
        [SerializeField] private float pressDuration   = 0.08f;
        [SerializeField, SearchableEnum] private Ease pressEase   = Ease.OutCubic;
        [SerializeField] private float releaseDuration = 0.12f;
        [SerializeField, SearchableEnum] private Ease releaseEase = Ease.OutBack;

        // ── State ─────────────────────────────────────────────────────────────────

        private bool _isPointerOver;
        private bool _isGamepadFocused;
        private bool _isPressed;

        private bool IsHighlighted => _isPointerOver || _isGamepadFocused;

        private Tween      _tween;
        private Sequence   _pressSequence;
        private Transform  Target => scaleTarget != null ? scaleTarget : transform;

        private Selectable _selectable;

        private void Awake()
        {
            if (scaleTarget == null)
            {
                Debug.LogError($"[HoverScaleEffect] scaleTarget is not assigned on '{name}'.", this);
                return;
            }

            _selectable = scaleTarget.GetComponent<Selectable>();
        }

        // ── Pointer handlers ─────────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData _)
        {
            if (!IsInteractable()) return;
            _isPointerOver = true;
            if (!_isPressed) Highlight();
        }

        public void OnPointerExit(PointerEventData _)
        {
            _isPointerOver = false;
            if (!_isGamepadFocused && !_isPressed) Restore();
        }

        public void OnPointerDown(PointerEventData _)
        {
            if (!IsInteractable()) return;
            _isPressed = true;
            Press();
        }

        public void OnPointerUp(PointerEventData _)
        {
            _isPressed = false;
            // Return to highlight if still hovering/focused, otherwise restore to 1
            if (IsHighlighted)
                Release();
            else
                Restore();
        }

        // ── Gamepad / keyboard focus handlers ────────────────────────────────────

        public void OnSelect(BaseEventData _)
        {
            _isGamepadFocused = true;
            if (!_isPressed) Highlight();
        }

        public void OnDeselect(BaseEventData _)
        {
            _isGamepadFocused = false;
            if (!_isPointerOver && !_isPressed) Restore();
        }

        // ── Animations ───────────────────────────────────────────────────────────

        private void Highlight()
        {
            StopAll();
            _tween = Tween.Scale(Target, highlightScale, inDuration, inEase);
        }

        private void Restore()
        {
            StopAll();
            _tween = Tween.Scale(Target, 1f, outDuration, outEase);
        }

        /// <summary>
        /// Dip to <see cref="pressScale"/> — quick, snappy, no bounce.
        /// </summary>
        private void Press()
        {
            StopAll();
            _tween = Tween.Scale(Target, pressScale, pressDuration, pressEase);
        }

        /// <summary>
        /// Bounce back from <see cref="pressScale"/> to <see cref="highlightScale"/>.
        /// Uses a Sequence so the two steps play one after the other atomically.
        /// </summary>
        private void Release()
        {
            StopAll();
            _pressSequence = Sequence.Create()
                .Chain(Tween.Scale(Target, pressScale,     pressDuration,   pressEase))
                .Chain(Tween.Scale(Target, highlightScale, releaseDuration, releaseEase));
        }

        private void StopAll()
        {
            if (_tween.isAlive)           _tween.Stop();
            if (_pressSequence.isAlive)   _pressSequence.Stop();
        }

        private bool IsInteractable()
            => _selectable == null || _selectable.interactable;

        private void OnDisable()
        {
            _isPointerOver    = false;
            _isGamepadFocused = false;
            _isPressed        = false;

            StopAll();
            Target.localScale = Vector3.one;
        }
    }
}
