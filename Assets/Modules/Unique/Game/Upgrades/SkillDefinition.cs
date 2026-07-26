using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vesolovsky.Game.Upgrades
{
    /// <summary>
    /// A skill: a leveled upgrade the player triggers on purpose and then waits out a cooldown
    /// before triggering again. Buying the first level unlocks it; later levels sharpen it -
    /// usually a shorter cooldown, sometimes a stronger effect - through <see cref="Level"/>.
    /// </summary>
    [CreateAssetMenu(
        menuName = "CardsChaos/Upgrades/Skill",
        fileName = "Skill")]
    public class SkillDefinition : LeveledUpgradeDefinition
    {
        [Serializable]
        public struct Level
        {
            [Tooltip("Skill points to buy this level.")]
            public int cost;

            [Tooltip("The effect strength at this level. Meaning is per-skill - Card Magnet reads " +
                     "it as how many cards to pull; skills whose only change is the cooldown leave " +
                     "it at zero.")]
            public float value;

            [Tooltip("Seconds before the skill can be used again after this level fires it.")]
            public float cooldown;
        }

        [Tooltip("Which skill this is. Pairs the definition with the handler that carries it out.")]
        [SerializeField] private SkillId skillId;

        [Tooltip("The key that activates the skill from the keyboard.")]
        [SerializeField] private Key activationKey;

        [Tooltip("The levels, lowest first. Level 1 both unlocks the skill and is its first use.")]
        [SerializeField] private List<Level> levels = new List<Level>();

        public SkillId SkillId => skillId;

        public Key ActivationKey => activationKey;

        public override int MaxLevel => levels.Count;

        public override int GetCost(int level) =>
            IsValidLevel(level) ? levels[level - 1].cost : 0;

        public override float GetValue(int level) =>
            IsValidLevel(level) ? levels[level - 1].value : 0f;

        /// <summary>The cooldown the skill starts when fired at <paramref name="level"/>.</summary>
        public float GetCooldown(int level) =>
            IsValidLevel(level) ? levels[level - 1].cooldown : 0f;

        private bool IsValidLevel(int level)
        {
            if (level >= 1 && level <= levels.Count)
                return true;

            Debug.LogError($"[{nameof(SkillDefinition)}] '{Id}' has no level {level}.", this);
            return false;
        }
    }
}
