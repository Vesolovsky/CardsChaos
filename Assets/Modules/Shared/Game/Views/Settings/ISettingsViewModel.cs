using Vesolovsky.Core.UISystem;
using Vesolovsky.Core.Services.Input;
using Vesolovsky.Core.Services.Settings;

namespace Vesolovsky.Game.Views
{ 
    public interface ISettingsViewModel : IViewModel
    {
        GameSettingsData Draft { get; }
        InputRebindDraft InputDraft { get; }

        void ResetGeneral();
        void ResetVideo();
        void ResetAudio();
        void ResetInput();
        void Apply();
    }
}
