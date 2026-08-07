using UnityEngine;

namespace Vesolovsky.Core.Audio
{
    public interface IAudioService
    {
        uint Play(AudioSFXKey sfxKey, GameObject emitter = null);
        void Stop(uint playingId, int fadeMs = 0);

        /// <summary>
        /// Set a global State (e.g., Music/Gameplay snapshot). Use for coarse, mode-like changes
        /// such as MainMenu vs Level.
        /// </summary>
        void SetState(AudioStateKey stateKey);

        /// <summary>
        /// Smoothly muffles or un-muffles the current music by sweeping a low-pass filter over it,
        /// without changing the track. Used by the pause menu to drop the music "behind glass"
        /// while it is up and bring it back when it closes.
        /// </summary>
        void SetMusicMuffled(bool muffled);
    }
}
