using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vesolovsky.Game.Upgrades
{
    /// <summary>
    /// A permanent upgrade: bought outright with skill points and, once bought, always on. Every
    /// level raises its <see cref="Level.value"/>, which the matching effect applier reads and
    /// pushes onto the thing it upgrades - the hand's slot count, the revealer's view radius.
    ///
    /// Level 0 is deliberately left without a value: it means the upgraded thing keeps whatever it
    /// was authored with, so the base amount lives on that component and is not duplicated here.
    /// </summary>
    [CreateAssetMenu(
        menuName = "CardsChaos/Upgrades/Permanent Upgrade",
        fileName = "PermanentUpgrade")]
    public class PermanentUpgradeDefinition : LeveledUpgradeDefinition
    {
        [Serializable]
        public struct Level
        {
            [Tooltip("Skill points to buy this level.")]
            public int cost;

            [Tooltip("The effect value once this level is owned. Its meaning depends on the kind - " +
                     "e.g. total held-card slots, or the fog revealer's view radius.")]
            public float value;
        }

        [Tooltip("Which effect this upgrade drives. The applier finds its definition by this.")]
        [SerializeField] private PermanentUpgradeKind kind;

        [Tooltip("The levels, lowest first. Level 1 is the first entry.")]
        [SerializeField] private List<Level> levels = new List<Level>();

        public PermanentUpgradeKind Kind => kind;

        public override int MaxLevel => levels.Count;

        public override int GetCost(int level) =>
            IsValidLevel(level) ? levels[level - 1].cost : 0;

        public override float GetValue(int level) =>
            IsValidLevel(level) ? levels[level - 1].value : 0f;

        private bool IsValidLevel(int level)
        {
            if (level >= 1 && level <= levels.Count)
                return true;

            Debug.LogError($"[{nameof(PermanentUpgradeDefinition)}] '{Id}' has no level {level}.", this);
            return false;
        }
    }
}
