using UnityEngine;
using Vesolovsky.Core.Services;
using Vesolovsky.Core.Services.Settings;
using Zenject;

namespace Vesolovsky.Core.Audio
{
    /// <summary>
    /// Drives a low-pass filter on a scene's environmental ambient source by how far the camera is
    /// from it: close up the ambience comes through in full, and the further the player walks away
    /// the more its high frequencies are rolled off, so it dulls into the distance and brightens
    /// back as they return.
    ///
    /// It is deliberately its own scene <see cref="AudioSource"/>, separate from the music the
    /// <see cref="UnityAudioService"/> owns, so the two play at once - the ambience underneath, the
    /// music over it. Author the source (clip, loop, Play On Awake, 3D settings, and its base
    /// volume) on the same object; this component shapes its tone with distance and scales its
    /// volume by the Ambient (times Master) settings slider, but never starts or stops it.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("Vesolovsky/Audio/Environmental Ambient")]
    public class EnvironmentalAmbient : MonoBehaviour
    {
        [Tooltip("At or nearer than this distance the ambience is at full brightness (Near Cutoff).")]
        [SerializeField] private float nearDistance = 2f;

        [Tooltip("At or beyond this distance the ambience is at its most muffled (Far Cutoff).")]
        [SerializeField] private float farDistance = 15f;

        [Tooltip("Cutoff, in Hz, when the camera is right on the source - high enough to colour nothing.")]
        [SerializeField] private float nearCutoff = 22000f;

        [Tooltip("Cutoff, in Hz, when the camera is far off - low enough that the highs are clearly gone.")]
        [SerializeField] private float farCutoff = 800f;

        [Tooltip("How quickly the cutoff chases the camera as it moves. Higher is snappier; 0 snaps " +
                 "instantly with no smoothing.")]
        [SerializeField] private float smoothing = 6f;

        [Tooltip("Cutoff, in Hz, the ambience is pulled down to while the pause menu is up - the " +
                 "same 'behind glass' muffle the music takes. Only ever lowers the distance cutoff, " +
                 "never raises it, so far-off ambience already duller than this stays as it was.")]
        [SerializeField] private float pauseMuffleCutoff = 700f;

        private AudioSource _source;
        private AudioLowPassFilter _lowPass;
        private ICameraService _cameraService;
        private IGameSettingsService _gameSettings;
        private IAudioService _audioService;

        // The authored source volume, captured before any settings scaling, so the Ambient slider
        // always scales from the level set on the prefab rather than from an already-scaled value.
        private float _baseVolume = 1f;

        // Both optional so the ambience still works in a scene without these services bound: it just
        // falls back to Camera.main for distance and leaves its authored volume untouched.
        [Inject]
        private void Inject(
            [InjectOptional] ICameraService cameraService,
            [InjectOptional] IGameSettingsService gameSettings,
            [InjectOptional] IAudioService audioService)
        {
            _cameraService = cameraService;
            _gameSettings = gameSettings;
            _audioService = audioService;
        }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _baseVolume = _source != null ? _source.volume : 1f;

            _lowPass = GetComponent<AudioLowPassFilter>();
            if (_lowPass == null)
                _lowPass = gameObject.AddComponent<AudioLowPassFilter>();

            _lowPass.cutoffFrequency = nearCutoff;
        }

        private void Start()
        {
            // Applied in Start rather than Awake so the injected settings service is guaranteed in.
            if (_gameSettings != null)
            {
                ApplyVolume(_gameSettings.Current);
                _gameSettings.Applied += ApplyVolume;
            }
        }

        private void OnDestroy()
        {
            if (_gameSettings != null)
                _gameSettings.Applied -= ApplyVolume;
        }

        private void Update()
        {
            Transform cameraTransform = ResolveCameraTransform();
            if (cameraTransform == null)
                return;

            float distance = Vector3.Distance(cameraTransform.position, transform.position);

            // Guard the degenerate range (far <= near) so it reads as a hard near/far switch rather
            // than dividing by zero.
            float t = farDistance > nearDistance
                ? Mathf.Clamp01((distance - nearDistance) / (farDistance - nearDistance))
                : (distance >= farDistance ? 1f : 0f);

            float target = Mathf.Lerp(nearCutoff, farCutoff, t);

            // While the pause menu is up the whole mix is muffled; the ambience follows the music
            // behind glass, pulled down to the pause cutoff (but never lifted above the distance one).
            if (_audioService != null && _audioService.IsMuffled)
                target = Mathf.Min(target, pauseMuffleCutoff);

            // Unscaled time so the sweep still eases while the pause menu has the game clock stopped.
            _lowPass.cutoffFrequency = smoothing > 0f
                ? Mathf.Lerp(_lowPass.cutoffFrequency, target, 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime))
                : target;
        }

        private void ApplyVolume(GameSettingsData settings)
        {
            if (_source == null || settings == null)
                return;

            // Master rides on top of Ambient, the same way it does over music and SFX, so the master
            // slider still pulls the ambience down with everything else.
            _source.volume = Mathf.Clamp01(_baseVolume * settings.AmbientVolume * settings.MasterVolume);
        }

        private Transform ResolveCameraTransform()
        {
            Camera camera = _cameraService?.MainCamera;
            if (camera == null)
                camera = Camera.main;

            return camera != null ? camera.transform : null;
        }
    }
}
