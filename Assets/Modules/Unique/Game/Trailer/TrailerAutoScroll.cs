#if UNITY_EDITOR || CARDSCHAOS_DEBUG_TOOLS
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Vesolovsky.Core.Utils;
using VInspector;

namespace Vesolovsky.Game.Trailer
{
    /// <summary>
    /// Rolls a scroll view down to the bottom on its own, at a set pace, for filming a list without
    /// a hand on the wheel.
    ///
    /// A scroll driven by the wheel arrives in notches and reads as exactly that on camera. This
    /// walks the content down at a steady speed instead, with an ease at each end so the move
    /// begins and settles rather than snapping into and out of motion.
    ///
    /// It belongs on the trailer object in the scene, beside the other tools, rather than on the
    /// screen it drives: the upgrades screen is a prefab spawned at runtime, and a debug tool left
    /// on a shipped prefab cannot be stripped out of a build the way a scene object can. So the
    /// target is left empty and found when the move starts - whichever scroll view is on screen
    /// with something to scroll, which during a take is the one being filmed. Point
    /// <see cref="scrollRect"/> at one explicitly only to settle an argument between two.
    ///
    /// Press Scroll To Bottom in the inspector, or Alt+S with the game view focused. The speed is
    /// in pixels of content per second, so it means the same thing however long the list turns out
    /// to be.
    ///
    /// Everything here runs on unscaled time, so a shot still rolls with the game paused behind the
    /// screen.
    /// </summary>
    [AddComponentMenu("CardsChaos/Trailer/Trailer Auto Scroll")]
    public class TrailerAutoScroll : MonoBehaviour, IDebugTool
    {
        // Below this the content has nowhere left to go and the ride is over.
        private const float ArrivalEpsilon = 0.5f;

        [Tooltip("The scroll view to drive. Leave it empty and the open one is found when the move " +
                 "starts, which is what lets this live on a scene object instead of on the screen " +
                 "it drives.")]
        [SerializeField] private ScrollRect scrollRect;

        [Tooltip("Pixels of content per second. The upgrades rows are about 100 px tall, so 120 " +
                 "reads roughly a row a second.")]
        [SerializeField] private float speed = 120f;

        [Tooltip("Seconds spent easing up to that speed at the start, and easing back down into " +
                 "the bottom at the end. 0 starts and stops dead.")]
        [SerializeField] private float smoothing = 0.6f;

        [Tooltip("Seconds held still before the move begins, so a take has a beat at the top of " +
                 "the list before anything moves.")]
        [SerializeField] private float startDelay = 0.5f;

        [Tooltip("Jump to the top of the list when the move starts. Off begins from wherever the " +
                 "list is currently sitting.")]
        [SerializeField] private bool startFromTop = true;

        [Header("Hotkey")]
        [Tooltip("Starts the move, and calls it off on the next press.")]
        [SerializeField] private TrailerHotkey toggleKey = new TrailerHotkey(Key.S);

        private bool _running;

        // Seconds since the move was asked for, the start delay included.
        private float _elapsed;

        // Pixels to cover, how many have been covered, and how long the whole move takes - all
        // fixed when the move starts, so it lands on the bottom exactly as the clock runs out
        // rather than crawling towards it forever on an ever-shrinking ease-out.
        private float _distance;
        private float _travelled;
        private float _duration;

        // Settled when the move starts and held for its length, so a panel opening behind the shot
        // cannot steal a move already under way.
        private ScrollRect _target;

        public bool IsScrolling => _running;

        [Button("Scroll To Bottom")]
        public void Play()
        {
            if (!Application.isPlaying)
            {
                Warn("the move only runs in play mode");
                return;
            }

            ScrollRect rect = Resolve();

            if (rect == null || rect.content == null || rect.viewport == null)
            {
                Warn("no open scroll view to drive - open the screen first, or assign one");
                return;
            }

            _target = rect;

            // The rows are built when the screen opens, so the content may still be the size it was
            // before they arrived. Settle the layout before measuring anything off it.
            Canvas.ForceUpdateCanvases();

            if (startFromTop)
                rect.verticalNormalizedPosition = 1f;

            float scrollable = Scrollable(rect);

            if (scrollable <= 0f)
            {
                Warn("the content already fits - there is nothing to scroll");
                return;
            }

            if (speed <= 0f)
            {
                Warn("speed must be above zero");
                return;
            }

            _distance = Mathf.Clamp01(rect.verticalNormalizedPosition) * scrollable;

            if (_distance <= ArrivalEpsilon)
            {
                Warn("the list is already at the bottom");
                return;
            }

            // An ease up and an ease down each cover half of what that time would have covered at
            // full speed, so the pair of them costs exactly one smoothing period on top of the
            // straight run.
            _duration = _distance / speed + Mathf.Max(smoothing, 0f);

            _travelled = 0f;
            _elapsed = 0f;
            _running = true;

            rect.velocity = Vector2.zero;
        }

