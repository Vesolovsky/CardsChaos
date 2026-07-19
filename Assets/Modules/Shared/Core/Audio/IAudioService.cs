using UnityEngine;

namespace Vesolovsky.Core.Audio
{
    public interface IAudioService
    {
        uint Play(AudioSFXKey sfxKey, GameObject emitter = null);
        void Stop(uint playingId, int fadeMs = 0);

        void SetRtpc(AudioRTPCKey rtpcKey, float normalizedValue, GameObject emitter = null);

        /// <summary>
        /// Set a global State (e.g., Music/Gameplay snapshot). Use for coarse, mode-like changes
        /// such as Paused/Playing, Combat/Exploration, Carrying/Idle.
        /// </summary>
        void SetState(AudioStateKey stateKey);
    }
}
