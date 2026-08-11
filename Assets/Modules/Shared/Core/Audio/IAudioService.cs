using UnityEngine;

namespace Vesolovsky.Core.Audio
{
    public interface IAudioService
    {
        uint Play(AudioSFXKey sfxKey, GameObject emitter = null);

        /// <summary>
        /// Plays a one-shot in 3D from a fixed world position, using the same pooled sources as the
        /// other overloads. The source is parented to the audio root, not to any scene object, so it
        /// is never destroyed out from under a still-playing clip and always returns to the pool -
        /// the way to give a transient like a card landing a spatial position without a per-object
        /// AudioSource.
        /// </summary>
        uint Play(AudioSFXKey sfxKey, Vector3 position);

        void Stop(uint playingId, int fadeMs = 0);

        /// <summary>
        /// Set a global State (e.g., Music/Gameplay snapshot). Use for coarse, mode-like changes
        /// such as MainMenu vs Level.
        /// </summary>
        void SetState(AudioStateKey stateKey);

        /// <summary>
        /// Smoothly muffles or un-muffles the mix by sweeping a low-pass filter over the music,
        /// without changing the track. Used by the pause menu to drop the sound "behind glass" while
        /// it is up. <see cref="IsMuffled"/> exposes the state so other muffled sources - the
        /// environmental ambient, which owns its own filter - can follow the same pause.
        /// </summary>
        void SetMuffled(bool muffled);

        /// <summary>Whether the mix is currently muffled (the pause menu is up).</summary>
        bool IsMuffled { get; }
    }
}
