using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Vesolovsky.Core.UISystem.Init;
using UniRx;
using UnityEngine;
using Zenject;

namespace Vesolovsky.Core.Services.Wallet
{
    public class WalletService : IWalletService, IAsyncInitializable
    {
        public event Action<CurrencyType, long> RealCurrencyChanged;

        private readonly Dictionary<CurrencyType, IReactiveProperty<long>> _displayedCurrencies;
        private readonly IWallet _wallet;

        [Inject]
        public WalletService(IWallet wallet)
        {
            _wallet = wallet;
            _displayedCurrencies = new Dictionary<CurrencyType, IReactiveProperty<long>>();
        }

        /// <summary>
        /// Returns a completed task: the wallet reads from already loaded save data.
        /// The signature stays async to satisfy <see cref="IAsyncInitializable"/>.
        /// </summary>
        public UniTask Initialize()
        {
            var currencyTypes = _wallet.FetchAllCurrencyTypes();

            foreach (var type in currencyTypes)
            {
                var balance = _wallet.FetchBalance(type);
                _displayedCurrencies[type] = new ReactiveProperty<long>(balance);
            }

            return UniTask.CompletedTask;
        }

        public IReadOnlyReactiveProperty<long> GetDisplayCurrency(CurrencyType type)
        {
            if (!_displayedCurrencies.ContainsKey(type))
            {
                Debug.LogError($"Currency type: '{type}' does not exist in displayed currencies.");
                return null;
            }

            return _displayedCurrencies[type];
        }

        public long GetRealCurrencyBalance(CurrencyType type)
        {
            var balance = _wallet.FetchBalance(type);

            if (balance == -1)
            {
                Debug.LogError($"Failed to fetch balance for currency type: '{type}'.");
            }

            return balance;
        }

        public void AddDisplayedCurrency(CurrencyType type, long delta)
        {
            if (!_displayedCurrencies.ContainsKey(type))
            {
                Debug.LogError($"Cannot add to displayed currency. Currency type: '{type}' does not exist.");
                return;
            }

            var newValue = _displayedCurrencies[type].Value + delta;
            var clampedNewValue = newValue < 0 ? 0 : newValue;

            _displayedCurrencies[type].Value = clampedNewValue;
        }

        public void SetDisplayedCurrency(CurrencyType type, long value)
        {
            if (!_displayedCurrencies.ContainsKey(type))
            {
                Debug.LogError($"Cannot set displayed currency. Currency type: '{type}' does not exist.");
                return;
            }

            _displayedCurrencies[type].Value = value;
        }

        public void AddRealCurrency(CurrencyType type, long delta, bool syncDisplayed = true)
        {
            var newBalance = _wallet.AddDelta(type, delta);

            if (syncDisplayed)
            {
                AddDisplayedCurrency(type, delta);
            }
            RealCurrencyChanged?.Invoke(type, newBalance);
        }

        public void SetRealCurrency(CurrencyType type, long value, bool syncDisplayed = true)
        {
            _wallet.SetBalance(type, value);

            if (syncDisplayed)
            {
                SetDisplayedCurrency(type, value);
            }
            RealCurrencyChanged?.Invoke(type, value);
        }

        public void SyncDisplayedCurrency(CurrencyType type)
        {
            if (!_displayedCurrencies.ContainsKey(type))
            {
                Debug.LogError($"Cannot sync displayed currency. Currency type: '{type}' does not exist.");
                return;
            }

            var realBalance = _wallet.FetchBalance(type);

            if (realBalance == -1)
            {
                Debug.LogError($"Failed to sync displayed currency. Could not fetch real balance for currency type: '{type}'.");
                return;
            }

            _displayedCurrencies[type].Value = realBalance;
        }
    }
}
