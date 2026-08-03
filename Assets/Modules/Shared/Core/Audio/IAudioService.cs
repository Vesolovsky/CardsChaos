using UnityEngine;

namespace Vesolovsky.Core.Audio
{
    public interface IAudioService
    {
        uint Play(AudioSFXKey sfxKey, GameObject emitter = null);
        void Stop(uint playingId, int fadeMs = 0);

        /// <summary>
        /// Sets an RTPC value normalized to the 0..1 range. Volume RTPCs are global;
        /// the emitter argument is reserved for emitter-scoped RTPC implementations.
        /// </summary>
        void SetRtpc(AudioRTPCKey rtpcKey, float normalizedValue, GameObject emitter = null);

        /// <summary>
        /// Set a global State (e.g., Music/Gameplay snapshot). Use for coarse, mode-like changes
        /// such as Paused/Playing, Combat/Exploration, Carrying/Idle.
        /// </summary>
        void SetState(AudioStateKey stateKey);
    }
}
