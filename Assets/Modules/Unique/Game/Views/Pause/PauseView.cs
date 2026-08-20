using System;
using Cysharp.Threading.Tasks;
using RoboRyanTron.SceneReference;
using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Audio;
using Vesolovsky.Core.Services;
using Vesolovsky.Core.Services.Save;
using Vesolovsky.Core.Services.Settings;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Core.UISystem.UIComponents;
using Vesolovsky.Game.Services.Pause;
using Vesolovsky.Game.Services.Skills;
using Vesolovsky.Game.Services.Stats;
using Zenject;

namespace Vesolovsky.Game.Views
{
    /// <summary>
    /// The pause menu. Its contents are wired elsewhere; this half is how Escape brings it up and
    /// takes it away, and what pausing does to the rest of the game.
    ///
    /// Escape is shared. While the player is inside something that owns it - a card close-up, the
    /// album, the upgrades screen - Escape backs out of that, and only once none of them holds the
    /// room does Escape mean "pause". The world-interaction lock is that "something owns the room"
    /// signal, so the pause comes up only while it is free; and it is read a frame in arrears as
    /// well, so the very Escape that closes a section can never also raise the pause behind it.
    ///
    /// While it is up it takes the room itself - freezing the camera and the card table and shutting
    /// the album and upgrades screen out - silences the skills, and stops the clock so cooldowns
    /// hold exactly where they were.
    /// </summary>
    public class PauseView : View<IPauseViewModel>
    {
        [Tooltip("Closes the pause menu. Escape does the same thing.")]
        [SerializeField] private VButton resumeButton;

        [Tooltip("Spawns SettingsView on the DynamicViewsCanvas.")]
        [SerializeField] private VButton settingsButton;

        [Tooltip("Visible only while the applied Auto Save setting is Off.")]
        [SerializeField] private VButton saveButton;

        [Tooltip("The sentence under the pause-menu buttons describing the applied save mode.")]
        [SerializeField] private VText autoSaveStatusText;

        [Tooltip("Reads \"Collection progress: X / Y\" - cards correctly filed over the whole deck. " +
                 "Filled in each time the menu opens from the saved collection snapshot.")]
        [SerializeField] private VText collectionProgressText;

        [Tooltip("Saves the room, hands it back, and returns to the main menu scene.")]
        [SerializeField] private VButton mainMenuButton;

        [Tooltip("The scene the Main Menu button leads to. Must be in Build Settings.")]
        [SerializeField] private SceneReference mainMenuScene;

        [Tooltip("Saves fully, then quits the game.")]
        [SerializeField] private VButton quitButton;

        private IWorldInteractionLock _worldLock;
        private ISkillGate _skillGate;
        private IPauseState _pauseState;
        private ISaveCoordinator _saveCoordinator;
        private IGameSettingsService _gameSettings;
        private DynamicViewsCanvas _dynamicViewsCanvas;
        private IPlayerStats _playerStats;
        private ISceneTransition _sceneTransition;

        private IDisposable _worldHandle;
        private bool _isOpen;
        private bool _isOpeningSettings;
        private bool _isSaving;

        // Set the moment the player commits to leaving for the menu, and only cleared if that
        // turns out to be impossible. Escape stands down while it is set: the room is on its way
        // out and there is nothing left here to pause.
        private bool _isLeaving;

        // The room was free on the previous frame. Required as well as "free now" so a section that
        // releases the room on this very Escape does not hand the same press straight to the pause.
        private bool _worldFreeLastFrame = true;

        [Inject]
        private void InjectPause(
            IWorldInteractionLock worldLock,
            [InjectOptional] ISkillGate skillGate,
            [InjectOptional] IPauseState pauseState,
            [InjectOptional] ISaveCoordinator saveCoordinator,
            [InjectOptional] IGameSettingsService gameSettings,
            [InjectOptional] DynamicViewsCanvas dynamicViewsCanvas,
            [InjectOptional] IPlayerStats playerStats,
            [InjectOptional] ISceneTransition sceneTransition)
        {
            _worldLock = worldLock;
            _skillGate = skillGate;
            _pauseState = pauseState;
            _saveCoordinator = saveCoordinator;
            _gameSettings = gameSettings;
            _dynamicViewsCanvas = dynamicViewsCanvas;
            _playerStats = playerStats;
            _sceneTransition = sceneTransition;
        }

