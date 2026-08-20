using System;
using System.Collections.Generic;
using Vesolovsky.Game.Upgrades;

namespace Vesolovsky.Game.Services.Skills
{
    /// <summary>
    /// One skill's running wait, as the save reads and writes it. A timed skill spends the first
    /// part of that wait switched on, so the two halves are carried separately - restoring them
    /// merged would turn a spell that was still running into plain cooldown.
    /// </summary>
    public readonly struct SkillCooldownSnapshot
    {
        public SkillId Id { get; }

        /// <summary>Seconds of ordinary cooldown left, not counting an active window still running.</summary>
        public float Remaining { get; }

        /// <summary>The full cooldown that <see cref="Remaining"/> is winding down from.</summary>
        public float Total { get; }

        /// <summary>Seconds the skill is still switched on for; zero for anything but a timed skill.</summary>
        public float ActiveRemaining { get; }

        /// <summary>The full active window that <see cref="ActiveRemaining"/> is winding down from.</summary>
        public float ActiveTotal { get; }

        public SkillCooldownSnapshot(
            SkillId id, float remaining, float total, float activeRemaining, float activeTotal)
        {
            Id = id;
            Remaining = remaining;
            Total = total;
            ActiveRemaining = activeRemaining;
            ActiveTotal = activeTotal;
        }
    }

    /// <summary>
    /// Fires skills and tracks their cooldowns. Whether a skill exists, is unlocked and off
    /// cooldown all funnel through here, so the keyboard and a future button press take the same
    /// path and cannot disagree.
    ///
    /// A timed skill (see <see cref="ITimedSkill"/>) spends the first stretch after a cast switched
    /// on, and only when that runs out does its cooldown begin. The two are reported apart - so the
    /// HUD can show a skill working rather than merely waiting - but they add up into the single
    /// wait <see cref="GetCooldownRemaining"/> returns, which is what the countdown reads.
    ///
    /// The read-only cooldown accessors are here for a UI to draw the wind-down; nothing in the
    /// system needs them to work.
    /// </summary>
    public interface ISkillService
    {
        /// <summary>Raised when a skill successfully fires, with which one - for feedback and UI.</summary>
        event Action<SkillId> Activated;

        /// <summary>
        /// Fires a skill if it is unlocked, off cooldown and its context allows it. Returns whether
        /// it fired; a fire that turned out to be a no-op returns false and starts no cooldown.
        /// </summary>
        bool TryActivate(SkillId id);

        /// <summary>
        /// Whether the player has the skill at all - bought to level one, or, for a task-unlocked
        /// skill, its unlocking task claimed. Says nothing about the cooldown.
        /// </summary>
        bool IsUnlocked(SkillId id);

        /// <summary>Whether the skill is unlocked and off cooldown right now.</summary>
        bool IsReady(SkillId id);

        /// <summary>
        /// Whether a timed skill is switched on at this moment - the window between a cast and the
        /// start of its cooldown. Always false for skills that do their work in one go.
        /// </summary>
        bool IsActive(SkillId id);

        /// <summary>Seconds the active window has left, or zero when the skill is not switched on.</summary>
        float GetActiveRemaining(SkillId id);

        /// <summary>The active window left as 0..1 of its full length, for a bar or a fading glow.</summary>
        float GetActiveNormalized(SkillId id);

        /// <summary>
        /// Seconds until the skill can be fired again - an active window still running plus the
        /// cooldown behind it. Zero when ready.
        /// </summary>
        float GetCooldownRemaining(SkillId id);

        /// <summary>
        /// The wind-down of whichever phase the skill is in, as 0..1 of that phase's length - the
        /// active window while it runs, the cooldown after it. A radial fill driven by this
        /// therefore sweeps twice for a timed skill, so the moment it stops working is visible.
        /// </summary>
        float GetCooldownNormalized(SkillId id);

        /// <summary>Every skill with time still to run, for the save to write down.</summary>
        IReadOnlyList<SkillCooldownSnapshot> GetActiveCooldowns();

        /// <summary>Restores a wait from a loaded save. Ignored when nothing is left to run.</summary>
        void RestoreCooldown(
            SkillId id, float remaining, float total, float activeRemaining, float activeTotal);

        /// <summary>Testing aid: clears every running wait so all skills read ready at once.</summary>
        void DebugResetCooldowns();
    }
}
