using UnityEngine;

namespace Vesolovsky.Game.Services.Stats
{
    /// <summary>
    /// How the player's numbers are written wherever they are shown.
    ///
    /// Shared rather than formatted at each call site, because the same figure now appears in more
    /// than one place - the pause menu while the game is being played, and the closing spread once
    /// it is finished - and two screens quoting the same number in two shapes would read as two
    /// different numbers.
    /// </summary>
    public static class StatsFormat
    {
        /// <summary>
        /// Playtime as "3h 07min".
        ///
        /// Hours count past 24 rather than rolling over - this is a duration, not a time of day,
        /// and a save with thirty hours in it should say thirty. Seconds are dropped: nobody reads
        /// a playtime to the second, and a figure that ticks while being looked at reads as a
        /// stopwatch rather than as a tally. The hours are unpadded because a leading zero on a
        /// duration looks like a clock; the minutes are padded because they are a part of the hour
        /// above them, and "3h 7min" invites being read as three hours and seventy.
        /// </summary>
        public static string Playtime(double seconds)
        {
            int total = Mathf.Max(0, (int)seconds);
            return $"{total / 3600}h {total % 3600 / 60:00}min";
        }
    }
}
