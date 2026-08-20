using System;
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
    ///
    /// It doubles as the room's card list: <see cref="ForEach"/> walks every card there is, and
    /// <see cref="Changed"/> says when that list has moved. Anything that has to keep a per-card
    /// judgement up to date - which cards on the floor are spare, say - hangs off those rather than
    /// scanning the scene for itself.
    /// </summary>
    public static class CardRegistry
    {
        /// <summary>
        /// Raised whenever a card joins or leaves the room. Deliberately carries nothing: the one
        /// listener re-runs its whole pass, and a payload would only invite a partial update that
        /// has to be kept in step with the full one.
        /// </summary>
        public static event Action Changed;

        private static readonly Dictionary<CardRef, List<Card>> ByRef =
            new Dictionary<CardRef, List<Card>>();

        public static void Add(Card card)
        {
            CardRef key = CardRef.From(card != null ? card.Identity : null);
            if (!key.IsValid)
                return;

            if (!ByRef.TryGetValue(key, out List<Card> copies))
                ByRef[key] = copies = new List<Card>();

            if (copies.Contains(card))
                return;

            copies.Add(card);
            Changed?.Invoke();
        }

        public static void Remove(Card card)
        {
            CardRef key = CardRef.From(card != null ? card.Identity : null);
            if (!key.IsValid || !ByRef.TryGetValue(key, out List<Card> copies))
                return;

            if (!copies.Remove(card))
                return;

            if (copies.Count == 0)
                ByRef.Remove(key);

            Changed?.Invoke();
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

        /// <summary>
        /// Hands every living card in the room to <paramref name="visit"/>, grouped by which card
        /// it is so a caller deciding something per card face works it out once and applies it to
        /// each copy. Cards awaiting destruction are skipped, the same way
        /// <see cref="CountOf"/> skips them.
        ///
        /// Written as a callback rather than an IEnumerable so nothing has to allocate an
        /// enumerator - this is walked over the room's whole card list, which is in the thousands.
        /// </summary>
        public static void ForEach(Action<CardRef, Card> visit)
        {
            if (visit == null)
                return;

            foreach (KeyValuePair<CardRef, List<Card>> entry in ByRef)
            {
                List<Card> copies = entry.Value;
                for (int i = 0; i < copies.Count; i++)
                {
                    Card card = copies[i];
                    if (card != null)
                        visit(entry.Key, card);
                }
            }
        }

        /// <summary>
        /// Whether any card in the room satisfies <paramref name="match"/>, stopping at the first
        /// that does. The early exit is the whole point: this answers questions asked several times
        /// a second - is there anything near enough to levitate - where walking the rest of the
        /// room after the answer is known would be the bulk of the cost.
        /// </summary>
        public static bool Any(Func<Card, bool> match)
        {
            if (match == null)
                return false;

            foreach (KeyValuePair<CardRef, List<Card>> entry in ByRef)
            {
                List<Card> copies = entry.Value;
                for (int i = 0; i < copies.Count; i++)
                {
                    Card card = copies[i];
                    if (card != null && match(card))
                        return true;
                }
            }

            return false;
        }
    }
}
