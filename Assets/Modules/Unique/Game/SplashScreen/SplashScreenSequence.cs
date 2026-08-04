using PrimeTween;
using RoboRyanTron.SceneReference;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VInspector;

namespace Vesolovsky.Game.SplashScreen
{
    /// <summary>
    /// Plays the studio splash as one cinematic beat: the logo drops in from above - rotating
    /// and shrinking to size, accelerating so it is moving fastest the instant it lands - then
    /// hits the ground with a single heavy impact (a squash on the logo plus a screenshake that
    /// jolts both the camera and the UI, like something huge just fell). Once the dust settles
    /// the wordmark wipes in, and after a hold the whole lockup fades out and the next scene loads.
    ///
    /// The whole thing is a single PrimeTween <see cref="Sequence"/> on unscaled time, so it is
    /// immune to <see cref="Time.timeScale"/>. Because it is one sequence, a skip is just
    /// <c>Complete()</c>: the sequence snaps to its end and the same finish callback loads the
    /// next scene, so there is only one exit path.
    ///
    /// Why the screenshake is split in two: the lockup lives on a Screen Space - Overlay Canvas,
    /// which ignores the camera. Shaking the camera alone would jolt the world (the sparks) but
    /// leave the logo dead still. So the impact shakes the camera (for the world) AND a UI
    /// transform (for the logo/wordmark) together, and they read as one hit.
    ///
    /// Expected setup (logo + wordmark as UI Images under a Screen Space - Overlay Canvas):
    ///  - <see cref="logoImage"/>: the mark. Its position, rotation and scale are animated.
    ///  - <see cref="textImage"/>: the wordmark. Image Type = Filled, Fill Method = Horizontal,
    ///    Origin = Left for a left-to-right wipe.
    ///  - <see cref="rootCanvasGroup"/>: CanvasGroup on the parent of both, drives fade in/out.
    ///  - <see cref="cameraShakeTarget"/>: the scene camera, jolted on impact (moves the world).
    ///  - <see cref="uiShakeTarget"/>: a UI transform jolted on impact (moves the lockup).
    ///    Defaults to this component's own transform.
    /// </summary>
    [AddComponentMenu("Vesolovsky/Game/Splash Screen Sequence")]
    [DisallowMultipleComponent]
    public class SplashScreenSequence : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The logo mark. Its position, rotation and scale are animated on the way in.")]
        [SerializeField] private Image logoImage;

        [Tooltip("The wordmark below the logo. Must be Image Type = Filled so its fillAmount " +
                 "can be wiped from 0 to 1.")]
        [SerializeField] private Image textImage;

        [Tooltip("CanvasGroup on the parent of both images. Drives the fade in and the exit fade.")]
        [SerializeField] private CanvasGroup rootCanvasGroup;

        [Tooltip("Scene camera, jolted on impact so the world (e.g. the sparks) shakes. Optional - " +
                 "leave empty to skip the world shake.")]
        [SerializeField] private Transform cameraShakeTarget;

        [Tooltip("UI transform jolted on impact so the lockup shakes (the Overlay canvas ignores " +
                 "the camera). Defaults to this transform if left empty.")]
        [SerializeField] private Transform uiShakeTarget;

        [Header("Entrance - heavy drop")]
        [Tooltip("Delay before anything animates, so the screen doesn't jump the instant it loads.")]
        [SerializeField, Min(0f)] private float startDelay = 0.4f;

        [Tooltip("How long the logo falls. Longer feels heavier.")]
        [SerializeField, Min(0f)] private float dropDuration = 0.85f;

        [Tooltip("How far above its resting spot the logo starts, in canvas units (pixels at the " +
                 "reference resolution).")]
        [SerializeField, Min(0f)] private float dropDistance = 320f;

        [Tooltip("Scale the logo starts at before settling to 1. Above 1 it looms large and " +
                 "shrinks to size as it lands.")]
        [SerializeField, Min(0f)] private float entranceStartScale = 1.35f;

        [Tooltip("Z tilt (degrees) the logo starts rotated by, straightening out as it lands.")]
        [SerializeField] private float entranceStartTilt = 10f;

        [Tooltip("How quickly the fade-in runs. Kept short so the logo is visible while it falls. " +
                 "Clamped to the drop duration.")]
        [SerializeField, Min(0f)] private float entranceFadeDuration = 0.3f;

        [Tooltip("Easing of the drop. Use an In* ease (InQuad/InCubic/InQuart) so it accelerates " +
                 "and is fastest at the moment of impact.")]
        [SerializeField] private Ease dropEase = Ease.InCubic;

        [Header("Impact - single heavy hit")]
        [Tooltip("How hard the logo squashes on landing. 0.16 = 16% wider and shorter at contact.")]
        [SerializeField, Range(0f, 0.6f)] private float impactSquash = 0.16f;

