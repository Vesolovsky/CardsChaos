using System;
using System.Collections.Generic;
using CardsChaos.Cards;
using UniRx;
using UnityEngine;
using Vesolovsky.Core.Services;
using Vesolovsky.Core.Services.Wallet;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.Services.Progress;
using Vesolovsky.Game.Services.Skills;
using Vesolovsky.Game.Services.Upgrades;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Views
{
    /// <summary>
    /// The upgrades screen's brain. It reads the upgrade system, spends skill points through it,
    /// and turns a one-time upgrade's raw objective into the words and the bar the task row shows.
    ///
    /// While it is open it takes the room the way the album does - so the camera and card table go
    /// quiet - and raises the skill gate on top, so even the skills that ignore the room lock fall
    /// silent too.
    /// </summary>
    public class UpgradesViewModel : ViewModel, IUpgradesViewModel
    {
        private readonly IUpgradeService _upgrades;
        private readonly ICollectionProgress _progress;
        private readonly IWalletService _wallet;
        private readonly IWorldInteractionLock _worldLock;
        private readonly ISkillGate _skillGate;
        private readonly UpgradeCatalog _catalog;

        private readonly ReactiveProperty<bool> _isOpen = new ReactiveProperty<bool>(false);
        private readonly ReactiveProperty<long> _skillPoints = new ReactiveProperty<long>(0);

        private IDisposable _worldHandle;

        public IReadOnlyReactiveProperty<bool> IsOpen => _isOpen;

        public IReadOnlyReactiveProperty<long> SkillPoints => _skillPoints;

        public IReadOnlyList<PermanentUpgradeDefinition> Permanents => _catalog.Permanents;

        public IReadOnlyList<SkillDefinition> Skills => _catalog.Skills;

        public IReadOnlyList<OneTimeUpgradeDefinition> OneTimes => _catalog.OneTimes;

        [Inject]
        public UpgradesViewModel(
            IUpgradeService upgrades,
            ICollectionProgress progress,
            IWalletService wallet,
            IWorldInteractionLock worldLock,
            ISkillGate skillGate,
            UpgradeCatalog catalog)
        {
            _upgrades = upgrades;
            _progress = progress;
            _wallet = wallet;
            _worldLock = worldLock;
            _skillGate = skillGate;
            _catalog = catalog;

            // Tracked live so a purchase, a page reward or a cheat is reflected in the header at
            // once. The balance itself is read fresh on each open (see Open).
            _wallet.RealCurrencyChanged += OnCurrencyChanged;
        }

        public void Open()
        {
            if (_isOpen.Value)
                return;

            // Do not stack on top of the album or a card close-up - they already hold the room.
            if (_worldLock.IsLocked)
                return;

            _worldHandle = _worldLock.Acquire(this);
            _skillGate.Blocked = true;

            RefreshSkillPoints();
            _isOpen.Value = true;
        }

        public void Close()
        {
            if (!_isOpen.Value)
                return;

            _isOpen.Value = false;
            _skillGate.Blocked = false;
            ReleaseWorld();
        }

        public int GetLevel(LeveledUpgradeDefinition definition) => _upgrades.GetLevel(definition);

        public int GetMaxLevel(LeveledUpgradeDefinition definition) =>
            definition != null ? definition.MaxLevel : 0;

        public int GetNextCost(LeveledUpgradeDefinition definition)
        {
            if (definition == null)
                return 0;

            int level = _upgrades.GetLevel(definition);
            return level >= definition.MaxLevel ? 0 : definition.GetCost(level + 1);
        }

        public bool IsMaxed(LeveledUpgradeDefinition definition) =>
            definition != null && _upgrades.GetLevel(definition) >= definition.MaxLevel;

        public bool CanAfford(LeveledUpgradeDefinition definition) =>
            definition != null && !IsMaxed(definition) && _skillPoints.Value >= GetNextCost(definition);

        public bool TryLevelUp(LeveledUpgradeDefinition definition) => _upgrades.TryLevelUp(definition);

        public bool IsUnlocked(OneTimeUpgradeDefinition definition) => _upgrades.IsUnlocked(definition);

        public bool TryClaim(OneTimeUpgradeDefinition definition) => _upgrades.TryClaim(definition);

        public UpgradeTaskProgress GetTaskProgress(OneTimeUpgradeDefinition definition)
        {
            if (definition == null || definition.Objective == null)
                return new UpgradeTaskProgress(string.Empty, string.Empty, 0f, false, false);

            CollectionObjective objective = definition.Objective;
            int required = objective.Required;
            int completed = objective.GetCompleted(_progress);
            float ratio = required > 0 ? Mathf.Clamp01((float)completed / required) : 0f;

            return new UpgradeTaskProgress(
                Describe(objective),
                Remaining(objective, required, completed),
                ratio,
                objective.IsSatisfied(_progress),
                _upgrades.IsUnlocked(definition));
        }

        public override void Dispose()
        {
            _wallet.RealCurrencyChanged -= OnCurrencyChanged;

            // A view torn down while open must not leave the room locked or the skills gated.
            _skillGate.Blocked = false;
            ReleaseWorld();

            base.Dispose();
        }

        private void OnCurrencyChanged(CurrencyType type, long value)
        {
            if (type == CurrencyType.SkillPoints)
                _skillPoints.Value = value;
        }

        private void RefreshSkillPoints() =>
            _skillPoints.Value = _wallet.GetRealCurrencyBalance(CurrencyType.SkillPoints);

        private void ReleaseWorld()
        {
            _worldHandle?.Dispose();
            _worldHandle = null;
        }

        private static string Describe(CollectionObjective objective)
        {
            switch (objective.Kind)
            {
                case CollectionObjective.ObjectiveKind.CompleteSpecificSets:
                    string names = JoinSetNames(objective.Sets);
                    string unit = CountNonNull(objective.Sets) == 1 ? "set" : "sets";
                    return $"Fully complete {names} {unit} to unlock this ability";

                case CollectionObjective.ObjectiveKind.CompletePages:
                    return $"Fully complete {objective.Count} pages to unlock this ability";

                case CollectionObjective.ObjectiveKind.CompleteAnySets:
                    return $"Fully complete {objective.Count} sets to unlock this ability";

                default:
                    return string.Empty;
            }
        }

        private static string Remaining(CollectionObjective objective, int required, int completed)
        {
            int remaining = Mathf.Max(0, required - completed);
            string unit = objective.CountsPages ? "page" : "set";

            if (remaining != 1)
                unit += "s";

            return $"{remaining} {unit} remaining";
        }

        private static string JoinSetNames(IReadOnlyList<CardSetDefinition> sets)
        {
            if (sets == null)
                return string.Empty;

            var names = new List<string>();
            foreach (CardSetDefinition set in sets)
            {
                if (set != null)
                    names.Add(set.SetName);
            }

            return string.Join(", ", names);
        }

        private static int CountNonNull(IReadOnlyList<CardSetDefinition> sets)
        {
            if (sets == null)
                return 0;

            int count = 0;
            foreach (CardSetDefinition set in sets)
            {
                if (set != null)
                    count++;
            }

            return count;
        }
    }
}
