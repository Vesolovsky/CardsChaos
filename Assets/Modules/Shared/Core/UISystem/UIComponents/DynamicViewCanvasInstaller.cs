using UnityEngine;
using Vesolovsky.Core.UISystem.UIComponents;
using Zenject;

namespace Vesolovsky.Game
{
    //TODO: add to the core
    public class DynamicViewCanvasInstaller : MonoInstaller
    {
        [SerializeField] private DynamicViewsCanvas dynamicViewsCanvas;

        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<DynamicViewsCanvas>()
                .FromInstance(dynamicViewsCanvas)
                .AsSingle();
        }
    }
}
