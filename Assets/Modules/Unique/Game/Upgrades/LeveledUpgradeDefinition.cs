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
    /// </summary>
    public abstract class LeveledUpgradeDefinition : UpgradeDefinition
    {
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
