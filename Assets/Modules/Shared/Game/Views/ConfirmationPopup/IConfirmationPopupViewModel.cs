using Vesolovsky.Core.UISystem;

namespace Vesolovsky.Game.Views
{ 
    public interface IConfirmationPopupViewModel : IViewModel
    {
        public string Title { get; }
        public string Description { get; }
        public ConfirmationPopupButtons Buttons { get; }
        public void Confirm();
        public void Decline();
    }
}