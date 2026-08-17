using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services.Input;
using Zenject;

namespace Vesolovsky.Core.Services
{
    [System.Serializable]
    public class CameraZoomSettings
    {
        [Tooltip("Field of view while the Zoom action is held. Lower is closer in; the camera's " +
                 "own authored field of view is what it eases back to, so the base lives on the " +
                 "camera and is not repeated here.")]
        public float ZoomedFieldOfView = 30f;

        [Tooltip("How sharply the view eases in and out of the zoom. Higher is snappier; 0 snaps " +
                 "with no ease at all.")]
        public float Smoothing = 12f;

        [Tooltip("The slowest the mouse is allowed to get while zoomed, as a fraction of its " +
                 "normal turn. A guard for a very aggressive zoom, so the camera can never feel " +
                 "stuck however narrow the view is set.")]
        [Range(0.05f, 1f)]
        public float MinLookScale = 0.2f;
    }

    /// <summary>
    /// Narrows the view while the Zoom action is held, so a card can be read from across the room.
    ///
    /// Hold to zoom, let go to come back - the same shape as Sprint, and for the same reason: the
    /// player should never be able to walk away and forget the room is magnified. The whole camera
    /// rig zooms together, the held-cards overlay included, because that overlay draws the cards in
    /// hand over the room and the cursor picks them through the main camera's ray - two different
    /// fields of view would have the hand drifting away from where it can actually be clicked.
    ///
    /// While the album, a close-up or a panel holds the room the zoom eases back out and stays
    /// there; and once it is fully out the controller stops writing the field of view at all, so
    /// nothing else that poses the camera has to fight a controller that is doing nothing.
    /// </summary>
    public class CameraZoomController : IInitializable, ITickable, ICameraZoom
    {
        // Below this the ease is close enough to be called arrived; without it the exponential
        // approach never quite reaches its target and the controller would write the field of view
        // to every camera for the rest of the session.
        private const float RestEpsilon = 0.001f;

        private readonly ICameraService _cameraService;
        private readonly IWorldInteractionLock _worldLock;
        private readonly CameraZoomSettings _settings;
        private readonly InputAction _zoom;

        private Camera[] _cameras = Array.Empty<Camera>();
        private float _baseFieldOfView;

        // How far in the view is, 0 out and 1 fully zoomed. The eased value rather than the key's
        // state, so everything reading the zoom sees the view that is actually being drawn.
        private float _amount;

        // Whether the last frame left the cameras on a zoomed field of view. Lets the idle case
        // return without touching them, and guarantees the one write that puts them back.
        private bool _applied;

        [Inject]
        public CameraZoomController(
            ICameraService cameraService,
            IWorldInteractionLock worldLock,
            CameraZoomSettings settings,
            IInputActions input)
        {
            _cameraService = cameraService;
            _worldLock = worldLock;
            _settings = settings;
            _zoom = input.Find(GameInputActions.Zoom);
        }

        public float LookScale
        {
            get
            {
                if (_baseFieldOfView <= 0f)
                    return 1f;

                float scale = CurrentFieldOfView / _baseFieldOfView;
                return Mathf.Clamp(scale, Mathf.Clamp01(_settings.MinLookScale), 1f);
            }
        }

        public void Initialize()
        {
            Camera camera = _cameraService.MainCamera;
            if (camera == null)
                return;

            // The rig, not just the one camera: the held-cards overlay is a child camera drawing on
            // top of this one, and it has to zoom in step. Collected once - the rig does not change
            // shape at runtime - and inactive ones are included so a camera switched on later is
            // already in the list.
            _cameras = camera.GetComponentsInChildren<Camera>(includeInactive: true);
            _baseFieldOfView = camera.fieldOfView;
        }

        public void Tick()
        {
            // Whoever holds the room owns the view - the album, a close-up. Letting go of the room
            // is not a reason to snap back, so the ease runs either way.
            bool held = !_worldLock.IsLocked && _zoom != null && _zoom.IsPressed();
            float target = held ? 1f : 0f;

            _amount = _settings.Smoothing > 0f
                ? Mathf.Lerp(_amount, target, 1f - Mathf.Exp(-_settings.Smoothing * Time.deltaTime))
                : target;

            if (Mathf.Abs(_amount - target) < RestEpsilon)
                _amount = target;

            if (_amount <= 0f)
            {
                // Fully back out. Put the authored field of view back once, then stand aside.
                if (!_applied)
                    return;

                _applied = false;
                ApplyFieldOfView(_baseFieldOfView);
                return;
            }

            _applied = true;
            ApplyFieldOfView(CurrentFieldOfView);
        }

        /// <summary>
        /// The field of view the rig should be on this frame. A zoomed value at or above the base
        /// is treated as no zoom at all rather than as a fisheye, so a mis-authored number can only
        /// ever do nothing.
        /// </summary>
        private float CurrentFieldOfView
        {
            get
            {
                float zoomed = _settings.ZoomedFieldOfView;
                if (zoomed <= 0f || zoomed >= _baseFieldOfView)
                    return _baseFieldOfView;

                return Mathf.Lerp(_baseFieldOfView, zoomed, _amount);
            }
        }

        private void ApplyFieldOfView(float fieldOfView)
        {
            for (int i = 0; i < _cameras.Length; i++)
            {
                Camera camera = _cameras[i];

                // An orthographic camera has no field of view to speak of; writing one would be a
                // silent no-op today and a surprise the day one is added to the rig.
                if (camera == null || camera.orthographic)
                    continue;

                camera.fieldOfView = fieldOfView;
            }
        }
    }
}
