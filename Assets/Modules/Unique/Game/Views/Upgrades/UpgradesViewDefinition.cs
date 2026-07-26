using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.UISystem;

namespace Vesolovsky.Game.Views
{ 
    public class UpgradesViewDefinition : IViewDefinition
    {
        public ViewName Name { get; set; } = ViewName.Upgrades;
        public string Address { get; set; } = "UpgradesView";
        public string Id { get; set; } = "UpgradesView";
        public IViewInitData ViewInitData { get; set; } = ViewInitDataDefaults.Default;
        public IViewModelInitData ViewModelInitData { get; set; } = ViewModelInitDataDefaults.Default;
    }
}