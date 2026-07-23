using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Vesolovsky.Core.Services
{
    [System.Serializable]
    public class CameraLookSettings
    {
        [Tooltip("Degrees turned per pixel of mouse movement while the right button is down.")]
        public float Sensitivity = 0.15f;

        public bool Invert = false;
    }

    /// <summary>
    /// Turns the camera left and right while the right mouse button is held.
    ///
    /// Yaw only. The tilt the camera was posed with in the scene is captured once and reapplied
    /// every frame, and roll is pinned at zero - the room is read at a glance off a floor covered
    /// in cards, and neither a horizon that tips nor a pitch the player has to correct would help
    /// with that.
    ///
    /// The pointer is needed for picking cards, so it is only taken for the length of the drag
    /// and put back exactly where it was let go of.
    /// </summary>
    public class CameraLookController : IInitializable, ITickable
    {
        private readonly ICameraService _cameraService;
        private readonly ICameraControl _control;
        private readonly CameraLookSettings _settings;

        private float _yaw;
        private float _pitch;
        private bool _dragging;
        private Vector2 _restorePosition;

        [Inject]
        public CameraLookController(
            ICameraService cameraService, ICameraControl control, CameraLookSettings settings)
        {
            _cameraService = cameraService;
            _control = control;
            _settings = settings;
        }

        public void Initialize()
        {
            Camera camera = _cameraService.MainCamera;
            if (camera == null)
                return;

            // Whatever tilt the shot was framed with in the editor is the tilt for the whole
            // game; only the heading is the player's to change.
            Vector3 euler = camera.transform.eulerAngles;

            _pitch = euler.x;
            _yaw = euler.y;

            Apply(camera);
        }

        public void Tick()
        {
            Mouse mouse = Mouse.current;
            Camera camera = _cameraService.MainCamera;

            if (mouse == null || camera == null)
                return;

            if (!_control.Enabled)
            {
                EndDrag(mouse);
                return;
            }

            // Only a fresh press starts a drag. Right button also leaves the close-up, and
            // without this the button still being down afterwards would swing the camera round
            // as a parting gift.
            if (mouse.rightButton.wasPressedThisFrame)
                BeginDrag(mouse);
            else if (_dragging && !mouse.rightButton.isPressed)
                EndDrag(mouse);

            if (!_dragging)
                return;

            float delta = mouse.delta.ReadValue().x;
            if (delta == 0f)
                return;

            _yaw += (_settings.Invert ? -delta : delta) * _settings.Sensitivity;

            Apply(camera);
        }

        private void Apply(Camera camera)
        {
            camera.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void BeginDrag(Mouse mouse)
        {
            if (_dragging)
                return;

            _restorePosition = mouse.position.ReadValue();
            _dragging = true;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void EndDrag(Mouse mouse)
        {
            if (!_dragging)
                return;

            _dragging = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Unlocking drops the pointer in the middle of the screen. Put it back on the card
            // the player was about to click before they decided to look around first.
            mouse.WarpCursorPosition(_restorePosition);
        }
    }
}
