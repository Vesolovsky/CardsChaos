using Cysharp.Threading.Tasks;
using PrimeTween;
using System.Threading;
using UnityEngine;

namespace Vesolovsky.Core.UISystem.Animations
{
    [AddComponentMenu("Vesolovsky/UI/Animations/Tweens/Scale/View Shake tween", 2)]
    public class ViewShakeScaleTween : ViewTween
    {
        [SerializeField] private ShakeSettings openSettings;
        [SerializeField] private ShakeSettings closeSettings;

        protected override async UniTask OpenAnimation(CancellationToken ct)
        {
            await Tween.ShakeScale(transform, openSettings).WithCancellation(ct);
        }

        protected override async UniTask CloseAnimation(CancellationToken ct)
        {
            await Tween.ShakeScale(transform, closeSettings).WithCancellation(ct);
        }

        protected override void SetToOpenedState()
        {
            return; 
        }

        protected override void SetToClosedState()
        {
            return;
        }
    }
}
