using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vesolovsky.Core.Utils
{
    /// <summary>
    /// Writes how long scene loads take, and how many long frames follow them, to the player log.
    ///
    /// It is a measuring tape, not a feature: nothing in the game reads it, and it only ever adds
    /// a handful of lines per scene to Player.log. Every line starts with "[LoadTiming]", so one
    /// build's numbers can be put next to another's to see whether a change actually helped.
    ///
    /// Two things are reported:
    /// <list type="bullet">
    /// <item>Each load a caller announces through <see cref="Track"/>: the time from the request
    /// to the new scene being in place.</item>
    /// <item>For the first <see cref="WatchSeconds"/> seconds of every scene: how many frames took
    /// longer than <see cref="LongFrameSeconds"/>, and the longest one. That is the "the game
    /// stutters at the start" number.</item>
    /// </list>
    /// </summary>
    public static class LoadTiming
    {
        private const string Prefix = "[LoadTiming]";

        /// <summary>A frame slower than this (about 20 FPS) reads as a visible hitch.</summary>
        private const float LongFrameSeconds = 0.05f;

        /// <summary>How long after a scene appears its frames are still counted.</summary>
        private const float WatchSeconds = 30f;

        private static string _sceneName;
        private static float _endsAt;
        private static int _longFrames;
        private static float _longestFrame;
        private static float _longFramesTotal;
        private static bool _watching;

        /// <summary>
        /// Reports, once <paramref name="operation"/> completes, how long it took from now.
        /// Safe to call with null, and safe to call more than once for the same load.
        /// </summary>
        public static void Track(string label, AsyncOperation operation)
        {
            if (operation == null)
                return;

            float startedAt = Time.realtimeSinceStartup;
            operation.completed += _ =>
            {
                float seconds = Time.realtimeSinceStartup - startedAt;
                Debug.Log($"{Prefix} {label}: {seconds:0.00} s (game running {Time.realtimeSinceStartup:0.0} s)");
            };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // Covers Enter Play Mode configurations that keep the managed domain alive.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _sceneName = null;
            _watching = false;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single)
                return;

            Debug.Log($"{Prefix} Scene '{scene.name}' loaded at {Time.realtimeSinceStartup:0.00} s");

            // A scene left before its window ran out still gets its numbers, cut short.
            Flush();

            _sceneName = scene.name;
            _endsAt = Time.realtimeSinceStartup + WatchSeconds;
            _longFrames = 0;
            _longestFrame = 0f;
            _longFramesTotal = 0f;

            if (!_watching)
                Watch().Forget();
        }

        /// <summary>
        /// Counts long frames until the window closes. Runs on UniTask's player loop rather than
        /// a MonoBehaviour, so it needs no object in any scene and survives every scene change.
        /// </summary>
        private static async UniTaskVoid Watch()
        {
            _watching = true;

            try
            {
                while (_sceneName != null)
                {
                    await UniTask.NextFrame(PlayerLoopTiming.Update);

                    float frame = Time.unscaledDeltaTime;
                    if (frame > LongFrameSeconds)
                    {
                        _longFrames++;
                        _longFramesTotal += frame;
                        if (frame > _longestFrame)
                            _longestFrame = frame;
                    }

                    if (Time.realtimeSinceStartup >= _endsAt)
                        Flush();
                }
            }
            finally
            {
                _watching = false;
            }
        }

        private static void Flush()
        {
            if (_sceneName == null)
                return;

            Debug.Log($"{Prefix} '{_sceneName}' first {WatchSeconds:0} s: {_longFrames} frames over " +
                      $"{LongFrameSeconds * 1000f:0} ms, longest {_longestFrame * 1000f:0} ms, " +
                      $"together {_longFramesTotal:0.00} s");

            _sceneName = null;
        }
    }
}
