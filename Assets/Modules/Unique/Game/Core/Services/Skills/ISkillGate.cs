namespace Vesolovsky.Game.Services.Skills
{
    /// <summary>
    /// A blanket off-switch for skill activation.
    ///
    /// The world-interaction lock is not enough on its own: the album takes the room but leaves
    /// Hand Sort usable, so skills deliberately look past that lock. A screen like the upgrades
    /// panel, though, means every skill should go quiet - it raises this instead, and the skill
    /// input reads it before anything else.
    /// </summary>
    public interface ISkillGate
    {
        bool Blocked { get; set; }
    }

    public class SkillGate : ISkillGate
    {
        public bool Blocked { get; set; }
    }
}
