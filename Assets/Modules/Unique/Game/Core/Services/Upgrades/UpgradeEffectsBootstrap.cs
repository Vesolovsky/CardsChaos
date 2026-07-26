using System;
using Vesolovsky.Core.UISystem.Init;
using Vesolovsky.Game.Services.Progress;
using Zenject;

namespace Vesolovsky.Game.Services.Upgrades
{
    /// <summary>
    /// Pushes the loaded upgrade state onto the world once, at the right moment.
    ///
    /// The passive effects - slot count, view radius, sprint - are applied by listening for
    /// <see cref="IUpgradeService.Changed"/>, but nothing raises it at startup. Worse, the state
    /// lives in the save, which is filled in by an async pass that finishes a frame or two after
    /// the container is built, so applying too early would apply nothing. This waits on the
    /// context initializator - the same signal the album and wallet wait on - and only then asks
    /// the service to announce itself, by which point the save is in and the appliers are already
    /// listening.
    /// </summary>
    public class UpgradeEffectsBootstrap : IInitializable, IDisposable
    {
        private readonly IUpgradeService _upgrades;
        private readonly ICollectionProgress _progress;
        private readonly IContextInitializator _contextInitializator;

        private bool _applied;

        [Inject]
        public UpgradeEffectsBootstrap(
            IUpgradeService upgrades,
            ICollectionProgress progress,
            [InjectOptional] IContextInitializator contextInitializator)
        {
            _upgrades = upgrades;
            _progress = progress;
            _contextInitializator = contextInitializator;
        }

        public void Initialize()
        {
            // No initializator to wait on (or it has already finished): the save is either in, or
            // there is no async load in this setup, so applying now is the best that can be done.
            if (_contextInitializator == null || _contextInitializator.InitializeCompleted)
            {
                Apply();
                return;
            }

            _contextInitializator.Initialized += Apply;
        }

        public void Dispose()
        {
            if (_contextInitializator != null)
                _contextInitializator.Initialized -= Apply;
        }

        private void Apply()
        {
            if (_applied)
                return;

            _applied = true;

            // Touching the tally seeds it from the loaded save before anything can complete a page,
            // so a save made before this feature does not pay out for pages finished back then.
            _ = _progress.CompletedPageCount;

            _upgrades.Refresh();
        }
    }
}
