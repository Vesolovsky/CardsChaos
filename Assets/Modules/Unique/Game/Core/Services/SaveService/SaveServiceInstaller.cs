using Vesolovsky.Core.Services.Save;
using Vesolovsky.Game.Services.Save;
using Zenject;

namespace Vesolovsky.Core.Services
{
    public class SaveServiceInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<GameSaveService>().AsSingle();
            Container.BindInterfacesAndSelfTo<SaveCoordinator<GameSave>>().AsSingle();
        }
    }
}
