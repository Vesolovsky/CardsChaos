using System;
using System.Collections.Generic;
using UnityEngine;
using Vesolovsky.Game.Upgrades;

namespace Vesolovsky.Game.Letters
{
    /// <summary>What makes a letter arrive.</summary>
    public enum LetterTriggerKind
    {
        /// <summary>The first time the player fires any of the listed skills.</summary>
        SkillFirstUse,

        /// <summary>The moment the count of correctly-filed cards first reaches a threshold.</summary>
        CorrectlyPlacedReached,

        /// <summary>The moment a named set is completed in full.</summary>
        SetCompleted,
    }

    /// <summary>
    /// One rule that queues a letter when a milestone is hit. Authored on the letters installer, so
    /// choosing "which set (or threshold, or skill) brings which letter" is a data edit, not code.
    /// Only the field for the chosen <see cref="Kind"/> is read.
    /// </summary>
    [Serializable]
    public class LetterTrigger
    {
        public LetterTriggerKind Kind;

        [Tooltip("The letter this rule brings into the room.")]
        public LetterId Letter;

        [Tooltip("SkillFirstUse: firing any of these for the first time triggers the letter.")]
        public List<SkillId> Skills = new List<SkillId>();

        [Tooltip("CorrectlyPlacedReached: the letter arrives when the number of correctly-filed " +
                 "cards first reaches this.")]
        public int CorrectlyPlacedThreshold;

        [Tooltip("SetCompleted: the set id (folder name, e.g. \"Unique Wands\") whose completion " +
                 "triggers the letter.")]
        public string SetId;
    }
}
