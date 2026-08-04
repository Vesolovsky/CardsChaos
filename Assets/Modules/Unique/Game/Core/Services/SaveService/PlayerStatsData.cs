namespace Vesolovsky.Game.Services.Save
{
    /// <summary>
    /// The player's running tally across the whole save - not analytics, just the numbers the game
    /// keeps on itself: how much has been thrown, how far has been walked, how long it has been
    /// played. The cumulative fields only ever grow (the peak aside), so they survive relaunches and
    /// read the same as a lifetime total.
    ///
    /// The collection figures (<see cref="CorrectlyPlacedCards"/>, <see cref="TotalCards"/>) are a
    /// snapshot the tracker keeps in step with the album while the room is loaded. They live here so
    /// a screen outside the gameplay scene - a menu, a summary - can read progress straight from the
    /// save without the album or catalog being present to work it out.
    /// See <see cref="Vesolovsky.Game.Services.Stats.IPlayerStats"/>.
    /// </summary>
    public sealed class PlayerStatsData
    {
        /// <summary>Times a card was thrown out of the hand and back onto the floor, all sessions.</summary>
        public long CardsThrown { get; set; }

        /// <summary>Times a card was taken off the floor into the hand, all sessions.</summary>
        public long CardsPickedUp { get; set; }

        /// <summary>Times a skill fired, all sessions. Only successful fires count.</summary>
        public long SkillsUsed { get; set; }

        /// <summary>How many times the room has been entered - one per gameplay session.</summary>
        public long SessionsPlayed { get; set; }

        /// <summary>Seconds spent in the room with the clock running (paused time does not count).</summary>
        public double PlaytimeSeconds { get; set; }

        /// <summary>Total horizontal distance the player has walked, in world units, sprint included.</summary>
        public double DistanceTraveled { get; set; }

        /// <summary>The part of <see cref="DistanceTraveled"/> covered while the sprint was held.</summary>
        public double DistanceSprinted { get; set; }

        /// <summary>
        /// The most cards ever correctly filed in the album at one moment - the high-water mark of
        /// progress. Unlike the live count it never falls when cards are taken back out.
        /// </summary>
        public int PeakCorrectlyPlaced { get; set; }

        /// <summary>
        /// How many cards are correctly filed in the album, as of the last time the room was loaded
        /// and kept in sync. A snapshot, not a running total - it rises and falls with the album.
        /// </summary>
        public int CorrectlyPlacedCards { get; set; }

        /// <summary>
        /// Every card in the game at snapshot time - the denominator of collection progress, and
        /// what "cards left to file" is measured against. Refreshed alongside
        /// <see cref="CorrectlyPlacedCards"/> so both come from the same moment.
        /// </summary>
        public int TotalCards { get; set; }

        /// <summary>
        /// Isolated copy for the off-thread save write, so serializing on a background thread never
        /// reads a field gameplay is mutating on the main thread. All fields are value types, so a
        /// flat field copy is a full deep copy.
        /// </summary>
        public PlayerStatsData Clone()
        {
            return new PlayerStatsData
            {
                CardsThrown = CardsThrown,
                CardsPickedUp = CardsPickedUp,
                SkillsUsed = SkillsUsed,
                SessionsPlayed = SessionsPlayed,
                PlaytimeSeconds = PlaytimeSeconds,
                DistanceTraveled = DistanceTraveled,
                DistanceSprinted = DistanceSprinted,
                PeakCorrectlyPlaced = PeakCorrectlyPlaced,
                CorrectlyPlacedCards = CorrectlyPlacedCards,
                TotalCards = TotalCards,
            };
        }
    }
}
