using System;

namespace CardsChaos.Cards.Album
{
    /// <summary>
    /// What the album is holding, independent of how it is drawn.
    ///
    /// A page belongs to a set and has one slot per card in that set, but a slot will take any
    /// card at all - putting the wrong one down is a move the player is allowed to make, and
    /// taking it back out is how they fix it. So a placement is a pair: the page it sits on, and
    /// the card sitting there, which are frequently not from the same set.
    /// </summary>
    public interface ICardAlbum
    {
        /// <summary>
        /// Raised with the id of the set whose page just changed. Both ends of a move report,
        /// so a card taken off one page and put on another raises twice.
        /// </summary>
        event Action<string> PageChanged;

        /// <summary>The card in a slot, or <see cref="CardRef.None"/> when it is empty.</summary>
        CardRef GetPlacement(string pageSetId, int slotIndex);

        /// <summary>
        /// Puts a card down. The slot must be empty - the album never silently displaces a card,
        /// because the displaced one would have nowhere to go.
        /// </summary>
        void Place(string pageSetId, int slotIndex, CardRef card);

        /// <summary>
        /// Lifts whatever is in a slot back out, returning it so the caller can hand it to the
        /// player. <see cref="CardRef.None"/> when the slot was already empty.
        /// </summary>
        CardRef Take(string pageSetId, int slotIndex);

        /// <summary>
        /// How many of a set's own cards are sitting in their own slots - the X of the "X / Y" on
        /// the set button. A card of this set parked on someone else's page does not count, and
        /// neither does a stranger filling a slot here.
        /// </summary>
        int CountCorrect(string pageSetId);
    }
}