        [Tooltip("How fast the squash reaches its extreme (the contact).")]
        [SerializeField, Min(0f)] private float impactSquashInDuration = 0.06f;

        [Tooltip("How long the logo takes to spring back from the squash.")]
        [SerializeField, Min(0f)] private float impactRecoverDuration = 0.3f;

        [Tooltip("How long the screenshake lasts before settling.")]
        [SerializeField, Min(0f)] private float shakeDuration = 0.5f;

        [Tooltip("Shake oscillations per second. Lower = slower, heavier jolts.")]
        [SerializeField, Min(0f)] private float shakeFrequency = 8f;

        [Tooltip("Camera shake strength (world units). Moves the world/sparks on impact.")]
        [SerializeField] private Vector3 cameraShakeStrength = new Vector3(0.15f, 0.25f, 0f);

        [Tooltip("UI shake strength (canvas units/pixels). Jolts the lockup on impact.")]
        [SerializeField] private Vector3 uiShakeStrength = new Vector3(14f, 26f, 0f);

        [Header("Wordmark wipe")]
        [Tooltip("Delay from the impact (logo landing) to the wordmark starting to wipe in. " +
                 "0 = the wordmark starts filling exactly on impact.")]
        [SerializeField, Min(0f)] private float delayBeforeText;

        [Tooltip("How long the wordmark takes to fill from empty to full.")]
        [SerializeField, Min(0f)] private float textFillDuration = 0.7f;

        [SerializeField] private Ease textFillEase = Ease.OutCubic;

        [Header("Exit")]
        [Tooltip("How long the finished lockup stays on screen before fading out.")]
        [SerializeField, Min(0f)] private float holdDuration = 1.2f;

        [Tooltip("How long the exit fade takes. Only runs when there's a next scene to load.")]
        [SerializeField, Min(0f)] private float exitFadeDuration = 0.5f;

        [SerializeField] private Ease exitFadeEase = Ease.InQuad;

        [Header("Flow")]
        [Tooltip("Scene loaded once the splash finishes (or is skipped). Must be in the Build " +
                 "Settings. Leave empty to just play the animation and hold on the logo.")]
        [SerializeField] private SceneReference nextScene;

        [Tooltip("Let any key / click / gamepad button skip straight to the end.")]
        [SerializeField] private bool allowSkip = true;

        private RectTransform _logoTransform;
        private Vector3 _logoRestPosition;
        private Vector3 _logoRestEuler;
        private Sequence _sequence;
        private bool _finished;

        private void Awake()
        {
            EnsureCached();
            ApplyInitialState();
        }

        /// <summary>Caches the logo transform and its resting pose once. Must run before the logo
        /// is ever moved, so the rest pose is the authored one - Awake covers that. Idempotent, so
        /// the editor Test button can safely call it too.</summary>
        private void EnsureCached()
        {
            if (_logoTransform == null && logoImage != null)
            {
                _logoTransform = logoImage.rectTransform;
                _logoRestPosition = _logoTransform.localPosition;
                _logoRestEuler = _logoTransform.localEulerAngles;
            }

            if (uiShakeTarget == null)
                uiShakeTarget = transform;
        }

