using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using Vesolovsky.Core.Analytics;
using Vesolovsky.Core.Services.Save;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.Services.Save;

namespace Vesolovsky.Game.Views
{ 
    public class AnalyticsConsentViewModel : ViewModel, IAnalyticsConsentViewModel
    {
        private const string PRIVACY_POLICY_URL = "https://armedchicken.com/privacy_policy";

        private readonly IUnityAnalyticsService _analyticsService;
        private readonly GameSaveService _gameSaveService;
        private readonly ISaveCoordinator _saveCoordinator;
        private IReactiveProperty<bool> _shouldShowView = new ReactiveProperty<bool>(false);

        public IReadOnlyReactiveProperty<bool> ShouldShowView => _shouldShowView;

        public AnalyticsConsentViewModel(IUnityAnalyticsService analyticsService, GameSaveService gameSaveService, ISaveCoordinator saveCoordinator)
        {
            _analyticsService = analyticsService;
            _gameSaveService = gameSaveService;
            _saveCoordinator = saveCoordinator;
        }

        public override async UniTask Initialize(IViewModelInitData viewModelInitData)
        {
            await base.Initialize(viewModelInitData);

            if (_gameSaveService.CurrentSave.IsFirstLaunch)
            {
                _shouldShowView.Value = true;
            }
            else
            {
                _shouldShowView.Value = false;
            }
        }

        public void EnableAnalytics()
        {
            _analyticsService.EnableAnalytics();
            _shouldShowView.Value = false;

            _gameSaveService.CurrentSave.IsFirstLaunch = false;
            _saveCoordinator.MarkDirty();
        }

        public void DisableAnalytics()
        {
            _analyticsService.DisableAnalytics();
            _shouldShowView.Value = false;

            _gameSaveService.CurrentSave.IsFirstLaunch = false;
            _saveCoordinator.MarkDirty();
        }

        public void OpenPrivacyPolicy()
        {
            Application.OpenURL(PRIVACY_POLICY_URL);
        }
    }
}