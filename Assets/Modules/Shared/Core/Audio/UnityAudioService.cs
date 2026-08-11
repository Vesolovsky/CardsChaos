using System;
using System.Collections.Generic;
using UnityEngine;
using Vesolovsky.Core.Services.Settings;
using Zenject;
using Object = UnityEngine.Object;

namespace Vesolovsky.Core.Audio
{
    public sealed class UnityAudioService : IAudioService, IInitializable, ITickable, IDisposable
    {
        private const string RootName = "[Audio] UnityAudioService";
        private const string SfxSourceName = "SFX Source";
        private const string MusicSourceName = "Music Source";
        private const float DefaultMinDistance = 1f;
        private const float DefaultMaxDistance = 30f;

        // The music low-pass sweeps between "off" (well above hearing, so it colours nothing) and a
        // muffled cutoff that reads as the music dropping behind glass while the pause menu is up.
        private const float MusicLowPassOpen = 22000f;
        private const float MusicLowPassMuffled = 700f;

        // Framerate-independent exponential approach for the muffle sweep, run on unscaled time so it
        // still animates while the pause menu has the game clock stopped.
        private const float MusicLowPassSmoothing = 9f;

        private sealed class SfxPlayback
        {
            public AudioSource Source;
            public float BaseVolume;
            public float FadeDuration;
            public float FadeElapsed;
            public float FadeStartFactor = 1f;
            public float FadeFactor = 1f;
        }

        private readonly UnityAudioCatalog _catalog;
        private readonly IGameSettingsService _gameSettings;
        private readonly Dictionary<uint, SfxPlayback> _activeSfx = new Dictionary<uint, SfxPlayback>();
        private readonly Stack<AudioSource> _sfxPool = new Stack<AudioSource>();
        private readonly List<uint> _finishedSfx = new List<uint>();

        // Keys already reported as having no clip, so a sound that fires often - a footstep, a card
        // landing - complains once and then stays quiet rather than filling the console every frame.
        private readonly HashSet<AudioSFXKey> _missingSfxReported = new HashSet<AudioSFXKey>();

        private GameObject _root;
        private AudioSource _musicSource;
        private AudioLowPassFilter _musicLowPass;
        private float _musicLowPassTarget = MusicLowPassOpen;
        private AudioMusicKey _currentMusicKey;
        private float _musicBaseVolume = 1f;
        private float _masterVolume = 1f;
        private float _musicVolume = 1f;
        private float _sfxVolume = 1f;
        private uint _nextPlayingId = 1;
        private bool _initialized;
        private bool _disposed;

        public UnityAudioService(
            UnityAudioCatalog catalog,
            [InjectOptional] IGameSettingsService gameSettings = null)
        {
            _catalog = catalog;
            _gameSettings = gameSettings;
        }

        public void Initialize()
        {
            EnsureInitialized();

            if (_catalog == null)
            {
                Debug.LogWarning(
                    $"[{nameof(UnityAudioService)}] No {nameof(UnityAudioCatalog)} is assigned " +
                    "to AudioInstaller. Audio playback will stay silent until a catalog is wired.");
            }

            if (_gameSettings != null)
            {
                ApplySettings(_gameSettings.Current);
                _gameSettings.Applied += ApplySettings;
            }
        }

        public uint Play(AudioSFXKey sfxKey, GameObject emitter = null)
        {
            if (!TryResolveSfx(sfxKey, out var clip, out var baseVolume, out var pitch))
                return 0;

            return StartSfx(AcquireSfxSource(emitter, null), clip, baseVolume, pitch);
        }

        public uint Play(AudioSFXKey sfxKey, Vector3 position)
        {
            if (!TryResolveSfx(sfxKey, out var clip, out var baseVolume, out var pitch))
                return 0;

            return StartSfx(AcquireSfxSource(null, position), clip, baseVolume, pitch);
        }

        /// <summary>
        /// Resolves a key's clip, volume and pitch, reporting a missing clip once, and makes sure the
        /// audio objects exist. False means nothing should play - the caller returns 0.
        /// </summary>
        private bool TryResolveSfx(AudioSFXKey sfxKey, out AudioClip clip, out float baseVolume, out float pitch)
        {
            clip = null;
            baseVolume = 0f;
            pitch = 1f;

            if (_disposed || sfxKey == AudioSFXKey.None)
                return false;

            if (_catalog == null || !_catalog.TryGetSfx(sfxKey, out clip, out baseVolume, out pitch))
            {
                // The game carries on silently for this key; the error just flags the gap so the
                // clip can be wired in later. Reported once per key so it never floods the console.
                if (_missingSfxReported.Add(sfxKey))
                    Debug.LogError(
                        $"[{nameof(UnityAudioService)}] No audio clip for SFX key '{sfxKey}'. " +
                        $"Add it to the {nameof(UnityAudioCatalog)}.");

                return false;
            }

            EnsureInitialized();
            return true;
        }

