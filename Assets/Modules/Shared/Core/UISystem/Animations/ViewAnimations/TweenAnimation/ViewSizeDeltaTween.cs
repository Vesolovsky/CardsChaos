using Cysharp.Threading.Tasks;
using PrimeTween;
using System.Threading;
using UnityEngine;

namespace Vesolovsky.Core.UISystem.Animations
{
    [AddComponentMenu("Vesolovsky/UI/Animations/Tweens/Scale/View Size delta tween")]
    [RequireComponent(typeof(RectTransform))]
    public class ViewSizeDeltaTween : ViewTween
    {
        [SerializeField] private TweenSettings<Vector2> openSettings;
        [SerializeField] private TweenSettings<Vector2> closeSettings;

        private RectTransform _rectTransform;

        protected override void Awake()
        {
            _rectTransform = (RectTransform)transform;
            base.Awake();
        }

        protected override async UniTask OpenAnimation(CancellationToken ct)
        {
            await Tween.UISizeDelta(_rectTransform, openSettings).WithCancellation(ct);
        }

        protected override async UniTask CloseAnimation(CancellationToken ct)
        {
            await Tween.UISizeDelta(_rectTransform, closeSettings).WithCancellation(ct);
        }

        protected override void SetToOpenedState()
        {
            _rectTransform.sizeDelta = openSettings.endValue;
        }

        protected override void SetToClosedState()
        {
            _rectTransform.sizeDelta = closeSettings.endValue;
        }
    }
}
