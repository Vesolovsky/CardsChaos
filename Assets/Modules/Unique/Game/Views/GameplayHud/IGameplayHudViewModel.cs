using System;
using CardsChaos.Cards;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.Upgrades;
using Vesolovsky.Game.Views.GameplayHud;

namespace Vesolovsky.Game.Views
{
    /// <summary>
    /// The HUD's read side of the room: the hand it counts, the skills it fires and watches cool
    /// down, and the two screens it opens. It holds no view state of its own - showing the hint,
    /// sliding the labels and drawing the cooldown rings is the view's business.
    /// </summary>
    public interface IGameplayHudViewModel : IViewModel
    {
        /// <summary>The hand the counter reads and the switch button flips.</summary>
        CardHand Hand { get; }

        /// <summary>
        /// Raised when any upgrade's level changes, so the HUD can re-check which skills the player
        /// now owns and show or hide their buttons.
        /// </summary>
        event Action SkillsChanged;

        /// <summary>Raised after an input rebind draft is applied to the live action asset.</summary>
        event Action BindingsChanged;

        /// <summary>
        /// Raised each time a skill actually fires. The HUD uses it to arm that skill's "ready" hint,
        /// so the hint only plays once the player has used the skill and its cooldown then ends -
        /// never on entry or unlock.
        /// </summary>
        event Action<SkillId> SkillActivated;

        /// <summary>
        /// Raised when a scene service asks for a HUD hint (the IHudHints channel), so the HUD can
        /// put it on the shared hint queue.
        /// </summary>
        event Action<HintId> HintRaised;

        /// <summary>Whether the "Show hints" setting is on. Defaults to true when there is no settings service.</summary>
        bool HintsEnabled { get; }

        /// <summary>Raised after settings are applied, so the HUD can re-read <see cref="HintsEnabled"/>.</summary>
        event Action HintsEnabledChanged;

        void ToggleAlbum();

        void ToggleUpgrades();

        void ToggleHandLayout();

        /// <summary>Whether the player has bought the skill at all - level one or higher.</summary>
        bool IsSkillOwned(SkillId id);

        /// <summary>Whether the skill is owned and off cooldown right now.</summary>
        bool IsSkillReady(SkillId id);

        /// <summary>
        /// Whether a timed skill is switched on right now - Muscle Memory doing its work rather
        /// than waiting to be cast again. Always false for the skills that act in one go.
        /// </summary>
        bool IsSkillActive(SkillId id);

        /// <summary>Seconds the skill stays switched on for, or zero when it is not.</summary>
        float GetSkillActiveRemaining(SkillId id);

        /// <summary>
        /// Seconds until the skill can be fired again - a spell still running plus the cooldown
        /// behind it. Zero when ready.
        /// </summary>
        float GetSkillCooldownRemaining(SkillId id);

        /// <summary>
        /// The wind-down of the phase the skill is in, as 1..0 of that phase's length. A timed
        /// skill sweeps this twice - once as it works, once as it recovers - so the ring shows the
        /// player how long the skill has left before it shows how long the wait has left.
        /// </summary>
        float GetSkillCooldownNormalized(SkillId id);

        /// <summary>The skill's trigger key as display text, for the bracketed part of its hint.</summary>
        string GetSkillKeyDisplay(SkillId id);

        /// <summary>
        /// Whether the skill's HUD button should pulse to say now is a good moment to use it. Only
        /// Levitate does: it returns true while the skill is ready and set-mates of the selected
        /// card are nearby. Cheap to poll every frame.
        /// </summary>
        bool ShouldPulseSkill(SkillId id);

        /// <summary>A gameplay action's key as display text, for a HUD hint. Names in GameInputActions.</summary>
        string GetActionKeyDisplay(string actionName);

        /// <summary>Fires the skill if it can be fired; a no-op otherwise.</summary>
        void TryActivateSkill(SkillId id);
    }
}
