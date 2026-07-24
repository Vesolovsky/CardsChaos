using System;

namespace CardsChaos.Cards.Album
{
    /// <summary>
    /// Names one card the way the album and the save file both refer to it - by set and number,
    /// never by prefab.
    ///
    /// A card in the album is a fact that has to survive being written to disk and read back, so
    /// it cannot be a reference to a scene object or even to an asset. The set id and the number
    /// are already the card's real name; the prefab is looked up from them when something needs
    /// to be drawn.
    /// </summary>
    public readonly struct CardRef : IEquatable<CardRef>
    {
        /// <summary>An empty slot.</summary>
        public static readonly CardRef None = default;

        public string SetId { get; }

        /// <summary>1-based, matching the number printed on the card.</summary>
        public int Number { get; }

        public CardRef(string setId, int number)
        {
            SetId = setId;
            Number = number;
        }

        public bool IsValid => !string.IsNullOrEmpty(SetId) && Number > 0;

        /// <summary>
        /// Whether this card is the one that belongs in a given slot of a given set's page.
        ///
        /// Slots are laid out in number order from zero, so slot 0 wants card 1. Both halves have
        /// to agree: the right number from the wrong set is still a misfiled card.
        /// </summary>
        public bool BelongsAt(string pageSetId, int slotIndex)
        {
            return IsValid && Number == slotIndex + 1 && SetId == pageSetId;
        }

        public static CardRef From(CardIdentity identity)
        {
            return identity == null ? None : new CardRef(identity.SetId, identity.Number);
        }

        public bool Equals(CardRef other) => Number == other.Number && SetId == other.SetId;

        public override bool Equals(object obj) => obj is CardRef other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(SetId, Number);

        public override string ToString() => IsValid ? $"{SetId}#{Number}" : "<empty>";

        public static bool operator ==(CardRef left, CardRef right) => left.Equals(right);

        public static bool operator !=(CardRef left, CardRef right) => !left.Equals(right);
    }
}
