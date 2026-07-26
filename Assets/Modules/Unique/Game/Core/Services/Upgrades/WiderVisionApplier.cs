using FOW;
using UnityEngine;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Upgrades
{
    /// <summary>
    /// Drives a fog-of-war revealer's view radius from the Wider Vision upgrade.
    ///
    /// Lives on the revealer object in the scene rather than in a plain service because it needs a
    /// direct reference to the <see cref="FogOfWarRevealer"/> to write, which is a scene object the
    /// container does not hold. The revealer's authored radius is the level-0 value, read once so
    /// it can be restored, and each bought level replaces it with that level's radius.
    /// </summary>
    [AddComponentMenu("CardsChaos/Upgrades/Wider Vision Applier")]
    public class WiderVisionApplier : MonoBehaviour
    {
        [Tooltip("The revealer whose ViewRadius this widens. Its authored radius is the starting, " +
                 "un-upgraded view.")]
        [SerializeField] private FogOfWarRevealer revealer;

        [Tooltip("The Wider Vision permanent upgrade definition.")]
        [SerializeField] private PermanentUpgradeDefinition definition;

        private IUpgradeService _upgrades;
        private float _baseRadius;
        private bool _baseCaptured;

        // Injected as a method so the reference is guaranteed set before it is used; the container
        // injects scene components during context startup, well before the upgrade state is pushed.
        [Inject]
        public void Construct(IUpgradeService upgrades)
        {
            _upgrades = upgrades;
            _upgrades.Changed += OnChanged;
        }

        private void OnDestroy()
        {
            if (_upgrades != null)
                _upgrades.Changed -= OnChanged;
        }

        private void OnChanged(UpgradeDefinition changed)
        {
            if (changed == null || changed == definition)
                Apply();
        }

        private void Apply()
        {
            if (revealer == null || definition == null)
                return;

            // Captured on the first push rather than in Awake: nothing changes the radius before
            // then, so this reads the authored value, and injection timing versus Awake need not
            // be reasoned about.
            if (!_baseCaptured)
            {
                _baseRadius = revealer.ViewRadius;
                _baseCaptured = true;
            }

            int level = _upgrades.GetLevel(definition);
            revealer.ViewRadius = level <= 0 ? _baseRadius : definition.GetValue(level);
        }
    }
}
