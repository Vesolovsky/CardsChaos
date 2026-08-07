using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Audio;
using Vesolovsky.Core.Services.Input;
using Zenject;

namespace Vesolovsky.Core.Services
{
    [System.Serializable]
    public class CameraPanSettings
    {
        [Tooltip("World units per second at full tilt. The table is small - a card is 0.063 wide.")]
        public float Speed = 0.35f;

        [Tooltip("How much faster the camera moves while the Sprint action is held, once the " +
                 "Sprint upgrade is unlocked. 1 is no faster; the upgrade has no effect until this " +
                 "is above 1.")]
        public float SprintMultiplier = 1.8f;

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

        [Header("Footsteps")]
        [Tooltip("World distance walked between footstep sounds. The room is small - a card is " +
                 "0.063 wide - so this is a fraction of a metre; sprinting covers it faster, so " +
                 "the cadence quickens on its own. 0 turns footstep sounds off.")]
        public float FootstepStride = 0.6f;
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
        private readonly IWorldInteractionLock _worldLock;
        private readonly CameraPanSettings _settings;
        private readonly IAudioService _audioService;
        private readonly InputAction _sprint;

        private Vector3 _velocity;

        // Distance walked since the last footstep sound, reset whenever the camera stops so the
        // next step after a pause lands on a fresh stride rather than one begun before the stop.
        private float _footstepDistance;

        /// <summary>
        /// Whether sprinting is available. Off until the game's Sprint upgrade is claimed and an
        /// applier turns it on; kept as a plain flag so the camera - which lives in Core - owes the
        /// upgrade system nothing.
        /// </summary>
        public bool SprintUnlocked { get; set; }

        /// <summary>
        /// Horizontal world-space distance the camera actually moved on the last <see cref="Tick"/> -
        /// what the sweep travelled, so a step stopped short by a wall reports the shorter distance,
        /// and a locked or idle frame reports zero. This is the mover's own account of the frame,
        /// which a stat tracker can total without ever mistaking a load-time teleport (which sets the
        /// position directly, not through here) for walking.
        /// </summary>
        public float LastMoveDistance { get; private set; }

        /// <summary>
        /// True on frames the sprint boost was in effect while the camera was being driven. Lets a
        /// consumer split <see cref="LastMoveDistance"/> into walked and sprinted without knowing
        /// anything about the input or the upgrade.
        /// </summary>
        public bool IsSprinting { get; private set; }

        [Inject]
        public CameraPanController(
            ICameraService cameraService,
            IWorldInteractionLock worldLock,
            CameraPanSettings settings,
            IInputActions input,
            IAudioService audioService)
        {
            _cameraService = cameraService;
            _worldLock = worldLock;
            _settings = settings;
            _audioService = audioService;
            _sprint = input.Find(GameInputActions.Sprint);
        }

        public void Tick()
        {
            // Reset up front so every early return below - locked, no keyboard, no camera - reports
            // a still frame, and only a real sweep writes a non-zero distance.
            LastMoveDistance = 0f;
            IsSprinting = false;

            // Dropping the carried velocity matters: without it the camera would resume
            // drifting the moment control came back.
            if (_worldLock.IsLocked)
            {
                _velocity = Vector3.zero;
                _footstepDistance = 0f;
                return;
            }

            Keyboard keyboard = Keyboard.current;
            Camera camera = _cameraService.MainCamera;

            if (keyboard == null || camera == null)
            {
                _footstepDistance = 0f;
                return;
            }

            Transform pivot = camera.transform;

            // Sprint scales the target speed while its action is held, so the ease still carries the
            // camera up to and down from the faster pace rather than snapping between the two.
            bool sprinting = SprintUnlocked && _sprint != null && _sprint.IsPressed();
            float speed = sprinting ? _settings.Speed * _settings.SprintMultiplier : _settings.Speed;

            Vector3 target = ReadDirection(keyboard, pivot) * speed;

            // Framerate independent exponential approach, so the ease does not change
            // with the refresh rate.
            _velocity = _settings.Smoothing > 0f
                ? Vector3.Lerp(_velocity, target, 1f - Mathf.Exp(-_settings.Smoothing * Time.deltaTime))
                : target;

            if (_velocity.sqrMagnitude > 0f)
            {
                Vector3 before = pivot.position;
                pivot.position = Sweep(pivot.position, _velocity * Time.deltaTime);

                // Measure what the sweep actually covered on the plane, not what was asked for: a
                // step into a wall reports the short travel it managed, and the eye-height axis is
                // dropped so a slide up a ramp is not counted as ground walked.
                Vector3 moved = pivot.position - before;
                moved.y = 0f;
                LastMoveDistance = moved.magnitude;
                IsSprinting = sprinting && LastMoveDistance > 0f;
            }

            AccumulateFootsteps();
        }

        /// <summary>
        /// Totals the ground covered this frame and fires a footstep each time a full stride has
        /// been walked. Sprinting eats the stride faster, so the steps quicken without any special
        /// case here. A frame that covered nothing - stopped, or pressed into a wall - resets the
        /// count so the cadence never resumes mid-stride after a pause.
        /// </summary>
        private void AccumulateFootsteps()
        {
            float stride = _settings.FootstepStride;

            if (stride <= 0f || LastMoveDistance <= 0f)
            {
                _footstepDistance = 0f;
                return;
            }

            _footstepDistance += LastMoveDistance;

            if (_footstepDistance < stride)
                return;

            // One step per frame is plenty: at the room's scale a single frame never spans a whole
            // stride, so carrying the remainder forward keeps the spacing even without a loop that
            // could burst if the stride were ever set very small.
            _footstepDistance -= stride;
            _audioService?.Play(AudioSFXKey.Footstep);
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
