using Cysharp.Threading.Tasks;
using PrimeTween;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace Vesolovsky.Core.UISystem.Animations
{
    [AddComponentMenu("Vesolovsky/UI/Animations/Tweens/View Alpha tween")]
    [RequireComponent(typeof(Graphic))]
    public class ViewAlphaTween : ViewTween
    {
        [SerializeField] private TweenSettings<float> openSettings;
        [SerializeField] private TweenSettings<float> closeSettings;

        private Graphic _graphic;

        protected override void Awake()
        {
            _graphic = GetComponent<Graphic>();
            base.Awake();
        }

        protected override async UniTask OpenAnimation(CancellationToken ct)
        {
            await Tween.Alpha(_graphic, openSettings).WithCancellation(ct);
        }

        protected override async UniTask CloseAnimation(CancellationToken ct)
        {
            await Tween.Alpha(_graphic, closeSettings).WithCancellation(ct);
        }

        protected override void SetToOpenedState()
        {
            _graphic.CrossFadeAlpha(openSettings.endValue, 0, true);
        }

        protected override void SetToClosedState()
        {
            _graphic.CrossFadeAlpha(closeSettings.endValue, 0, true);
        }
    }
}
