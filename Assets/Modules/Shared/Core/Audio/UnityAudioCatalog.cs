using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vesolovsky.Core.Audio
{
    [CreateAssetMenu(menuName = "Vesolovsky/Audio/Unity Audio Catalog", fileName = "UnityAudioCatalog")]
    public sealed class UnityAudioCatalog : ScriptableObject
    {
        [Serializable]
        private sealed class SfxEntry
        {
            [SerializeField] private AudioSFXKey key;
            [SerializeField] private AudioClip clip;
            [SerializeField, Range(0f, 1f)] private float volume = 1f;

            public AudioSFXKey Key => key;
            public AudioClip Clip => clip;
            public float Volume => volume;
        }

        [Serializable]
        private sealed class MusicEntry
        {
            [SerializeField] private AudioMusicKey key;
            [SerializeField] private AudioClip clip;
            [SerializeField, Range(0f, 1f)] private float volume = 1f;

            public AudioMusicKey Key => key;
            public AudioClip Clip => clip;
            public float Volume => volume;
        }

        [Serializable]
        private sealed class MusicStateEntry
        {
            [SerializeField] private AudioStateKey state;
            [SerializeField] private AudioMusicKey music;

            public AudioStateKey State => state;
            public AudioMusicKey Music => music;
        }

        [SerializeField] private List<SfxEntry> sfx = new List<SfxEntry>();
        [SerializeField] private List<MusicEntry> music = new List<MusicEntry>();
        [SerializeField] private List<MusicStateEntry> musicStates = new List<MusicStateEntry>();

        public bool TryGetSfx(AudioSFXKey key, out AudioClip clip, out float volume)
        {
            for (var index = 0; index < sfx.Count; index++)
            {
                var entry = sfx[index];
                if (entry == null || entry.Key != key || entry.Clip == null)
                    continue;

                clip = entry.Clip;
                volume = Mathf.Clamp01(entry.Volume);
                return true;
            }

            clip = null;
            volume = 0f;
            return false;
        }

        public bool TryGetMusic(AudioStateKey state, out AudioMusicKey key, out AudioClip clip, out float volume)
        {
            key = AudioMusicKey.None;
            clip = null;
            volume = 0f;

            for (var stateIndex = 0; stateIndex < musicStates.Count; stateIndex++)
            {
                var stateEntry = musicStates[stateIndex];
                if (stateEntry == null || stateEntry.State != state)
                    continue;

                key = stateEntry.Music;
                break;
            }

            if (key == AudioMusicKey.None)
                return false;

            for (var musicIndex = 0; musicIndex < music.Count; musicIndex++)
            {
                var musicEntry = music[musicIndex];
                if (musicEntry == null || musicEntry.Key != key || musicEntry.Clip == null)
                    continue;

                clip = musicEntry.Clip;
                volume = Mathf.Clamp01(musicEntry.Volume);
                return true;
            }

            return false;
        }
    }
}