        private uint StartSfx(AudioSource source, AudioClip clip, float baseVolume, float pitch)
        {
            source.clip = clip;
            source.pitch = pitch;
            source.volume = CalculateSfxVolume(baseVolume, 1f);

            var playingId = NextPlayingId();
            _activeSfx.Add(playingId, new SfxPlayback
            {
                Source = source,
                BaseVolume = baseVolume,
            });

            source.Play();
            return playingId;
        }

        public void Stop(uint playingId, int fadeMs = 0)
        {
            if (playingId == 0 || !_activeSfx.TryGetValue(playingId, out var playback))
                return;

            if (fadeMs <= 0 || playback.Source == null)
            {
                ReleaseSfx(playingId);
                return;
            }

            playback.FadeDuration = fadeMs / 1000f;
            playback.FadeElapsed = 0f;
            playback.FadeStartFactor = playback.FadeFactor;
        }

        public bool IsMuffled { get; private set; }

        public void SetMuffled(bool muffled)
        {
            if (_disposed)
                return;

            EnsureInitialized();

            IsMuffled = muffled;

            // Only the target moves here; Tick sweeps the actual cutoff toward it, so the muffle
            // eases in and out rather than snapping the moment the menu opens or closes.
            _musicLowPassTarget = muffled ? MusicLowPassMuffled : MusicLowPassOpen;
        }

        public void SetState(AudioStateKey stateKey)
        {
            if (_disposed)
                return;

            EnsureInitialized();

            if (stateKey == AudioStateKey.None)
            {
                StopMusic();
                return;
            }

            if (_catalog == null || !_catalog.TryGetMusic(stateKey, out var musicKey, out var clip, out var baseVolume))
                return;

            if (_currentMusicKey == musicKey && _musicSource.clip == clip && _musicSource.isPlaying)
            {
                _musicBaseVolume = baseVolume;
                RefreshMusicVolume();
                return;
            }

            _musicSource.Stop();
            _currentMusicKey = musicKey;
            _musicBaseVolume = baseVolume;
            _musicSource.clip = clip;
            RefreshMusicVolume();
            _musicSource.Play();
        }

        public void Tick()
        {
            if (_disposed)
                return;

            UpdateMusicLowPass();

            if (_activeSfx.Count == 0)
                return;

            _finishedSfx.Clear();

            foreach (var pair in _activeSfx)
            {
                var playback = pair.Value;
                if (playback.Source == null)
                {
                    _finishedSfx.Add(pair.Key);
                    continue;
                }

                if (playback.FadeDuration > 0f)
                {
                    playback.FadeElapsed += Time.unscaledDeltaTime;
                    var progress = Mathf.Clamp01(playback.FadeElapsed / playback.FadeDuration);
                    playback.FadeFactor = Mathf.Lerp(playback.FadeStartFactor, 0f, progress);
                    playback.Source.volume = CalculateSfxVolume(playback.BaseVolume, playback.FadeFactor);

                    if (progress >= 1f)
                    {
                        _finishedSfx.Add(pair.Key);
                        continue;
                    }
                }

                if (!playback.Source.isPlaying)
                    _finishedSfx.Add(pair.Key);
            }

            for (var index = 0; index < _finishedSfx.Count; index++)
                ReleaseSfx(_finishedSfx[index]);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_gameSettings != null)
                _gameSettings.Applied -= ApplySettings;

            foreach (var playback in _activeSfx.Values)
            {
                if (playback.Source != null)
                    Object.Destroy(playback.Source.gameObject);
            }

            _activeSfx.Clear();
            _sfxPool.Clear();
            _finishedSfx.Clear();

            if (_root != null)
                Object.Destroy(_root);

            _root = null;
            _musicSource = null;
            _musicLowPass = null;
        }

        private void EnsureInitialized()
        {
            if (_initialized || _disposed)
                return;

            _initialized = true;
            _root = new GameObject(RootName);
            Object.DontDestroyOnLoad(_root);

            var musicObject = new GameObject(MusicSourceName);
            musicObject.transform.SetParent(_root.transform, false);
            _musicSource = musicObject.AddComponent<AudioSource>();
            ConfigureCommonSource(_musicSource);
            _musicSource.loop = true;
            _musicSource.spatialBlend = 0f;

            // Sits idle above hearing so it colours nothing until the pause menu sweeps it down.
            _musicLowPass = musicObject.AddComponent<AudioLowPassFilter>();
            _musicLowPass.cutoffFrequency = MusicLowPassOpen;
            _musicLowPass.lowpassResonanceQ = 1f;
            _musicLowPassTarget = MusicLowPassOpen;
        }

