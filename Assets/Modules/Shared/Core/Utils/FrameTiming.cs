using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Vesolovsky.Core.Utils
{
    /// <summary>
    /// Helpers for animations that must not be started on a frame that is not really a frame.
    ///
    /// The first frames after a scene loads are its most expensive: Zenject installing, the canvas
    /// building its first mesh, fonts and addressables warming up, shaders compiling. One of those
    /// frames can easily cost a second of wall-clock time.
    ///
    /// That is a problem for anything animating on unscaled time, because
    /// <see cref="Time.unscaledDeltaTime"/> reports what the frame actually cost and - unlike
    /// <see cref="Time.deltaTime"/> - is not clamped by <see cref="Time.maximumDeltaTime"/>. A
    /// tween handed a one-second delta advances a whole second in a single frame, so an animation
    /// started on the frame the scene appeared is largely over before anything is drawn. It reads
    /// as the animation never having played at all, and it only shows up where the hitch is big -
    /// which is why it tends to be fine in the editor on a warm project and wrong everywhere else.
    /// </summary>
    public static class FrameTiming
    {
        /// <summary>A frame this cheap is a real frame rather than the tail of a load.</summary>
        private const float DefaultSettledFrameSeconds = 0.1f;

        /// <summary>
        /// How long to keep waiting before giving up and playing anyway. A hitch that outlasts
        /// this is not something an animation should keep hiding behind.
        /// </summary>
        private const int DefaultMaxFrames = 60;

        /// <summary>
        /// Waits until the game is drawing frames at a believable rate, so whatever runs next is
        /// measured against real time rather than against the load it was waiting on.
        /// </summary>
        /// <returns>True when the wait was cancelled, and the caller should stand down.</returns>
        public static async UniTask<bool> WaitForSettledFrame(
            CancellationToken token,
            float settledFrameSeconds = DefaultSettledFrameSeconds,
            int maxFrames = DefaultMaxFrames)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                if (await UniTask.NextFrame(token).SuppressCancellationThrow())
                    return true;

                if (Time.unscaledDeltaTime <= settledFrameSeconds)
                    break;
            }

            return false;
        }
    }
}
