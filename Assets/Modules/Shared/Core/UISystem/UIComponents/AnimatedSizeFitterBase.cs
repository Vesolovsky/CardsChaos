namespace Vesolovsky.Core.UISystem.UIComponents
{
    using System;
    using PrimeTween;
    using RoboRyanTron.SearchableEnum;
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.UI;
    using VInspector;

    public enum Axis
    {
        Horizontal = 0,
        Vertical = 1
    }

    //Add to the core
    [ExecuteAlways]
    public abstract class AnimatedSizeFitterBase : MonoBehaviour
    {
        public enum TargetMode
        {
            MinSize,
            PreferredSize
        }

        [Tab("Setup")]
        [SerializeField] private TargetMode targetMode;
#if UNITY_EDITOR
        [SerializeField] private bool setToContentInEditor = true;
#endif

        [Tab("Animation")]
        [SerializeField] private float duration = 0.25f;
        [SerializeField, SearchableEnum] private Ease ease = Ease.InOutSine;
        [SerializeField] private float epsilon = 0.25f;

        [Tab("Events")]
        [SerializeField] private UnityEvent animationStart;
        [SerializeField] private UnityEvent animationComplete;

        private RectTransform _rect;
        private Tween _tween;
        private bool _started;

        public float Duration => duration;
        public Ease Ease => ease;
        public float Epsilon => epsilon;
        public UnityEvent AnimationStart => animationStart;
        public UnityEvent AnimationComplete => animationComplete;

        protected RectTransform Rect
        {
            get
            {
                if (_rect == null) _rect = (RectTransform)transform;
                return _rect;
            }
        }

        protected abstract Axis Axis { get; }

        public event Action Started;
        public event Action Completed;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying || setToContentInEditor == false) return;

            SetToContentInstant();
        }

        private void Update()
        {
            if (Application.isPlaying || setToContentInEditor == false) return;
            SetToContentInstant();
        }
#endif

        public void AnimateToContent()
        {
            Canvas.ForceUpdateCanvases();
            float target = GetContentSize();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                SetInstant(target);
                return;
            }
#endif

            AnimateTo(target);
        }

        public void SetToContentInstant()
        {
            Canvas.ForceUpdateCanvases();
            float target = GetContentSize();
            SetInstant(target);
        }

        public void AnimateTo(float target)
        {
            float current = GetCurrentSize();
            if (Mathf.Abs(target - current) <= epsilon)
            {
                StopTween();
                SetSize(target);
                MarkForRebuild();
                TryFireComplete(true);
                return;
            }

            if (!_tween.isAlive) FireStart();

            float start = current;

            StopTween();
            _tween = Tween.Custom(
                startValue: start,
                endValue: target,
                duration: duration,
                onValueChange: v =>
                {
                    SetSize(v);
                    MarkForRebuild();
                },
                ease: ease
            ).OnComplete(() =>
            {
                SetSize(target);
                MarkForRebuild();
                TryFireComplete(true);
            });
        }

        public void SetInstant(float value)
        {
            StopTween();
            SetSize(value);
            MarkForRebuild();
            TryFireComplete(true);
        }

        public void StopTween()
        {
            if (_tween.isAlive) _tween.Stop();
            _tween = default;
        }

        protected virtual float GetContentSize()
        {
            return targetMode == TargetMode.MinSize
                ? LayoutUtility.GetMinSize(Rect, AxisToInt())
                : LayoutUtility.GetPreferredSize(Rect, AxisToInt());
        }

        private int AxisToInt()
        {
            if (Axis == Axis.Horizontal)
            {
                return 0;
            }
            else
            {
                return 1;
            }
        }

        protected virtual float GetCurrentSize()
        {
            return Axis == 0 ? Rect.rect.width : Rect.rect.height;
        }

        protected virtual void SetSize(float size)
        {
            Rect.SetSizeWithCurrentAnchors((RectTransform.Axis)Axis, size);
        }

        protected virtual void MarkForRebuild()
        {
            LayoutRebuilder.MarkLayoutForRebuild(Rect);
        }

        private void FireStart()
        {
            if (_started) return;
            _started = true;
            animationStart?.Invoke();
            Started?.Invoke();
        }

        private void TryFireComplete(bool force)
        {
            if (!force) return;
            if (_started)
            {
                animationComplete?.Invoke();
                Completed?.Invoke();
            }
            _started = false;
        }
    }
}