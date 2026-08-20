using UnityEngine;

namespace Vesolovsky.Game.Upgrades
{
    /// <summary>
    /// An upgrade bought a level at a time, each level with its own skill-point price. Permanent
    /// upgrades and skills are both leveled; what a level costs and how strong it is lives in the
    /// concrete definition, so the upgrade service can drive either through this one contract
    /// without knowing which it is holding.
    ///
    /// Levels are one-based: level 0 means "not bought", level 1 is the first purchase, and
    /// <see cref="MaxLevel"/> is the last. Costs and values are asked for by the level they buy.
    ///
    /// A leveled upgrade can also be earned rather than bought - see <see cref="UnlockedBy"/> -
    /// in which case it has no shop row and runs at its single level 1 while its task is claimed.
    /// </summary>
    public abstract class LeveledUpgradeDefinition : UpgradeDefinition
    {
        [Tooltip("Leave empty for a normal, buyable upgrade. Set it to a one-time upgrade to make " +
                 "this unlock by claiming that task instead of by spending skill points - it is " +
                 "then hidden from the shop and is 'owned' exactly while the task is claimed. A " +
                 "task-unlocked upgrade runs at its single level 1, so author one level below.")]
        [SerializeField] private OneTimeUpgradeDefinition unlockedBy;

        /// <summary>
        /// The task that unlocks this upgrade, or null for one bought with skill points. When set,
        /// the upgrade is not shown in the shop and is owned - at level 1 - exactly while the task
        /// is claimed. <see cref="Vesolovsky.Game.Services.Upgrades.IUpgradeService.GetLevel"/> is
        /// the one place that turns this into an effective level.
        /// </summary>
        public OneTimeUpgradeDefinition UnlockedBy => unlockedBy;

        /// <summary>Whether this is unlocked by a task rather than bought with skill points.</summary>
        public bool IsTaskUnlocked => unlockedBy != null;

        /// <summary>The highest level this upgrade can reach - the number of levels authored.</summary>
        public abstract int MaxLevel { get; }

        /// <summary>
        /// Skill points to raise the upgrade to <paramref name="level"/> from the one below it.
        /// Only meaningful for levels 1..<see cref="MaxLevel"/>.
        /// </summary>
        public abstract int GetCost(int level);

        /// <summary>
        /// The effect magnitude the upgrade grants at <paramref name="level"/>. What the number
        /// means is the concrete upgrade's business - extra card slots, a view radius, cards
        /// pulled - and level 0 is always "no effect", left to the thing being upgraded to define.
        /// </summary>
        public abstract float GetValue(int level);
    }
}
