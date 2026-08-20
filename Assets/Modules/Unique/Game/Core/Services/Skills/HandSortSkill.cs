using System;
using System.Collections.Generic;
using CardsChaos.Cards;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Skills
{
    /// <summary>
    /// Tidies the cards in hand.
    ///
    /// Within a set the cards go in number order, lowest first. When the hand holds more than one
    /// set, the biggest run comes to the top and the rest follow in order of size - hold five of
    /// one set, three of another and two of a third, and the five are what is under the cursor
    /// when the sort finishes. That is the run most nearly worth filing, so it is the one put
    /// within reach. Ties between equal-sized runs are broken by set id, only so the result is
    /// stable rather than wandering from one press to the next.
    ///
    /// This is the one skill that also works inside the album, so it never checks the world lock.
    /// </summary>
    public class HandSortSkill : ISkillHandler
    {
        private readonly CardHand _hand;

        [Inject]
        public HandSortSkill(CardHand hand)
        {
            _hand = hand;
        }

        public SkillId Id => SkillId.HandSort;

        public bool CanActivate() => _hand.Cards.Count > 1;

        public bool Activate(SkillDefinition definition, int level)
        {
            IReadOnlyList<Card> cards = _hand.Cards;
            if (cards.Count < 2)
                return false;

            // Gather the hand into runs, one per set, preserving nothing of the old order - the
            // sort below decides everything.
            var runs = new Dictionary<string, List<Card>>();
            foreach (Card card in cards)
            {
                string setId = card.Identity != null ? card.Identity.SetId : string.Empty;

                if (!runs.TryGetValue(setId, out List<Card> run))
                {
                    run = new List<Card>();
                    runs[setId] = run;
                }

                run.Add(card);
            }

            foreach (List<Card> run in runs.Values)
                run.Sort(CompareByNumber);

            // Biggest run first: index 0 is the top of the hand, so descending by size puts the
            // longest run where the player's hand already is.
            var ordered = new List<KeyValuePair<string, List<Card>>>(runs);
            ordered.Sort((a, b) =>
            {
                int bySize = b.Value.Count.CompareTo(a.Value.Count);
                return bySize != 0
                    ? bySize
                    : string.Compare(a.Key, b.Key, StringComparison.Ordinal);
            });

            var order = new List<Card>(cards.Count);
            foreach (KeyValuePair<string, List<Card>> run in ordered)
                order.AddRange(run.Value);

            _hand.Reorder(order);
            return true;
        }

        private static int CompareByNumber(Card a, Card b)
        {
            int na = a.Identity != null ? a.Identity.Number : 0;
            int nb = b.Identity != null ? b.Identity.Number : 0;
            return na.CompareTo(nb);
        }
    }
}
