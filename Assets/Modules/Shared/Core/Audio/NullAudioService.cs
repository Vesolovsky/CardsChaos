using UnityEngine;

namespace Vesolovsky.Core.Audio
{
    /// <summary>
    /// No-op audio backend. Keeps UI audio hooks functional until a real
    /// implementation (FMOD, Wwise, Unity Audio) is bound instead.
    /// </summary>
    public class NullAudioService : IAudioService
    {
        public uint Play(AudioSFXKey sfxKey, GameObject emitter = null) => 0;
        public void Stop(uint playingId, int fadeMs = 0) { }
        public void SetRtpc(AudioRTPCKey rtpcKey, float normalizedValue, GameObject emitter = null) { }
        public void SetState(AudioStateKey stateKey) { }
    }
}
