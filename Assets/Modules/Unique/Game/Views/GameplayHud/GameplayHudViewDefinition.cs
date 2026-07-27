using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.UISystem;

namespace Vesolovsky.Game.Views
{ 
    public class GameplayHudViewDefinition : IViewDefinition
    {
        public ViewName Name { get; set; } = ViewName.GameplayHud;
        public string Address { get; set; } = "GameplayHudView";
        public string Id { get; set; } = "GameplayHudView";
        public IViewInitData ViewInitData { get; set; } = ViewInitDataDefaults.Default;
        public IViewModelInitData ViewModelInitData { get; set; } = ViewModelInitDataDefaults.Default;
    }
}