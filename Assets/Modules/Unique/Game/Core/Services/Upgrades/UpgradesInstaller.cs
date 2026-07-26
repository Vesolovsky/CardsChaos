using UnityEngine;
using Vesolovsky.Game.Services.Progress;
using Vesolovsky.Game.Services.Skills;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Upgrades
{
    /// <summary>
    /// Wires the whole upgrade, progression and skill system into the gameplay scene.
    ///
    /// It belongs on the scene context: the effects reach into scene objects - the hand, the
    /// camera - while the state they read lives in the save, which the scene container resolves
    /// from its parent. The one thing to author is the catalog reference; everything else is code.
    /// </summary>
    public class UpgradesInstaller : MonoInstaller
    {
        [SerializeField] private UpgradeCatalog catalog;

        public override void InstallBindings()
        {
            if (catalog == null)
                Debug.LogError($"[{nameof(UpgradesInstaller)}] No {nameof(UpgradeCatalog)} assigned.", this);

            Container.Bind<UpgradeCatalog>().FromInstance(catalog).AsSingle();

            // Collection progress before the service that reads it - and NonLazy, because it has to
            // be listening to the album from the start rather than waiting for something to ask.
            Container.BindInterfacesAndSelfTo<CollectionProgress>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<UpgradeService>().AsSingle();

            // Pays out skill points for finished pages; NonLazy so it subscribes without prompting.
            Container.BindInterfacesAndSelfTo<SkillPointRewarder>().AsSingle().NonLazy();

            Container.Bind<IAlbumSetOrder>().To<AlbumSetOrder>().AsSingle();
            Container.Bind<IAlbumFocusRequest>().To<AlbumFocusRequest>().AsSingle();

            // Read by the skill input, set by the upgrades view while it is open.
            Container.Bind<ISkillGate>().To<SkillGate>().AsSingle();

            // Each handler is registered as an ISkillHandler; the service takes them as a list.
            Container.BindInterfacesAndSelfTo<CardMagnetSkill>().AsSingle();
            Container.BindInterfacesAndSelfTo<SmartAlbumOpenSkill>().AsSingle();
            Container.BindInterfacesAndSelfTo<HandSortSkill>().AsSingle();

            Container.BindInterfacesAndSelfTo<SkillService>().AsSingle();
            Container.BindInterfacesTo<SkillInputController>().AsSingle();

            // Passive-effect appliers. NonLazy so they subscribe to the upgrade service up front and
            // are ready for the startup push the bootstrap sends once the save is in.
            Container.BindInterfacesTo<CardSlotUpgradeApplier>().AsSingle().NonLazy();
            Container.BindInterfacesTo<SprintUpgradeApplier>().AsSingle().NonLazy();
            Container.BindInterfacesTo<UpgradeEffectsBootstrap>().AsSingle().NonLazy();
        }
    }
}
