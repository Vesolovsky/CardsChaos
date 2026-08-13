using CardsChaos.Cards.Album;

namespace CardsChaos.Cards
{
    /// <summary>
    /// What the room needs to know about duplicates, without knowing anything about upgrades.
    ///
    /// Two different questions live here, and they are not the same one. <see cref="HasDuplicate"/>
    /// asks whether a card was authored twice at all - a fact about the card, and what a duplicate
    /// box will and will not take, because putting the only copy of a card in a box would leave its
    /// album slot unfillable. <see cref="IsSpare"/> asks whether one particular card in hand is the
    /// copy that is not needed for the album: its twin is already filed, or the player is holding
    /// both copies and this is the second of them. That is the one drawn grey, and the one a throw
    /// files for the player.
    ///
    /// Both rewards written against this are off until their upgrade is owned.
    /// </summary>
    public interface IDuplicateCards
    {
        /// <summary>
        /// Whether this card exists in the room more than once - counting the copy the album has
        /// already swallowed. False for the great majority of cards, which are authored once.
        /// </summary>
        bool HasDuplicate(CardRef card);

        /// <summary>
        /// Whether this particular card in hand is the copy the album does not need: its twin is
        /// filed already, or both copies are in hand and this is the one marked spare. False for a
        /// card that is not in the hand at all.
        /// </summary>
        bool IsSpare(Card card);

        /// <summary>
        /// Whether a freely thrown spare should fly itself into the duplicate box instead of onto
        /// the floor. False until the reward that grants it has been claimed.
        /// </summary>
        bool AutoStoresThrownDuplicates { get; }

        /// <summary>
        /// Sends a card in hand to the duplicate box if the reward is owned, the card is a spare
        /// and the box still has room. Returns false when any of that does not hold, and then the
        /// card is left in hand for the ordinary throw to deal with.
        /// </summary>
        bool TryAutoStore(CardHand hand, Card card);
    }
}
