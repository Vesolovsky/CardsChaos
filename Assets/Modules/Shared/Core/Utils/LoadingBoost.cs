using UnityEngine;

namespace Vesolovsky.Core.Utils
{
    /// <summary>
    /// Lets a scene load use more of each frame while nothing on screen needs to stay smooth.
    ///
    /// Out of the box Unity loads conservatively so a game can keep animating during a load: the
    /// main thread spends only a few milliseconds a frame integrating loaded objects
    /// (<see cref="Application.backgroundLoadingPriority"/>) and uploading textures to the GPU
    /// (<see cref="QualitySettings.asyncUploadTimeSlice"/>). Behind a closed scene wipe, or a splash
    /// that is only holding its finished logo, that care buys nothing - the rest of every frame is
    /// simply wasted. With hundreds of card textures to bring in, it adds up to seconds.
    ///
    /// So for the length of one covered load both budgets are raised, and the moment the load
    /// completes they go back to exactly what they were. Only call it where the screen really is
    /// covered: anything animating at the same time would stutter.
    /// </summary>
    public static class LoadingBoost
    {
        /// <summary>Main-thread milliseconds per frame for uploading textures while boosted.</summary>
        private const int BoostedUploadTimeSliceMs = 8;

        private static int _active;
        private static ThreadPriority _savedPriority;
        private static int _savedUploadTimeSlice;

        /// <summary>
        /// Raises the loading budgets now and puts them back once <paramref name="operation"/>
        /// completes. Null is ignored, so a load that failed to start changes nothing. Overlapping
        /// loads are counted, and the settings come back only when the last of them is done.
        /// </summary>
        public static void During(AsyncOperation operation)
        {
            if (operation == null || operation.isDone)
                return;

            Enter();
            operation.completed += _ => Exit();
        }

        private static void Enter()
        {
            if (_active++ > 0)
                return;

            _savedPriority = Application.backgroundLoadingPriority;
            _savedUploadTimeSlice = QualitySettings.asyncUploadTimeSlice;

            Application.backgroundLoadingPriority = ThreadPriority.High;
            QualitySettings.asyncUploadTimeSlice =
                Mathf.Max(_savedUploadTimeSlice, BoostedUploadTimeSliceMs);
        }

        private static void Exit()
        {
            if (_active == 0 || --_active > 0)
                return;

            Application.backgroundLoadingPriority = _savedPriority;
            QualitySettings.asyncUploadTimeSlice = _savedUploadTimeSlice;
        }

        // Covers Enter Play Mode configurations that keep the managed domain alive: a play session
        // stopped mid-load must not leave the next one thinking a boost is still running.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _active = 0;
    }
}
