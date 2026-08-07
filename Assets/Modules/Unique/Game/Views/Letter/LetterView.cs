using UnityEngine;
using UnityEngine.UI;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Core.UISystem.UIComponents;

namespace Vesolovsky.Game.Views
{
    public class LetterView : View<ILetterViewModel>
    {
        [Tooltip("The body of the note - the environmental story.")]
        [SerializeField] private VText bodyText;

        [Tooltip("The signature line - who left the letter.")]
        [SerializeField] private VText signatureText;

        [Tooltip("The full-screen dimmed backdrop. Clicking it closes the letter, the same as Escape. " +
                 "Any Button works (put one on the darkening image); the letter itself sits on top " +
                 "and swallows clicks, so only the backdrop dismisses.")]
        [SerializeField] private Button backdropButton;

        protected override void InitialViewSetup(IViewInitData viewInitData)
        {
            // A letter is handwritten, so the whole note takes the author's font - body and
            // signature alike. Drop the body's font line below if you would rather keep the body in
            // a plain reading font and only the signature in the author's hand.
            ApplyText(bodyText, ViewModel.Body, ViewModel.Font);
            ApplyText(signatureText, ViewModel.Signature, ViewModel.Font);

            if (backdropButton != null)
                backdropButton.onClick.AddListener(ViewModel.RequestClose);

            base.InitialViewSetup(viewInitData);
        }

        private static void ApplyText(VText text, string value, TMPro.TMP_FontAsset font)
        {
            if (text == null)
                return;

            if (font != null)
                text.font = font;

            text.SetText(value);
        }
    }
}
