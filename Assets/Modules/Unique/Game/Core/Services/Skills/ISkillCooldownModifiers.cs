using Vesolovsky.Game.Services.Upgrades;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Skills
{
    /// <summary>
    /// How much the claimed cooldown-reduction rewards shorten a skill's cooldown.
    ///
    /// The skill service owns the base cooldown (off the definition's level) and multiplies it by
    /// this; keeping the "which reward touches which skill" wiring here rather than in the service
    /// leaves the service generic - adding another reduction is a change to this one class.
    /// </summary>
    public interface ISkillCooldownModifiers
    {
        /// <summary>
        /// The factor to apply to <paramref name="id"/>'s base cooldown, 1 when nothing reduces it.
        /// Several reductions on one skill stack multiplicatively - each takes its cut of what the
        /// one before it left.
        /// </summary>
        float GetMultiplier(SkillId id);
    }

    public class SkillCooldownModifiers : ISkillCooldownModifiers
    {
        private readonly UpgradeCatalog _catalog;
        private readonly IUpgradeService _upgrades;

        [Inject]
        public SkillCooldownModifiers(UpgradeCatalog catalog, IUpgradeService upgrades)
        {
            _catalog = catalog;
            _upgrades = upgrades;
        }

        public float GetMultiplier(SkillId id)
        {
            float multiplier = 1f;

            // Traveler: every skill's cooldown is cut.
            multiplier *= ReductionFactor(OneTimeUpgradeKind.AllSkillsCooldownReduction);

            // Playmaker: Hand Sort's cooldown is cut. A new per-skill reduction is another case here.
            if (id == SkillId.HandSort)
                multiplier *= ReductionFactor(OneTimeUpgradeKind.HandSortCooldownReduction);

            return multiplier;
        }

        /// <summary>
        /// The multiplier a single reduction reward contributes - (1 - its fraction) once claimed,
        /// or 1 while it is not. The reward's <see cref="OneTimeUpgradeDefinition.Value"/> is the
        /// fraction, so 0.2 authored there means a 20% shorter cooldown.
        /// </summary>
        private float ReductionFactor(OneTimeUpgradeKind kind)
        {
            OneTimeUpgradeDefinition definition = _catalog.FindOneTime(kind);
            if (definition == null || !_upgrades.IsUnlocked(definition))
                return 1f;

            // Clamped so a mis-authored fraction can only ever shorten, never lengthen or invert.
            return UnityEngine.Mathf.Clamp01(1f - definition.Value);
        }
    }
}
