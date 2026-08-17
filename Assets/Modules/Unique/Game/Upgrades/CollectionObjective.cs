using System;
using System.Collections.Generic;
using CardsChaos.Cards;
using UnityEngine;
using Vesolovsky.Game.Services.Progress;

namespace Vesolovsky.Game.Upgrades
{
    /// <summary>
    /// The task a one-time upgrade asks of the player before it can be claimed, written against
    /// <see cref="ICollectionProgress"/> so it speaks in completed pages and sets.
    ///
    /// One objective covers the cases the game needs today - finish these particular sets, finish
    /// any so-many sets, finish so-many pages - selected by <see cref="Kind"/>. A new kind of task
    /// is a new enum case and a new branch in <see cref="IsSatisfied"/>; nothing outside this file
    /// has to change.
    /// </summary>
    [Serializable]
    public class CollectionObjective
    {
        public enum ObjectiveKind
        {
            /// <summary>Every one of <see cref="sets"/> must be completed.</summary>
            CompleteSpecificSets,

            /// <summary>Any <see cref="count"/> sets completed, whichever they are.</summary>
            CompleteAnySets,

            /// <summary>Any <see cref="count"/> pages completed, across every set.</summary>
            CompletePages,

            /// <summary>Any <see cref="count"/> duplicates put away in the duplicate boxes.</summary>
            StoreDuplicates,
        }

        [SerializeField] private ObjectiveKind kind;

        [Tooltip("The sets that must be completed. Used only by 'Complete Specific Sets'.")]
        [SerializeField] private List<CardSetDefinition> sets = new List<CardSetDefinition>();

        [Tooltip("How many sets, pages or duplicates are asked for. Used by the count-based kinds.")]
        [SerializeField] private int count = 1;

        public ObjectiveKind Kind => kind;

        public IReadOnlyList<CardSetDefinition> Sets => sets;

        /// <summary>The authored count for the count-based kinds; unused by specific-sets.</summary>
        public int Count => count;

        /// <summary>
        /// The singular noun the objective is counted in - what the task row puts after its
        /// remaining number. A new kind that counts something else names it here.
        /// </summary>
        public string UnitName
        {
            get
            {
                switch (kind)
                {
                    case ObjectiveKind.CompletePages:
                        return "page";

                    case ObjectiveKind.StoreDuplicates:
                        return "duplicate";

                    default:
                        return "set";
                }
            }
        }

        /// <summary>How many completed sets or pages the objective asks for in total.</summary>
        public int Required =>
            kind == ObjectiveKind.CompleteSpecificSets ? (sets?.Count ?? 0) : count;

        /// <summary>
        /// How much of the objective is done, capped at <see cref="Required"/> so a task shown as
        /// finished never reports more than it needed - useful for a progress bar and a remaining
        /// count.
        /// </summary>
        public int GetCompleted(ICollectionProgress progress)
        {
            if (progress == null)
                return 0;

            switch (kind)
            {
                case ObjectiveKind.CompleteSpecificSets:
                    int done = 0;
                    if (sets != null)
                    {
                        foreach (CardSetDefinition set in sets)
                        {
                            if (set != null && progress.IsSetCompleted(set.SetId))
                                done++;
                        }
                    }

                    return done;

                case ObjectiveKind.CompleteAnySets:
                    return Mathf.Min(progress.CompletedSetCount, count);

                case ObjectiveKind.CompletePages:
                    return Mathf.Min(progress.CompletedPageCount, count);

                case ObjectiveKind.StoreDuplicates:
                    return Mathf.Min(progress.StoredDuplicateCount, count);

                default:
                    return 0;
            }
        }

        public bool IsSatisfied(ICollectionProgress progress)
        {
            if (progress == null)
                return false;

            switch (kind)
            {
                case ObjectiveKind.CompleteSpecificSets:
                    if (sets == null || sets.Count == 0)
                        return false;

                    foreach (CardSetDefinition set in sets)
                    {
                        if (set == null || !progress.IsSetCompleted(set.SetId))
                            return false;
                    }

                    return true;

                case ObjectiveKind.CompleteAnySets:
                    return progress.CompletedSetCount >= count;

                case ObjectiveKind.CompletePages:
                    return progress.CompletedPageCount >= count;

                case ObjectiveKind.StoreDuplicates:
                    return progress.StoredDuplicateCount >= count;

                default:
                    return false;
            }
        }
    }
}
