using Zenject;
using Vesolovsky.Core.UISystem.Services;
using Vesolovsky.Core.Utils;

namespace Vesolovsky.Core.UISystem
{
    public class UIInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<ViewFactory>().AsSingle();
            Container.BindInterfacesAndSelfTo<SceneViewsService>().AsSingle();
            Container.BindInterfacesAndSelfTo<VPrefabFactory>().AsSingle();
        }
    }
}
