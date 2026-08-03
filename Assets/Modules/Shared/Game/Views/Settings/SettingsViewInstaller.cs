using Vesolovsky.Core.UISystem.Animations;
using Zenject;

namespace Vesolovsky.Game.Views
{ 
    public class SettingsViewInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<SettingsViewModel>().AsSingle();
            Container.BindInterfacesAndSelfTo<SettingsView>().FromComponentInHierarchy().AsSingle();
            Container.Bind<IViewAnimation>().FromComponentInHierarchy().AsSingle();
        }
    }
}