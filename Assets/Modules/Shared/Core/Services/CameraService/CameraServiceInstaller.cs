using UnityEngine;
using Zenject;

namespace Vesolovsky.Core.Services
{
    public class CameraServiceInstaller : MonoInstaller
    {
        [SerializeField] private MainCamera mainCamera;
        [SerializeField] private CameraPanSettings panSettings = new CameraPanSettings();

        public override void InstallBindings()
        {
            Container.BindInstance(mainCamera).AsSingle();
            Container.BindInstance(panSettings).AsSingle();

            Container.BindInterfacesAndSelfTo<CameraService>().AsSingle();
            Container.BindInterfacesTo<CameraPanController>().AsSingle();
        }
    }
}
