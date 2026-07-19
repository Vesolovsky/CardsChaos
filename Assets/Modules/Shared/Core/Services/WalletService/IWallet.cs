using System.Collections.Generic;

namespace Vesolovsky.Core.Services.Wallet
{
    /// <summary>
    /// Interface for a real wallet. Backed by local save data.
    /// </summary>
    public interface IWallet
    {
        public List<CurrencyType> FetchAllCurrencyTypes();
        public long FetchBalance(CurrencyType type);
        public void SetBalance(CurrencyType type, long value);
        public long AddDelta(CurrencyType type, long value);
    }
}
