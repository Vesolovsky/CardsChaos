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

        int GetMaxLevel(LeveledUpgradeDefinition definition);

        /// <summary>Cost of the next level, or 0 when the upgrade is maxed.</summary>
        int GetNextCost(LeveledUpgradeDefinition definition);

        bool IsMaxed(LeveledUpgradeDefinition definition);

        /// <summary>Buys the next level; returns whether it happened.</summary>
        bool TryLevelUp(LeveledUpgradeDefinition definition);

        bool IsUnlocked(OneTimeUpgradeDefinition definition);

        /// <summary>Claims a finished one-time upgrade; returns whether it happened.</summary>
        bool TryClaim(OneTimeUpgradeDefinition definition);

        /// <summary>The unlock task's state, recomputed on demand (the view reads it on open).</summary>
        UpgradeTaskProgress GetTaskProgress(OneTimeUpgradeDefinition definition);
    }
}
