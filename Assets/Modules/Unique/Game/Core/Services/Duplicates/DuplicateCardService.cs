using System;
using System.Collections.Generic;
using CardsChaos.Cards;
using CardsChaos.Cards.Album;
using Vesolovsky.Game.Services.Upgrades;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Duplicates
{
    /// <summary>
    /// The one place that answers "is this card a spare" and turns the two duplicate rewards on.
    ///
    /// Both rewards read live off the upgrade service, so claiming or buying one takes effect at
    /// once. The shading is pushed rather than polled: only the handful of cards in hand can ever
    /// be shaded, and the three things that can change the answer - the album gaining or losing a
    /// card, the hand changing, an upgrade being bought - each re-run the same short pass.
    /// </summary>
    public class DuplicateCardService : IDuplicateCards, IInitializable, IDisposable
    {
        private readonly ICardAlbum _album;
        private readonly CardHand _hand;
        private readonly UpgradeCatalog _catalog;
        private readonly IUpgradeService _upgrades;

        // The cards currently drawn grey, so the ones that stop qualifying can be given their
        // colour back without walking every card in the room.
        private readonly List<Card> _shaded = new List<Card>();
        private readonly List<Card> _wanted = new List<Card>();

        [Inject]
        public DuplicateCardService(
            ICardAlbum album,
            CardHand hand,
            UpgradeCatalog catalog,
            IUpgradeService upgrades)
        {
            _album = album;
            _hand = hand;
            _catalog = catalog;
            _upgrades = upgrades;
        }

        public bool AutoStoresThrownDuplicates =>
            IsClaimed(OneTimeUpgradeKind.AutoStoreThrownDuplicates);

        /// <summary>Whether duplicates in hand are drawn grey - the bought half of the pair.</summary>
        private bool ShadesDuplicatesInHand => IsBought(PermanentUpgradeKind.DuplicateSight);

        public void Initialize()
        {
            if (_album != null)
                _album.PageChanged += OnPageChanged;

            if (_hand != null)
                _hand.Changed += RefreshShading;

            if (_upgrades != null)
                _upgrades.Changed += OnUpgradeChanged;

            RefreshShading();
        }

        public void Dispose()
        {
            if (_album != null)
                _album.PageChanged -= OnPageChanged;

            if (_hand != null)
                _hand.Changed -= RefreshShading;

            if (_upgrades != null)
                _upgrades.Changed -= OnUpgradeChanged;
        }

        public bool IsDuplicate(CardRef card)
        {
            return card.IsValid && _album != null && _album.Contains(card);
        }

        public bool IsDuplicate(Card card)
        {
            return card != null && IsDuplicate(CardRef.From(card.Identity));
        }

        public bool TryAutoStore(CardHand hand, Card card)
        {
            if (hand == null || card == null || !card.IsHeld)
                return false;

            if (!AutoStoresThrownDuplicates || !IsDuplicate(card))
                return false;

            // Every box full is not a failure worth announcing: the throw simply happens as it
            // always did, and the card lands on the floor.
            if (!CardStackContainer.TryFindAutoPlacement(
                    card,
                    out CardStackContainer container,
                    out CardStackContainer.SlotTarget target))
            {
                return false;
            }

            return container.TryStore(hand, card, target);
        }

        private void OnPageChanged(string setId) => RefreshShading();

        private void OnUpgradeChanged(UpgradeDefinition definition) => RefreshShading();

        private void RefreshShading()
        {
            _wanted.Clear();

            if (_hand != null && ShadesDuplicatesInHand)
            {
                foreach (Card card in _hand.Cards)
                {
                    if (card != null && IsDuplicate(card))
                        _wanted.Add(card);
                }
            }

            // Cleared first so a card that has just been filed, thrown or sold out of the reward
            // is not left grey. Card ignores a value it already has, so nothing is re-applied.
            foreach (Card card in _shaded)
            {
                if (card != null && !_wanted.Contains(card))
                    card.SetShaded(false);
            }

            foreach (Card card in _wanted)
                card.SetShaded(true);

            _shaded.Clear();
            _shaded.AddRange(_wanted);
        }

        private bool IsBought(PermanentUpgradeKind kind)
        {
            if (_catalog == null || _upgrades == null)
                return false;

            PermanentUpgradeDefinition definition = _catalog.FindPermanent(kind);
            return definition != null && _upgrades.GetLevel(definition) >= 1;
        }

        private bool IsClaimed(OneTimeUpgradeKind kind)
        {
            if (_catalog == null || _upgrades == null)
                return false;

            return _upgrades.IsUnlocked(_catalog.FindOneTime(kind));
        }
    }
}
