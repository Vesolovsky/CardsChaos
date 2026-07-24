namespace Vesolovsky.Game.Services.Album
{
    /// <summary>
    /// One card sitting in one slot, as it is written to the save file.
    ///
    /// Flat rather than nested per page, because it is the shape that survives a schema change
    /// best: a set that is renamed, split or dropped leaves rows that are simply skipped on load
    /// instead of a whole branch of the file that no longer parses.
    ///
    /// The page and the card carry separate set ids on purpose. They agree for a correctly filed
    /// card and disagree for a misfiled one, and a misfiled card is a state the player is allowed
    /// to leave the album in.
    /// </summary>
    public class AlbumPlacement
    {
        /// <summary>The set whose page the card is lying on.</summary>
        public string PageSetId { get; set; }

        /// <summary>0-based position on that page.</summary>
        public int Slot { get; set; }

        public string CardSetId { get; set; }

        /// <summary>1-based, matching the number printed on the card.</summary>
        public int CardNumber { get; set; }
    }
}
