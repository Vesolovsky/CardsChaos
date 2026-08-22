using System;
using Vesolovsky.Core.Services.Save;
using Vesolovsky.Game.Services.Save;
using Zenject;

namespace Vesolovsky.Game.Services.Stats
{
    /// <summary>
    /// The player's tally as the save last recorded it, for scenes that have no room to count in.
    ///
    /// <see cref="PlayerStatsService"/> is the real tracker: it rides gameplay events, ticks the
    /// clock and works the collection figures out from the album, the duplicate boxes and the
    /// catalog. None of that exists in the main menu - and none of it needs to, because the
    /// tracker writes its figures into the save precisely so they can be read back later. This is
    /// that read, and nothing else.
    ///
    /// Nothing here ever changes, so <see cref="Changed"/> is declared and never raised: the save
    /// cannot move while the menu is the only thing running.
    /// </summary>
    public sealed class SavedPlayerStats : IPlayerStats
    {
        private readonly ISaveService<GameSave> _saveService;

        [Inject]
        public SavedPlayerStats(ISaveService<GameSave> saveService)
        {
            _saveService = saveService;
        }

        /// <summary>Never raised - a saved tally is a photograph, not a counter.</summary>
        public event Action Changed
        {
            add { }
            remove { }
        }

        private PlayerStatsData Stats => _saveService.CurrentSave?.PlayerStats;

        public long CardsThrown => Stats?.CardsThrown ?? 0L;

        public long CardsPickedUp => Stats?.CardsPickedUp ?? 0L;

        public long SkillsUsed => Stats?.SkillsUsed ?? 0L;

        public long SessionsPlayed => Stats?.SessionsPlayed ?? 0L;

        public double PlaytimeSeconds => Stats?.PlaytimeSeconds ?? 0d;

        public double DistanceTraveled => Stats?.DistanceTraveled ?? 0d;

        public double DistanceSprinted => Stats?.DistanceSprinted ?? 0d;

        public int PeakCorrectlyPlaced => Stats?.PeakCorrectlyPlaced ?? 0;

        public int PeakAlbumCorrect => Stats?.PeakAlbumCorrect ?? 0;

        public int PeakDuplicatesStored => Stats?.PeakDuplicatesStored ?? 0;

        public int PeakCorrectPlacementStreak => Stats?.PeakCorrectPlacementStreak ?? 0;

        public int TotalCards => Stats?.TotalCards ?? 0;

        public int CorrectlyPlacedCards => Stats?.CorrectlyPlacedCards ?? 0;

        public int CardsRemainingToPlace => Math.Max(0, TotalCards - CorrectlyPlacedCards);
    }
}
