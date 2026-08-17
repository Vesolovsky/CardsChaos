#if UNITY_EDITOR || CARDSCHAOS_DEBUG_TOOLS
using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services;
using Vesolovsky.Core.Utils;
using VInspector;
using Zenject;

namespace Vesolovsky.Game.Trailer
{
    /// <summary>
    /// Puts the camera on a remembered shot at the press of a key, and remembers new ones from
    /// wherever the camera is standing.
    ///
    /// Drop it on any object in the gameplay scene (the same object the Cheats component sits on
    /// does nicely) and point it at a <see cref="TrailerShotBook"/>. While the game runs, Alt+1..0
    /// jump to the first ten shots in the book and Alt+C appends the camera's current pose as a new
    /// one - so lining a take up is: walk to it, press Alt+C, and it is there to jump back to on the
    /// next take. The book is an asset, so those captures survive leaving play mode.
    ///
    /// Jumping tells the look controller where the camera ended up, so the shot's tilt sticks
    /// instead of being swung back to the scene's authored tilt by the first right-drag.
    /// </summary>
    [AddComponentMenu("CardsChaos/Trailer/Trailer Camera Shots")]
    public class TrailerCameraShots : MonoBehaviour, IDebugTool
    {
        // Alt+1 is the first shot and Alt+0 the tenth, the way a hotbar reads.
        private static readonly Key[] ShotKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0,
        };

        [Tooltip("The shots this scene can jump between. Create one with " +
                 "Assets > Create > CardsChaos > Trailer > Shot Book.")]
        [SerializeField] private TrailerShotBook book;

        [Tooltip("Whether a shot's field of view is applied along with its pose. Off leaves the " +
                 "camera's own lens alone, however the shot was captured.")]
        [SerializeField] private bool applyFieldOfView = true;

        [Header("Hotkeys")]
        [Tooltip("Held with 1..0 to jump to the first ten shots in the book. None means the bare " +
                 "number keys jump, which will clash with anything gameplay binds them to.")]
        [SerializeField] private TrailerModifier shotModifier = TrailerModifier.Alt;

        [Tooltip("Appends the camera's current pose to the book as a new shot.")]
        [SerializeField] private TrailerHotkey captureKey = new TrailerHotkey(Key.C);

        private ICameraService _cameraService;
        private ICameraPoseOverride _pose;

        public TrailerShotBook Book => book;

        [Inject]
        public void Construct(
            [InjectOptional] ICameraService cameraService,
            [InjectOptional] ICameraPoseOverride pose)
        {
            _cameraService = cameraService;
            _pose = pose;
        }

        /// <summary>
        /// Appends the camera's current pose to the book. The button and Alt+C both land here; in
        /// the editor the asset is dirtied, so the capture is kept when play mode ends.
        /// </summary>
        [Button("Capture Current Pose")]
        public void CaptureCurrentPose()
        {
            Camera camera = ResolveCamera();

            if (book == null || camera == null)
            {
                Debug.LogWarning($"[{nameof(TrailerCameraShots)}] Nothing to capture with - " +
                                 "assign a shot book and make sure a main camera exists.", this);

                return;
            }

            Transform pivot = camera.transform;
            TrailerShot shot = book.Add(pivot.position, pivot.rotation, camera.fieldOfView);

            Debug.Log($"[{nameof(TrailerCameraShots)}] Captured '{shot.Name}' " +
                      $"({book.Count} shot(s) in '{book.name}').", book);
        }

        /// <summary>Jumps the camera to a shot. False when there is no such shot to jump to.</summary>
        public bool Go(int index)
        {
            TrailerShot shot = book != null ? book.Get(index) : null;

            if (shot == null)
                return false;

            Apply(shot);
            return true;
        }

        /// <summary>
        /// Stands the camera on a shot. The look controller is told the new heading and tilt at the
        /// same time, so control handed back to the player carries on from the shot rather than
        /// snapping away from it.
        /// </summary>
        public void Apply(TrailerShot shot)
        {
            Camera camera = ResolveCamera();

            if (shot == null || camera == null)
                return;

            var rotation = Quaternion.Euler(shot.Rotation);
            camera.transform.SetPositionAndRotation(shot.Position, rotation);

            if (applyFieldOfView && shot.FieldOfView > 0f)
                camera.fieldOfView = shot.FieldOfView;

            _pose?.SetPose(shot.Rotation.y, shot.Rotation.x);
        }

        /// <summary>
        /// The camera to pose. Injected in play mode; found in the scene otherwise, so the asset's
        /// inspector buttons work at edit time too.
        /// </summary>
        public Camera ResolveCamera()
        {
            Camera camera = _cameraService?.MainCamera;

            if (camera != null)
                return camera;

            MainCamera rig = FindFirstObjectByType<MainCamera>();
            return rig != null ? rig.Camera : Camera.main;
        }

        private void Update()
        {
            if (captureKey.WasPressed())
                CaptureCurrentPose();

            ReadShotKeys();
        }

        private void ReadShotKeys()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null || book == null)
                return;

            for (int i = 0; i < ShotKeys.Length; i++)
            {
                if (!keyboard[ShotKeys[i]].wasPressedThisFrame)
                    continue;

                // Checked only once a number key has actually been pressed, so the modifier costs
                // nothing on the overwhelming majority of frames.
                if (!ModifierHeld(keyboard))
                    return;

                Go(i);
                return;
            }
        }

        private bool ModifierHeld(Keyboard keyboard)
        {
            return shotModifier switch
            {
                TrailerModifier.Ctrl => keyboard.ctrlKey.isPressed,
                TrailerModifier.Alt => keyboard.altKey.isPressed,
                TrailerModifier.Shift => keyboard.shiftKey.isPressed,
                _ => true,
            };
        }
    }
}
#endif