        /// <summary>
        /// Hands out a pooled SFX source configured for one of three modes: 2D (no emitter, no
        /// position), 3D tracking a scene object (<paramref name="emitter"/>), or 3D at a fixed
        /// world point (<paramref name="position"/>). The positional mode parents to the audio root
        /// rather than to a scene object, so the source is never destroyed with a transient like a
        /// thrown card and always makes it back to the pool.
        /// </summary>
        private AudioSource AcquireSfxSource(GameObject emitter, Vector3? position)
        {
            AudioSource source = null;
            while (_sfxPool.Count > 0 && source == null)
                source = _sfxPool.Pop();

            if (source == null)
            {
                var sourceObject = new GameObject(SfxSourceName);
                source = sourceObject.AddComponent<AudioSource>();
            }

            source.gameObject.SetActive(true);
            source.transform.SetParent(emitter != null ? emitter.transform : _root.transform, false);

            // Reset the local offset a pooled source may carry, then place it: an emitter or the root
            // sits it at their origin, a bare position moves it out to that world point.
            source.transform.localPosition = Vector3.zero;
            if (position.HasValue)
                source.transform.position = position.Value;

            ConfigureCommonSource(source);
            source.loop = false;
            source.spatialBlend = emitter != null || position.HasValue ? 1f : 0f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = DefaultMinDistance;
            source.maxDistance = DefaultMaxDistance;
            return source;
        }

        private void ReleaseSfx(uint playingId)
        {
            if (!_activeSfx.TryGetValue(playingId, out var playback))
                return;

            _activeSfx.Remove(playingId);
            var source = playback.Source;
            if (source == null)
                return;

            source.Stop();
            source.clip = null;
            source.transform.SetParent(_root.transform, false);
            source.gameObject.SetActive(false);
            _sfxPool.Push(source);
        }

        private void StopMusic()
        {
            if (_musicSource != null)
            {
                _musicSource.Stop();
                _musicSource.clip = null;
            }

            _currentMusicKey = AudioMusicKey.None;
            _musicBaseVolume = 1f;
        }

        private void RefreshAllVolumes()
        {
            RefreshMusicVolume();
            RefreshSfxVolumes();
        }

        private void RefreshMusicVolume()
        {
            if (_musicSource != null)
                _musicSource.volume = Mathf.Clamp01(_musicBaseVolume * _musicVolume * _masterVolume);
        }

        private void RefreshSfxVolumes()
        {
            foreach (var playback in _activeSfx.Values)
            {
                if (playback.Source != null)
                    playback.Source.volume = CalculateSfxVolume(playback.BaseVolume, playback.FadeFactor);
            }
        }

        private float CalculateSfxVolume(float baseVolume, float fadeFactor)
        {
            return Mathf.Clamp01(baseVolume * _sfxVolume * _masterVolume * fadeFactor);
        }

        private void UpdateMusicLowPass()
        {
            if (_musicLowPass == null)
                return;

            float current = _musicLowPass.cutoffFrequency;
            if (Mathf.Approximately(current, _musicLowPassTarget))
                return;

            float step = 1f - Mathf.Exp(-MusicLowPassSmoothing * Time.unscaledDeltaTime);
            float next = Mathf.Lerp(current, _musicLowPassTarget, step);

            // Snap the last sliver so the cutoff actually lands on the target and Tick can leave it
            // alone rather than chasing it forever a fraction of a hertz short.
            if (Mathf.Abs(next - _musicLowPassTarget) < 1f)
                next = _musicLowPassTarget;

            _musicLowPass.cutoffFrequency = next;
        }

        private void ApplySettings(GameSettingsData settings)
        {
            _masterVolume = Mathf.Clamp01(settings.MasterVolume);
            _musicVolume = Mathf.Clamp01(settings.MusicVolume);
            _sfxVolume = Mathf.Clamp01(settings.SfxVolume);
            RefreshAllVolumes();
        }

        private uint NextPlayingId()
        {
            uint playingId;
            do
            {
                playingId = _nextPlayingId++;
                if (_nextPlayingId == 0)
                    _nextPlayingId = 1;
            }
            while (playingId == 0 || _activeSfx.ContainsKey(playingId));

            return playingId;
        }

        private static void ConfigureCommonSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.mute = false;
            source.bypassEffects = false;
            source.bypassListenerEffects = false;
            source.bypassReverbZones = false;
            source.pitch = 1f;
            source.panStereo = 0f;
            source.priority = 128;
        }
    }
}