        /// <summary>Inspector button: replays the splash. PrimeTween only ticks in Play mode, so
        /// this does nothing in Edit mode - enter Play once, then press Test to replay as many
        /// times as you like (tweak values live between presses).</summary>
        [Button]
        public void Test()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"{nameof(SplashScreenSequence)}: PrimeTween animates only in " +
                                 "Play mode. Enter Play, then press Test to replay the splash.", this);
                return;
            }

            Play();
        }

        private void Start() => Play();

        /// <summary>Puts every animated property at its "before" value so nothing flashes on
        /// the first frame: the logo lifted up, tilted, scaled up and hidden.</summary>
        private void ApplyInitialState()
        {
            if (_logoTransform != null)
            {
                _logoTransform.localPosition = _logoRestPosition + Vector3.up * dropDistance;
                _logoTransform.localEulerAngles = _logoRestEuler + new Vector3(0f, 0f, entranceStartTilt);
                _logoTransform.localScale = Vector3.one * entranceStartScale;
            }

            if (textImage != null)
                textImage.fillAmount = 0f;

            // The lockup starts hidden. A CanvasGroup drives the fade when present (one alpha for
            // everything); otherwise we fall back to fading the logo image itself.
            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 0f;
                if (logoImage != null)
                    SetAlpha(logoImage, 1f);
            }
            else if (logoImage != null)
            {
                SetAlpha(logoImage, 0f);
            }
        }

        /// <summary>Builds and starts the splash sequence. Safe to call again - it rebuilds
        /// from the initial state.</summary>
        public void Play()
        {
            if (logoImage == null || textImage == null)
            {
                Debug.LogError($"{nameof(SplashScreenSequence)} needs both a logo and a text " +
                               "image assigned.", this);
                return;
            }

            EnsureCached();

            if (_sequence.isAlive)
                _sequence.Stop();

            _finished = false;
            ApplyInitialState();

            _sequence = Sequence.Create(useUnscaledTime: true);

            if (startDelay > 0f)
                _sequence.ChainDelay(startDelay);

            // --- Heavy entrance: the logo drops, rotating and shrinking to size, all accelerating
            //     (In* ease) so it's moving fastest the instant it lands. The fade runs alongside,
            //     kept short so we watch it fall in rather than just appear. ---
            Vector3 dropFrom = _logoRestPosition + Vector3.up * dropDistance;
            Vector3 tiltFrom = _logoRestEuler + new Vector3(0f, 0f, entranceStartTilt);
            float fadeDuration = Mathf.Min(entranceFadeDuration, dropDuration);

            Tween fadeIn = rootCanvasGroup != null
                ? Tween.Alpha(rootCanvasGroup, 0f, 1f, fadeDuration, Ease.OutQuad)
                : Tween.Alpha(logoImage, 0f, 1f, fadeDuration, Ease.OutQuad);

            _sequence
                .Chain(Tween.LocalPosition(_logoTransform, dropFrom, _logoRestPosition, dropDuration, dropEase))
                .Group(Tween.LocalRotation(_logoTransform, tiltFrom, _logoRestEuler, dropDuration, dropEase))
                .Group(Tween.Scale(_logoTransform, entranceStartScale, 1f, dropDuration, dropEase))
                .Group(fadeIn);

            // --- Impact: one heavy hit. The logo squashes and springs back while the camera and
            //     the UI both jolt. The shakes are inserted at the contact moment so they overlap
            //     the squash instead of queueing after it. ---
            float contactTime = _sequence.duration; // the drop has just landed

            _sequence
                .Chain(Tween.Scale(_logoTransform,
                    new Vector3(1f + impactSquash, 1f - impactSquash, 1f), impactSquashInDuration, Ease.OutQuad))
                .Chain(Tween.Scale(_logoTransform, Vector3.one, impactRecoverDuration, Ease.OutBack));

            if (cameraShakeTarget != null)
                _sequence.Insert(contactTime,
                    Tween.ShakeLocalPosition(cameraShakeTarget, cameraShakeStrength, shakeDuration, shakeFrequency));

            if (uiShakeTarget != null)
                _sequence.Insert(contactTime,
                    Tween.ShakeLocalPosition(uiShakeTarget, uiShakeStrength, shakeDuration, shakeFrequency));

            // --- Wordmark wipes in ON impact (in parallel with the squash and shake), so it
            //     starts the instant the logo lands instead of queueing after the shake ends.
            //     delayBeforeText offsets it from that contact moment (0 = exactly on impact). ---
            _sequence.Insert(contactTime + delayBeforeText,
                Tween.UIFillAmount(textImage, 0f, 1f, textFillDuration, textFillEase));

            // --- Hold, then exit only if there's actually a scene to move on to. ---
            if (holdDuration > 0f)
                _sequence.ChainDelay(holdDuration);

            if (HasNextScene() && rootCanvasGroup != null && exitFadeDuration > 0f)
                _sequence.Chain(Tween.Alpha(rootCanvasGroup, 1f, 0f, exitFadeDuration, exitFadeEase));

            _sequence.ChainCallback(this, static self => self.OnSequenceFinished());
        }

        private void Update()
        {
            if (!allowSkip || !_sequence.isAlive)
                return;

            if (WasSkipPressed())
                _sequence.Complete(); // snaps to the end and fires OnSequenceFinished.
        }

        private void OnSequenceFinished()
        {
            if (_finished)
                return;

            _finished = true;
            LoadNextScene();
        }

        private bool HasNextScene() =>
            nextScene != null && !string.IsNullOrEmpty(nextScene.SceneName);

        private void LoadNextScene()
        {
            if (!HasNextScene())
                return;

            try
            {
                nextScene.LoadSceneAsync();
            }
            catch (SceneReference.SceneLoadException e)
            {
                Debug.LogWarning($"Splash could not load the next scene: {e.Message}", this);
            }
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        private static bool WasSkipPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                return true;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;

            if (Gamepad.current != null &&
                (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                 Gamepad.current.startButton.wasPressedThisFrame))
                return true;

            return false;
        }

        private void OnDestroy()
        {
            if (_sequence.isAlive)
                _sequence.Stop();
        }
    }
}
