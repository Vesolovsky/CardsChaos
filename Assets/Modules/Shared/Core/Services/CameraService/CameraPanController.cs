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

        [Header("Collision")]
        [Tooltip("Radius of the sphere the camera sweeps with. Keep it comfortably above the " +
                 "near clip plane, otherwise a wall the sphere is resting against still pokes " +
                 "through the lens. 0 turns collision off.")]
        public float CollisionRadius = 0.12f;

        [Tooltip("What the camera cannot pass through. Cards in hand are triggers and are never " +
                 "in the way; cards on the floor sit well below the sphere.")]
        public LayerMask CollisionMask = ~0;

        [Tooltip("Gap kept between the sphere and whatever it lands on, so the next sweep never " +
                 "starts flush against the surface.")]
        public float SkinWidth = 0.005f;
    }

    /// <summary>
    /// Walks the main camera around the room on WASD / arrow keys.
    ///
    /// Movement is flattened onto the horizontal plane and follows the camera's own yaw,
    /// so "forward" is whichever way the camera faces rather than a fixed world axis -
    /// otherwise a tilted camera would drive itself into the floor.
    ///
    /// The step is swept as a sphere rather than applied outright, so the camera stops at the
    /// furniture instead of ending up inside a mesh with the room turned inside out.
    /// </summary>
    public class CameraPanController : ITickable
    {
        // Enough to round an inside corner; past that the leftover step is small enough to drop.
        private const int MaxSlides = 3;

        private readonly ICameraService _cameraService;
        private readonly ICameraControl _control;
        private readonly CameraPanSettings _settings;

        private Vector3 _velocity;

        [Inject]
        public CameraPanController(
            ICameraService cameraService, ICameraControl control, CameraPanSettings settings)
        {
            _cameraService = cameraService;
            _control = control;
            _settings = settings;
        }

        public void Tick()
        {
            // Dropping the carried velocity matters: without it the camera would resume
            // drifting the moment control came back.
            if (!_control.Enabled)
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
                pivot.position = Sweep(pivot.position, _velocity * Time.deltaTime);
        }

        /// <summary>
        /// Walks the step with a sphere instead of teleporting the camera, and whenever it lands
        /// on something the leftover distance is carried along the surface rather than dropped -
        /// without that the camera would stick the instant it brushed a wall at an angle.
        /// </summary>
        private Vector3 Sweep(Vector3 position, Vector3 delta)
        {
            float radius = _settings.CollisionRadius;
            if (radius <= 0f)
                return position + delta;

            for (int slide = 0; slide < MaxSlides; slide++)
            {
                float distance = delta.magnitude;
                if (distance <= 0f)
                    break;

                Vector3 direction = delta / distance;

                bool blocked = Physics.SphereCast(position, radius, direction, out RaycastHit hit,
                    distance + _settings.SkinWidth, _settings.CollisionMask,
                    QueryTriggerInteraction.Ignore);

                // A zero distance means the sphere already overlaps the surface, and PhysX leaves
                // no usable normal to slide along there. Letting the step through is the lesser
                // evil - the alternative is a camera sealed inside whatever it clipped into.
                if (!blocked || hit.distance <= 0f)
                {
                    position += delta;
                    break;
                }

                float travelled = Mathf.Max(hit.distance - _settings.SkinWidth, 0f);
                position += direction * travelled;

                // Only the part of the remaining step that runs along the surface survives, and
                // it is flattened again afterwards: sliding along anything that is not perfectly
                // upright would otherwise ramp the eye height, and the player has no way back
                // down.
                delta = Flatten(Vector3.ProjectOnPlane(direction * (distance - travelled), hit.normal));

                // The carried velocity has to lose the blocked component too, or the camera keeps
                // pressing into the wall and judders as every frame re-collides with it.
                _velocity = Flatten(Vector3.ProjectOnPlane(_velocity, hit.normal));
            }

            return position;
        }

        private static Vector3 Flatten(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
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
