using System;
using Vesolovsky.Core.Services.Wallet;
using Vesolovsky.Game.Services.Progress;
using Zenject;

namespace Vesolovsky.Game.Services.Upgrades
{
    /// <summary>
    /// Pays a skill point into the wallet each time the player completes an album page.
    ///
    /// It leans entirely on <see cref="ICollectionProgress"/> for the "once only" rule: that
    /// service raises <see cref="ICollectionProgress.PageCompleted"/> exactly once per page ever,
    /// so there is nothing to de-duplicate here - a page finished, emptied and finished again is a
    /// single event and a single point.
    /// </summary>
    public class SkillPointRewarder : IInitializable, IDisposable
    {
        private const int PointsPerPage = 1;

        private readonly ICollectionProgress _progress;
        private readonly IWalletService _wallet;

        [Inject]
        public SkillPointRewarder(ICollectionProgress progress, IWalletService wallet)
        {
            _progress = progress;
            _wallet = wallet;
        }

        public void Initialize()
        {
            _progress.PageCompleted += OnPageCompleted;
        }

        public void Dispose()
        {
            _progress.PageCompleted -= OnPageCompleted;
        }

        private void OnPageCompleted(string setId, int pageIndex)
        {
            _wallet.AddRealCurrency(CurrencyType.SkillPoints, PointsPerPage);
        }
    }
}
