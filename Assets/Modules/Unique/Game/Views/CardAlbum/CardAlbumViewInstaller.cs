using Vesolovsky.Core.UISystem.Animations;
using Zenject;

namespace Vesolovsky.Game.Views
{ 
    public class CardAlbumViewInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<CardAlbumViewModel>().AsSingle();
            Container.BindInterfacesAndSelfTo<CardAlbumView>().FromComponentInHierarchy().AsSingle();
            Container.Bind<IViewAnimation>().FromComponentInHierarchy().AsSingle();
        }
    }
}