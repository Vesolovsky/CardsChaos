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
        // Rich-text colour the set names in a task line are written in - the album's warm gold.
        private const string SetNameColor = "#C39258";

        private readonly IUpgradeService _upgrades;
        private readonly ICollectionProgress _progress;
        private readonly IWalletService _wallet;
        private readonly IWorldInteractionLock _worldLock;
        private readonly ISkillGate _skillGate;
        private readonly UpgradeCatalog _catalog;

        private readonly ReactiveProperty<bool> _isOpen = new ReactiveProperty<bool>(false);
        private readonly ReactiveProperty<long> _skillPoints = new ReactiveProperty<long>(0);

        // Built once from the catalog: only the buyable skills. A task-unlocked skill (Levitate)
        // has no price and no shop row - it is earned through its task, which shows in the OneTimes
        // list like every other one-time reward.
        private readonly List<SkillDefinition> _buyableSkills = new List<SkillDefinition>();

        private IDisposable _worldHandle;

        public IReadOnlyReactiveProperty<bool> IsOpen => _isOpen;

        public IReadOnlyReactiveProperty<long> SkillPoints => _skillPoints;

        public IReadOnlyList<PermanentUpgradeDefinition> Permanents => _catalog.Permanents;

        public IReadOnlyList<SkillDefinition> Skills => _buyableSkills;

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

            foreach (SkillDefinition skill in _catalog.Skills)
            {
                if (skill != null && !skill.IsTaskUnlocked)
                    _buyableSkills.Add(skill);
            }

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

        public bool DebugForceClaim(OneTimeUpgradeDefinition definition)
        {
            if (definition == null || _upgrades.IsUnlocked(definition))
                return false;

            _upgrades.DebugForceUnlock(definition);
            return true;
        }

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

        private string Describe(CollectionObjective objective)
        {
            switch (objective.Kind)
            {
                case CollectionObjective.ObjectiveKind.CompleteSpecificSets:
                    // Only what is still missing is named, so a task half done reads as the work
                    // that is left rather than as a list the player has to check off themselves.
                    List<CardSetDefinition> named = CollectSets(objective.Sets, onlyUnfinished: true);

                    // Nothing left - the task is done. The line falls back to the whole list so it
                    // still reads as a sentence while the row waits to be claimed.
                    if (named.Count == 0)
                        named = CollectSets(objective.Sets, onlyUnfinished: false);

                    string names = JoinSetNames(named);
                    string unit = named.Count == 1 ? "set" : "sets";
                    return $"Fully complete {names} {unit} to unlock this ability";

                case CollectionObjective.ObjectiveKind.CompletePages:
                    return $"Fully complete {objective.Count} pages to unlock this ability";

                case CollectionObjective.ObjectiveKind.CompleteAnySets:
                    return $"Fully complete {objective.Count} sets to unlock this ability";

                case CollectionObjective.ObjectiveKind.StoreDuplicates:
                    return $"Put {objective.Count} duplicates away in the box to unlock this ability";

                default:
                    return string.Empty;
            }
        }

        private static string Remaining(CollectionObjective objective, int required, int completed)
        {
            int remaining = Mathf.Max(0, required - completed);
            string unit = objective.UnitName;

            if (remaining != 1)
                unit += "s";

            return $"{remaining} {unit} remaining";
        }

        /// <summary>
        /// The sets an objective names, optionally thinned to the ones still unfinished. Nulls are
        /// dropped either way, so the caller can count the result as the number it puts in the text.
        /// </summary>
        private List<CardSetDefinition> CollectSets(
            IReadOnlyList<CardSetDefinition> sets, bool onlyUnfinished)
        {
            var result = new List<CardSetDefinition>();
            if (sets == null)
                return result;

            foreach (CardSetDefinition set in sets)
            {
                if (set == null)
                    continue;

                if (onlyUnfinished && _progress.IsSetCompleted(set.SetId))
                    continue;

                result.Add(set);
            }

            return result;
        }

        /// <summary>
        /// The set names run together, each picked out in the album's warm gold and bolded so the
        /// names stand off the rest of the sentence rather than dissolving into it.
        /// </summary>
        private static string JoinSetNames(IReadOnlyList<CardSetDefinition> sets)
        {
            if (sets == null)
                return string.Empty;

            var names = new List<string>();
            foreach (CardSetDefinition set in sets)
            {
                if (set != null)
                    names.Add($"<b><color={SetNameColor}>{set.SetName}</color></b>");
            }

            return string.Join(", ", names);
        }
    }
}
