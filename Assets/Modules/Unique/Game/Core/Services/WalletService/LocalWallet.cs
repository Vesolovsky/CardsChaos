using System.Collections.Generic;
using System.Linq;
using Vesolovsky.Core.Services.Save;
using UnityEngine;
using Zenject;
using Vesolovsky.Core.Services.Wallet;
using Vesolovsky.Game.Services.Save;

namespace Vesolovsky.Game.Services.Wallet
{
    public class LocalWallet : IWallet
    {
        private Dictionary<CurrencyType, long> Currencies => _saveService.CurrentSave.Currencies;

        private readonly ISaveService<GameSave> _saveService;
        private readonly ISaveCoordinator _saveCoordinator;

        [Inject]
        public LocalWallet(ISaveService<GameSave> saveService, ISaveCoordinator saveCoordinator)
        {
            _saveService = saveService;
            _saveCoordinator = saveCoordinator;
        }

        public List<CurrencyType> FetchAllCurrencyTypes()
        {
            return Currencies.Keys.ToList();
        }

        public long FetchBalance(CurrencyType type)
        {
            if (!Currencies.ContainsKey(type))
            {
                Debug.LogError($"Currency type: '{type}' does not exist in the wallet.");
                return -1L;
            }

            return Currencies[type];
        }

        public void SetBalance(CurrencyType type, long value)
        {
            if (!Currencies.ContainsKey(type))
            {
                Debug.LogError($"Cannot set balance. Currency type: '{type}' does not exist in the wallet.");
                return;
            }

            Currencies[type] = value;
            _saveCoordinator.MarkDirty();
        }

        public long AddDelta(CurrencyType type, long value)
        {
            if (!Currencies.ContainsKey(type))
            {
                Debug.LogError($"Cannot add delta. Currency type {type} does not exist in the wallet.");
                return 0L;
            }

            var newValue = Currencies[type] + value;
            var clampedNewValue = newValue < 0 ? 0 : newValue;
            Currencies[type] = clampedNewValue;
            _saveCoordinator.MarkDirty();
            return clampedNewValue;
        }
    }
}
