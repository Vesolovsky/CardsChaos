using System;
using CardsChaos.Cards;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services.Input;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.Services.Hud;
using Vesolovsky.Game.Services.Skills;
using Vesolovsky.Game.Services.Upgrades;
using Vesolovsky.Game.Upgrades;
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
        private readonly IInputActions _input;

        public event Action SkillsChanged;
        public event Action BindingsChanged;

        public CardHand Hand => _hand;

        [Inject]
        public GameplayHudViewModel(
            CardHand hand,
            ISkillService skills,
            IUpgradeService upgrades,
            UpgradeCatalog catalog,
            [InjectOptional] IGameplayPanels panels,
            [InjectOptional] IInputActions input)
        {
            _hand = hand;
            _skills = skills;
            _upgrades = upgrades;
            _catalog = catalog;
            _panels = panels;
            _input = input;

            _upgrades.Changed += OnUpgradesChanged;

            if (_input != null)
                _input.BindingsChanged += OnBindingsChanged;
        }

        public void ToggleAlbum() => _panels?.ToggleAlbum();

        public void ToggleUpgrades() => _panels?.ToggleUpgrades();

        public void ToggleHandLayout() => _hand.ToggleLayout();

        public bool IsSkillOwned(SkillId id)
        {
            SkillDefinition definition = _catalog.FindSkill(id);
            return definition != null && _upgrades.GetLevel(definition) > 0;
        }

        public bool IsSkillReady(SkillId id) => _skills.IsReady(id);

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

            if (_input != null)
                _input.BindingsChanged -= OnBindingsChanged;

            base.Dispose();
        }

        // The upgrade service reports the definition that changed (or null for "assume all"); the
        // HUD only cares that something moved, so it re-reads every skill's owned state.
        private void OnUpgradesChanged(UpgradeDefinition _) => SkillsChanged?.Invoke();

        private void OnBindingsChanged() => BindingsChanged?.Invoke();
    }
}
