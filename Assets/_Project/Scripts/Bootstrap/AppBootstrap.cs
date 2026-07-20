using System;
using System.Collections;
using System.Collections.Generic;
using BlockEscape.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BlockEscape.Bootstrap
{
    public sealed class AppBootstrap : MonoBehaviour
    {
        [SerializeField] private string _mainMenuScene = "MainMenu";
        [SerializeField] private string _gameplayScene = "TetrisDemo";
        [SerializeField, Min(0f)] private float _musicFadeSeconds = 1.25f;

        private AudioClip _menuTrack;
        private AudioClip[] _gameplayTracks = Array.Empty<AudioClip>();
        private readonly Dictionary<string, AudioClip> _sfxClips = new(StringComparer.OrdinalIgnoreCase);
        private AudioSource _musicSource;
        private AudioSource _sfxSource;
        private Coroutine _fadeRoutine;
        private float _musicVolume = 0.7f;
        private bool _gameplayMusicActive;
        private bool _musicPaused;

        public static AppBootstrap EnsureExists()
        {
            var existing = FindAnyObjectByType<AppBootstrap>();
            return existing != null
                ? existing
                : new GameObject("App Services (Runtime)").AddComponent<AppBootstrap>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Time.timeScale = 1f;
            SaveService.Load();
            SaveService.ApplyDisplaySettings();

            _musicSource = GetComponent<AudioSource>();
            if (_musicSource == null)
                _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = false;
            _musicSource.spatialBlend = 0f;
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.loop = false;
            _sfxSource.spatialBlend = 0f;
            var tracks = Resources.LoadAll<AudioClip>("Audio/BGM");
            _menuTrack = Array.Find(tracks, track => track.name == "BGM_02");
            _gameplayTracks = Array.FindAll(tracks, track => track != _menuTrack);
            foreach (var clip in Resources.LoadAll<AudioClip>("Audio/SFX"))
                _sfxClips[clip.name] = clip;
            SaveService.AudioSettingsChanged += ApplyMusicVolume;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            SaveService.ApplyAudioSettings();

            var input = InputService.Current;
            if (input == null)
                input = new GameObject("Input Service (Persistent)").AddComponent<InputService>();
            input.SetGameplayEnabled(false);
        }

        private void Start()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (ShouldLoadMainMenu(sceneName))
                SceneManager.LoadScene(_mainMenuScene);
            else
                PlayForScene(sceneName);
        }

        public static bool ShouldLoadMainMenu(string sceneName) => sceneName == "Bootstrap";

        private void Update()
        {
            if (_musicPaused || !_gameplayMusicActive || _fadeRoutine != null || _musicSource == null)
                return;
            if (!_musicSource.isPlaying || _musicSource.clip.length - _musicSource.time <= _musicFadeSeconds)
                TransitionTo(ChooseRandomGameplayTrack(), false, _musicSource.isPlaying);
        }

        private void OnDestroy()
        {
            SaveService.AudioSettingsChanged -= ApplyMusicVolume;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        private void ApplyMusicVolume(SaveData data)
        {
            _musicVolume = data.musicVolume;
            if (_musicSource != null && _fadeRoutine == null)
                _musicSource.volume = _musicVolume;
            if (_sfxSource != null)
                _sfxSource.volume = data.sfxVolume;
        }

        public void PlaySfx(string clipName, float volumeScale = 1f)
        {
            if (_sfxSource != null && _sfxClips.TryGetValue(clipName, out var clip))
                _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        public void SetMusicPaused(bool paused)
        {
            _musicPaused = paused;
            if (_musicSource == null)
                return;
            if (paused)
                _musicSource.Pause();
            else
                _musicSource.UnPause();
        }

        private void HandleActiveSceneChanged(Scene previous, Scene current)
        {
            PlayForScene(current.name);
        }

        private void PlayForScene(string sceneName)
        {
            SetMusicPaused(false);
            BindUiClickSounds();
            _gameplayMusicActive = sceneName == _gameplayScene;
            if (sceneName == _mainMenuScene)
                TransitionTo(_menuTrack, true, true);
            else if (_gameplayMusicActive)
                TransitionTo(ChooseRandomGameplayTrack(), false, true);
        }

        private void BindUiClickSounds()
        {
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include))
            {
                button.onClick.RemoveListener(PlayUiClick);
                button.onClick.AddListener(PlayUiClick);
            }
        }

        private void PlayUiClick()
        {
            PlaySfx("click");
        }

        private AudioClip ChooseRandomGameplayTrack()
        {
            if (_gameplayTracks.Length == 0)
                return null;

            AudioClip next;
            do
                next = _gameplayTracks[UnityEngine.Random.Range(0, _gameplayTracks.Length)];
            while (_gameplayTracks.Length > 1 && next == _musicSource.clip);
            return next;
        }

        private void TransitionTo(AudioClip track, bool loop, bool fadeOut)
        {
            if (_musicSource == null || track == null)
                return;
            if (_musicSource.clip == track && _musicSource.isPlaying)
            {
                _musicSource.loop = loop;
                return;
            }

            if (_fadeRoutine != null)
                StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeToTrack(track, loop, fadeOut));
        }

        private IEnumerator FadeToTrack(AudioClip track, bool loop, bool fadeOut)
        {
            var duration = Mathf.Max(0.01f, _musicFadeSeconds);
            if (fadeOut && _musicSource.isPlaying)
            {
                var startVolume = _musicSource.volume;
                for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    _musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                    yield return null;
                }
            }

            _musicSource.Stop();
            _musicSource.clip = track;
            _musicSource.loop = loop;
            _musicSource.volume = 0f;
            _musicSource.Play();
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                _musicSource.volume = Mathf.Lerp(0f, _musicVolume, elapsed / duration);
                yield return null;
            }

            _musicSource.volume = _musicVolume;
            _fadeRoutine = null;
        }
    }
}
