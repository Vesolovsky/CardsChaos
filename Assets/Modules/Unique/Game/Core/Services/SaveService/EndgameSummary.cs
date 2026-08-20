using System;

namespace Vesolovsky.Game.Services.Save
{
    /// <summary>
    /// The game as it stood the moment the last card went in - the closing tally, frozen.
    ///
    /// Everything in here is also somewhere else in the save, and that is exactly the point. The
    /// live counters keep running: the player can go back into the room afterwards, walk about,
    /// pick cards up, and the playtime clock never stops. None of that is allowed to change the
    /// numbers on the ending, because those numbers are what the ending *was*. So they are copied
    /// aside once, at the moment they mean something, and read back from here forever after.
    ///
    /// The completion date is the one figure that exists nowhere else: nothing in the save has
    /// ever recorded when the game was finished, and it cannot be worked out afterwards.
    ///
    /// Null until the final card is filed, and dropped again on a new game - a fresh save has no
    /// ending behind it.
    /// </summary>
    public sealed class EndgameSummary
    {
        /// <summary>The whole tally as it was, copied so the live one may carry on moving.</summary>
        public PlayerStatsData Stats { get; set; }

        /// <summary>When the game was finished, in the player's own local time. Written once.</summary>
        public DateTime CompletedAt { get; set; }

        /// <summary>Isolated copy for the off-thread save write, like every other block in the save.</summary>
        public EndgameSummary Clone()
        {
            return new EndgameSummary
            {
                Stats = Stats?.Clone(),
                CompletedAt = CompletedAt,
            };
        }
    }
}
