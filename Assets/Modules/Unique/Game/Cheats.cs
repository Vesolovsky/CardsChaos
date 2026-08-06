using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services.Wallet;
using Vesolovsky.Game.Services.Skills;
using Vesolovsky.Game.Services.Upgrades;
using Vesolovsky.Game.Upgrades;
using Zenject;
// F11's bulk-claim complements the per-task "Debug Force Unlock" button on each task row.

namespace Vesolovsky.Game
{
    /// <summary>
    /// Temporary testing helper: F12 drops 100 skill points into the wallet, so upgrades and
    /// skills can be bought without grinding pages; F11 claims every one-time reward (the magnet
    /// bonus, the cooldown cuts, the skill-point payout, Levitate and its pulse) without having to
    /// finish their sets first; Ctrl+F1 clears every skill's running cooldown so they can be fired
    /// again at once. Drop it on any empty object in the scene; it is meant to be pulled back out
    /// before shipping.
    /// </summary>
    [AddComponentMenu("CardsChaos/Debug/Cheats")]
    public class Cheats : MonoBehaviour
    {
        private const int SkillPointsPerPress = 100;

        private IWalletService _wallet;
        private IUpgradeService _upgrades;
        private UpgradeCatalog _catalog;
        private ISkillService _skills;

        [Inject]
        public void Construct(
            IWalletService wallet,
            [InjectOptional] IUpgradeService upgrades,
            [InjectOptional] UpgradeCatalog catalog,
            [InjectOptional] ISkillService skills)
        {
            _wallet = wallet;
            _upgrades = upgrades;
            _catalog = catalog;
            _skills = skills;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.f12Key.wasPressedThisFrame && _wallet != null)
                _wallet.AddRealCurrency(CurrencyType.SkillPoints, SkillPointsPerPress);

            if (keyboard.f11Key.wasPressedThisFrame && _upgrades != null && _catalog != null)
            {
                foreach (OneTimeUpgradeDefinition oneTime in _catalog.OneTimes)
                    _upgrades.DebugForceUnlock(oneTime);
            }

            bool ctrlHeld = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            if (keyboard.f1Key.wasPressedThisFrame && ctrlHeld && _skills != null)
                _skills.DebugResetCooldowns();
        }
    }
}
