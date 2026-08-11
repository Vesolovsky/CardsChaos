using System;
using UnityEngine;

namespace Vesolovsky.Core.Services.Settings
{
    /// <summary>
    /// Project-scoped owner of applied settings. Settings intentionally live outside the game
    /// save, so resetting progress does not reset the player's device and accessibility choices.
    /// </summary>
    public sealed class GameSettingsService : IGameSettingsService
    {
        private const string PLAYER_PREFS_KEY = "GameSettings.V1";
        private static readonly int[] SupportedFpsLimits = { -1, 30, 60, 120, 144, 165, 240 };

        private GameSettingsData _current;

        public GameSettingsData Current => _current.Clone();

        public event Action<GameSettingsData> Applied;

        public GameSettingsService()
        {
            _current = Load();
            ApplyVideo(_current);
        }

        public void Apply(GameSettingsData settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            _current = Sanitize(settings);

            ApplyVideo(_current);
            Persist(_current);

            Applied?.Invoke(_current.Clone());
        }

        private static GameSettingsData Load()
        {
            GameSettingsData settings = GameSettingsData.CreateDefaults();

            if (!PlayerPrefs.HasKey(PLAYER_PREFS_KEY))
                return Sanitize(settings);

            try
            {
                // Overwriting a fully populated default object also acts as a lightweight
                // migration: fields added in a later version keep their new defaults.
                JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(PLAYER_PREFS_KEY), settings);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not read saved game settings. Defaults will be used. Exception: {exception}");
                settings = GameSettingsData.CreateDefaults();
            }

            return Sanitize(settings);
        }

        private static void Persist(GameSettingsData settings)
        {
            PlayerPrefs.SetString(PLAYER_PREFS_KEY, JsonUtility.ToJson(settings));
            PlayerPrefs.Save();
        }

        private static GameSettingsData Sanitize(GameSettingsData source)
        {
            GameSettingsData defaults = GameSettingsData.CreateDefaults();
            GameSettingsData result = source?.Clone() ?? defaults;

            result.MouseSensitivity = IsFinite(result.MouseSensitivity)
                ? Mathf.Clamp(result.MouseSensitivity, GameSettingsData.MIN_MOUSE_SENSITIVITY,
                    GameSettingsData.MAX_MOUSE_SENSITIVITY)
                : defaults.MouseSensitivity;

            result.AutoSaveIntervalSeconds = IsFinite(result.AutoSaveIntervalSeconds)
                ? Mathf.Max(1f, result.AutoSaveIntervalSeconds)
                : defaults.AutoSaveIntervalSeconds;

            int qualityLevelCount = QualitySettings.names.Length;
            result.QualityLevel = qualityLevelCount > 0
                ? Mathf.Clamp(result.QualityLevel, 0, qualityLevelCount - 1)
                : 0;

            if (!IsSupportedFullScreenMode(result.FullScreenMode))
                result.FullScreenMode = defaults.FullScreenMode;

            SanitizeResolution(result, defaults);

            if (!IsSupportedFpsLimit(result.FpsLimit))
                result.FpsLimit = defaults.FpsLimit;

            result.MasterVolume = SanitizeVolume(result.MasterVolume, defaults.MasterVolume);
            result.MusicVolume = SanitizeVolume(result.MusicVolume, defaults.MusicVolume);
            result.SfxVolume = SanitizeVolume(result.SfxVolume, defaults.SfxVolume);
            result.AmbientVolume = SanitizeVolume(result.AmbientVolume, defaults.AmbientVolume);

            return result;
        }

        private static void SanitizeResolution(GameSettingsData result, GameSettingsData defaults)
        {
            // Borderless fullscreen always follows the desktop mode. This also makes settings
            // portable when a save is moved to a machine with a different monitor.
            if (result.FullScreenMode == UnityEngine.FullScreenMode.FullScreenWindow)
            {
                result.ResolutionWidth = defaults.ResolutionWidth;
                result.ResolutionHeight = defaults.ResolutionHeight;
                result.RefreshRate = defaults.RefreshRate;
                return;
            }

            if (result.ResolutionWidth <= 0 || result.ResolutionHeight <= 0 || result.RefreshRate <= 0)
            {
                CopyDefaultResolution(result, defaults);
                return;
            }

            Resolution[] availableResolutions = Screen.resolutions;
            if (availableResolutions == null || availableResolutions.Length == 0)
                return;

            Resolution? closestAtSize = null;
            int closestRefreshDistance = int.MaxValue;

            foreach (Resolution resolution in availableResolutions)
            {
                if (resolution.width != result.ResolutionWidth || resolution.height != result.ResolutionHeight)
                    continue;

                int refreshRate = Mathf.Max(1, Mathf.RoundToInt((float)resolution.refreshRateRatio.value));
                int distance = Mathf.Abs(refreshRate - result.RefreshRate);

                if (distance >= closestRefreshDistance)
                    continue;

                closestAtSize = resolution;
                closestRefreshDistance = distance;
            }

            if (closestAtSize.HasValue)
            {
                Resolution resolution = closestAtSize.Value;
                result.RefreshRate = Mathf.Max(1,
                    Mathf.RoundToInt((float)resolution.refreshRateRatio.value));
                return;
            }

            CopyDefaultResolution(result, defaults);
        }

        private static void CopyDefaultResolution(GameSettingsData target, GameSettingsData defaults)
        {
            target.ResolutionWidth = defaults.ResolutionWidth;
            target.ResolutionHeight = defaults.ResolutionHeight;
            target.RefreshRate = defaults.RefreshRate;
        }

        private static void ApplyVideo(GameSettingsData settings)
        {
            if (QualitySettings.names.Length > 0 && QualitySettings.GetQualityLevel() != settings.QualityLevel)
                QualitySettings.SetQualityLevel(settings.QualityLevel, true);

            QualitySettings.vSyncCount = settings.VSync ? 1 : 0;
            Application.targetFrameRate = settings.FpsLimit;

            var refreshRate = new RefreshRate
            {
                numerator = (uint)settings.RefreshRate,
                denominator = 1
            };

            Screen.SetResolution(
                settings.ResolutionWidth,
                settings.ResolutionHeight,
                settings.FullScreenMode,
                refreshRate);
        }

        private static bool IsSupportedFullScreenMode(FullScreenMode mode)
        {
            return mode == UnityEngine.FullScreenMode.FullScreenWindow
                   || mode == UnityEngine.FullScreenMode.ExclusiveFullScreen
                   || mode == UnityEngine.FullScreenMode.Windowed;
        }

        private static float SanitizeVolume(float volume, float defaultValue)
        {
            return IsFinite(volume) ? Mathf.Clamp01(volume) : defaultValue;
        }

        private static bool IsSupportedFpsLimit(int value)
        {
            for (int i = 0; i < SupportedFpsLimits.Length; i++)
            {
                if (SupportedFpsLimits[i] == value)
                    return true;
            }

            return false;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