        protected override void InitialViewSetup(IViewInitData viewInitData)
        {
            base.InitialViewSetup(viewInitData);

            if (resumeButton != null)
                resumeButton.Bind(Close);

            settingsButton?.Bind(() => OpenSettings().Forget());
            saveButton?.Bind(() => SaveProgress().Forget());
            mainMenuButton?.Bind(() => OpenMainMenu().Forget());
            quitButton?.Bind(QuitGame);

            if (_gameSettings != null)
            {
                _gameSettings.Applied += OnSettingsApplied;
                RefreshSaveUi(_gameSettings.Current.AutoSave);
            }
            else
            {
                RefreshSaveUi(_saveCoordinator == null || _saveCoordinator.IsAutoSaveEnabled);
            }

            // GameplayScene currently has one persistent pause view, so it is also the natural
            // scene-level place to start the configured gameplay music state.
            AudioService.SetState(AudioStateKey.Music_Level);
        }

        private void Update()
        {
            // The room is on its way out; there is nothing here left to pause or resume.
            if (_isLeaving)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            bool worldFree = _worldLock == null || !_worldLock.IsLocked;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                // Settings and its rebinding popup own Escape while they are on top of Pause.
                // In particular, canceling a rebind must not also close the pause menu behind it.
                if (HasSettingsOverlay())
                {
                    _worldFreeLastFrame = worldFree;
                    return;
                }

                if (_isOpen)
                    Close();
                else if (worldFree && _worldFreeLastFrame)
                    Open();
            }

            _worldFreeLastFrame = worldFree;
        }

        /// <summary>Brings the pause menu up and pauses the game behind it.</summary>
        public void Open()
        {
            if (_isOpen)
                return;

            _isOpen = true;

            // Take the room the way the album and upgrades screen do, so the camera, the cards and
            // both of those panels fall quiet behind the menu. The gate silences even the skills
            // that ignore the room lock, and the pause state stops the clock.
            _worldHandle = _worldLock?.Acquire(this);

            if (_skillGate != null)
                _skillGate.Blocked = true;

            if (_pauseState != null)
                _pauseState.IsPaused = true;

            // The collection snapshot is kept in step with the album while the room is loaded, so
            // by the time the menu can come up it is current; reading it on open is enough.
            RefreshCollectionProgress();

            AudioService.Play(AudioSFXKey.PauseOpen);

            // Music and the environmental ambient keep playing behind the menu, only muffled - a
            // low-pass eased shut over the mix, which reads as the room going quiet while it is held.
            AudioService.SetMuffled(true);

            Show(destroyCancellationToken).Forget();
        }

        /// <summary>Takes the pause menu away and hands the game back. Wired to the Resume button.</summary>
        public void Close()
        {
            if (!_isOpen)
                return;

            _isOpen = false;
            ReleaseRoom();

            // Open the filters back up, so music and ambient swell back to full behind the closing menu.
            AudioService.SetMuffled(false);

            Hide(destroyCancellationToken).Forget();
        }

        private void RefreshCollectionProgress()
        {
            if (collectionProgressText == null || _playerStats == null)
                return;

            collectionProgressText.SetText(
                $"Collection progress: {_playerStats.CorrectlyPlacedCards} / {_playerStats.TotalCards}");
        }

        private void QuitGame()
        {
            // Full synchronous save before the process leaves. ApplicationSaveHandler also saves on
            // OnApplicationQuit, so a window-close from outside the menu is covered as well.
            _saveCoordinator?.SaveBlocking();
            Application.Quit();
        }

