using System;
using Vesolovsky.Game.Upgrades;

namespace Vesolovsky.Game.Services.Upgrades
{
    /// <summary>
    /// Owns what the player has bought and unlocked: the level of every permanent upgrade and
    /// skill, and which one-time upgrades have been claimed. It is the only thing that spends
    /// skill points, and the only thing that decides whether a purchase or a claim is allowed.
    ///
    /// State is written straight through to the save. Effects are not applied here - the service
    /// only records the change and announces it through <see cref="Changed"/>; the appliers and
    /// skills listen and act.
    /// </summary>
    public interface IUpgradeService
    {
        /// <summary>
        /// Raised whenever an upgrade's level or claimed state changes, with the definition that
        /// changed. Also raised with null to mean "assume everything changed", which is how the
        /// appliers are prompted to apply the loaded state once the save is in.
        /// </summary>
        event Action<UpgradeDefinition> Changed;

        /// <summary>The current level of a leveled upgrade, 0 when it has never been bought.</summary>
        int GetLevel(LeveledUpgradeDefinition definition);

        /// <summary>Whether the next level exists and the player can afford it.</summary>
        bool CanLevelUp(LeveledUpgradeDefinition definition);

        /// <summary>
        /// Buys the next level, spending its cost. Returns false and changes nothing when the
        /// upgrade is maxed or the player is short.
        /// </summary>
        bool TryLevelUp(LeveledUpgradeDefinition definition);

        /// <summary>Whether a one-time upgrade has been claimed.</summary>
        bool IsUnlocked(OneTimeUpgradeDefinition definition);

        /// <summary>Whether a one-time upgrade's task is done and it is still waiting to be claimed.</summary>
        bool CanClaim(OneTimeUpgradeDefinition definition);

        /// <summary>
        /// Claims a one-time upgrade whose task is complete. Returns false and changes nothing when
        /// the task is unmet or it is already claimed.
        /// </summary>
        bool TryClaim(OneTimeUpgradeDefinition definition);

        /// <summary>
        /// Re-announces the whole state through <see cref="Changed"/>, so effects that depend on it
        /// can be applied. Used once the save has loaded, and available whenever a fresh push is
        /// wanted.
        /// </summary>
        void Refresh();
    }
}
