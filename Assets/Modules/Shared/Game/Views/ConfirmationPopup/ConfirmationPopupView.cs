using Cysharp.Threading.Tasks;
using UnityEngine;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Core.UISystem.UIComponents;

namespace Vesolovsky.Game.Views
{ 
    public class ConfirmationPopupView : View<IConfirmationPopupViewModel>
    {
        [SerializeField] private VText titleText;
        [SerializeField] private VText descriptionText;
        [SerializeField] private VButton confirmButton;
        [SerializeField] private VButton declineButton;

        protected override void InitialViewSetup(IViewInitData viewInitData)
        {
            titleText.SetText(ViewModel.Title);
            descriptionText.SetText(ViewModel.Description);

            declineButton.gameObject.SetActive(ViewModel.Buttons == ConfirmationPopupButtons.Decline);

            confirmButton.Bind(OnConfirmButton);
            declineButton.Bind(OnDeclineButton);

            base.InitialViewSetup(viewInitData);
        }

        private void OnConfirmButton()
        {
            ViewModel.Confirm();
            Unload().Forget();
        }

        private void OnDeclineButton()
        {
            ViewModel.Decline();
            Unload().Forget();
        }
    }
}