        [Button("Stop")]
        public void Stop()
        {
            _running = false;
        }

        [Button("Back To Top")]
        public void BackToTop()
        {
            ScrollRect rect = _target != null ? _target : Resolve();

            if (rect == null)
                return;

            Stop();

            rect.verticalNormalizedPosition = 1f;
            rect.velocity = Vector2.zero;
        }

        private void Update()
        {
            if (!toggleKey.WasPressed())
                return;

            if (_running)
                Stop();
            else
                Play();
        }

        private void LateUpdate()
        {
            if (!_running)
                return;

            ScrollRect rect = _target;
            float scrollable = rect != null ? Scrollable(rect) : 0f;

            if (scrollable <= 0f)
            {
                Stop();
                return;
            }

            // Unscaled, and driven from LateUpdate so this is the last word on the position each
            // frame - the scroll view applies its own inertia there too.
            float delta = Time.unscaledDeltaTime;
            _elapsed += delta;

            float time = _elapsed - startDelay;

            if (time < 0f)
            {
                rect.velocity = Vector2.zero;
                return;
            }

            if (time >= _duration)
            {
                Arrive(rect);
                return;
            }

            _travelled += speed * Pace(time) * delta;

            float remaining = Mathf.Max(_distance - _travelled, 0f);

            if (remaining <= ArrivalEpsilon)
            {
                Arrive(rect);
                return;
            }

            rect.verticalNormalizedPosition = Mathf.Clamp01(remaining / scrollable);

            // The scroll view is left with its inertia intact for the player, so it would otherwise
            // carry on coasting from whatever the last drag or wheel notch put into it.
            rect.velocity = Vector2.zero;
        }

        /// <summary>
        /// How much of the full speed to run at this far into the move: up from nothing over the
        /// first smoothing period, down to nothing over the last one, flat out in between.
        /// </summary>
        private float Pace(float time)
        {
            if (smoothing <= 0f)
                return 1f;

            float rise = time / smoothing;
            float fall = (_duration - time) / smoothing;

            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(rise, fall)));
        }

        private void Arrive(ScrollRect rect)
        {
            _running = false;

            rect.verticalNormalizedPosition = 0f;
            rect.velocity = Vector2.zero;
        }

        /// <summary>How many pixels of content there are to travel past. Zero when it all fits.</summary>
        private static float Scrollable(ScrollRect rect)
        {
            if (rect.content == null || rect.viewport == null)
                return 0f;

            return Mathf.Max(rect.content.rect.height - rect.viewport.rect.height, 0f);
        }

        /// <summary>
        /// The scroll view to drive: the one that was assigned, or - the usual case - whichever is
        /// on screen right now with content taller than its viewport. A take has one panel open, so
        /// that is the one being filmed.
        /// </summary>
        private ScrollRect Resolve()
        {
            if (scrollRect != null && scrollRect.isActiveAndEnabled)
                return scrollRect;

            ScrollRect found = null;
            int candidates = 0;

            foreach (ScrollRect rect in FindObjectsByType<ScrollRect>(FindObjectsSortMode.None))
            {
                if (!rect.isActiveAndEnabled || Scrollable(rect) <= 0f)
                    continue;

                candidates++;
                found ??= rect;
            }

            if (candidates > 1)
            {
                Debug.LogWarning($"[{nameof(TrailerAutoScroll)}] {candidates} scroll views are open " +
                                 $"and scrollable; driving '{found.name}'. Assign one to be sure.",
                    this);
            }

            return found;
        }

        private void Warn(string reason)
        {
            Debug.LogWarning($"[{nameof(TrailerAutoScroll)}] {reason}.", this);
        }
    }
}
#endif
