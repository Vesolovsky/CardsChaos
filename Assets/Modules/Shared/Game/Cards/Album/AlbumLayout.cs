using UnityEngine;

namespace CardsChaos.Cards.Album
{
    /// <summary>
    /// How the album carves a set's cards into pages.
    ///
    /// The page size lives here rather than only on the view because the progression system counts
    /// pages too - a skill point is earned per completed page - and both ends have to agree on
    /// where one page ends and the next begins. <see cref="Vesolovsky.Game.Views.Album.AlbumPageStrip"/>
    /// keeps its own serialized slots-per-page for the grid it lays out; that value must match
    /// <see cref="CardsPerPage"/>.
    /// </summary>
    public static class AlbumLayout
    {
        /// <summary>Cards to a page. Two rows of five in the album grid.</summary>
        public const int CardsPerPage = 10;

        /// <summary>Which page a zero-based slot falls on.</summary>
        public static int PageOfSlot(int slotIndex) => slotIndex / CardsPerPage;

        /// <summary>The zero-based slot a page begins at.</summary>
        public static int FirstSlotOfPage(int pageIndex) => pageIndex * CardsPerPage;

        /// <summary>How many pages a set of the given card count spans - at least one.</summary>
        public static int PageCount(int cardCount) =>
            Mathf.Max(1, Mathf.CeilToInt(cardCount / (float)CardsPerPage));
    }
}
