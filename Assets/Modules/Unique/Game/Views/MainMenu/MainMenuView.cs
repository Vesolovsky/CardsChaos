using System;
using System.Globalization;
using Cysharp.Threading.Tasks;
using RoboRyanTron.SceneReference;
using UnityEngine;
using Vesolovsky.Core.Audio;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Core.UISystem.UIComponents;
using Vesolovsky.Game.Views.MainMenu;
using Zenject;

namespace Vesolovsky.Game.Views
{
    /// <summary>
    /// The main menu. Seven cards spread in a fan, and each one is a way out of this screen.
    ///
    /// The cards are authored in the prefab, one object each, and this only says what a click on
    /// each of them means. Two of the seven are conditional rather than fixed:
    /// <list type="bullet">
    /// <item>Continue is only in the fan when there is a game to go back to, and carries the
    /// collection count read off the save.</item>
    /// <item>New Game only asks before it wipes anything. With no save there is nothing to warn
    /// about, and a confirmation that says the progress will be lost would simply be untrue.</item>
    /// </list>
    ///
    /// Settings and the album come up over the menu as their own views on the dynamic canvas, the
    /// same way the pause menu opens them; everything else either loads a scene or leaves the
    /// process. Scenes are left through the shared transition, and the fan is dealt back off the
    /// table first, so the menu never simply blinks out.
    /// </summary>
    public class MainMenuView : View<IMainMenuViewModel>
    {
        [SerializeField] private MainMenuCardFan fan;

        [Header("Scenes")]
        [Tooltip("Loaded by Continue and by New Game once its save has been wiped.")]
        [SerializeField] private SceneReference gameplayScene;

        [SerializeField] private SceneReference creditsScene;

        [Header("Card lines")]
        [Tooltip("The second line on the Continue card. {0} is when the save was last played. " +
                 "Formatted invariantly, so the separators come out exactly as written here " +
                 "whatever the player's regional settings are.")]
        [SerializeField] private string lastPlayedFormat = "Last played: {0:dd:MM:yyyy:HH:mm}";

        [Tooltip("The second line on the Album card. {0} is the cards placed correctly, {1} " +
                 "every card there is to place.")]
        [SerializeField] private string albumProgressFormat = "Cards collected {0}/{1}";

        [Header("New game")]
        [SerializeField] private string newGameTitle = "Start a new game?";

        [SerializeField, TextArea]
        private string newGameDescription =
            "This will erase your current progress and start a new save. This cannot be undone.";

        [Header("Discord")]
        [Tooltip("Opened in the player's browser by the Discord card. Left empty the card does " +
                 "nothing but say so in the log - which is where it stands until the server exists.")]
        [SerializeField] private string discordUrl = string.Empty;

        private ISceneTransition _sceneTransition;
        private DynamicViewsCanvas _dynamicViewsCanvas;

        private bool _isLeaving;
        private bool _isOpeningPanel;

        [Inject]
        private void InjectMenu(
            ISceneTransition sceneTransition,
            [InjectOptional] DynamicViewsCanvas dynamicViewsCanvas)
        {
            _sceneTransition = sceneTransition;
            _dynamicViewsCanvas = dynamicViewsCanvas;
        }

        /// <summary>
        /// Where Settings, the album and the confirmation popup are put. The dynamic canvas when
        /// the scene has one - it draws over the menu and is what every other screen in the game
        /// uses - and this view's own transform as a fallback so a bare test scene still works.
        /// </summary>
        private Transform PanelParent =>
            _dynamicViewsCanvas != null ? _dynamicViewsCanvas.transform : transform;

        protected override void InitialViewSetup(IViewInitData viewInitData)
        {
            base.InitialViewSetup(viewInitData);

            if (fan == null)
            {
                Debug.LogError($"[{nameof(MainMenuView)}] No card fan assigned; the menu has no " +
                               "buttons.", this);

                return;
            }

            AudioService.SetState(AudioStateKey.Music_MainMenu);

            ApplyCardLines();

            fan.CardClicked += OnCardClicked;

            // Settled before the deal so the arc is struck around the cards that are actually
            // there - with Continue away, the remaining six close the fan up rather than leaving
            // a hole at the left-hand end.
            fan.ApplyLayout();
        }

        /// <summary>
        /// The two cards that say something about the save behind them, rather than only naming
        /// where they go: Continue carries when it was last played, and Album carries how much of
        /// the collection is in. Both lines simply do not appear when the save cannot answer -
        /// an empty line is better than a made-up date or an invented "0/0".
        /// </summary>
        private void ApplyCardLines()
        {
            // Continue is in the fan only when there is something to continue.
            bool hasSave = ViewModel.HasStartedGame;
            fan.SetCardShown(MainMenuAction.Continue, hasSave);

            if (hasSave)
            {
                DateTime? lastPlayed = ViewModel.LastPlayedAt;

                SetCardLine(MainMenuAction.Continue, lastPlayed.HasValue
                    ? string.Format(CultureInfo.InvariantCulture, lastPlayedFormat, lastPlayed.Value)
                    : string.Empty);
            }

            SetCardLine(MainMenuAction.Album, ViewModel.HasCollectionProgress
                ? string.Format(CultureInfo.InvariantCulture, albumProgressFormat,
                    ViewModel.CardsCollected, ViewModel.TotalCards)
                : string.Empty);
        }

        private void SetCardLine(MainMenuAction action, string text)
        {
            MainMenuCard card = fan.Find(action);
            if (card != null)
                card.SetSubLabel(text);
        }

