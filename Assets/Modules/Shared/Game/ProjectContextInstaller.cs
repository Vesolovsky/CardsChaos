using UnityEngine;
using Vesolovsky.Core.Analytics;
using Vesolovsky.Core.Services.Settings;
using Zenject;

namespace Vesolovsky.Game
{
    public class ProjectContextInstaller : MonoInstaller
    {
        [SerializeField] private SceneTransition sceneTransition;

        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<GameSettingsService>()
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<SceneTransition>()
                .FromInstance(sceneTransition)
                .AsSingle();

            SignalBusInstaller.Install(Container);
            //Container.BindInterfacesAndSelfTo<UnityAnalyticsService>().AsSingle();
        }
    }
}
