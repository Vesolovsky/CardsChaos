using System;
using System.Collections.Generic;
using Vesolovsky.Game.Upgrades;

namespace Vesolovsky.Game.Services.Skills
{
    /// <summary>One skill's running cooldown, as the save reads and writes it.</summary>
    public readonly struct SkillCooldownSnapshot
    {
        public SkillId Id { get; }
        public float Remaining { get; }
        public float Total { get; }

        public SkillCooldownSnapshot(SkillId id, float remaining, float total)
        {
            Id = id;
            Remaining = remaining;
            Total = total;
        }
    }

    /// <summary>
    /// Fires skills and tracks their cooldowns. Whether a skill exists, is unlocked and off
    /// cooldown all funnel through here, so the keyboard and a future button press take the same
    /// path and cannot disagree.
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

        /// <summary>Whether the skill is unlocked and off cooldown right now.</summary>
        bool IsReady(SkillId id);

        /// <summary>Seconds left on the cooldown, or zero when ready.</summary>
        float GetCooldownRemaining(SkillId id);

        /// <summary>Cooldown left as 0..1 of its full length, for a radial fill and the like.</summary>
        float GetCooldownNormalized(SkillId id);

        /// <summary>Every skill with time still on its cooldown, for the save to write down.</summary>
        IReadOnlyList<SkillCooldownSnapshot> GetActiveCooldowns();

        /// <summary>Restores a cooldown from a loaded save. Ignored when nothing is left to run.</summary>
        void RestoreCooldown(SkillId id, float remaining, float total);
    }
}
