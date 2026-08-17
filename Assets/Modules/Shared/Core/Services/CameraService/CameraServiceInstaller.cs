using UnityEngine;
using Zenject;

namespace Vesolovsky.Core.Services
{
    public class CameraServiceInstaller : MonoInstaller
    {
        [SerializeField] private MainCamera mainCamera;
        [SerializeField] private CameraPanSettings panSettings = new CameraPanSettings();
        [SerializeField] private CameraLookSettings lookSettings = new CameraLookSettings();
        [SerializeField] private CameraZoomSettings zoomSettings = new CameraZoomSettings();

        public override void InstallBindings()
        {
            Container.BindInstance(mainCamera).AsSingle();
            Container.BindInstance(panSettings).AsSingle();
            Container.BindInstance(lookSettings).AsSingle();
            Container.BindInstance(zoomSettings).AsSingle();

            Container.BindInterfacesAndSelfTo<CameraService>().AsSingle();

            // Lives here because the camera rig is the thing that most obviously stands down when
            // something takes the room, but it is not the rig's alone - the card table and the
            // album read the same lock, which is the entire point of there being one.
            Container.BindInterfacesTo<WorldInteractionLock>().AsSingle();

            // AndSelf so the Sprint upgrade's applier can reach the concrete controller to flip its
            // sprint flag; the interface binding still drives the tick.
            Container.BindInterfacesAndSelfTo<CameraPanController>().AsSingle();
            Container.BindInterfacesTo<CameraLookController>().AsSingle();
            Container.BindInterfacesTo<CameraZoomController>().AsSingle();

            // The camera has to be pointed before the table is asked what the cursor is over, or
            // the highlight always answers for where the player was aiming last frame. The zoom
            // goes first of the three: it sets the field of view the look controller reads to slow
            // itself down, and the one the cursor's pick ray is cast through.
            Container.BindExecutionOrder<CameraZoomController>(-21);
            Container.BindExecutionOrder<CameraLookController>(-20);
            Container.BindExecutionOrder<CameraPanController>(-20);
        }
    }
}
