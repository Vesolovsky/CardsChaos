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

        protected override void SetToOpenedState()
        {
            _canvasGroup.alpha = 1;
        }

        protected override void SetToClosedState()
        {
            _canvasGroup.alpha = 0;
        }

    }
}
