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
        Decline = 1 << 0,
    }

    public class ConfirmationPopupViewModelInitData : IViewModelInitData
    {
        public LocalizedString Title { get; }
        public LocalizedString Description { get; }
        public ConfirmationPopupButtons Buttons { get; }

        public Action ConfirmAction;
        public Action DeclineAction;

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
    }

    public class ConfirmationPopupViewModel : ViewModel, IConfirmationPopupViewModel
    {
        public string Title { get; private set; }
        public string Description { get; private set; }
        public ConfirmationPopupButtons Buttons { get; private set; }

        private Action _confirmAction;
        private Action _declineAction;


        public override async UniTask Initialize(IViewModelInitData viewModelInitData)
        {
            var initData = (ConfirmationPopupViewModelInitData)viewModelInitData;

            Title = await initData.Title.GetLocalizedStringAsync();
            Description = await initData.Description.GetLocalizedStringAsync();
            Buttons = initData.Buttons;

            _confirmAction = initData.ConfirmAction;
            _declineAction = initData.DeclineAction;

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
    }
}