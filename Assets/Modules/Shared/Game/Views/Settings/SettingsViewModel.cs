using Vesolovsky.Core.UISystem;
using Vesolovsky.Core.Services.Input;
using Vesolovsky.Core.Services.Settings;
using Zenject;

namespace Vesolovsky.Game.Views
{ 
    public class SettingsViewModel : ViewModel, ISettingsViewModel
    {
        private readonly IGameSettingsService _settings;

        public GameSettingsData Draft { get; private set; }
        public InputRebindDraft InputDraft { get; }

        // Current returns a fresh defensive copy, so comparing the draft against it tells us whether
        // anything is still waiting to be applied.
        public bool HasUnsavedChanges =>
            !Draft.ValueEquals(_settings.Current) || (InputDraft?.IsDirty ?? false);

        [Inject]
        public SettingsViewModel(
            IGameSettingsService settings,
            [InjectOptional] IInputActions inputActions)
        {
            _settings = settings;
            Draft = settings.Current;
            InputDraft = inputActions?.CreateRebindDraft();
        }

        public void ResetGeneral()
        {
            GameSettingsData defaults = GameSettingsData.CreateDefaults();
            Draft.MouseSensitivity = defaults.MouseSensitivity;
            Draft.InvertMouseX = defaults.InvertMouseX;
            Draft.AutoSave = defaults.AutoSave;
            Draft.AutoSaveIntervalSeconds = defaults.AutoSaveIntervalSeconds;
            Draft.ShowHints = defaults.ShowHints;
        }

        public void ResetVideo()
        {
            GameSettingsData defaults = GameSettingsData.CreateDefaults();
            Draft.QualityLevel = defaults.QualityLevel;
            Draft.FullScreenMode = defaults.FullScreenMode;
            Draft.ResolutionWidth = defaults.ResolutionWidth;
            Draft.ResolutionHeight = defaults.ResolutionHeight;
            Draft.RefreshRate = defaults.RefreshRate;
            Draft.VSync = defaults.VSync;
            Draft.FpsLimit = defaults.FpsLimit;
        }

        public void ResetAudio()
        {
            GameSettingsData defaults = GameSettingsData.CreateDefaults();
            Draft.MasterVolume = defaults.MasterVolume;
            Draft.MusicVolume = defaults.MusicVolume;
            Draft.SfxVolume = defaults.SfxVolume;
            Draft.AmbientVolume = defaults.AmbientVolume;
        }

        public void ResetInput()
        {
            InputDraft?.ResetAll();
        }

        public void Apply()
        {
            // Input and regular settings share the same Apply boundary, but keep separate storage:
            // Input System owns binding override JSON, while this service owns the settings JSON.
            InputDraft?.Apply();
            _settings.Apply(Draft);
            Draft = _settings.Current;
        }

        public override void Dispose()
        {
            InputDraft?.Dispose();
            base.Dispose();
        }

    }
}
