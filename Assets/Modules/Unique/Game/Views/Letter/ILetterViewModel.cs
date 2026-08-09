using TMPro;
using Vesolovsky.Core.UISystem;

namespace Vesolovsky.Game.Views
{
    public interface ILetterViewModel : IViewModel
    {
        /// <summary>The environmental story printed on the letter.</summary>
        string Body { get; }

        /// <summary>Who signed it - the author's display name.</summary>
        string Signature { get; }

        /// <summary>The author's handwriting. Null when the author has no font assigned.</summary>
        TMP_FontAsset Font { get; }

        /// <summary>
        /// The endgame certificate state: hide the note content, show the Certificate object, and
        /// ignore Body/Signature/Font (the certificate's message is fixed in the prefab).
        /// </summary>
        bool IsCertificate { get; }

        /// <summary>
        /// Asks whoever opened the letter to close it - raised by clicking the dimmed backdrop, and
        /// handled the same way as Escape (the inspector unloads the view and hands the room back).
        /// </summary>
        void RequestClose();
    }
}
