using Zenject;

namespace Vesolovsky.Core.Audio
{
    public class AudioInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<NullAudioService>().AsSingle();
        }
    }
}
