using Cysharp.Threading.Tasks;
using UnityEngine;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Core.UISystem.UIComponents;

namespace Vesolovsky.Game.Views
{ 
    public class ConfirmationPopupView : View<IConfirmationPopupViewModel>, IPopup
    {
        [SerializeField] private VText titleText;
        [SerializeField] private VText descriptionText;
        [SerializeField] private VButton confirmButton;
        [SerializeField] private VButton declineButton;

        protected override void InitialViewSetup(IViewInitData viewInitData)
        {
            if (titleText != null)
                titleText.SetText(ViewModel.Title);

            if (descriptionText != null)
                descriptionText.SetText(ViewModel.Description);

            bool showConfirm = (ViewModel.Buttons & ConfirmationPopupButtons.Confirm) != 0;
            bool showDecline = (ViewModel.Buttons & ConfirmationPopupButtons.Decline) != 0;

            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(showConfirm);
                confirmButton.Bind(OnConfirmButton);
            }

            if (declineButton != null)
            {
                declineButton.gameObject.SetActive(showDecline);
                declineButton.Bind(OnDeclineButton);
            }

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
