namespace Vesolovsky.Game.Views
{
    /// <summary>
    /// Everything a task item needs to draw a one-time upgrade's unlock task, worked out by the
    /// view model so the item only has to place strings and stretch a bar.
    /// </summary>
    public readonly struct UpgradeTaskProgress
    {
        /// <summary>The "Fully complete ... to unlock this ability" line.</summary>
        public readonly string Description;

        /// <summary>The "X set(s)/page(s) remaining" line.</summary>
        public readonly string RemainingText;

        /// <summary>How far along the task is, 0 to 1, for the progress bar.</summary>
        public readonly float FillRatio;

        /// <summary>Whether the task is finished and the upgrade can be claimed.</summary>
        public readonly bool IsComplete;

        /// <summary>Whether the upgrade has already been claimed.</summary>
        public readonly bool IsUnlocked;

        public UpgradeTaskProgress(
            string description, string remainingText, float fillRatio, bool isComplete, bool isUnlocked)
        {
            Description = description;
            RemainingText = remainingText;
            FillRatio = fillRatio;
            IsComplete = isComplete;
            IsUnlocked = isUnlocked;
        }
    }
}
