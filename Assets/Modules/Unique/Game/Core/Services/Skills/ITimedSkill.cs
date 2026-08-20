using Vesolovsky.Game.Upgrades;

namespace Vesolovsky.Game.Services.Skills
{
    /// <summary>
    /// A skill that stays switched on for a spell after it is cast rather than doing its work in
    /// one go. Implemented alongside <see cref="ISkillHandler"/> by the handful of skills that
    /// have a duration; the skill service asks every handler it fires whether it is one of these.
    ///
    /// The window itself is run by <see cref="SkillService"/>, not by the handler - it already owns
    /// the clock, stops it for the pause menu and writes it into the save, and doing it in one
    /// place is what keeps a second, subtly different timer from growing beside the cooldown. The
    /// handler only says how long the spell lasts; anything that wants to know whether it is still
    /// running asks <see cref="ISkillService.IsActive"/>.
    ///
    /// The cooldown does not start until the window closes, so the whole wait a player sees is the
    /// spell followed by the cooldown authored for that level.
    /// </summary>
    public interface ITimedSkill
    {
        /// <summary>
        /// How many seconds the skill stays active when fired at <paramref name="level"/>. Zero or
        /// less means it behaves as an ordinary instant skill.
        /// </summary>
        float GetActiveDuration(SkillDefinition definition, int level);
    }
}