        private void OnCardClicked(MainMenuCard card)
        {
            if (_isLeaving)
                return;

            switch (card.Action)
            {
                case MainMenuAction.Continue:
                    LoadScene(gameplayScene).Forget();
                    break;

                case MainMenuAction.NewGame:
                    NewGame().Forget();
                    break;

                case MainMenuAction.Settings:
                    OpenPanel(new SettingsViewDefinition()).Forget();
                    break;

                case MainMenuAction.Album:
                    OpenPanel(CreateReadOnlyAlbumDefinition()).Forget();
                    break;

                case MainMenuAction.Credits:
                    LoadScene(creditsScene).Forget();
                    break;

                case MainMenuAction.Discord:
                    OpenDiscord();
                    break;

                case MainMenuAction.Quit:
                    Application.Quit();
                    break;

                default:
                    Debug.LogError($"[{nameof(MainMenuView)}] Card '{card.name}' has no action " +
                                   "assigned, so nothing happens when it is clicked.", card);
                    break;
            }
        }

        /// <summary>
        /// The album opened from the menu is a display case: every card the player has filed, and
        /// no way to move any of them. Same view, same prefab - the read-only flag is the only
        /// difference, and the album's own dependencies on the room are simply not there to
        /// resolve in this scene anyway.
        /// </summary>
        private static CardAlbumViewDefinition CreateReadOnlyAlbumDefinition()
        {
            return new CardAlbumViewDefinition
            {
                ViewModelInitData = new CardAlbumViewModelInitData { ReadOnly = true }
            };
        }

        private async UniTaskVoid NewGame()
        {
            // Nothing to lose, so nothing to ask about. A confirmation that promises to erase
            // progress the player does not have is worse than no confirmation at all.
            if (!ViewModel.HasStartedGame)
            {
                await StartNewGame();
                return;
            }

            if (_isOpeningPanel || HasPanelOpen())
                return;

            var definition = new ConfirmationPopupViewDefinition
            {
                ViewModelInitData = new ConfirmationPopupViewModelInitData(
                    newGameTitle,
                    newGameDescription,
                    ConfirmationPopupButtons.ConfirmAndDecline,
                    confirmAction: () => StartNewGame().Forget())
            };

            _isOpeningPanel = true;
            try
            {
                // Straight in rather than through the loading queue: the popup is an answer to a
                // click and should be on screen on the next frame, not behind whatever else the
                // queue happens to be chewing through.
                await SceneViewsService.AddView(definition, PanelParent, throughQueue: false);
            }
            finally
            {
                _isOpeningPanel = false;
            }
        }

        private async UniTask StartNewGame()
        {
            await ViewModel.StartNewGame();
            await LoadScene(gameplayScene);
        }

        private async UniTask LoadScene(SceneReference scene)
        {
            if (_isLeaving)
                return;

            if (scene == null || string.IsNullOrEmpty(scene.SceneName))
            {
                Debug.LogError($"[{nameof(MainMenuView)}] No scene assigned for that card.", this);
                return;
            }

            _isLeaving = true;

            // The cards are dealt back off the table by the scene hide, and the transition covers
            // the load itself - the same two steps every other scene change in the game takes.
            await SceneViewsService.HideScene();
            await Cover(fadeIn: true);

            AsyncOperation operation;
            try
            {
                operation = scene.LoadSceneAsync();
            }
            catch (Exception exception)
            {
                // Nearly always the scene missing from Build Settings. The menu has already dealt
                // itself off the table by this point, so leaving it there would strand the player
                // in a screen that no longer answers - it is brought back instead, and the reason
                // said out loud.
                Debug.LogError($"[{nameof(MainMenuView)}] Could not load scene " +
                               $"'{scene.SceneName}'; returning to the menu.", this);

                Debug.LogException(exception, this);

                await Cover(fadeIn: false);
                await SceneViewsService.ShowScene();

                _isLeaving = false;
                return;
            }

            await UniTask.WaitUntil(() => operation.isDone);

            await Cover(fadeIn: false);
        }

        /// <summary>
        /// Runs one half of the wipe over a scene change, and swallows it if it fails.
        ///
        /// The wipe is decoration over the load; the load is the point. A transition wired to an
        /// asset that is no longer in the project must not be the reason a player cannot get into
        /// their game, so it is reported and the change carries on uncovered.
        /// </summary>
        private async UniTask Cover(bool fadeIn)
        {
            try
            {
                await (fadeIn ? _sceneTransition.FadeIn() : _sceneTransition.FadeOut());
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private async UniTaskVoid OpenPanel(IViewDefinition definition)
        {
            if (_isOpeningPanel || HasPanelOpen())
                return;

            _isOpeningPanel = true;
            try
            {
                await SceneViewsService.AddView(definition, PanelParent);
            }
            finally
            {
                _isOpeningPanel = false;
            }
        }

        /// <summary>
        /// Whether something is already up over the menu. Without it a second click while a panel
        /// is loading - or while one is open behind its own backdrop - would stack a duplicate on
        /// top of it. Mirrors the guard the pause menu uses for the same reason.
        /// </summary>
        private bool HasPanelOpen()
        {
            if (SceneViewsService == null)
                return false;

            foreach (IView view in SceneViewsService.LoadedViews)
            {
                if (view.IsShown &&
                    (view is SettingsView || view is CardAlbumView || view is ConfirmationPopupView))
                {
                    return true;
                }
            }

            return false;
        }

        private void OpenDiscord()
        {
            if (string.IsNullOrWhiteSpace(discordUrl))
            {
                Debug.Log($"[{nameof(MainMenuView)}] No Discord URL set yet, so the card has " +
                          "nowhere to send the player.", this);

                return;
            }

            Application.OpenURL(discordUrl);
        }

        protected override void OnDestroy()
        {
            if (fan != null)
                fan.CardClicked -= OnCardClicked;

            base.OnDestroy();
        }
    }
}
