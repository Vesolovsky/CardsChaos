using UnityEngine.InputSystem;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Skills
{
    /// <summary>
    /// Turns each skill's activation key into a call to fire it.
    ///
    /// It reads the keys straight off every skill definition rather than hard-coding them, so
    /// rebinding a skill is an edit to its asset. Firing goes through the skill service, which is
    /// where locked, cooling-down and out-of-context presses are turned away - this only has to
    /// notice the key.
    ///
    /// Unlike the card table's input, this keeps running while the album is open, because Hand Sort
    /// is meant to work there. The skills that must not fire in the album refuse for themselves.
    /// </summary>
    public class SkillInputController : ITickable
    {
        private readonly UpgradeCatalog _catalog;
        private readonly ISkillService _skills;
        private readonly ISkillGate _gate;

        [Inject]
        public SkillInputController(UpgradeCatalog catalog, ISkillService skills, ISkillGate gate)
        {
            _catalog = catalog;
            _skills = skills;
            _gate = gate;
        }

        public void Tick()
        {
            // A fullscreen panel (the upgrades screen) silences every skill at once, including the
            // ones that otherwise ignore the world lock to stay usable inside the album.
            if (_gate.Blocked)
                return;

            foreach (SkillDefinition definition in _catalog.Skills)
            {
                if (definition == null)
                    continue;

                // The key is read straight off the skill's own action, so rebinding it is an edit to
                // the input asset and nothing here has to know.
                InputAction action = definition.ActivationAction != null
                    ? definition.ActivationAction.action
                    : null;

                if (action != null && action.WasPressedThisFrame())
                    _skills.TryActivate(definition.SkillId);
            }
        }
    }
}
