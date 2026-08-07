using System;
using Cysharp.Threading.Tasks;
using TMPro;
using Vesolovsky.Core.UISystem;

namespace Vesolovsky.Game.Views
{
    /// <summary>
    /// What the letter's controller hands the view when it opens it: the story to print, the name
    /// that signs it, the handwriting it is drawn in, and the callback that closes it.
    /// </summary>
    public class LetterViewModelInitData : IViewModelInitData
    {
        public string Body { get; }
        public string Signature { get; }
        public TMP_FontAsset Font { get; }

        /// <summary>Invoked when the view asks to be closed - clicking the dimmed backdrop.</summary>
        public Action CloseRequested { get; }

        public LetterViewModelInitData(string body, string signature, TMP_FontAsset font,
            Action closeRequested)
        {
            Body = body;
            Signature = signature;
            Font = font;
            CloseRequested = closeRequested;
        }
    }

    public class LetterViewModel : ViewModel, ILetterViewModel
    {
        public string Body { get; private set; }
        public string Signature { get; private set; }
        public TMP_FontAsset Font { get; private set; }

        private Action _closeRequested;

        public override async UniTask Initialize(IViewModelInitData viewModelInitData)
        {
            if (viewModelInitData is LetterViewModelInitData data)
            {
                Body = data.Body ?? string.Empty;
                Signature = data.Signature ?? string.Empty;
                Font = data.Font;
                _closeRequested = data.CloseRequested;
            }

            await base.Initialize(viewModelInitData);
        }

        public void RequestClose() => _closeRequested?.Invoke();
    }
}
