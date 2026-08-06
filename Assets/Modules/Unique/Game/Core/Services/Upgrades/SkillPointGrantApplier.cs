using System;
using UnityEngine;
using Vesolovsky.Core.Services.Wallet;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Upgrades
{
    /// <summary>
    /// Pays out the "Care Package" reward - a lump of skill points - the moment its task is claimed.
    ///
    /// Unlike the passive rewards (a longer magnet reach, a shorter cooldown) that are simply read
    /// where they matter, a points payout has to happen once and stay happened. It listens for the
    /// claim rather than the load: <see cref="IUpgradeService.Changed"/> carries the exact
    /// definition when a one-time upgrade is claimed, and null on the load-time refresh, so reacting
    /// only to "this definition changed" pays exactly once - at claim - and never again on reload.
    /// </summary>
    public class SkillPointGrantApplier : IInitializable, IDisposable
    {
        private readonly IUpgradeService _upgrades;
        private readonly IWalletService _wallet;
        private readonly OneTimeUpgradeDefinition _definition;

        [Inject]
        public SkillPointGrantApplier(
            IUpgradeService upgrades, IWalletService wallet, UpgradeCatalog catalog)
        {
            _upgrades = upgrades;
            _wallet = wallet;
            _definition = catalog.FindOneTime(OneTimeUpgradeKind.SkillPointsGrant);
        }

        public void Initialize()
        {
            _upgrades.Changed += OnChanged;
        }

        public void Dispose()
        {
            _upgrades.Changed -= OnChanged;
        }

        private void OnChanged(UpgradeDefinition changed)
        {
            // Only the claim of this exact reward pays. The null "assume all" refresh and every other
            // upgrade's change are ignored, so a reload of an already-claimed save never re-pays.
            if (_definition == null || changed != _definition)
                return;

            int points = Mathf.Max(0, Mathf.RoundToInt(_definition.Value));
            if (points > 0)
                _wallet.AddRealCurrency(CurrencyType.SkillPoints, points);
        }
    }
}
