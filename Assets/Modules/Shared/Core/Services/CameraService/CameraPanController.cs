using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Vesolovsky.Core.Services
{
    [System.Serializable]
    public class CameraPanSettings
    {
        [Tooltip("World units per second at full tilt. The table is small - a card is 0.063 wide.")]
        public float Speed = 0.35f;

        [Tooltip("How sharply the pan eases in and out. Higher is snappier; 0 disables smoothing.")]
        public float Smoothing = 12f;
    }

    /// <summary>
    /// Lets gameplay suspend the camera while something else owns the input, without
    /// having to reach for the controller itself.
    /// </summary>
    public interface ICameraPanControl
    {
        bool Enabled { get; set; }
    }

    /// <summary>
    /// Slides the main camera across the table on WASD / arrow keys.
    ///
    /// Movement is flattened onto the horizontal plane and follows the camera's own yaw,
    /// so "forward" is whichever way the camera faces rather than a fixed world axis -
    /// otherwise a tilted camera would drive itself into the floor.
    /// </summary>
    public class CameraPanController : ITickable, ICameraPanControl
    {
        private readonly ICameraService _cameraService;
        private readonly CameraPanSettings _settings;

        private Vector3 _velocity;

        public bool Enabled { get; set; } = true;

        [Inject]
        public CameraPanController(ICameraService cameraService, CameraPanSettings settings)
        {
            _cameraService = cameraService;
            _settings = settings;
        }

        public void Tick()
        {
            // Dropping the carried velocity matters: without it the camera would resume
            // drifting the moment control came back.
            if (!Enabled)
            {
                _velocity = Vector3.zero;
                return;
            }

            Keyboard keyboard = Keyboard.current;
            Camera camera = _cameraService.MainCamera;

            if (keyboard == null || camera == null)
                return;

            Transform pivot = camera.transform;
            Vector3 target = ReadDirection(keyboard, pivot) * _settings.Speed;

            // Framerate independent exponential approach, so the ease does not change
            // with the refresh rate.
            _velocity = _settings.Smoothing > 0f
                ? Vector3.Lerp(_velocity, target, 1f - Mathf.Exp(-_settings.Smoothing * Time.deltaTime))
                : target;

            if (_velocity.sqrMagnitude > 0f)
                pivot.position += _velocity * Time.deltaTime;
        }

        private static Vector3 ReadDirection(Keyboard keyboard, Transform pivot)
        {
            float x = 0f;
            float z = 0f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                x -= 1f;

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                x += 1f;

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                z -= 1f;

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                z += 1f;

            if (x == 0f && z == 0f)
                return Vector3.zero;

            Vector3 forward = Vector3.ProjectOnPlane(pivot.forward, Vector3.up);

            // Looking straight down leaves nothing of forward on the plane; the top of the
            // screen is then the camera's own up vector.
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(pivot.up, Vector3.up);

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            // Clamped so a diagonal is not faster than a straight line.
            return Vector3.ClampMagnitude(right * x + forward * z, 1f);
        }
    }
}
