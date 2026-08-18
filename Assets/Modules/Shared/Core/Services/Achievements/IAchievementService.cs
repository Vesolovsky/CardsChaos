namespace Vesolovsky.Core.Services.Achievements
{
    /// <summary>
    /// Where an achievement is awarded, expressed in the platform's own terms - an API name, the
    /// string authored on the store's back end.
    ///
    /// Game code never calls this with a raw string; it goes through the game's own achievement ids
    /// (see <c>Vesolovsky.Game.Services.Achievements.GameAchievements</c>), which is the one place
    /// an id is turned into the name Steam knows it by.
    ///
    /// Awarding is idempotent by design. The platform is the record of what the player has earned,
    /// not the save file, so re-awarding something already earned must be free and silent - which is
    /// what lets a tracker re-check every condition on load without showering the player in toasts.
    /// </summary>
    public interface IAchievementService
    {
        /// <summary>
        /// Whether awards actually reach a platform. False means the game is running without one
        /// (Steam not started, an unsupported build), and every call here is a no-op.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Whether the platform already has this one recorded as earned. False when it is not
        /// earned, and also when the platform is absent or its stats have not arrived yet.
        /// </summary>
        bool IsUnlocked(string apiName);

        /// <summary>
        /// Awards it. A no-op when it is already earned, so a caller may call this as often as its
        /// condition is re-evaluated.
        /// </summary>
        void Unlock(string apiName);

        /// <summary>
        /// Shows the "23 / 100" toast for a counted achievement, without awarding it. Purely
        /// cosmetic - Steam ignores a report that is not higher than the last, and awarding is still
        /// <see cref="Unlock"/>'s job.
        /// </summary>
        void ReportProgress(string apiName, int current, int required);

        /// <summary>
        /// Testing hook: clears every achievement and stat on the platform for this user, so the
        /// unlock path can be walked again. Never reached from play.
        /// </summary>
        void DebugResetAll();
    }
}
