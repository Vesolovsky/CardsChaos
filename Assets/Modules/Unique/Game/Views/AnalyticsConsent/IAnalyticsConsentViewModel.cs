using UniRx;
using Vesolovsky.Core.UISystem;

namespace Vesolovsky.Game.Views
{ 
    public interface IAnalyticsConsentViewModel : IViewModel
    {
        public void EnableAnalytics();
        public void DisableAnalytics();
        public void OpenPrivacyPolicy();
        public IReadOnlyReactiveProperty<bool> ShouldShowView { get; }
    }
}