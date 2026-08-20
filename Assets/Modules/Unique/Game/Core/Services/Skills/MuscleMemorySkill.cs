using Vesolovsky.Core.Audio;
using Vesolovsky.Core.Services;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Skills
{
    /// <summary>
    /// Switches on the hand's memory for where a spare belongs: while it runs, a duplicate thrown
    /// with nothing aimed at flies itself into a duplicate box from wherever the player is
    /// standing, instead of landing on the floor.
    ///
    /// The skill itself does nothing at the moment it fires - there is no effect to play out, only
    /// a window to open - so the handler is little more than the length of that window. The window
    /// is run by the skill service (see <see cref="ITimedSkill"/>), and the throw is filed by the
    /// duplicate service, which asks whether this skill is active at the moment a card leaves the
    /// hand. Neither of them has to know about the other.
    ///
    /// Every level lengthens the window and shortens the wait behind it, so the two numbers a level
    /// carries are its seconds active (the definition's value) and its cooldown.
    /// </summary>
    public class MuscleMemorySkill : ISkillHandler, ITimedSkill
    {
        private readonly IWorldInteractionLock _worldLock;
        private readonly IAudioService _audioService;

        [Inject]
        public MuscleMemorySkill(IWorldInteractionLock worldLock, IAudioService audioService)
        {
            _worldLock = worldLock;
            _audioService = audioService;
        }

        public SkillId Id => SkillId.MuscleMemory;

        // Nothing can be thrown while the album or a close-up holds the room, so a cast there would
        // burn the whole window on a room the player cannot reach. Unlike Hand Sort, this skill has
        // no use inside the album.
        public bool CanActivate() => !_worldLock.IsLocked;

        public bool Activate(SkillDefinition definition, int level)
        {
            _audioService?.Play(AudioSFXKey.SkillMuscleMemory);

            // Always a real cast: the window opens whether or not the player is holding a spare
            // right now, because what it is for is the throws still to come.
            return true;
        }

        public float GetActiveDuration(SkillDefinition definition, int level) =>
            definition != null ? definition.GetValue(level) : 0f;
    }
}
