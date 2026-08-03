using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.UISystem;

namespace Vesolovsky.Game.Views
{ 
    public class SettingsViewDefinition : IViewDefinition
    {
        public ViewName Name { get; set; } = ViewName.Settings;
        public string Address { get; set; } = "SettingsView";
        public string Id { get; set; } = "SettingsView";
        public IViewInitData ViewInitData { get; set; } = ViewInitDataDefaults.Default;
        public IViewModelInitData ViewModelInitData { get; set; } = ViewModelInitDataDefaults.Default;
    }
}