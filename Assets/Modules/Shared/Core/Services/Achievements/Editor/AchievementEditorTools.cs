using System;
using System.Diagnostics;
using System.Threading;
using Steamworks;
using UnityEditor;
using UnityEngine;
using Vesolovsky.Core.Services.Steam;
using Zenject;
using Debug = UnityEngine.Debug;

namespace Vesolovsky.Core.Services.Achievements.Editor
{
    /// <summary>
    /// Clears every achievement and stat this Steam account holds for the game, so the unlock path
    /// can be walked again. The counterpart to "Clear Save" - and usually wanted together with it,
    /// because a save that still believes the collection is finished will not re-award anything.
    ///
    /// The awkward part is that this is an editor menu item and Steamworks is a runtime session.
    /// Outside play mode nothing has called SteamAPI.Init, and every Steamworks call throws until
    /// something does; inside play mode the game already owns a session that must not be torn down
    /// underneath it. So the two cases are handled separately - see <see cref="ClearAchievements"/>.
    /// </summary>
    public static class AchievementEditorTools
    {
        // How long to wait for Steam to hand over this user's stats before resetting them. A reset
        // issued before they arrive is refused, and a fresh session has only just asked for them.
        private const double StatsWaitSeconds = 5d;

        // How long to keep pumping after the reset, so the store actually leaves the process before
        // the session is closed behind it.
        private const double StoreWaitSeconds = 3d;

        private const string LogPrefix = "[Clear Achievements]";

        [MenuItem("Vesolovsky/Clear Achievements", false)]
        public static void ClearAchievements()
        {
            // Unlike a save file, this reaches the account and cannot be undone from here - the
            // achievements have to be earned again. Worth one dialog.
            bool confirmed = EditorUtility.DisplayDialog(
                "Clear Steam achievements?",
                "This clears every achievement and stat this Steam account holds for the game.\n\n" +
                "It cannot be undone - they have to be earned again.",
                "Clear them",
                "Cancel");

            if (!confirmed)
                return;

            // In play mode the game owns the session. Going through its own service rather than
            // straight to Steam matters: the service caches what it believes is already earned, and
            // resetting behind its back would leave that cache insisting everything is still won,
            // so nothing would re-award for the rest of the session.
            if (Application.isPlaying && TryClearThroughRunningGame())
                return;

            ClearInOwnSession();
        }

        private static bool TryClearThroughRunningGame()
        {
            DiContainer container = ProjectContext.Instance != null
                ? ProjectContext.Instance.Container
                : null;

            IAchievementService service = container?.TryResolve<IAchievementService>();
            if (service == null)
            {
                Debug.LogWarning(
                    $"{LogPrefix} Play mode is running but no achievement service could be " +
                    "resolved; falling back to a session of our own.");

                return false;
            }

            // Reports "no Steam session" itself when there is none, and clears its own caches.
            service.DebugResetAll();
            return true;
        }

        /// <summary>
        /// The out-of-play-mode path: bring a session up just for this, do the work, put it back
        /// down. Init works in the editor because steam_appid.txt sits in the project root, which is
        /// the editor's working directory - the same reason play mode can reach Steam at all.
        /// </summary>
        private static void ClearInOwnSession()
        {
            if (SteamService.IsRunning)
            {
                // A play session left its API up (an exited play mode that never got to shut down).
                // Use it rather than initializing a second time, and leave it as we found it.
                Reset(ownsSession: false);
                return;
            }

            if (!SteamAPI.Init())
            {
                Debug.LogError(
                    $"{LogPrefix} Could not start Steam. Check the client is running, that this " +
                    "account has the app, and that steam_appid.txt is in the project root.");

                return;
            }

            try
            {
                Reset(ownsSession: true);
            }
            finally
            {
                SteamAPI.Shutdown();
            }
        }

        private static void Reset(bool ownsSession)
        {
            bool statsReceived = false;
            bool statsStored = false;

            using (Callback<UserStatsReceived_t>.Create(_ => statsReceived = true))
            using (Callback<UserStatsStored_t>.Create(_ => statsStored = true))
            {
                // Only a session started a moment ago has to wait; one that has been up has had its
                // stats for a long time and will never send another unprompted.
                if (ownsSession && !PumpUntil(() => statsReceived, StatsWaitSeconds))
                {
                    Debug.LogWarning(
                        $"{LogPrefix} Steam did not send this user's stats within " +
                        $"{StatsWaitSeconds}s. Trying the reset anyway; if nothing clears, that is why.");
                }

                if (!SteamUserStats.ResetAllStats(bAchievementsToo: true))
                {
                    Debug.LogError($"{LogPrefix} Steam refused the reset.");
                    return;
                }

                // The reset is local until this pushes it; without it nothing changes on the account.
                if (!SteamUserStats.StoreStats())
                {
                    Debug.LogError($"{LogPrefix} Steam refused to store the cleared stats.");
                    return;
                }

                if (!PumpUntil(() => statsStored, StoreWaitSeconds))
                {
                    Debug.LogWarning(
                        $"{LogPrefix} Cleared, but Steam did not confirm the store within " +
                        $"{StoreWaitSeconds}s. It may still be on its way.");

                    return;
                }
            }

            Debug.Log($"{LogPrefix} Every achievement and stat was cleared on this account.");
        }

        /// <summary>
        /// Drives the callback queue until <paramref name="done"/> comes true or the time runs out.
        ///
        /// Blocking rather than riding EditorApplication.update because this is one deliberate click
        /// that has to finish before the session is closed behind it, and a second or two of a frozen
        /// editor is a fair price for not having to hold half-finished state across frames.
        /// </summary>
        private static bool PumpUntil(Func<bool> done, double seconds)
        {
            var clock = Stopwatch.StartNew();

            while (clock.Elapsed.TotalSeconds < seconds)
            {
                SteamAPI.RunCallbacks();

                if (done())
                    return true;

                Thread.Sleep(25);
            }

            return done();
        }
    }
}
