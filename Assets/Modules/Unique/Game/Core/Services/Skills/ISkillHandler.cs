using Vesolovsky.Game.Upgrades;

namespace Vesolovsky.Game.Services.Skills
{
    /// <summary>
    /// The effect behind one skill. The skill service owns unlocking and cooldowns and asks a
    /// handler only two things: whether the skill can fire right now given where the player is
    /// (in the album, holding a card), and to fire it.
    ///
    /// Activation can decline - the magnet with nothing to pull, a sort of a single card - by
    /// returning false, which the service reads as "nothing happened" and so charges no cooldown.
    /// </summary>
    public interface ISkillHandler
    {
        SkillId Id { get; }

        /// <summary>Whether the current context allows the skill (world state, a card in hand).</summary>
        bool CanActivate();

        /// <summary>
        /// Carries out the skill at the given level, reading whatever it needs from the definition.
        /// Returns false when it turns out there was nothing to do.
        /// </summary>
        bool Activate(SkillDefinition definition, int level);
    }
}
