using System;
using Cysharp.Threading.Tasks;
using Vesolovsky.Core.Services.Save;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.Services.Save;
using Zenject;

namespace Vesolovsky.Game.Views
{
    /// <summary>
    /// What the main menu needs to know about the save, and the one thing it can do to it.
    ///
    /// Everything here is read straight off the loaded save rather than worked out from the album
    /// and the catalog. That is deliberate: the menu scene has no room, no hand and no card
    /// tracker, and the collection figures are kept in the save precisely so a screen outside
    /// gameplay can read progress without any of that being present.
    /// </summary>
    public class MainMenuViewModel : ViewModel, IMainMenuViewModel
    {
        private readonly ISaveService<GameSave> _saveService;
        private readonly ISaveCoordinator _saveCoordinator;

        [Inject]
        public MainMenuViewModel(ISaveService<GameSave> saveService, ISaveCoordinator saveCoordinator)
        {
            _saveService = saveService;
            _saveCoordinator = saveCoordinator;
        }

        private GameSave Save => _saveService.CurrentSave;

        /// <summary>
        /// A save file on disk proves nothing on its own - quitting from this very menu writes
        /// one, and so does answering the analytics prompt. What proves a game was actually
        /// started is that the room has been captured, a card has been filed, or a session has
        /// been counted; any one of the three is enough, so a save from before the stats block
        /// existed still offers Continue.
        /// </summary>
        public bool HasStartedGame
        {
            get
            {
                GameSave save = Save;
                if (save == null)
                    return false;

                return save.World != null
                       || (save.Album != null && save.Album.Count > 0)
                       || (save.PlayerStats != null && save.PlayerStats.SessionsPlayed > 0);
            }
        }

        public int CardsCollected => Save?.PlayerStats?.CorrectlyPlacedCards ?? 0;

        public int TotalCards => Save?.PlayerStats?.TotalCards ?? 0;

        public bool HasCollectionProgress => TotalCards > 0;

        public DateTime? LastPlayedAt => Save?.PlayerStats?.LastPlayedAt;

        public async UniTask StartNewGame()
        {
            _saveService.ClearSave();

            // ClearSave only touches the in-memory save and deliberately does not mark it dirty,
            // so the write has to be forced - otherwise the coordinator would decide there was
            // nothing to save and the old file would still be sitting there.
            await _saveCoordinator.SaveNow(force: true);
        }
    }
}
