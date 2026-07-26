using System;
using CardsChaos.Cards;
using UnityEngine;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Upgrades
{
    /// <summary>
    /// Keeps the hand's slot count in step with the Extra Card Slot upgrade.
    ///
    /// The base capacity stays where it was authored, on the hand: level 0 leaves it untouched,
    /// and each bought level replaces it with that level's value. Reading the base off the hand at
    /// start is what lets the upgrade's own numbers be the plain "6, 7, 8..." totals rather than
    /// deltas that have to know what they are added to.
    /// </summary>
    public class CardSlotUpgradeApplier : IInitializable, IDisposable
    {
        private readonly CardHand _hand;
        private readonly IUpgradeService _upgrades;
        private readonly PermanentUpgradeDefinition _definition;

        private int _baseSlots;

        [Inject]
        public CardSlotUpgradeApplier(CardHand hand, IUpgradeService upgrades, UpgradeCatalog catalog)
        {
            _hand = hand;
            _upgrades = upgrades;
            _definition = catalog.FindPermanent(PermanentUpgradeKind.ExtraCardSlot);
        }

        public void Initialize()
        {
            _baseSlots = _hand.SlotCount;
            _upgrades.Changed += OnChanged;
        }

        public void Dispose()
        {
            _upgrades.Changed -= OnChanged;
        }

        private void OnChanged(UpgradeDefinition changed)
        {
            if (changed == null || changed == _definition)
                Apply();
        }

        private void Apply()
        {
            if (_definition == null)
                return;

            int level = _upgrades.GetLevel(_definition);
            _hand.SlotCount = level <= 0 ? _baseSlots : Mathf.RoundToInt(_definition.GetValue(level));
        }
    }
}
