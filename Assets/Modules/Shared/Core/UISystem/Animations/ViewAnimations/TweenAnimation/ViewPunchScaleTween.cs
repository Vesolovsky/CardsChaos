using Cysharp.Threading.Tasks;
using PrimeTween;
using System.Threading;
using UnityEngine;

namespace Vesolovsky.Core.UISystem.Animations
{

    [AddComponentMenu("Vesolovsky/UI/Animations/Tweens/Scale/View Punch tween", 1)]
    public class ViewPunchScaleTween : ViewTween
    {
        [SerializeField] private ShakeSettings openSettings;
        [SerializeField] private ShakeSettings closeSettings;

        protected override async UniTask OpenAnimation(CancellationToken ct)
        {
            await Tween.PunchScale(transform, openSettings).WithCancellation(ct);
        }

        protected override async UniTask CloseAnimation(CancellationToken ct)
        {
            await Tween.PunchScale(transform, closeSettings).WithCancellation(ct);
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
