using System;
using Vesolovsky.Core.Services.Wallet;
using Vesolovsky.Game.Services.Progress;
using Zenject;

namespace Vesolovsky.Game.Services.Upgrades
{
    /// <summary>
    /// Pays skill points into the wallet as the collection comes together: one for every album
    /// page completed, and a bonus on top for finishing a whole set.
    ///
    /// The card that finishes a set is also the card that finishes that set's last page, so it
    /// pays both at once - one point for the page and the set bonus behind it. That is deliberate:
    /// the last card of a set is the hardest one to find, and it should feel like it.
    ///
    /// It leans entirely on <see cref="ICollectionProgress"/> for the "once only" rule. That
    /// service raises both events exactly once per page and per set ever - a page is recorded
    /// permanently the first time it is correct, and neither event can fire again for something
    /// already on that list - so there is nothing to de-duplicate here. A set emptied and refilled
    /// is a single event and a single payout.
    /// </summary>
    public class SkillPointRewarder : IInitializable, IDisposable
    {
        private const int PointsPerPage = 1;
        private const int PointsPerSet = 2;

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
            _progress.SetCompleted += OnSetCompleted;
        }

        public void Dispose()
        {
            _progress.PageCompleted -= OnPageCompleted;
            _progress.SetCompleted -= OnSetCompleted;
        }

        private void OnPageCompleted(string setId, int pageIndex)
        {
            _wallet.AddRealCurrency(CurrencyType.SkillPoints, PointsPerPage);
        }

        private void OnSetCompleted(string setId)
        {
            _wallet.AddRealCurrency(CurrencyType.SkillPoints, PointsPerSet);
        }
    }
}
