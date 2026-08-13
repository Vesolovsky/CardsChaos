using System.Collections.Generic;
using CardsChaos.Cards.Album;

namespace CardsChaos.Cards
{
    /// <summary>
    /// Every physical card alive in the room, counted by which card it is.
    ///
    /// It exists to answer one question cheaply: does this card have a duplicate at all? Only some
    /// cards are authored twice (see <see cref="CardDuplicates"/>), and the only place that fact is
    /// written down is the scene itself - so it is read back by counting copies rather than stored
    /// a second time where it could drift. Cards join in Awake and leave in OnDestroy, which covers
    /// the ones the save spawns and the ones filing an album slot destroys.
    /// </summary>
    public static class CardRegistry
    {
        private static readonly Dictionary<CardRef, List<Card>> ByRef =
            new Dictionary<CardRef, List<Card>>();

        public static void Add(Card card)
        {
            CardRef key = CardRef.From(card != null ? card.Identity : null);
            if (!key.IsValid)
                return;

            if (!ByRef.TryGetValue(key, out List<Card> copies))
                ByRef[key] = copies = new List<Card>();

            if (!copies.Contains(card))
                copies.Add(card);
        }

        public static void Remove(Card card)
        {
            CardRef key = CardRef.From(card != null ? card.Identity : null);
            if (!key.IsValid || !ByRef.TryGetValue(key, out List<Card> copies))
                return;

            copies.Remove(card);
            if (copies.Count == 0)
                ByRef.Remove(key);
        }

        /// <summary>How many physical copies of this card are in the room right now.</summary>
        public static int CountOf(CardRef card)
        {
            if (!card.IsValid || !ByRef.TryGetValue(card, out List<Card> copies))
                return 0;

            // Destroying an object runs OnDestroy at the end of the frame, so a card filed into the
            // album is still on this list for the rest of it. Unity's own null check already knows
            // it is gone, and a card in flight to the album must not count as a copy still around.
            int alive = 0;
            for (int i = 0; i < copies.Count; i++)
            {
                if (copies[i] != null)
                    alive++;
            }

            return alive;
        }
    }
}
