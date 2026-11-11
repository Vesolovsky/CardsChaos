using Vesolovsky.Core.UISystem;

namespace Vesolovsky.Game.Views
{ 
    public interface IConfirmationPopupViewModel : IViewModel
    {
        public string Title { get; }
        public string Description { get; }
        public ConfirmationPopupButtons Buttons { get; }

        /// <summary>Null keeps the label the prefab gives the button.</summary>
        public string ConfirmLabel { get; }

        /// <summary>Null keeps the label the prefab gives the button.</summary>
        public string DeclineLabel { get; }

        public string AlternateLabel { get; }

        public void Confirm();
        public void Decline();
        public void Alternate();
    }
}