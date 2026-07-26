using System;
using System.Collections.Generic;
using UnityEngine;
using Vesolovsky.Core.Services.Save;
using Vesolovsky.Core.Services.Wallet;
using Vesolovsky.Game.Services.Progress;
using Vesolovsky.Game.Services.Save;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Upgrades
{
    /// <summary>
    /// The save-backed <see cref="IUpgradeService"/>.
    ///
    /// It works directly on the save's own dictionaries, the way <c>LocalWallet</c> and
    /// <c>LocalCardAlbum</c> do, so there is no second copy of the state to keep in step - reading
    /// a level reads the save, buying one writes it. Skill points are spent through the wallet,
    /// which is the one place the balance lives.
    /// </summary>
    public class UpgradeService : IUpgradeService
    {
        public event Action<UpgradeDefinition> Changed;

        private readonly IWalletService _wallet;
        private readonly ISaveService<GameSave> _saveService;
        private readonly ISaveCoordinator _saveCoordinator;
        private readonly ICollectionProgress _progress;

        [Inject]
        public UpgradeService(
            IWalletService wallet,
            ISaveService<GameSave> saveService,
            ISaveCoordinator saveCoordinator,
            ICollectionProgress progress)
        {
            _wallet = wallet;
            _saveService = saveService;
            _saveCoordinator = saveCoordinator;
            _progress = progress;
        }

        private GameSave Save => _saveService.CurrentSave;

        public int GetLevel(LeveledUpgradeDefinition definition)
        {
            if (definition == null)
                return 0;

            Dictionary<string, int> levels = Save?.UpgradeLevels;
            return levels != null && levels.TryGetValue(definition.Id, out int level) ? level : 0;
        }

        public bool CanLevelUp(LeveledUpgradeDefinition definition)
        {
            if (definition == null)
                return false;

            int level = GetLevel(definition);
            if (level >= definition.MaxLevel)
                return false;

            return Balance >= definition.GetCost(level + 1);
        }

        public bool TryLevelUp(LeveledUpgradeDefinition definition)
        {
            if (!CanLevelUp(definition))
                return false;

            int nextLevel = GetLevel(definition) + 1;
            int cost = definition.GetCost(nextLevel);

            // Checked inside CanLevelUp already, but spending is the irreversible half, so it is
            // gated once more here rather than trusting the balance not to have moved.
            if (!TrySpend(cost))
                return false;

            Levels[definition.Id] = nextLevel;
            _saveCoordinator.MarkDirty();
            Changed?.Invoke(definition);
            return true;
        }

        public bool IsUnlocked(OneTimeUpgradeDefinition definition)
        {
            if (definition == null)
                return false;

            List<string> unlocked = Save?.UnlockedOneTimeUpgrades;
            return unlocked != null && unlocked.Contains(definition.Id);
        }

        public bool CanClaim(OneTimeUpgradeDefinition definition)
        {
            if (definition == null || IsUnlocked(definition))
                return false;

            return definition.Objective != null && definition.Objective.IsSatisfied(_progress);
        }

        public bool TryClaim(OneTimeUpgradeDefinition definition)
        {
            if (!CanClaim(definition))
                return false;

            Unlocked.Add(definition.Id);
            _saveCoordinator.MarkDirty();
            Changed?.Invoke(definition);
            return true;
        }

        public void Refresh() => Changed?.Invoke(null);

        // Created lazily the same way the album builds its pages: the container assembles this
        // before the async save load, so the save's collections cannot be touched until something
        // actually reads or writes state, which only happens once the player is in the room.
        private Dictionary<string, int> Levels => Save.UpgradeLevels ??= new Dictionary<string, int>();

        private List<string> Unlocked => Save.UnlockedOneTimeUpgrades ??= new List<string>();

        private long Balance => _wallet.GetRealCurrencyBalance(CurrencyType.SkillPoints);

        private bool TrySpend(int cost)
        {
            if (cost < 0)
            {
                Debug.LogError($"[{nameof(UpgradeService)}] Refused a negative cost of {cost}.");
                return false;
            }

            if (Balance < cost)
                return false;

            // The wallet clamps at zero and would silently swallow an overspend; the balance check
            // above is what keeps a purchase honest, so this only ever subtracts what is there.
            _wallet.AddRealCurrency(CurrencyType.SkillPoints, -cost);
            return true;
        }
    }
}
