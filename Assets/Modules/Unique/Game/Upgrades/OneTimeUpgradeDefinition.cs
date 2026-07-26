using UnityEngine;

namespace Vesolovsky.Game.Upgrades
{
    /// <summary>
    /// A one-time upgrade: not bought with skill points but earned by finishing a task, then
    /// claimed. Its <see cref="Objective"/> is the task; while the task is unmet the upgrade cannot
    /// be claimed, and once claimed it is on for good.
    ///
    /// There are no levels - a one-time upgrade is either had or not - so it does not derive from
    /// <see cref="LeveledUpgradeDefinition"/>.
    /// </summary>
    [CreateAssetMenu(
        menuName = "CardsChaos/Upgrades/One-Time Upgrade",
        fileName = "OneTimeUpgrade")]
    public class OneTimeUpgradeDefinition : UpgradeDefinition
    {
        [Tooltip("Which effect this upgrade unlocks. The applier finds its definition by this.")]
        [SerializeField] private OneTimeUpgradeKind kind;

        [Tooltip("What the player must finish before the upgrade can be claimed.")]
        [SerializeField] private CollectionObjective objective = new CollectionObjective();

        [Tooltip("An optional effect parameter, for upgrades that need one number - e.g. a sprint " +
                 "speed multiplier. Ignored by upgrades that carry their tuning elsewhere.")]
        [SerializeField] private float value;

        public OneTimeUpgradeKind Kind => kind;

        public CollectionObjective Objective => objective;

        public float Value => value;
    }
}
