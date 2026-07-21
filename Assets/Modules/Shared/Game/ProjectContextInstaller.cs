using UnityEngine;
using Vesolovsky.Core.Analytics;
using Zenject;

namespace Vesolovsky.Game
{
    public class ProjectContextInstaller : MonoInstaller
    {
        [SerializeField] private SceneTransition sceneTransition;

        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<SceneTransition>()
                .FromInstance(sceneTransition)
                .AsSingle();

            SignalBusInstaller.Install(Container);
            //Container.BindInterfacesAndSelfTo<UnityAnalyticsService>().AsSingle();
        }
    }
}
