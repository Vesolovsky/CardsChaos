using System.Collections.Generic;
using UniRx;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.Upgrades;

namespace Vesolovsky.Game.Views
{
    /// <summary>
    /// The upgrades screen's side of the system: what to list, what it costs, and the moves the
    /// player can make - buy a level, claim a finished task.
    ///
    /// The view builds one skill item per permanent upgrade and per skill, and one task item per
    /// one-time upgrade, then reads level, cost and task progress back through here.
    /// </summary>
    public interface IUpgradesViewModel : IViewModel
    {
        /// <summary>Whether the screen is up. While it is, movement and skills are off.</summary>
        IReadOnlyReactiveProperty<bool> IsOpen { get; }

        /// <summary>The player's skill-point balance, for the header count.</summary>
        IReadOnlyReactiveProperty<long> SkillPoints { get; }

        IReadOnlyList<PermanentUpgradeDefinition> Permanents { get; }

        IReadOnlyList<SkillDefinition> Skills { get; }

        IReadOnlyList<OneTimeUpgradeDefinition> OneTimes { get; }

        void Open();

        /// <summary>Hides the screen and gives input back. The view stays loaded.</summary>
        void Close();

        int GetLevel(LeveledUpgradeDefinition definition);

        /// <summary>
        /// The row's blurb, with every number a purchase would move written as the step it would
        /// make - "4→5" - so that no two states of an upgrade read alike. At either end, where
        /// there is nothing to step from or to, the single number stands alone. Skills also get
        /// their cooldown appended. Re-read whenever the row redraws, because buying a level moves
        /// the numbers on.
        /// </summary>
        string GetDescription(LeveledUpgradeDefinition definition);

        int GetMaxLevel(LeveledUpgradeDefinition definition);

        /// <summary>Cost of the next level, or 0 when the upgrade is maxed.</summary>
        int GetNextCost(LeveledUpgradeDefinition definition);

        bool IsMaxed(LeveledUpgradeDefinition definition);

        /// <summary>Whether the current balance covers the definition's next level - false when maxed.</summary>
        bool CanAfford(LeveledUpgradeDefinition definition);

        /// <summary>Buys the next level; returns whether it happened.</summary>
        bool TryLevelUp(LeveledUpgradeDefinition definition);

        bool IsUnlocked(OneTimeUpgradeDefinition definition);

        /// <summary>Claims a finished one-time upgrade; returns whether it happened.</summary>
        bool TryClaim(OneTimeUpgradeDefinition definition);

        /// <summary>
        /// Testing hook behind the task row's editor button: claims a one-time upgrade even with its
        /// task unmet, so its reward can be tried without building the sets. Returns whether it went
        /// from locked to claimed (false when it was already claimed), which the row uses to decide
        /// whether to play its unlock animation.
        /// </summary>
        bool DebugForceClaim(OneTimeUpgradeDefinition definition);

        /// <summary>The unlock task's state, recomputed on demand (the view reads it on open).</summary>
        UpgradeTaskProgress GetTaskProgress(OneTimeUpgradeDefinition definition);
    }
}
