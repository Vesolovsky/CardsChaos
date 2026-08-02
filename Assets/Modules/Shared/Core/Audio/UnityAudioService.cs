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

        private GameObject _root;
        private AudioSource _musicSource;
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
            if (_disposed || sfxKey == AudioSFXKey.None || _catalog == null ||
                !_catalog.TryGetSfx(sfxKey, out var clip, out var baseVolume))
                return 0;

            EnsureInitialized();

            var source = AcquireSfxSource(emitter);
            source.clip = clip;
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

        public void SetRtpc(AudioRTPCKey rtpcKey, float normalizedValue, GameObject emitter = null)
        {
            var value = Mathf.Clamp01(normalizedValue);

            switch (rtpcKey)
            {
                case AudioRTPCKey.MasterVolume:
                    _masterVolume = value;
                    RefreshAllVolumes();
                    break;
                case AudioRTPCKey.MusicVolume:
                    _musicVolume = value;
                    RefreshMusicVolume();
                    break;
                case AudioRTPCKey.SfxVolume:
                    _sfxVolume = value;
                    RefreshSfxVolumes();
                    break;
            }
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
            if (_disposed || _activeSfx.Count == 0)
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
        }

        private AudioSource AcquireSfxSource(GameObject emitter)
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
            ConfigureCommonSource(source);
            source.loop = false;
            source.spatialBlend = emitter != null ? 1f : 0f;
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

        private void ApplySettings(GameSettingsData settings)
        {
            SetRtpc(AudioRTPCKey.MasterVolume, settings.MasterVolume);
            SetRtpc(AudioRTPCKey.MusicVolume, settings.MusicVolume);
            SetRtpc(AudioRTPCKey.SfxVolume, settings.SfxVolume);
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
