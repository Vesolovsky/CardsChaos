using Vesolovsky.Core.UISystem.Animations;
using Zenject;

namespace Vesolovsky.Game.Views
{ 
    public class MainMenuViewInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<MainMenuViewModel>().AsSingle();
            Container.BindInterfacesAndSelfTo<MainMenuView>().FromComponentInHierarchy().AsSingle();
            Container.Bind<IViewAnimation>().FromComponentInHierarchy().AsSingle();
        }
    }
}