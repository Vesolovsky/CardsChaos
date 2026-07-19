using System;
using UniRx;

namespace Vesolovsky.Core.Services.Wallet
{
    public interface IWalletService
    {
        public event Action<CurrencyType, long> RealCurrencyChanged;

        public IReadOnlyReactiveProperty<long> GetDisplayCurrency(CurrencyType type);
        public long GetRealCurrencyBalance(CurrencyType type);

        public void AddDisplayedCurrency(CurrencyType type, long delta);
        public void SetDisplayedCurrency(CurrencyType type, long value);

        public void AddRealCurrency(CurrencyType type, long delta, bool syncDisplayed = true);
        public void SetRealCurrency(CurrencyType type, long value, bool syncDisplayed = true);

        /// <summary>
        /// Synchronize the displayed currency value with its actual (real) value.
        /// </summary>
        public void SyncDisplayedCurrency(CurrencyType type);
    }
}
