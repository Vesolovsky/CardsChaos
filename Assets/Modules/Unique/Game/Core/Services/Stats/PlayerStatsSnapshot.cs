using System;
using Vesolovsky.Game.Services.Save;

namespace Vesolovsky.Game.Services.Stats
{
    /// <summary>
    /// A fixed set of numbers wearing the <see cref="IPlayerStats"/> shape, so a screen that reads
    /// the live tally can be handed a frozen one instead without knowing the difference.
    ///
    /// Used for the ending: the closing spread asks for stats, and after the game is finished what
    /// it should get is the tally as it stood that day rather than whatever the counters have
    /// drifted to since.
    /// </summary>
    public sealed class PlayerStatsSnapshot : IPlayerStats
    {
        private readonly PlayerStatsData _data;

        public PlayerStatsSnapshot(PlayerStatsData data)
        {
            // An empty block rather than a null: every caller reads these straight into a label,
            // and zeros are a truthful answer where nothing was ever recorded.
            _data = data ?? new PlayerStatsData();
        }

        /// <summary>Never raised - a snapshot is exactly what does not change.</summary>
        public event Action Changed
        {
            add { }
            remove { }
        }

        public long CardsThrown => _data.CardsThrown;

        public long CardsPickedUp => _data.CardsPickedUp;

        public long SkillsUsed => _data.SkillsUsed;

        public long SessionsPlayed => _data.SessionsPlayed;

        public double PlaytimeSeconds => _data.PlaytimeSeconds;

        public double DistanceTraveled => _data.DistanceTraveled;

        public double DistanceSprinted => _data.DistanceSprinted;

        public int PeakCorrectlyPlaced => _data.PeakCorrectlyPlaced;

        public int PeakAlbumCorrect => _data.PeakAlbumCorrect;

        public int PeakDuplicatesStored => _data.PeakDuplicatesStored;

        public int PeakCorrectPlacementStreak => _data.PeakCorrectPlacementStreak;

        public int TotalCards => _data.TotalCards;

        public int CorrectlyPlacedCards => _data.CorrectlyPlacedCards;

        public int CardsRemainingToPlace => Math.Max(0, TotalCards - CorrectlyPlacedCards);
    }
}