        private async UniTask OpenMainMenu()
        {
            if (_isLeaving)
                return;

            if (mainMenuScene == null || string.IsNullOrEmpty(mainMenuScene.SceneName))
            {
                Debug.LogError($"[{nameof(PauseView)}] No main menu scene assigned, so the Main " +
                               "Menu button has nowhere to go.", this);

                return;
            }

            _isLeaving = true;

            // The room's save contributors are torn down with the gameplay scene, so the world has
            // to be captured while they are still alive - before the load below.
            if (_saveCoordinator != null)
                await _saveCoordinator.SaveNow(force: true);

            // Hand the room back before leaving it, rather than trusting teardown order to do it.
            // The muffle in particular is not the room's - the audio service lives on the project
            // context and outlives this scene - so a menu reached through a pause that was never
            // closed would come up with its music behind a low-pass filter.
            Close();

            await SceneViewsService.HideScene();

            try
            {
                if (_sceneTransition != null)
                    await _sceneTransition.FadeIn();

                AsyncOperation operation = mainMenuScene.LoadSceneAsync();
                await UniTask.WaitUntil(() => operation.isDone);

                if (_sceneTransition != null)
                    await _sceneTransition.FadeOut();
            }
            catch (Exception exception)
            {
                // Nearly always the menu scene missing from Build Settings. The HUD and the pause
                // menu have already been taken away by this point, so leaving it there would strand
                // the player looking at a room they can neither play nor leave - the menu is put
                // back up instead, and the reason said out loud.
                Debug.LogError($"[{nameof(PauseView)}] Could not reach the main menu scene " +
                               $"'{mainMenuScene.SceneName}'; staying in the room.", this);

                Debug.LogException(exception, this);

                await SceneViewsService.ShowScene();

                _isLeaving = false;
                Open();
            }
        }

        private async UniTask SaveProgress()
        {
            if (_saveCoordinator == null || _isSaving)
                return;

            _isSaving = true;
            try
            {
                await _saveCoordinator.SaveNow();
                await ShowSavedConfirmation();
            }
            finally
            {
                _isSaving = false;
            }
        }

        private async UniTask ShowSavedConfirmation()
        {
            if (_dynamicViewsCanvas == null || this == null)
                return;

            var definition = new ConfirmationPopupViewDefinition
            {
                ViewModelInitData = new ConfirmationPopupViewModelInitData(
                    "Progress saved",
                    "Your progress has been saved.",
                    ConfirmationPopupButtons.Confirm)
            };

            // Lives on the shared DynamicViewsCanvas and unloads itself when its single button is
            // pressed; HasSettingsOverlay() already treats it as owning Escape while it is up.
            await SceneViewsService.AddView(
                definition,
                _dynamicViewsCanvas.transform,
                throughQueue: false);
        }

        private async UniTask OpenSettings()
        {
            if (_isOpeningSettings || _dynamicViewsCanvas == null || HasSettingsOverlay())
                return;

            _isOpeningSettings = true;
            try
            {
                await SceneViewsService.AddView(
                    new SettingsViewDefinition(),
                    _dynamicViewsCanvas.transform);
            }
            finally
            {
                _isOpeningSettings = false;
            }
        }

        private bool HasSettingsOverlay()
        {
            if (_isOpeningSettings)
                return true;

            if (SceneViewsService == null)
                return false;

            foreach (IView view in SceneViewsService.LoadedViews)
            {
                if (view.IsShown && (view is SettingsView || view is ConfirmationPopupView))
                    return true;
            }

            return false;
        }

        private void OnSettingsApplied(GameSettingsData settings)
        {
            RefreshSaveUi(settings.AutoSave);
        }

        private void RefreshSaveUi(bool autoSaveEnabled)
        {
            autoSaveStatusText?.SetText(autoSaveEnabled
                ? "Your progress is saved automatically."
                : "Your progress is NOT saved automatically!");

            if (saveButton != null)
                saveButton.gameObject.SetActive(!autoSaveEnabled);
        }

        private void ReleaseRoom()
        {
            _worldHandle?.Dispose();
            _worldHandle = null;

            if (_skillGate != null)
                _skillGate.Blocked = false;

            if (_pauseState != null)
                _pauseState.IsPaused = false;
        }

        protected override void OnDestroy()
        {
            if (_gameSettings != null)
                _gameSettings.Applied -= OnSettingsApplied;

            // A menu torn down while it is up must not leave the room locked, the skills gated, the
            // clock stopped or the music stuck behind its muffle filter.
            if (_isOpen)
            {
                ReleaseRoom();
                AudioService.SetMuffled(false);
            }

            base.OnDestroy();
        }
    }
}
