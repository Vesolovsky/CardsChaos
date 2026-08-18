#if UNITY_EDITOR || CARDSCHAOS_DEBUG_TOOLS
using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services.Achievements;
using Vesolovsky.Core.Utils;
using Zenject;

namespace Vesolovsky.Game.Services.Achievements
{
    /// <summary>
    /// Testing helper for the Steam achievements. Drop it on any empty object in the scene; the
    /// build strip takes it back out.
    ///
    /// All four keys are three-key combos on purpose: two of them change what the account has
    /// permanently earned, which is not something to hit while reaching for F12.
    ///
    /// <list type="bullet">
    /// <item>Ctrl+Shift+F9 — prints what Steam currently has for every achievement. Start here:
    /// it also says whether there is a Steam session at all.</item>
    /// <item>Ctrl+Shift+F10 — awards the single achievement picked in the Inspector. The one to
    /// use for "does any of this work", rather than firing all fifteen.</item>
    /// <item>Ctrl+Shift+F11 — awards every achievement, for checking icons and text against the
    /// store page.</item>
    /// <item>Ctrl+Shift+F12 — clears every achievement and stat on the account, so the unlock path
    /// can be walked again.</item>
    /// </list>
    ///
    /// With no Steam client running these only log - which is still a real test of the conditions,
    /// just not of the award reaching Steam.
    /// </summary>
    [AddComponentMenu("CardsChaos/Debug/Achievement Debug Tool")]
    public class AchievementDebugTool : MonoBehaviour, IDebugTool
    {
        [Tooltip("Which achievement Ctrl+Shift+F10 awards. Pick one that is cheap to reset and " +
                 "easy to recognise when its toast appears.")]
        [SerializeField] private AchievementId testAchievement = AchievementId.HouseByThrow;

        private IAchievementService _achievements;

        [Inject]
        public void Construct([InjectOptional] IAchievementService achievements)
        {
            _achievements = achievements;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _achievements == null)
                return;

            bool ctrl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            bool shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            if (!ctrl || !shift)
                return;

            if (keyboard.f9Key.wasPressedThisFrame)
                DumpStatus();

            if (keyboard.f10Key.wasPressedThisFrame)
                UnlockOne();

            if (keyboard.f11Key.wasPressedThisFrame)
                UnlockEverything();

            if (keyboard.f12Key.wasPressedThisFrame)
                _achievements.DebugResetAll();
        }

        private void UnlockOne()
        {
            _achievements.Unlock(testAchievement);
            Debug.Log(
                $"[{nameof(AchievementDebugTool)}] Asked Steam for '{testAchievement}' " +
                $"({GameAchievements.ApiName(testAchievement)}).");
        }

        private void UnlockEverything()
        {
            foreach (AchievementId id in (AchievementId[])Enum.GetValues(typeof(AchievementId)))
                _achievements.Unlock(id);

            Debug.Log($"[{nameof(AchievementDebugTool)}] Awarded every achievement.");
        }

        /// <summary>
        /// Reads every achievement back off Steam. This is the honest check: it reports what the
        /// platform holds, not what this session happens to have asked for. Everything reading as
        /// locked while a session is up usually means the config has not been published yet.
        /// </summary>
        private void DumpStatus()
        {
            var report = new StringBuilder();
            report.AppendLine($"[{nameof(AchievementDebugTool)}] Steam session: " +
                              $"{(_achievements.IsAvailable ? "up" : "NOT running - awards are only logged")}");

            foreach (AchievementId id in (AchievementId[])Enum.GetValues(typeof(AchievementId)))
            {
                string apiName = GameAchievements.ApiName(id);
                string state = _achievements.IsUnlocked(apiName) ? "UNLOCKED" : "locked";
                report.AppendLine($"  {state,-8} {apiName}  ({id})");
            }

            Debug.Log(report.ToString());
        }
    }
}
#endif
