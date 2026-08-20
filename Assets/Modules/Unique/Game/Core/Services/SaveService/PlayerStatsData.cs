using System;

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

        /// <summary>
        /// The last moment the player was actually in the room, in their own local time. Kept in
        /// step with <see cref="PlaytimeSeconds"/> - the same frames that count as playing are the
        /// frames that move this - so it reads as when they stopped playing rather than when they
        /// started, and time spent sitting in a menu never counts.
        ///
        /// Null on a save from before it was recorded, and on one that has never been played.
        /// </summary>
        public DateTime? LastPlayedAt { get; set; }

        /// <summary>Total horizontal distance the player has walked, in world units, sprint included.</summary>
        public double DistanceTraveled { get; set; }

        /// <summary>The part of <see cref="DistanceTraveled"/> covered while the sprint was held.</summary>
        public double DistanceSprinted { get; set; }

        /// <summary>
        /// The most originals/duplicates ever correctly placed at one moment - the high-water mark
        /// of progress. Unlike the live count it never falls when cards are taken back out.
        /// </summary>
        public int PeakCorrectlyPlaced { get; set; }

        /// <summary>
        /// Correct album originals plus valid duplicates in their boxes, kept in sync while the room
        /// is loaded. A snapshot, not a running total - it rises and falls with both destinations.
        /// </summary>
        public int CorrectlyPlacedCards { get; set; }

        /// <summary>
        /// Every card plus every duplicate of one at snapshot time - the collection denominator and
        /// what "cards left to place" is measured against. Refreshed alongside
        /// <see cref="CorrectlyPlacedCards"/> so both come from the same moment.
        /// </summary>
        public int TotalCards { get; set; }

        /// <summary>
        /// The most duplicates ever sitting in the duplicate box at one moment. A high-water mark
        /// like <see cref="PeakCorrectlyPlaced"/>, because the duplicate task is measured against
        /// it: emptying a box afterwards must not take back a task the player has already done.
        /// </summary>
        public int PeakDuplicatesStored { get; set; }

        /// <summary>
        /// The most originals ever correctly filed in the album at one moment - the album half of
        /// <see cref="PeakCorrectlyPlaced"/> on its own, with boxed duplicates left out. Kept apart
        /// because the album milestones are counted in filed cards only, and a player who had boxed
        /// a few hundred duplicates would otherwise cross them without filing that many cards.
        /// </summary>
        public int PeakAlbumCorrect { get; set; }

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
                LastPlayedAt = LastPlayedAt,
                DistanceTraveled = DistanceTraveled,
                DistanceSprinted = DistanceSprinted,
                PeakCorrectlyPlaced = PeakCorrectlyPlaced,
                CorrectlyPlacedCards = CorrectlyPlacedCards,
                TotalCards = TotalCards,
                PeakDuplicatesStored = PeakDuplicatesStored,
                PeakAlbumCorrect = PeakAlbumCorrect,
            };
        }
    }
}
