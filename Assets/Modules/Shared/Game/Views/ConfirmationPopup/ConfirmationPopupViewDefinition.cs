using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.UISystem;

namespace Vesolovsky.Game.Views
{ 
    public class ConfirmationPopupViewDefinition : IViewDefinition
    {
        public ViewName Name { get; set; } = ViewName.ConfirmationPopup;
        public string Address { get; set; } = "ConfirmationPopupView";
        public string Id { get; set; } = "ConfirmationPopupView";
        public IViewInitData ViewInitData { get; set; } = ViewInitDataDefaults.Default;
        public IViewModelInitData ViewModelInitData { get; set; } = ViewModelInitDataDefaults.Default;
    }
}