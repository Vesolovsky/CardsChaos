using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services.Wallet;
using Zenject;

namespace Vesolovsky.Game
{
    /// <summary>
    /// Temporary testing helper: F12 drops 100 skill points into the wallet, so upgrades and
    /// skills can be bought without grinding pages. Drop it on any empty object in the scene; it is
    /// meant to be pulled back out before shipping.
    /// </summary>
    [AddComponentMenu("CardsChaos/Debug/Cheats")]
    public class Cheats : MonoBehaviour
    {
        private const int SkillPointsPerPress = 100;

        private IWalletService _wallet;

        [Inject]
        public void Construct(IWalletService wallet) => _wallet = wallet;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _wallet == null)
                return;

            if (keyboard.f12Key.wasPressedThisFrame)
                _wallet.AddRealCurrency(CurrencyType.SkillPoints, SkillPointsPerPress);
        }
    }
}
