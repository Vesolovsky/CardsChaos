using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Core.UISystem.UIComponents;

namespace Vesolovsky.Game.Views
{ 
    public class AnalyticsConsentView : View<IAnalyticsConsentViewModel>
    {
        [SerializeField] private VButton enableAnalyticsButton;
        [SerializeField] private VButton disableAnalyticsButton;
        [SerializeField] private VButton privacyPolicyButton;

        protected override void InitialViewSetup(IViewInitData viewInitData)
        {
            base.InitialViewSetup(viewInitData);

            enableAnalyticsButton.Bind(ViewModel.EnableAnalytics);
            disableAnalyticsButton.Bind(ViewModel.DisableAnalytics);
            privacyPolicyButton.Bind(ViewModel.OpenPrivacyPolicy);

            ViewModel.ShouldShowView
                .Subscribe(OnShouldShowViewChanged)
                .AddTo(this);
        }

        private void OnShouldShowViewChanged(bool shouldShowView)
        {
            if(shouldShowView)
            {
                Show(destroyCancellationToken).Forget();
            }
            else
            {
                Hide(destroyCancellationToken).Forget();
            }
        }
    }
}