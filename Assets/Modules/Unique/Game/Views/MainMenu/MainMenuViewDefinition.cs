using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.UISystem;

namespace Vesolovsky.Game.Views
{ 
    public class MainMenuViewDefinition : IViewDefinition
    {
        public ViewName Name { get; set; } = ViewName.MainMenu;
        public string Address { get; set; } = "MainMenuView";
        public string Id { get; set; } = "MainMenuView";
        public IViewInitData ViewInitData { get; set; } = ViewInitDataDefaults.Default;
        public IViewModelInitData ViewModelInitData { get; set; } = ViewModelInitDataDefaults.Default;
    }
}