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

            // Lives here because the camera rig is the thing that most obviously stands down when
            // something takes the room, but it is not the rig's alone - the card table and the
            // album read the same lock, which is the entire point of there being one.
            Container.BindInterfacesTo<WorldInteractionLock>().AsSingle();
            Container.BindInterfacesTo<CameraPanController>().AsSingle();
            Container.BindInterfacesTo<CameraLookController>().AsSingle();

            // The camera has to be pointed before the table is asked what the cursor is over, or
            // the highlight always answers for where the player was aiming last frame.
            Container.BindExecutionOrder<CameraLookController>(-20);
            Container.BindExecutionOrder<CameraPanController>(-20);
        }
    }
}
