namespace Vesolovsky.Game.Services.Achievements
{
    /// <summary>
    /// Every achievement the game can award, as game code refers to them. The string Steam knows
    /// each one by lives in <see cref="GameAchievements"/>, so a name can be corrected on the
    /// partner site without a rename rippling through the tracker.
    /// </summary>
    public enum AchievementId
    {
        /// <summary>100 cards correctly filed in the album.</summary>
        AlbumHundred,

        /// <summary>100 duplicates put away in the duplicate box.</summary>
        DuplicatesHundred,

        /// <summary>Birds of the Sun and MoonBirds both completed.</summary>
        SetsBirds,

        /// <summary>Every travel set completed.</summary>
        SetsGlobetrotter,

        /// <summary>Every vehicle set completed.</summary>
        SetsVehicles,

        /// <summary>Every early-childhood set completed.</summary>
        SetsChildhood,

        /// <summary>The endgame card filed into its slot - the collection finished.</summary>
        TheCollector,

        /// <summary>Every food set completed.</summary>
        SetsCuisine,

        /// <summary>A house of cards brought down with the Levitate skill.</summary>
        HouseByLevitate,

        /// <summary>A house of cards knocked down by a thrown card.</summary>
        HouseByThrow,

        /// <summary>Every letter read.</summary>
        AllLetters,

        /// <summary>1000 cards correctly filed in the album.</summary>
        AlbumThousand,

        /// <summary>Every duplicate in the game put away.</summary>
        AllDuplicates,

        /// <summary>Every task finished and claimed.</summary>
        AllTasks,

        /// <summary>Every buyable skill at its top level.</summary>
        AllSkillsMaxed,

        /// <summary>The whole collection finished inside the time limit.</summary>
        SwiftCollector,
    }
}
