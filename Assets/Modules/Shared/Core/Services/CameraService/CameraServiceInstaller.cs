using UnityEngine;
using Zenject;

namespace Vesolovsky.Core.Services
{
    public class CameraServiceInstaller : MonoInstaller
    {
        [SerializeField] private MainCamera mainCamera;

        public override void InstallBindings()
        {
            Container.BindInstance(mainCamera).AsSingle();

            Container.BindInterfacesAndSelfTo<CameraService>().AsSingle();
        }
    }
}
