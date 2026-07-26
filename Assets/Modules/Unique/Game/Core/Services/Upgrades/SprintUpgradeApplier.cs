using System;
using Vesolovsky.Core.Services;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Upgrades
{
    /// <summary>
    /// Flips the camera's sprint on once the Sprint one-time upgrade is claimed.
    ///
    /// The camera controller only carries a bool; this is the seam that turns it, so the generic
    /// camera code in Core stays unaware that an upgrade is what decides it.
    /// </summary>
    public class SprintUpgradeApplier : IInitializable, IDisposable
    {
        private readonly CameraPanController _camera;
        private readonly IUpgradeService _upgrades;
        private readonly OneTimeUpgradeDefinition _definition;

        [Inject]
        public SprintUpgradeApplier(
            CameraPanController camera, IUpgradeService upgrades, UpgradeCatalog catalog)
        {
            _camera = camera;
            _upgrades = upgrades;
            _definition = catalog.FindOneTime(OneTimeUpgradeKind.Sprint);
        }

        public void Initialize()
        {
            _upgrades.Changed += OnChanged;
        }

        public void Dispose()
        {
            _upgrades.Changed -= OnChanged;
        }

        private void OnChanged(UpgradeDefinition changed)
        {
            if (changed == null || changed == _definition)
                Apply();
        }

        private void Apply()
        {
            _camera.SprintUnlocked = _definition != null && _upgrades.IsUnlocked(_definition);
        }
    }
}
