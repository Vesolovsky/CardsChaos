using Cysharp.Threading.Tasks;
using PrimeTween;
using System.Threading;
using UnityEngine;

namespace Vesolovsky.Core.UISystem.Animations
{
    //TODO: Add to the core
    [AddComponentMenu("Vesolovsky/UI/Animations/Tweens/View Fade tween")]
    [RequireComponent(typeof(CanvasGroup))]
    public class ViewFadeTween : ViewTween
    {
        [SerializeField] private TweenSettings<float> openSettings;
        [SerializeField] private TweenSettings<float> closeSettings;

        private CanvasGroup _canvasGroup;

        protected override void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            base.Awake();
        }

        protected override async UniTask OpenAnimation(CancellationToken ct)
        {
            _canvasGroup.blocksRaycasts = true;
            await Tween.Alpha(_canvasGroup, openSettings).WithCancellation(ct);
        }

        protected override async UniTask CloseAnimation(CancellationToken ct)
        {
            await Tween.Alpha(_canvasGroup, closeSettings).WithCancellation(ct);
            _canvasGroup.blocksRaycasts = false;
        }

        // The immediate paths have to move the raycast blocking as well as the alpha, or a view
        // shown or hidden without its animation ends up disagreeing with itself. The closed case
        // is the one that bites: a StayHidden view sits at alpha 0 over the whole screen and, if
        // it is still blocking, quietly swallows every click meant for what is behind it.
        protected override void SetToOpenedState()
        {
            _canvasGroup.alpha = 1;
            _canvasGroup.blocksRaycasts = true;
        }

        protected override void SetToClosedState()
        {
            _canvasGroup.alpha = 0;
            _canvasGroup.blocksRaycasts = false;
        }

    }
}
