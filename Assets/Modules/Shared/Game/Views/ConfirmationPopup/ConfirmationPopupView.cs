using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Core.UISystem.UIComponents;
using Zenject;

namespace Vesolovsky.Game.Views
{ 
    public class ConfirmationPopupView : View<IConfirmationPopupViewModel>, IPopup
    {
        [SerializeField] private VText titleText;
        [SerializeField] private VText descriptionText;
        [SerializeField] private VButton confirmButton;
        [SerializeField] private VButton declineButton;

        private DiContainer _container;

        [Inject]
        private void InjectPopup(DiContainer container)
        {
            _container = container;
        }

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

            SetLabel(confirmButton, ViewModel.ConfirmLabel);
            SetLabel(declineButton, ViewModel.DeclineLabel);

            if ((ViewModel.Buttons & ConfirmationPopupButtons.Alternate) != 0)
                CreateAlternateButton();

            base.InitialViewSetup(viewInitData);
        }

        private void OnConfirmButton()
        {
            ViewModel.Confirm();
            Unload().Forget();
        }

        /// <summary>
        /// The prefab carries two buttons. A third answer is a copy of the Confirm button dropped
        /// in between the two - same look, same layout group, its own label and action. Built
        /// through the container so the copy gets its audio service like the original.
        /// </summary>
        private void CreateAlternateButton()
        {
            if (confirmButton == null)
                return;

            Transform row = confirmButton.transform.parent;
            GameObject copy = _container != null
                ? _container.InstantiatePrefab(confirmButton.gameObject, row)
                : Instantiate(confirmButton.gameObject, row);

            copy.name = "AlternateButton";
            copy.SetActive(true);
            copy.transform.SetSiblingIndex(confirmButton.transform.GetSiblingIndex() + 1);

            var alternateButton = copy.GetComponent<VButton>();
            alternateButton.Bind(OnAlternateButton);
            SetLabel(alternateButton, ViewModel.AlternateLabel);
        }

        private static void SetLabel(VButton button, string label)
        {
            if (button == null || string.IsNullOrEmpty(label))
                return;

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.SetText(label);
        }

        private void OnAlternateButton()
        {
            ViewModel.Alternate();
            Unload().Forget();
        }

        private void OnDeclineButton()
        {
            ViewModel.Decline();
            Unload().Forget();
        }
    }
}
