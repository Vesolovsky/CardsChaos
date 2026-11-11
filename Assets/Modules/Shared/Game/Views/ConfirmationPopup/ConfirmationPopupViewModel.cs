using Cysharp.Threading.Tasks;
using System;
using UnityEngine.Localization;
using Vesolovsky.Core.UISystem;

namespace Vesolovsky.Game.Views
{
    [Flags]
    public enum ConfirmationPopupButtons
    {
        None = 0,
        Confirm = 1 << 0,
        Decline = 1 << 1,
        ConfirmAndDecline = Confirm | Decline,

        /// <summary>
        /// A third answer between Confirm and Decline - "Don't save" next to "Save" and "Cancel".
        /// The prefab has no button for it; the view clones the Confirm button at runtime, so it
        /// needs <see cref="ConfirmationPopupViewModelInitData.AlternateLabel"/> to say something.
        /// </summary>
        Alternate = 1 << 2,
    }

    public class ConfirmationPopupViewModelInitData : IViewModelInitData
    {
        public LocalizedString Title { get; }
        public LocalizedString Description { get; }
        public string RawTitle { get; }
        public string RawDescription { get; }
        public ConfirmationPopupButtons Buttons { get; }

        public Action ConfirmAction;
        public Action DeclineAction;
        public Action AlternateAction;

        /// <summary>Overrides the Confirm button's prefab label when set.</summary>
        public string ConfirmLabel;

        /// <summary>Overrides the Decline button's prefab label when set.</summary>
        public string DeclineLabel;

        /// <summary>The label of the <see cref="ConfirmationPopupButtons.Alternate"/> button.</summary>
        public string AlternateLabel;

        public ConfirmationPopupViewModelInitData(
            LocalizedString title,
            LocalizedString description,
            ConfirmationPopupButtons buttons,
            Action confirmAction = null,
            Action declineAction = null)
        {
            Title = title;
            Description = description;
            Buttons = buttons;
            ConfirmAction = confirmAction;
            DeclineAction = declineAction;
        }

        /// <summary>
        /// Creates a popup from runtime text. Settings rebinding uses this overload because the
        /// action and control names are assembled dynamically (for example "Rebind Sprint").
        /// </summary>
        public ConfirmationPopupViewModelInitData(
            string title,
            string description,
            ConfirmationPopupButtons buttons,
            Action confirmAction = null,
            Action declineAction = null)
        {
            RawTitle = title;
            RawDescription = description;
            Buttons = buttons;
            ConfirmAction = confirmAction;
            DeclineAction = declineAction;
        }
    }

    public class ConfirmationPopupViewModel : ViewModel, IConfirmationPopupViewModel
    {
        public string Title { get; private set; }
        public string Description { get; private set; }
        public ConfirmationPopupButtons Buttons { get; private set; }
        public string ConfirmLabel { get; private set; }
        public string DeclineLabel { get; private set; }
        public string AlternateLabel { get; private set; }

        private Action _confirmAction;
        private Action _declineAction;
        private Action _alternateAction;


        public override async UniTask Initialize(IViewModelInitData viewModelInitData)
        {
            var initData = (ConfirmationPopupViewModelInitData)viewModelInitData;

            if (initData.RawTitle != null || initData.RawDescription != null)
            {
                Title = initData.RawTitle ?? string.Empty;
                Description = initData.RawDescription ?? string.Empty;
            }
            else
            {
                Title = await initData.Title.GetLocalizedStringAsync();
                Description = await initData.Description.GetLocalizedStringAsync();
            }
            Buttons = initData.Buttons;

            _confirmAction = initData.ConfirmAction;
            _declineAction = initData.DeclineAction;
            _alternateAction = initData.AlternateAction;

            ConfirmLabel = initData.ConfirmLabel;
            DeclineLabel = initData.DeclineLabel;
            AlternateLabel = initData.AlternateLabel;

            await base.Initialize(viewModelInitData);
        }

        public void Confirm()
        {
            _confirmAction?.Invoke();
        }

        public void Decline()
        {
            _declineAction?.Invoke();
        }

        public void Alternate()
        {
            _alternateAction?.Invoke();
        }
    }
}
