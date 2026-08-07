using RoboRyanTron.SearchableEnum;
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
            [SerializeField, SearchableEnum] private AudioSFXKey key;

            [Tooltip("Every clip mapped to this key. One is chosen at random on each play, so a " +
                     "sound that fires often - footsteps, cards landing - never repeats the exact " +
                     "same sample twice in a row. A single clip is fine; leave the list with just one.")]
            [SerializeField] private List<AudioClip> clips = new List<AudioClip>();

            [SerializeField, Range(0f, 1f)] private float volume = 1f;

            [Tooltip("Pitch is picked at random inside this range on every play, so repeated sounds " +
                     "vary in tone rather than sounding mechanical. Leave both at 1 for no variation.")]
            [SerializeField] private Vector2 pitchRange = Vector2.one;

            public AudioSFXKey Key => key;
            public List<AudioClip> Clips => clips;
            public float Volume => volume;
            public Vector2 PitchRange => pitchRange;
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

        // Which clip each key played last, so a key with several clips does not pick the same one
        // twice running. Runtime-only: it must not survive a domain reload as serialized state, or
        // the "avoid the last clip" rule would carry a stale index across a play session.
        [NonSerialized] private Dictionary<AudioSFXKey, int> _lastClipIndex;

        /// <summary>
        /// Resolves a random clip for the key, along with the volume and a per-play random pitch.
        /// A key with more than one clip never returns the same clip it returned last time.
        /// </summary>
        public bool TryGetSfx(AudioSFXKey key, out AudioClip clip, out float volume, out float pitch)
        {
            clip = null;
            volume = 0f;
            pitch = 1f;

            SfxEntry entry = FindSfx(key);
            if (entry == null)
                return false;

            clip = PickClip(key, entry);
            if (clip == null)
                return false;

            volume = Mathf.Clamp01(entry.Volume);
            pitch = RandomPitch(entry.PitchRange);
            return true;
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

        private SfxEntry FindSfx(AudioSFXKey key)
        {
            for (var index = 0; index < sfx.Count; index++)
            {
                var entry = sfx[index];
                if (entry != null && entry.Key == key)
                    return entry;
            }

            return null;
        }

        /// <summary>
        /// Picks a random non-null clip from the entry. With more than one clip it avoids repeating
        /// the clip played last for this key, so a rapid-fire sound stays varied. Empty slots in the
        /// list are ignored, so a half-filled list still picks fairly among the real clips.
        /// </summary>
        private AudioClip PickClip(AudioSFXKey key, SfxEntry entry)
        {
            List<AudioClip> clips = entry.Clips;
            if (clips == null || clips.Count == 0)
                return null;

            // Count the playable clips first so a list padded with empty slots still picks fairly.
            int playable = 0;
            for (var index = 0; index < clips.Count; index++)
            {
                if (clips[index] != null)
                    playable++;
            }

            if (playable == 0)
                return null;

            _lastClipIndex ??= new Dictionary<AudioSFXKey, int>();
            int last = _lastClipIndex.TryGetValue(key, out int stored) ? stored : -1;

            // The last clip is only worth skipping when there is another to pick instead and the
            // stored index still points at a real clip (the list may have shrunk under it).
            bool skipLast = playable > 1 && last >= 0 && last < clips.Count && clips[last] != null;
            int candidateCount = skipLast ? playable - 1 : playable;

            int target = UnityEngine.Random.Range(0, candidateCount);
            int chosenIndex = -1;
            int seen = 0;

            for (var index = 0; index < clips.Count; index++)
            {
                if (clips[index] == null || (skipLast && index == last))
                    continue;

                if (seen == target)
                {
                    chosenIndex = index;
                    break;
                }

                seen++;
            }

            if (chosenIndex < 0)
                return null;

            _lastClipIndex[key] = chosenIndex;
            return clips[chosenIndex];
        }

        private static float RandomPitch(Vector2 range)
        {
            float min = range.x;
            float max = range.y;

            if (max < min)
                (min, max) = (max, min);

            return Mathf.Approximately(min, max) ? min : UnityEngine.Random.Range(min, max);
        }
    }
}
