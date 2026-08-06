using System;
using CardsChaos.Cards;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.Upgrades;

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

        /// <summary>Seconds left on the skill's cooldown, or zero when ready.</summary>
        float GetSkillCooldownRemaining(SkillId id);

        /// <summary>Cooldown left as 1..0 of its full length - one when it starts, zero when ready.</summary>
        float GetSkillCooldownNormalized(SkillId id);

        /// <summary>The skill's trigger key as display text, for the bracketed part of its hint.</summary>
        string GetSkillKeyDisplay(SkillId id);

        /// <summary>
        /// Whether the skill's HUD button should pulse to say now is a good moment to use it. Only
        /// the "They sense more..." reward drives this, and only for Levitate: it returns true while
        /// that reward is owned, the skill is ready, and set-mates of the selected card are nearby.
        /// Cheap to poll every frame.
        /// </summary>
        bool ShouldPulseSkill(SkillId id);

        /// <summary>A gameplay action's key as display text, for a HUD hint. Names in GameInputActions.</summary>
        string GetActionKeyDisplay(string actionName);

        /// <summary>Fires the skill if it can be fired; a no-op otherwise.</summary>
        void TryActivateSkill(SkillId id);
    }
}
