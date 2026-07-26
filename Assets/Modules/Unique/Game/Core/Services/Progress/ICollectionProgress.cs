using System;

namespace Vesolovsky.Game.Services.Progress
{
    /// <summary>
    /// The single source of truth for what the player has finished in the album, expressed in the
    /// terms the rest of the game rewards against: pages and sets.
    ///
    /// "Completed" here is permanent. A page counts the moment it is first fully and correctly
    /// filed and keeps counting for good, even if the player later lifts a card back out. That is
    /// what stops a page from paying its skill point twice, and it is what lets a one-time
    /// upgrade's unlock condition stay satisfied once it has been met.
    /// </summary>
    public interface ICollectionProgress
    {
        /// <summary>
        /// Raised the first time a page is completed, with the set id and the zero-based page
        /// index. Fires once per page, ever.
        /// </summary>
        event Action<string, int> PageCompleted;

        /// <summary>
        /// Raised the first time every page of a set has been completed, with the set id. Fires
        /// once per set, ever.
        /// </summary>
        event Action<string> SetCompleted;

        /// <summary>Raised after any change to the tallies, for anything that just wants to refresh.</summary>
        event Action Changed;

        /// <summary>How many distinct pages have ever been completed, across every set.</summary>
        int CompletedPageCount { get; }

        /// <summary>How many sets have been completed in full.</summary>
        int CompletedSetCount { get; }

        /// <summary>Whether every page of the given set has been completed.</summary>
        bool IsSetCompleted(string setId);
    }
}
