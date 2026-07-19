using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace Vesolovsky.Core.Services.Save
{
    /// <summary>
    /// Default <see cref="ISaveCoordinator"/>. Generic over the save type so this stays in
    /// Core and does not need to reference the game-specific save model; bind the closed
    /// type (e.g. <c>SaveCoordinator&lt;GameSave&gt;</c>) in the game installer.
    /// </summary>
    public class SaveCoordinator<T> : ISaveCoordinator, ITickable where T : IGameSave
    {
        private const float MIN_AUTO_SAVE_INTERVAL_SECONDS = 1f;
        private const float DEFAULT_AUTO_SAVE_INTERVAL_SECONDS = 300f;

        public event Action Saved;

        private readonly ISaveService<T> _saveService;

        private float _autoSaveIntervalSeconds = DEFAULT_AUTO_SAVE_INTERVAL_SECONDS;
        private float _secondsSinceLastAutoSave;
        private bool _isAutoSaveEnabled;
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
        public SaveCoordinator(ISaveService<T> saveService)
        {
            _saveService = saveService;
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
    }
}
