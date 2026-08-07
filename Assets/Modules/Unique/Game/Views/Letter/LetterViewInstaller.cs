using Vesolovsky.Core.UISystem.Animations;
using Zenject;

namespace Vesolovsky.Game.Views
{ 
    public class LetterViewInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<LetterViewModel>().AsSingle();
            Container.BindInterfacesAndSelfTo<LetterView>().FromComponentInHierarchy().AsSingle();
            Container.Bind<IViewAnimation>().FromComponentInHierarchy().AsSingle();
        }
    }
}