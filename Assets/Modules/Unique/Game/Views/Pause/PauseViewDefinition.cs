using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.UISystem;

namespace Vesolovsky.Game.Views
{ 
    public class PauseViewDefinition : IViewDefinition
    {
        public ViewName Name { get; set; } = ViewName.Pause;
        public string Address { get; set; } = "PauseView";
        public string Id { get; set; } = "PauseView";
        public IViewInitData ViewInitData { get; set; } = ViewInitDataDefaults.Default;
        public IViewModelInitData ViewModelInitData { get; set; } = ViewModelInitDataDefaults.Default;
    }
}