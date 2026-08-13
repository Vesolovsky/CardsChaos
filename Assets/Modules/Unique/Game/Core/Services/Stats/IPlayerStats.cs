using System;

namespace Vesolovsky.Game.Services.Stats
{
    /// <summary>
    /// The player's progress in plain numbers - not analytics, just what the game keeps on itself
    /// for a stats screen, a milestone check or an achievement to read later.
    ///
    /// Every number here is saved. The cumulative ones (thrown, walked, played...) grow across the
    /// whole life of the save; the collection ones (correctly filed, total, left to file) are a
    /// snapshot the tracker keeps in step with the album while the room is loaded, so they can be
    /// read from the save even outside the gameplay scene where the album is not around to ask.
    /// </summary>
    public interface IPlayerStats
    {
        /// <summary>
        /// Raised when a cumulative counter changes on a discrete event - a throw, a pickup, a skill,
        /// a new progress peak. The continuously ticking figures (playtime, distance) are left for a
        /// reader to poll each frame rather than firing this every frame.
        /// </summary>
        event Action Changed;

        // --- Cumulative, saved ---

        /// <summary>Times a card was thrown out of the hand, all sessions.</summary>
        long CardsThrown { get; }

        /// <summary>Times a card was taken off the floor into the hand, all sessions.</summary>
        long CardsPickedUp { get; }

        /// <summary>Times a skill fired, all sessions.</summary>
        long SkillsUsed { get; }

        /// <summary>How many gameplay sessions have been played.</summary>
        long SessionsPlayed { get; }

        /// <summary>Seconds spent in the room with the clock running.</summary>
        double PlaytimeSeconds { get; }

        /// <summary>Total distance walked in world units, sprint included.</summary>
        double DistanceTraveled { get; }

        /// <summary>The part of <see cref="DistanceTraveled"/> covered while sprinting.</summary>
        double DistanceSprinted { get; }

        /// <summary>The most cards ever correctly filed at one moment - the best progress reached.</summary>
        int PeakCorrectlyPlaced { get; }

        // --- Collection snapshot, saved ---

        /// <summary>Every original and its one allowed duplicate - the collection denominator.</summary>
        int TotalCards { get; }

        /// <summary>Correct album originals plus valid duplicates stored in duplicate containers.</summary>
        int CorrectlyPlacedCards { get; }

        /// <summary>How many originals/duplicates remain to be put in their correct destination.</summary>
        int CardsRemainingToPlace { get; }
    }
}
