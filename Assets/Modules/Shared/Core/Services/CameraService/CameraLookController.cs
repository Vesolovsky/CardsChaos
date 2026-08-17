using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services.Settings;
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
    public class CameraLookController : IInitializable, ITickable, IDisposable, ICameraHeading,
        ICameraPoseOverride
    {
        private readonly ICameraService _cameraService;
        private readonly IWorldInteractionLock _worldLock;
        private readonly CameraLookSettings _settings;
        private readonly IGameSettingsService _gameSettings;

        // Optional: the camera rig is expected to work in a scene with no zoom bound at all, and
        // without one every turn is simply at full sensitivity.
        private readonly ICameraZoom _zoom;

        private float _yaw;
        private float _pitch;
        private bool _dragging;
        private Vector2 _restorePosition;

        [Inject]
        public CameraLookController(
            ICameraService cameraService,
            IWorldInteractionLock worldLock,
            CameraLookSettings settings,
            [InjectOptional] ICameraZoom zoom = null,
            [InjectOptional] IGameSettingsService gameSettings = null)
        {
            _cameraService = cameraService;
            _worldLock = worldLock;
            _settings = settings;
            _zoom = zoom;
            _gameSettings = gameSettings;

            if (_gameSettings == null)
                return;

            ApplySettings(_gameSettings.Current);
            _gameSettings.Applied += ApplySettings;
        }

        public void Dispose()
        {
            if (_gameSettings != null)
                _gameSettings.Applied -= ApplySettings;
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

            if (_worldLock.IsLocked)
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

            // Slowed in proportion to how far the view is zoomed in: a narrowed view covers less
            // room per degree, so at full sensitivity the same flick of the hand would swing it
            // right past whatever the player leaned in to look at.
            float sensitivity = _settings.Sensitivity * (_zoom?.LookScale ?? 1f);
            _yaw += (_settings.Invert ? -delta : delta) * sensitivity;

            Apply(camera);
        }

        public float Heading => _yaw;

        /// <summary>
        /// Points the camera at a saved heading on load. Pitch keeps the authored tilt captured in
        /// <see cref="Initialize"/>, so only the yaw is taken from the save.
        /// </summary>
        public void SetHeading(float yawDegrees)
        {
            _yaw = yawDegrees;

            Camera camera = _cameraService.MainCamera;
            if (camera != null)
                Apply(camera);
        }

        /// <summary>
        /// Takes the tilt as well as the heading, for a tool that has posed the camera itself and
        /// needs this controller to agree with where the camera now is - see
        /// <see cref="ICameraPoseOverride"/>. Gameplay never calls it: the authored tilt captured in
        /// <see cref="Initialize"/> is the tilt for the whole game.
        /// </summary>
        public void SetPose(float yawDegrees, float pitchDegrees)
        {
            _pitch = pitchDegrees;
            SetHeading(yawDegrees);
        }

        private void Apply(Camera camera)
        {
            camera.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void ApplySettings(GameSettingsData settings)
        {
            _settings.Sensitivity = settings.MouseSensitivity;
            _settings.Invert = settings.InvertMouseX;
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
