using System;
using Vesolovsky.Core.Services;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Upgrades
{
    /// <summary>
    /// Keeps how fast the player walks in step with the Move Speed upgrade.
    ///
    /// The base pace stays where it was authored, on the camera's pan settings: level 0 leaves it
    /// untouched and each bought level replaces it with that level's own speed. Reading the base
    /// off the camera is what lets the upgrade's numbers be plain walking speeds rather than
    /// multipliers that have to know what they are multiplying - the same shape as the Extra Card
    /// Slot and Wider Vision appliers.
    ///
    /// Sprinting is untouched: it scales whatever the current walking speed is, so a bought level
    /// speeds up the walk and the sprint together.
    /// </summary>
    public class MoveSpeedUpgradeApplier : IInitializable, IDisposable
    {
        private readonly CameraPanController _camera;
        private readonly IUpgradeService _upgrades;
        private readonly PermanentUpgradeDefinition _definition;

        [Inject]
        public MoveSpeedUpgradeApplier(
            CameraPanController camera, IUpgradeService upgrades, UpgradeCatalog catalog)
        {
            _camera = camera;
            _upgrades = upgrades;
            _definition = catalog.FindPermanent(PermanentUpgradeKind.MoveSpeed);
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
            if (_definition == null)
                return;

            int level = _upgrades.GetLevel(_definition);
            _camera.Speed = level <= 0 ? _camera.BaseSpeed : _definition.GetValue(level);
        }
    }
}
