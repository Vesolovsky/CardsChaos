using System;
using UnityEngine;

namespace Vesolovsky.Core.Services.Settings
{
    /// <summary>
    /// Serializable snapshot of all player settings. Views should edit a clone and pass it to
    /// <see cref="IGameSettingsService.Apply"/> only after the player confirms the changes.
    /// </summary>
    [Serializable]
    public sealed class GameSettingsData
    {
        public const float MIN_MOUSE_SENSITIVITY = 0.01f;
        public const float MAX_MOUSE_SENSITIVITY = 1f;
        public const float DEFAULT_MOUSE_SENSITIVITY = 0.15f;
        public const float DEFAULT_AUTO_SAVE_INTERVAL_SECONDS = 300f;

        public float MouseSensitivity;
        public bool InvertMouseX;
        public bool AutoSave;
        public float AutoSaveIntervalSeconds;

        public int QualityLevel;
        public FullScreenMode FullScreenMode;
        public int ResolutionWidth;
        public int ResolutionHeight;
        public int RefreshRate;
        public bool VSync;
        public int FpsLimit;

        public float MasterVolume;
        public float MusicVolume;
        public float SfxVolume;

        public static GameSettingsData CreateDefaults()
        {
            Resolution nativeResolution = Screen.currentResolution;
            int width = nativeResolution.width > 0 ? nativeResolution.width : Mathf.Max(1, Screen.width);
            int height = nativeResolution.height > 0 ? nativeResolution.height : Mathf.Max(1, Screen.height);
            int refreshRate = Mathf.RoundToInt((float)nativeResolution.refreshRateRatio.value);

            return new GameSettingsData
            {
                MouseSensitivity = DEFAULT_MOUSE_SENSITIVITY,
                InvertMouseX = false,
                AutoSave = true,
                AutoSaveIntervalSeconds = DEFAULT_AUTO_SAVE_INTERVAL_SECONDS,

                QualityLevel = Mathf.Max(0, QualitySettings.names.Length - 1),
                FullScreenMode = UnityEngine.FullScreenMode.FullScreenWindow,
                ResolutionWidth = width,
                ResolutionHeight = height,
                RefreshRate = Mathf.Max(1, refreshRate),
                VSync = true,
                FpsLimit = -1,

                MasterVolume = 1f,
                MusicVolume = 1f,
                SfxVolume = 1f
            };
        }

        public GameSettingsData Clone()
        {
            return new GameSettingsData
            {
                MouseSensitivity = MouseSensitivity,
                InvertMouseX = InvertMouseX,
                AutoSave = AutoSave,
                AutoSaveIntervalSeconds = AutoSaveIntervalSeconds,

                QualityLevel = QualityLevel,
                FullScreenMode = FullScreenMode,
                ResolutionWidth = ResolutionWidth,
                ResolutionHeight = ResolutionHeight,
                RefreshRate = RefreshRate,
                VSync = VSync,
                FpsLimit = FpsLimit,

                MasterVolume = MasterVolume,
                MusicVolume = MusicVolume,
                SfxVolume = SfxVolume
            };
        }
    }
}
