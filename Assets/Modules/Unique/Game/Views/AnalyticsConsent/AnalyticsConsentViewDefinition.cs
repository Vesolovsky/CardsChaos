using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.UISystem;

namespace Vesolovsky.Game.Views
{ 
    public class AnalyticsConsentViewDefinition : IViewDefinition
    {
        public ViewName Name { get; set; } = ViewName.AnalyticsConsent;
        public string Address { get; set; } = "AnalyticsConsentView";
        public string Id { get; set; } = "AnalyticsConsentView";
        public IViewInitData ViewInitData { get; set; } = ViewInitDataDefaults.Default;
        public IViewModelInitData ViewModelInitData { get; set; } = ViewModelInitDataDefaults.Default;
    }
}