using Vesolovsky.Core.UISystem.Animations;
using Zenject;

namespace Vesolovsky.Game.Views
{ 
    public class UpgradesViewInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<UpgradesViewModel>().AsSingle();
            Container.BindInterfacesAndSelfTo<UpgradesView>().FromComponentInHierarchy().AsSingle();
            Container.Bind<IViewAnimation>().FromComponentInHierarchy().AsSingle();
        }
    }
}