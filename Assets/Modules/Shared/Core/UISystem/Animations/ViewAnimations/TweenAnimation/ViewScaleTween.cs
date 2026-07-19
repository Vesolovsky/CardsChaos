using Cysharp.Threading.Tasks;
using PrimeTween;
using System.Threading;
using UnityEngine;
using Vesolovsky.Core.Utils.Extensions;

namespace Vesolovsky.Core.UISystem.Animations
{

    [AddComponentMenu("Vesolovsky/UI/Animations/Tweens/Scale/View Scale tween", 0)]
    public class ViewScaleTween : ViewTween
    {
        [SerializeField] private TweenSettings<float> openSettings;
        [SerializeField] private TweenSettings<float> closeSettings;

        protected override async UniTask OpenAnimation(CancellationToken ct)
        {
            await Tween.Scale(transform, openSettings).WithCancellation(ct);
        }

        protected override async UniTask CloseAnimation(CancellationToken ct)
        {
            await Tween.Scale(transform, closeSettings).WithCancellation(ct);
        }

        protected override void SetToOpenedState()
        {
            transform.localScale = VectorExtensions.GetUniformVector(openSettings.endValue);
        }

        protected override void SetToClosedState()
        {
            transform.localScale = VectorExtensions.GetUniformVector(closeSettings.endValue);
        }
    }
}
