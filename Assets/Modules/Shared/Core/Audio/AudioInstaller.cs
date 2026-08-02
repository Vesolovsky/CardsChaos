using UnityEngine;
using Zenject;

namespace Vesolovsky.Core.Audio
{
    public class AudioInstaller : MonoInstaller
    {
        [SerializeField] private UnityAudioCatalog audioCatalog;

        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<UnityAudioService>()
                .AsSingle()
                .WithArguments(audioCatalog)
                .NonLazy();
        }
    }
}
