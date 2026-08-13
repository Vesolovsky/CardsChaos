using CardsChaos.Cards.Album;

namespace CardsChaos.Cards
{
    /// <summary>
    /// What the room needs to know about duplicates, without knowing anything about upgrades.
    ///
    /// A card is a duplicate when a copy of it is already filed in the album: the album holds the
    /// one that counts, so the physical card still in the room is the spare that belongs in a
    /// duplicate box. That single rule is what both duplicate rewards are written against - one
    /// drains the colour out of such a card while it is in hand, the other sends it straight to a
    /// box when it is thrown - and both are off until their upgrade is owned.
    /// </summary>
    public interface IDuplicateCards
    {
        /// <summary>Whether a copy of this card is already filed in the album.</summary>
        bool IsDuplicate(CardRef card);

        /// <summary>Whether a copy of this physical card is already filed in the album.</summary>
        bool IsDuplicate(Card card);

        /// <summary>
        /// Whether a freely thrown duplicate should fly itself into a duplicate box instead of onto
        /// the floor. False until the reward that grants it has been claimed.
        /// </summary>
        bool AutoStoresThrownDuplicates { get; }

        /// <summary>
        /// Sends a card in hand to a duplicate box if the reward is owned, the card is a duplicate
        /// and a box still has room. Returns false when any of that does not hold, and then the
        /// card is left in hand for the ordinary throw to deal with.
        /// </summary>
        bool TryAutoStore(CardHand hand, Card card);
    }
}
