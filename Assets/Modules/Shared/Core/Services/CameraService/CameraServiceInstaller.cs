using UnityEngine;
using Zenject;

namespace Vesolovsky.Core.Services
{
    public class CameraServiceInstaller : MonoInstaller
    {
        [SerializeField] private MainCamera mainCamera;
        [SerializeField] private CameraPanSettings panSettings = new CameraPanSettings();
        [SerializeField] private CameraLookSettings lookSettings = new CameraLookSettings();

        public override void InstallBindings()
        {
            Container.BindInstance(mainCamera).AsSingle();
            Container.BindInstance(panSettings).AsSingle();
            Container.BindInstance(lookSettings).AsSingle();

            Container.BindInterfacesAndSelfTo<CameraService>().AsSingle();
            Container.BindInterfacesTo<CameraControl>().AsSingle();
            Container.BindInterfacesTo<CameraPanController>().AsSingle();
            Container.BindInterfacesTo<CameraLookController>().AsSingle();

            // The camera has to be pointed before the table is asked what the cursor is over, or
            // the highlight always answers for where the player was aiming last frame.
            Container.BindExecutionOrder<CameraLookController>(-20);
            Container.BindExecutionOrder<CameraPanController>(-20);
        }
    }
}
