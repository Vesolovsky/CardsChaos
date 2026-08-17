#if UNITY_EDITOR || CARDSCHAOS_DEBUG_TOOLS
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services;
using Vesolovsky.Core.Utils;
using VInspector;
using Zenject;

namespace Vesolovsky.Game.Trailer
{
    /// <summary>
    /// Rides the camera along a <see cref="TrailerDollyTrack"/> and hands it back afterwards.
    ///
    /// One key does both: the first press takes the camera off the player and starts it rolling,
    /// the next puts it back exactly where the ride began. While it rides it holds the world
    /// interaction lock - the same one the close-up and the album take - so walking, looking around
    /// and picking cards up are all off, and nothing can fight the dolly for the camera.
    ///
    /// Speed comes out of the track's length and the duration set here, and the track samples by
    /// distance, so the camera holds an even pace whatever the waypoints are spaced like. The ease
    /// curve is what softens the start and the stop.
    /// </summary>
    [AddComponentMenu("CardsChaos/Trailer/Trailer Dolly")]
    public class TrailerDolly : MonoBehaviour, IDebugTool
    {
        [Tooltip("The rails to ride. Any object with a Trailer Dolly Track on it.")]
        [SerializeField] private TrailerDollyTrack track;

        [Tooltip("Seconds to travel the whole track. Longer is slower; this is the one dial that " +
                 "decides how the shot feels.")]
        [SerializeField] private float duration = 20f;

        [Tooltip("How the ride eases in and out over that time. A straight line is a constant " +
                 "speed the whole way; the default softens both ends.")]
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Start over at the beginning on reaching the end.")]
        [SerializeField] private bool loop;

        [Tooltip("Ride back the way it came on reaching the end. Beats Loop when both are set.")]
        [SerializeField] private bool pingPong;

        [Tooltip("Field of view held for the ride. 0 keeps the camera's own.")]
        [SerializeField] private float fieldOfView;

        [Tooltip("Put the camera back where the ride began when it stops. Off leaves it standing " +
                 "wherever the ride was called off - useful to carry straight on filming from the " +
                 "end of a move.")]
        [SerializeField] private bool restoreCameraOnStop = true;

        [Header("Hotkey")]
        [Tooltip("Starts the ride, and stops it on the next press.")]
        [SerializeField] private TrailerHotkey toggleKey = new TrailerHotkey(Key.T);

        private ICameraService _cameraService;
        private ICameraPoseOverride _pose;
        private IWorldInteractionLock _worldLock;

        private IDisposable _worldHandle;

        private float _time;
        private int _direction = 1;

        private Vector3 _restorePosition;
        private Quaternion _restoreRotation;
        private float _restoreFieldOfView;

        public bool IsRiding { get; private set; }

        [Inject]
        public void Construct(
            [InjectOptional] ICameraService cameraService,
            [InjectOptional] ICameraPoseOverride pose,
            [InjectOptional] IWorldInteractionLock worldLock)
        {
            _cameraService = cameraService;
            _pose = pose;
            _worldLock = worldLock;
        }

        [Button("Start / Stop Ride")]
        public void Toggle()
        {
            if (IsRiding)
                Stop();
            else
                Play();
        }

        /// <summary>Takes the camera and starts it at the head of the track.</summary>
        [Button("Start Ride")]
        public void Play()
        {
            if (IsRiding)
                return;

            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[{nameof(TrailerDolly)}] The ride only runs in play mode. Use " +
                                 "the track's own Preview slider to frame it in the scene view.",
                    this);

                return;
            }

            Camera camera = _cameraService?.MainCamera;
            track?.Rebuild();

            if (camera == null || track == null || track.WaypointCount < 2)
            {
                Debug.LogWarning($"[{nameof(TrailerDolly)}] Nothing to ride - assign a track with " +
                                 "at least two waypoints, in a scene with a main camera.", this);

                return;
            }

            Transform pivot = camera.transform;
            _restorePosition = pivot.position;
            _restoreRotation = pivot.rotation;
            _restoreFieldOfView = camera.fieldOfView;

            _time = 0f;
            _direction = 1;
            IsRiding = true;

            // The same lock the close-up takes. Without it the player's own pan would keep writing
            // the camera's position from under the ride.
            _worldHandle = _worldLock?.Acquire(this);

            Apply(camera);
        }

        /// <summary>Gives the camera back to the player.</summary>
        [Button("Stop Ride")]
        public void Stop()
        {
            if (!IsRiding)
                return;

            IsRiding = false;

            _worldHandle?.Dispose();
            _worldHandle = null;

            Camera camera = _cameraService?.MainCamera;

            if (camera == null)
                return;

            if (restoreCameraOnStop)
            {
                camera.transform.SetPositionAndRotation(_restorePosition, _restoreRotation);

                if (_restoreFieldOfView > 0f)
                    camera.fieldOfView = _restoreFieldOfView;
            }

            // Either way the look controller has to be told where the camera ended up, or the next
            // right-drag swings it back to wherever it thought the camera was.
            Vector3 euler = camera.transform.eulerAngles;
            _pose?.SetPose(euler.y, euler.x);
        }

        private void Update()
        {
            if (toggleKey.WasPressed())
                Toggle();
        }

        private void LateUpdate()
        {
            if (!IsRiding)
                return;

            Camera camera = _cameraService?.MainCamera;

            if (camera == null || track == null)
            {
                Stop();
                return;
            }

            Advance();
            Apply(camera);
        }

        private void Advance()
        {
            if (duration <= 0f)
            {
                _time = 0f;
                return;
            }

            // Scaled time, so a ride pauses with the rest of the game rather than running on behind
            // an open pause menu.
            _time += Time.deltaTime * _direction;

            if (_time > duration)
            {
                if (pingPong)
                {
                    // Reflected rather than reset, so the turn keeps whatever the overshoot was and
                    // the ride does not stutter at the far end.
                    _time = Mathf.Max(duration - (_time - duration), 0f);
                    _direction = -1;
                }
                else if (loop)
                {
                    _time -= duration;
                }
                else
                {
                    // Held on the last frame of the move rather than dropped back to the player: a
                    // take that ends by snapping the camera away is a take you cannot use.
                    _time = duration;
                }
            }
            else if (_time < 0f)
            {
                if (pingPong)
                {
                    _time = Mathf.Min(-_time, duration);
                    _direction = 1;
                }
                else
                {
                    _time = 0f;
                }
            }
        }

        private void Apply(Camera camera)
        {
            float travelled = duration > 0f ? Mathf.Clamp01(_time / duration) : 0f;
            Pose pose = track.Sample(ease.Evaluate(travelled));

            camera.transform.SetPositionAndRotation(pose.position, pose.rotation);

            if (fieldOfView > 0f)
                camera.fieldOfView = fieldOfView;
        }

        private void OnDisable()
        {
            // Never leave the world locked or the camera parked down the rails because the object
            // was switched off (or the scene unloaded) mid-ride.
            Stop();
        }
    }
}
#endif
