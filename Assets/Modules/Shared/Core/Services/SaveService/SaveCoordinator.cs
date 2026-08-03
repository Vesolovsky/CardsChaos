using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vesolovsky.Core.Services.Settings;
using Zenject;

namespace Vesolovsky.Core.Services.Save
{
    /// <summary>
    /// Default <see cref="ISaveCoordinator"/>. Generic over the save type so this stays in
    /// Core and does not need to reference the game-specific save model; bind the closed
    /// type (e.g. <c>SaveCoordinator&lt;GameSave&gt;</c>) in the game installer.
    /// </summary>
    public class SaveCoordinator<T> : ISaveCoordinator, ITickable, IDisposable where T : IGameSave
    {
        private const float MIN_AUTO_SAVE_INTERVAL_SECONDS = 1f;
        private const float DEFAULT_AUTO_SAVE_INTERVAL_SECONDS = 300f;

        public event Action Saved;

        private readonly ISaveService<T> _saveService;
        private readonly IGameSettingsService _gameSettings;

        private readonly List<ISaveContributor> _contributors = new List<ISaveContributor>();

        private float _autoSaveIntervalSeconds = DEFAULT_AUTO_SAVE_INTERVAL_SECONDS;
        private float _secondsSinceLastAutoSave;
        private bool _isAutoSaveEnabled = true;
        private bool _isSaving;

        public bool HasUnsavedChanges { get; private set; }

        public bool IsAutoSaveEnabled
        {
            get => _isAutoSaveEnabled;
            set
            {
                if (_isAutoSaveEnabled == value) return;

                _isAutoSaveEnabled = value;
                _secondsSinceLastAutoSave = 0f;
            }
        }

        public float AutoSaveIntervalSeconds
        {
            get => _autoSaveIntervalSeconds;
            set => _autoSaveIntervalSeconds = Mathf.Max(MIN_AUTO_SAVE_INTERVAL_SECONDS, value);
        }

        [Inject]
        public SaveCoordinator(
            ISaveService<T> saveService,
            [InjectOptional] IGameSettingsService gameSettings = null)
        {
            _saveService = saveService;
            _gameSettings = gameSettings;

            if (_gameSettings == null)
                return;

            ApplySettings(_gameSettings.Current);
            _gameSettings.Applied += ApplySettings;
        }

        public void Dispose()
        {
            if (_gameSettings != null)
                _gameSettings.Applied -= ApplySettings;
        }

        /// <summary>
        /// Drives the auto-save timer. Uses unscaled time so a paused game (timeScale 0)
        /// still auto-saves.
        /// </summary>
        public void Tick()
        {
            if (_isAutoSaveEnabled == false) return;

            // Holding the timer while a write is in flight keeps Tick from queueing up extra
            // SaveNow calls behind the running one when a write takes longer than the interval.
            if (_isSaving) return;

            _secondsSinceLastAutoSave += Time.unscaledDeltaTime;
            if (_secondsSinceLastAutoSave < _autoSaveIntervalSeconds) return;

            _secondsSinceLastAutoSave = 0f;

            if (HasUnsavedChanges == false) return;

            SaveNow().Forget();
        }

        public void MarkDirty()
        {
            HasUnsavedChanges = true;
        }

        public void AddContributor(ISaveContributor contributor)
        {
            if (contributor != null && !_contributors.Contains(contributor))
                _contributors.Add(contributor);
        }

        public void RemoveContributor(ISaveContributor contributor)
        {
            _contributors.Remove(contributor);
        }

        // Runs on the main thread, right before a write, so live state (the room, the skills) lands
        // in the save at one moment and off the per-frame path.
        private void CaptureContributors()
        {
            for (int i = 0; i < _contributors.Count; i++)
                _contributors[i]?.CaptureForSave();
        }

        public async UniTask SaveNow(bool force = false)
        {
            if (!force && !HasUnsavedChanges) return;

            // A write is already running (auto-save, or a double-clicked save button).
            // Wait it out rather than interleaving two writes to the same file.
            if (_isSaving)
            {
                await UniTask.WaitUntil(() => !_isSaving);

                // That write may have already flushed what we came here for.
                if (!force && !HasUnsavedChanges) return;
            }

            _isSaving = true;

            // Cleared before the await, not after: a mutation that happens while the write is
            // in flight re-marks the save as dirty instead of being swallowed by this write.
            HasUnsavedChanges = false;

            try
            {
                // Capture on the main thread before Save takes its off-thread snapshot.
                CaptureContributors();
                await _saveService.Save();
                Saved?.Invoke();
            }
            catch (Exception e)
            {
                HasUnsavedChanges = true;
                Debug.LogError($"Failed to write the save file. Changes are kept in memory and will be retried on the next save. Exception: {e}");
            }
            finally
            {
                _isSaving = false;
            }
        }

        public void SaveBlocking()
        {
            HasUnsavedChanges = false;

            try
            {
                CaptureContributors();
                _saveService.SaveBlocking();
                Saved?.Invoke();
            }
            catch (Exception e)
            {
                HasUnsavedChanges = true;
                Debug.LogError($"Failed to write the save file on quit. Exception: {e}");
            }
        }

        private void ApplySettings(GameSettingsData settings)
        {
            AutoSaveIntervalSeconds = settings.AutoSaveIntervalSeconds;
            IsAutoSaveEnabled = settings.AutoSave;
        }
    }
}
