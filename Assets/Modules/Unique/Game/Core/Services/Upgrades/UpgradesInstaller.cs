using UnityEngine;
using Vesolovsky.Game.Services.Hud;
using Vesolovsky.Game.Services.Pause;
using Vesolovsky.Game.Services.Progress;
using Vesolovsky.Game.Services.Save;
using Vesolovsky.Game.Services.Skills;
using Vesolovsky.Game.Services.Stats;
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

        [Tooltip("Tuning for the Levitate skill - reach, rise, hover time, the pulse's poll rate.")]
        [SerializeField] private LevitateSettings levitateSettings;

        [Tooltip("Prints the running player-stat tally to the console for validation. " +
                 "Leave off for release builds.")]
        [SerializeField] private bool logPlayerStats;

        public override void InstallBindings()
        {
            if (catalog == null)
                Debug.LogError($"[{nameof(UpgradesInstaller)}] No {nameof(UpgradeCatalog)} assigned.", this);

            if (levitateSettings == null)
                Debug.LogError($"[{nameof(UpgradesInstaller)}] No {nameof(LevitateSettings)} assigned.", this);

            Container.Bind<UpgradeCatalog>().FromInstance(catalog).AsSingle();
            Container.Bind<LevitateSettings>().FromInstance(levitateSettings).AsSingle();

            // Collection progress before the service that reads it - and NonLazy, because it has to
            // be listening to the album from the start rather than waiting for something to ask.
            Container.BindInterfacesAndSelfTo<CollectionProgress>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<UpgradeService>().AsSingle();

            // Pays out skill points for finished pages; NonLazy so it subscribes without prompting.
            Container.BindInterfacesAndSelfTo<SkillPointRewarder>().AsSingle().NonLazy();

            Container.Bind<IAlbumSetOrder>().To<AlbumSetOrder>().AsSingle();
            Container.Bind<IAlbumFocusRequest>().To<AlbumFocusRequest>().AsSingle();

            // The HUD's channel to open the album and upgrades screens. Bound here beside the
            // album focus request because both are scene-level lines between the HUD or a skill and
            // a screen that lives in its own context.
            Container.Bind<IGameplayPanels>().To<GameplayPanels>().AsSingle();

            // Read by the skill input, set by the upgrades view while it is open.
            Container.Bind<ISkillGate>().To<SkillGate>().AsSingle();

            // How the cooldown-reduction rewards shorten a skill's cooldown, and who can be levitated
            // for the skill and its HUD pulse. Both are read live where they matter.
            Container.Bind<ISkillCooldownModifiers>().To<SkillCooldownModifiers>().AsSingle();
            Container.Bind<ILevitateTargeting>().To<LevitateTargeting>().AsSingle();

            // "Is the clock stopped" - set by the pause menu, read by anything that runs on game
            // time (currently the skill cooldowns).
            Container.Bind<IPauseState>().To<PauseState>().AsSingle();

            // Each handler is registered as an ISkillHandler; the service takes them as a list.
            Container.BindInterfacesAndSelfTo<CardMagnetSkill>().AsSingle();
            Container.BindInterfacesAndSelfTo<SmartAlbumOpenSkill>().AsSingle();
            Container.BindInterfacesAndSelfTo<HandSortSkill>().AsSingle();
            Container.BindInterfacesAndSelfTo<LevitateSkill>().AsSingle();

            Container.BindInterfacesAndSelfTo<SkillService>().AsSingle();
            Container.BindInterfacesTo<SkillInputController>().AsSingle();

            // Reads the room back out of the save on load and writes it in before every save.
            // NonLazy so its load-time apply runs even though nothing resolves it directly.
            Container.BindInterfacesAndSelfTo<WorldSaveService>().AsSingle().NonLazy();

            // Internal progress tally - cards thrown, distance walked, time played, skills used.
            // NonLazy so it starts its clock and subscribes to the hand, skills and album the moment
            // the scene loads, rather than only when a stats screen first asks for the numbers.
            Container.BindInterfacesAndSelfTo<PlayerStatsService>()
                .AsSingle()
                .WithArguments(logPlayerStats)
                .NonLazy();

            // Passive-effect appliers. NonLazy so they subscribe to the upgrade service up front and
            // are ready for the startup push the bootstrap sends once the save is in.
            Container.BindInterfacesTo<CardSlotUpgradeApplier>().AsSingle().NonLazy();
            Container.BindInterfacesTo<SprintUpgradeApplier>().AsSingle().NonLazy();

            // The one reward that acts rather than is read: it pays out skill points on claim, so it
            // must be listening for that claim from the start.
            Container.BindInterfacesTo<SkillPointGrantApplier>().AsSingle().NonLazy();

            Container.BindInterfacesTo<UpgradeEffectsBootstrap>().AsSingle().NonLazy();
        }
    }
}
