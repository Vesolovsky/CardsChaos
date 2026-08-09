using System;
using CardsChaos.Cards;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services.Input;
using Vesolovsky.Core.Services.Settings;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.Services.Hud;
using Vesolovsky.Game.Services.Skills;
using Vesolovsky.Game.Services.Upgrades;
using Vesolovsky.Game.Upgrades;
using Vesolovsky.Game.Views.GameplayHud;
using Zenject;

namespace Vesolovsky.Game.Views
{
    /// <summary>
    /// The HUD's link to the room's services. It counts nothing and animates nothing - it hands the
    /// view the hand to read, forwards a fire to the skill service and an open to the panel channel,
    /// and answers what a skill's state is so the view can draw it.
    ///
    /// Everything it needs is bound on the scene above it - the hand, the skills, the upgrade record
    /// - so the HUD stays a thin face over systems that already exist. The panel channel is optional
    /// so the HUD still builds in a scene without the upgrade system wired in.
    /// </summary>
    public class GameplayHudViewModel : ViewModel, IGameplayHudViewModel
    {
        private readonly CardHand _hand;
        private readonly ISkillService _skills;
        private readonly IUpgradeService _upgrades;
        private readonly UpgradeCatalog _catalog;
        private readonly IGameplayPanels _panels;
        private readonly IHudHints _hudHints;
        private readonly IInputActions _input;
        private readonly IGameSettingsService _settings;
        private readonly ILevitateTargeting _levitateTargeting;
        private readonly OneTimeUpgradeDefinition _levitatePulse;

        public event Action SkillsChanged;
        public event Action BindingsChanged;
        public event Action HintsEnabledChanged;
        public event Action<SkillId> SkillActivated;
        public event Action<HintId> HintRaised;

        public CardHand Hand => _hand;

        // No settings service (a bare test scene, say) means hints are on - the same default the
        // settings themselves carry.
        public bool HintsEnabled => _settings == null || _settings.Current.ShowHints;

        [Inject]
        public GameplayHudViewModel(
            CardHand hand,
            ISkillService skills,
            IUpgradeService upgrades,
            UpgradeCatalog catalog,
            [InjectOptional] IGameplayPanels panels,
            [InjectOptional] IHudHints hudHints,
            [InjectOptional] IInputActions input,
            [InjectOptional] IGameSettingsService settings,
            [InjectOptional] ILevitateTargeting levitateTargeting)
        {
            _hand = hand;
            _skills = skills;
            _upgrades = upgrades;
            _catalog = catalog;
            _panels = panels;
            _hudHints = hudHints;
            _input = input;
            _settings = settings;
            _levitateTargeting = levitateTargeting;
            _levitatePulse = catalog != null ? catalog.FindOneTime(OneTimeUpgradeKind.LevitatePulse) : null;

            _upgrades.Changed += OnUpgradesChanged;
            _skills.Activated += OnSkillActivated;

            if (_hudHints != null)
                _hudHints.Raised += OnHintRaised;

            if (_input != null)
                _input.BindingsChanged += OnBindingsChanged;

            if (_settings != null)
                _settings.Applied += OnSettingsApplied;
        }

        public void ToggleAlbum() => _panels?.ToggleAlbum();

        public void ToggleUpgrades() => _panels?.ToggleUpgrades();

        public void ToggleHandLayout() => _hand.ToggleLayout();

        // Owned means unlocked either way it can be: bought, or - for Levitate - its task claimed.
        // The skill service is the one place that folds those two together, so ask it rather than
        // read the level here, which would miss a task-unlocked skill.
        public bool IsSkillOwned(SkillId id) => _skills.IsUnlocked(id);

        public bool IsSkillReady(SkillId id) => _skills.IsReady(id);

        public bool ShouldPulseSkill(SkillId id)
        {
            // Only Levitate pulses, only once the "They sense more..." reward is owned, only while
            // the skill is actually ready to fire, and only when there is something near to raise.
            if (id != SkillId.Levitate || _levitatePulse == null || _levitateTargeting == null)
                return false;

            return _upgrades.IsUnlocked(_levitatePulse)
                   && _skills.IsReady(id)
                   && _levitateTargeting.HasTargets();
        }

        public float GetSkillCooldownRemaining(SkillId id) => _skills.GetCooldownRemaining(id);

        public float GetSkillCooldownNormalized(SkillId id) => _skills.GetCooldownNormalized(id);

        public string GetSkillKeyDisplay(SkillId id)
        {
            SkillDefinition definition = _catalog.FindSkill(id);
            InputActionReference reference = definition != null ? definition.ActivationAction : null;
            InputAction action = reference != null ? reference.action : null;

            if (action == null)
                return "?";

            string display = action.GetBindingDisplayString().ToUpperInvariant();
            return string.IsNullOrWhiteSpace(display) ? "-" : display;
        }

        public string GetActionKeyDisplay(string actionName) =>
            _input != null ? _input.Display(actionName) : "?";

        public void TryActivateSkill(SkillId id) => _skills.TryActivate(id);

        public override void Dispose()
        {
            _upgrades.Changed -= OnUpgradesChanged;
            _skills.Activated -= OnSkillActivated;

            if (_hudHints != null)
                _hudHints.Raised -= OnHintRaised;

            if (_input != null)
                _input.BindingsChanged -= OnBindingsChanged;

            if (_settings != null)
                _settings.Applied -= OnSettingsApplied;

            base.Dispose();
        }

        // The upgrade service reports the definition that changed (or null for "assume all"); the
        // HUD only cares that something moved, so it re-reads every skill's owned state.
        private void OnUpgradesChanged(UpgradeDefinition _) => SkillsChanged?.Invoke();

        private void OnSkillActivated(SkillId id) => SkillActivated?.Invoke(id);

        private void OnHintRaised(HintId id) => HintRaised?.Invoke(id);

        private void OnBindingsChanged() => BindingsChanged?.Invoke();

        private void OnSettingsApplied(GameSettingsData _) => HintsEnabledChanged?.Invoke();
    }
}
