using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using Vesolovsky.Core.Services.Steam;
using Zenject;

namespace Vesolovsky.Core.Services.Achievements
{
    /// <summary>
    /// Awards achievements through Steam.
    ///
    /// Two things make this more than a one-line wrapper around SetAchievement.
    ///
    /// The first is that the player's stats arrive from Steam a moment after the API comes up, and
    /// until they do, every read and write is refused. An award that lands in that window is not
    /// dropped - it is parked in <see cref="_pending"/> and retried, both when Steam announces the
    /// stats have arrived and on a slow poll, so a condition met in the very first seconds of a
    /// session still reaches the player.
    ///
    /// The second is that Steam is the record of what has been earned, not the save. That is what
    /// makes awarding idempotent and cheap enough for a tracker to re-check every condition on load:
    /// what Steam already has is skipped, so a returning player sees no toasts for old work, and a
    /// player who finished something while Steam was closed gets it the next time they play online.
    ///
    /// With no Steam session at all this degrades to a console line per award, which is what makes
    /// the whole feature testable in the editor without the client running.
    /// </summary>
    public class SteamAchievementService : IAchievementService, ITickable, IDisposable
    {
        // How often a parked award is retried while the stats have still not arrived. Slow on
        // purpose: this is a safety net under the callback, not the mechanism.
        private const float RetryIntervalSeconds = 1f;

        private readonly ISteamService _steam;

        // Awarded this session or read back as already earned. Purely a cache to keep the common
        // "re-check everything" pass from crossing into native code once per condition per frame.
        private readonly HashSet<string> _earned = new HashSet<string>();

        // Awards Steam has not accepted yet, almost always because the stats are still in flight.
        private readonly HashSet<string> _pending = new HashSet<string>();

        // Names Steam has told us it does not have. Kept so the mistake is reported once rather than
        // on every re-check, and deliberately separate from _earned - a refused name is not earned.
        private readonly HashSet<string> _refused = new HashSet<string>();

        // Scratch for draining _pending, so a retry does not allocate.
        private readonly List<string> _retry = new List<string>();

        private Callback<UserStatsReceived_t> _statsReceived;
        private float _secondsSinceRetry;
        private bool _warnedUnavailable;

        [Inject]
        public SteamAchievementService(ISteamService steam)
        {
            _steam = steam;

            if (!_steam.IsInitialized)
                return;

            // Steam sends this once shortly after the API comes up, and again whenever the stats are
            // re-fetched. Either way it is the moment a parked award can go through.
            _statsReceived = Callback<UserStatsReceived_t>.Create(OnUserStatsReceived);
        }

        public bool IsAvailable => _steam.IsInitialized;

        public void Dispose()
        {
            _statsReceived?.Dispose();
            _statsReceived = null;
        }

        public bool IsUnlocked(string apiName)
        {
            if (string.IsNullOrEmpty(apiName))
                return false;

            if (_earned.Contains(apiName))
                return true;

            if (!IsAvailable)
                return false;

            // Fails while the stats are still in flight, and fails for a name that does not exist on
            // the partner site - neither of which is "the player has earned it".
            if (!SteamUserStats.GetAchievement(apiName, out bool achieved))
                return false;

            if (achieved)
                _earned.Add(apiName);

            return achieved;
        }

        public void Unlock(string apiName)
        {
            if (string.IsNullOrEmpty(apiName))
                return;

            // Already known earned - this session or from Steam's own record. The whole point of
            // the cache: a tracker re-checking its conditions must cost nothing here.
            if (_earned.Contains(apiName) || _refused.Contains(apiName))
                return;

            if (!IsAvailable)
            {
                LogWithoutSteam(apiName);
                return;
            }

            if (!TryAward(apiName))
                _pending.Add(apiName);
        }

        public void ReportProgress(string apiName, int current, int required)
        {
            if (!IsAvailable || string.IsNullOrEmpty(apiName))
                return;

            // A finished or over-shot report is not progress, it is an award - and Steam refuses a
            // progress call at or above the maximum anyway.
            if (required <= 0 || current <= 0 || current >= required)
                return;

            if (_earned.Contains(apiName))
                return;

            SteamUserStats.IndicateAchievementProgress(apiName, (uint)current, (uint)required);
        }

        public void DebugResetAll()
        {
            if (!IsAvailable)
            {
                Debug.LogWarning($"[{nameof(SteamAchievementService)}] No Steam session; nothing to reset.");
                return;
            }

            SteamUserStats.ResetAllStats(bAchievementsToo: true);
            SteamUserStats.StoreStats();

            _earned.Clear();
            _pending.Clear();
            _refused.Clear();

            Debug.Log($"[{nameof(SteamAchievementService)}] Every Steam achievement and stat was reset.");
        }

        /// <summary>
        /// The safety net under <see cref="OnUserStatsReceived"/>: retries parked awards on a slow
        /// beat, so an award still gets through if the stats callback is missed or arrives before
        /// this service was built.
        /// </summary>
        public void Tick()
        {
            if (_pending.Count == 0 || !IsAvailable)
                return;

            _secondsSinceRetry += Time.unscaledDeltaTime;
            if (_secondsSinceRetry < RetryIntervalSeconds)
                return;

            _secondsSinceRetry = 0f;
            FlushPending();
        }

        private void OnUserStatsReceived(UserStatsReceived_t received)
        {
            if (received.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogWarning(
                    $"[{nameof(SteamAchievementService)}] Steam could not load this user's stats " +
                    $"({received.m_eResult}); achievements earned now will be retried.");

                return;
            }

            FlushPending();
        }

        private void FlushPending()
        {
            if (_pending.Count == 0)
                return;

            _retry.Clear();
            _retry.AddRange(_pending);

            foreach (string apiName in _retry)
            {
                if (TryAward(apiName))
                    _pending.Remove(apiName);
            }
        }

        /// <summary>
        /// One attempt at an award. False means Steam refused - the stats are not in yet, or the
        /// name is not one this app has - and the caller should park it and try again.
        /// </summary>
        private bool TryAward(string apiName)
        {
            // Reading first is what tells a "not ready yet" refusal apart from a real award, and it
            // is also how an achievement earned on another machine is picked up without a toast.
            if (!SteamUserStats.GetAchievement(apiName, out bool achieved))
                return false;

            if (achieved)
            {
                _earned.Add(apiName);
                return true;
            }

            if (!SteamUserStats.SetAchievement(apiName))
            {
                Debug.LogError(
                    $"[{nameof(SteamAchievementService)}] Steam refused the achievement '{apiName}'. " +
                    "Check the API name matches the one on the partner site exactly.");

                // Not worth retrying a name Steam has told us it does not know - and recorded so the
                // error is reported once rather than on every re-check of its condition.
                _refused.Add(apiName);
                return true;
            }

            // What actually sends it to Steam and pops the toast. Called per award rather than
            // batched: awards come in ones and twos, minutes apart.
            SteamUserStats.StoreStats();

            _earned.Add(apiName);
            Debug.Log($"[{nameof(SteamAchievementService)}] Achievement unlocked: {apiName}");
            return true;
        }

        private void LogWithoutSteam(string apiName)
        {
            // Still recorded as earned, so the same award is not logged again every time its
            // condition is re-checked - the console reads like the real thing would.
            _earned.Add(apiName);

            if (!_warnedUnavailable)
            {
                _warnedUnavailable = true;
                Debug.Log(
                    $"[{nameof(SteamAchievementService)}] No Steam session - achievements are only " +
                    "logged this session, not awarded.");
            }

            Debug.Log($"[{nameof(SteamAchievementService)}] (offline) Achievement earned: {apiName}");
        }
    }
}
