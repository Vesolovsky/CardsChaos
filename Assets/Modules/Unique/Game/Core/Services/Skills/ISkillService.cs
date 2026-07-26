using System;
using Vesolovsky.Game.Upgrades;

namespace Vesolovsky.Game.Services.Skills
{
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
    }
}
