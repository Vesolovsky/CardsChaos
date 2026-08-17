namespace CardsChaos.Cards
{
    /// <summary>
    /// How many cards of a set are authored into the room twice - the one rule the placer fills
    /// against and the collection total is measured with, so the two can never disagree.
    ///
    /// A duplicate is roughly <see cref="Share"/> of the set, rounded to the nearest
    /// <see cref="Step"/> so a set's duplicates stack in tidy fives, with a tie rounding down. A
    /// set small enough for the rounding to reach zero simply has no duplicates - not every set
    /// needs them, and one box has to hold the lot.
    ///
    /// The share is tuned rather than round: at 1500 cards (8 sets of 20, 37 of 30, 2 of 40, 3 of
    /// 50) anything in 35%..37.5% lands on exactly 500 duplicates - 5, 10, 15 and 20 per set - and
    /// so on a round 2000 cards to collect. 36% sits in the middle of that band, as far from either
    /// rounding edge as the numbers allow, so a set gained or lost does not swing the total.
    /// </summary>
    public static class CardDuplicates
    {
        /// <summary>The share of a set that gets a second copy, before rounding.</summary>
        public const float Share = 0.36f;

        /// <summary>Duplicate counts are always a multiple of this.</summary>
        public const int Step = 5;

        // Share as a whole-number fraction. The rounding is done in integers because a float share
        // of a set size is not exact - 0.36f * 50 is not quite 18 - and a tie landing on the wrong
        // side of a step would quietly move a whole size class of sets by five cards.
        private const int ShareNumerator = 9;
        private const int ShareDenominator = 25;

        /// <summary>
        /// How many of this set's cards get a second copy. Zero for a set outside the collection:
        /// the endgame card is not something the player sorts.
        /// </summary>
        public static int QuotaFor(CardSetDefinition set)
        {
            return set == null || !set.CountsTowardCollection ? 0 : QuotaFor(set.CardCount);
        }

        /// <summary>The same rule for a bare card count, for tools that only have the number.</summary>
        public static int QuotaFor(int cardCount)
        {
            if (cardCount <= 0)
                return 0;

            // Nearest whole step with ties rounding down: floor((2a + b - 1) / 2b), where a is the
            // wanted count and b the step, both scaled by the share's denominator.
            int wanted = cardCount * ShareNumerator;
            int step = Step * ShareDenominator;
            int steps = (2 * wanted + step - 1) / (2 * step);

            int quota = steps * Step;
            return quota > cardCount ? cardCount : quota;
        }

        /// <summary>Every duplicate in the game - what one box has to be able to hold.</summary>
        public static int TotalQuota(ICardCatalog catalog)
        {
            if (catalog == null)
                return 0;

            int total = 0;
            foreach (CardSetDefinition set in catalog.Sets)
                total += QuotaFor(set);

            return total;
        }
    }
}
