using System;
using System.Collections.Generic;
using CardsChaos.Cards;
using CardsChaos.Cards.Album;
using Vesolovsky.Game.Services.Skills;
using Vesolovsky.Game.Services.Upgrades;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Duplicates
{
    /// <summary>
    /// The one place that decides which card in hand is a spare, and turns the two duplicate
    /// rewards on.
    ///
    /// The rule is per physical card, not per card face. Within the hand, each card the album still
    /// wants keeps one copy back; everything beyond that is spare. So a lone copy of a card already
    /// filed is spare, both-in-hand leaves exactly one spare, and throwing the kept one promotes the
    /// other back to ordinary - which is what the player sees when the grey moves.
    ///
    /// The marking is sticky: a card that was kept stays kept for as long as it is in hand, so
    /// turning the pile over with the wheel does not walk the grey from one copy to the other.
    ///
    /// The grey wash reads live off the upgrade service, so claiming Déjà vu's task takes effect at
    /// once; filing a thrown spare away reads the Muscle Memory skill instead, and so is on only
    /// while that skill is running. The shading is pushed rather than polled: only the handful of
    /// cards in hand can ever be spare, and the three things that change the answer - the album
    /// gaining or losing a card, the hand changing, an upgrade being claimed - each re-run the same
    /// short pass.
    /// </summary>
    public class DuplicateCardService : IDuplicateCards, IInitializable, IDisposable
    {
        private readonly ICardAlbum _album;
        private readonly CardHand _hand;
        private readonly UpgradeCatalog _catalog;
        private readonly IUpgradeService _upgrades;
        private readonly ISkillService _skills;

        // The spares in hand, newest pass first. Kept as state because the marking is sticky and
        // because a card that stops being spare has to be told to drop the grey.
        private readonly List<Card> _spare = new List<Card>();
        private readonly List<Card> _nextSpare = new List<Card>();

        // Scratch for one pass, reused so a hand change does not allocate.
        private readonly Dictionary<CardRef, int> _keepQuota = new Dictionary<CardRef, int>();
        private readonly List<Card> _ordered = new List<Card>();

        [Inject]
        public DuplicateCardService(
            ICardAlbum album,
            CardHand hand,
            UpgradeCatalog catalog,
            IUpgradeService upgrades,
            ISkillService skills)
        {
            _album = album;
            _hand = hand;
            _catalog = catalog;
            _upgrades = upgrades;
            _skills = skills;
        }

        // Asked at the moment of the throw rather than tracked, so the window closing mid-throw is
        // simply a throw that lands on the floor - there is no state here to fall out of step.
        public bool AutoStoresThrownDuplicates =>
            _skills != null && _skills.IsActive(SkillId.MuscleMemory);

        /// <summary>Whether spares in hand are drawn grey - Déjà vu, earned by boxing duplicates.</summary>
        private bool ShadesSparesInHand => IsOwned(PermanentUpgradeKind.DuplicateSight);

        public void Initialize()
        {
            if (_album != null)
                _album.PageChanged += OnPageChanged;

            if (_hand != null)
                _hand.Changed += Refresh;

            if (_upgrades != null)
                _upgrades.Changed += OnUpgradeChanged;

            Refresh();
        }

        public void Dispose()
        {
            if (_album != null)
                _album.PageChanged -= OnPageChanged;

            if (_hand != null)
                _hand.Changed -= Refresh;

            if (_upgrades != null)
                _upgrades.Changed -= OnUpgradeChanged;
        }

        public bool HasDuplicate(CardRef card)
        {
            if (!card.IsValid)
                return false;

            // The copy the album swallowed is gone as an object but still counts: a card filed away
            // with its twin in the room is exactly the case the box exists for.
            int copies = CardRegistry.CountOf(card);
            if (_album != null && _album.Contains(card))
                copies++;

            return copies >= 2;
        }

        public bool IsSpare(Card card)
        {
            return card != null && _spare.Contains(card);
        }

        public bool TryAutoStore(CardHand hand, Card card)
        {
            if (hand == null || card == null || !card.IsHeld || !AutoStoresThrownDuplicates)
                return false;

            if (!IsAutoStorable(card))
                return false;

            // Every stack full is not a failure worth announcing: the throw simply happens as it
            // always did, and the card lands on the floor.
            if (!CardStackContainer.TryFindAutoPlacement(
                    card,
                    out CardStackContainer container,
                    out CardStackContainer.SlotTarget target))
            {
                return false;
            }

            // The player did not aim this one, so it gets the long way in: a card that files itself
            // from across the room should look like it meant to.
            return container.TryStore(
                hand, card, target, CardStackContainer.PlacementFlight.Flourish);
        }

        /// <summary>
        /// Which thrown cards Muscle Memory files for the player. With the grey wash owned, exactly
        /// the card shown as spare - what you see is what happens, and throwing the kept copy is
        /// still an ordinary throw. Without it there is nothing on screen telling the two copies
        /// apart, so throwing either of them files one, and the second throw behaves normally
        /// because by then only the album's copy is left.
        /// </summary>
        private bool IsAutoStorable(Card card)
        {
            return ShadesSparesInHand ? IsSpare(card) : SpareCountInHand(card) > 0;
        }

        private int SpareCountInHand(Card card)
        {
            CardRef key = CardRef.From(card.Identity);
            if (!key.IsValid || _hand == null)
                return 0;

            int copies = 0;
            foreach (Card held in _hand.Cards)
            {
                if (held != null && CardRef.From(held.Identity) == key)
                    copies++;
            }

            return copies - KeepCount(key);
        }

        /// <summary>How many copies of a card the hand must keep back for the album: one, or none
        /// once the album already holds it.</summary>
        private int KeepCount(CardRef card)
        {
            return _album != null && _album.Contains(card) ? 0 : 1;
        }

        private void OnPageChanged(string setId) => Refresh();

        private void OnUpgradeChanged(UpgradeDefinition definition) => Refresh();

        private void Refresh()
        {
            _nextSpare.Clear();

            if (_hand != null)
                MarkSpares();

            // Cleared first so a card that has just been filed, thrown or promoted back is not left
            // grey. Card ignores a value it already has, so nothing is re-applied.
            foreach (Card card in _spare)
            {
                if (card != null && !_nextSpare.Contains(card))
                    card.SetShaded(false);
            }

            bool shade = ShadesSparesInHand;
            foreach (Card card in _nextSpare)
                card.SetShaded(shade);

            _spare.Clear();
            _spare.AddRange(_nextSpare);
        }

        /// <summary>
        /// Walks the hand and puts every copy beyond what the album still wants into
        /// <see cref="_nextSpare"/>. Cards already marked spare are considered last, so the copy
        /// that was kept stays kept and the grey does not jump between two identical cards.
        /// </summary>
        private void MarkSpares()
        {
            _keepQuota.Clear();
            _ordered.Clear();

            // Two passes rather than one: everything the last pass kept goes to the front, so the
            // album's share is spent on those cards again and the grey stays on the same copy.
            AddToOrder(alreadySpare: false);
            AddToOrder(alreadySpare: true);

            foreach (Card card in _ordered)
            {
                CardRef key = CardRef.From(card.Identity);

                if (!_keepQuota.TryGetValue(key, out int keep))
                    keep = KeepCount(key);

                if (keep > 0)
                    _keepQuota[key] = keep - 1;
                else
                    _nextSpare.Add(card);
            }
        }

        private void AddToOrder(bool alreadySpare)
        {
            foreach (Card card in _hand.Cards)
            {
                if (card == null || !CardRef.From(card.Identity).IsValid)
                    continue;

                if (_spare.Contains(card) == alreadySpare)
                    _ordered.Add(card);
            }
        }

        /// <summary>
        /// Whether a permanent upgrade is owned. Déjà vu is earned rather than bought these days,
        /// but the upgrade service folds that into the same level, so this needs no second case.
        /// </summary>
        private bool IsOwned(PermanentUpgradeKind kind)
        {
            if (_catalog == null || _upgrades == null)
                return false;

            PermanentUpgradeDefinition definition = _catalog.FindPermanent(kind);
            return definition != null && _upgrades.GetLevel(definition) >= 1;
        }
    }
}
