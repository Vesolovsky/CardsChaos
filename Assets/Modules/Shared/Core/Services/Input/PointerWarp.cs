using System;
using UnityEngine;

namespace Vesolovsky.Core.Services.Input
{
    /// <summary>
    /// Announces that something has moved the OS pointer on purpose, and where it was sent.
    ///
    /// Warping the pointer does not update <c>Mouse.position</c> straight away: the control keeps
    /// reporting the old reading until the platform sends the next mouse event, a frame or two
    /// later. Anything that draws its own cursor from that control therefore draws the pointer in
    /// the wrong place for those frames, which the player sees as the cursor appearing somewhere
    /// unexpected and then jumping.
    ///
    /// Guessing when the reading has caught up does not work - what the control reports in the
    /// meantime depends on the platform, and on whether the pointer was locked. So a warp says
    /// where it went instead of leaving anyone to infer it: a listener draws at the target from the
    /// first frame and goes back to following the live reading once the two agree.
    ///
    /// Static because the two ends have no reason to know each other - whatever locks the pointer
    /// lives in Core, whatever draws the cursor is a project's own UI - and there is nothing that
    /// owns both. A subscriber unsubscribes when it goes away, as with any static event.
    /// </summary>
    public static class PointerWarp
    {
        /// <summary>Raised with the screen position the pointer has just been sent to.</summary>
        public static event Action<Vector2> Warped;

        /// <summary>
        /// Call immediately after warping the pointer, with the same position that was asked for.
        /// </summary>
        public static void Announce(Vector2 position) => Warped?.Invoke(position);
    }
}
