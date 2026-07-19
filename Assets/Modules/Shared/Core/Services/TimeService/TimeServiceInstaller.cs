using Zenject;

namespace Vesolovsky.Core.Services
{
    public class TimeServiceInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<TimeService>().AsSingle();
        }
    }
}
