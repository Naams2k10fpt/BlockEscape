using System;
using System.Collections.Generic;
using BlockEscape.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BlockEscape.UI
{
    public sealed class OptionsMenu : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Slider _masterVolume;
        [SerializeField] private Slider _musicVolume;
        [SerializeField] private Slider _sfxVolume;
        [SerializeField] private Text _resolutionText;
        [SerializeField] private Button _previousResolutionButton;
        [SerializeField] private Button _nextResolutionButton;
        [SerializeField] private Toggle _fullscreenToggle;
        [SerializeField] private Toggle _vSyncToggle;
        [SerializeField] private Button _applyButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _controlsButton;
        [SerializeField] private InputRebindMenu _inputRebindMenu;

        private readonly List<Vector2Int> _resolutions = new();
        private int _selectedResolution;
        private bool _listenersBound;
        private bool _previewingAudio;
        private float _savedMasterVolume;
        private float _savedMusicVolume;
        private float _savedSfxVolume;

        public event Action Closed;
        public bool IsVisible =>
            _panel != null && _panel.activeSelf ||
            _inputRebindMenu != null && _inputRebindMenu.IsVisible;

        private void OnEnable()
        {
            BindButtons();
        }

        private void OnDisable()
        {
            RestoreAudioPreview();
            UnbindButtons();
        }

        public void Configure(
            GameObject panel,
            Slider masterVolume,
            Slider musicVolume,
            Slider sfxVolume,
            Text resolutionText,
            Button previousResolutionButton,
            Button nextResolutionButton,
            Toggle fullscreenToggle,
            Toggle vSyncToggle,
            Button applyButton,
            Button backButton,
            Button controlsButton = null,
            InputRebindMenu inputRebindMenu = null)
        {
            UnbindButtons();
            _panel = panel;
            _masterVolume = masterVolume;
            _musicVolume = musicVolume;
            _sfxVolume = sfxVolume;
            _resolutionText = resolutionText;
            _previousResolutionButton = previousResolutionButton;
            _nextResolutionButton = nextResolutionButton;
            _fullscreenToggle = fullscreenToggle;
            _vSyncToggle = vSyncToggle;
            _applyButton = applyButton;
            _backButton = backButton;
            _controlsButton = controlsButton;
            _inputRebindMenu = inputRebindMenu;
            ConfigureSliders();
            if (isActiveAndEnabled) BindButtons();
        }

        public void Show()
        {
            _inputRebindMenu?.Hide();
            var data = SaveService.Data;
            _savedMasterVolume = data.masterVolume;
            _savedMusicVolume = data.musicVolume;
            _savedSfxVolume = data.sfxVolume;
            _previewingAudio = true;
            PopulateFromSave();
            if (_panel != null) _panel.SetActive(true);
            if (_applyButton != null) _applyButton.Select();
        }

        public void Hide()
        {
            RestoreAudioPreview();
            if (_panel != null) _panel.SetActive(false);
            _inputRebindMenu?.Hide();
        }

        private void ShowControls()
        {
            if (_panel != null) _panel.SetActive(false);
            _inputRebindMenu?.Show();
        }

        private void HandleControlsClosed()
        {
            if (_panel != null) _panel.SetActive(true);
            _controlsButton?.Select();
        }

        private void ConfigureSliders()
        {
            ConfigureSlider(_masterVolume);
            ConfigureSlider(_musicVolume);
            ConfigureSlider(_sfxVolume);
        }

        private static void ConfigureSlider(Slider slider)
        {
            if (slider == null)
                return;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
        }

        private void PopulateFromSave()
        {
            ConfigureSliders();
            var data = SaveService.Data;
            if (_masterVolume != null) _masterVolume.SetValueWithoutNotify(data.masterVolume);
            if (_musicVolume != null) _musicVolume.SetValueWithoutNotify(data.musicVolume);
            if (_sfxVolume != null) _sfxVolume.SetValueWithoutNotify(data.sfxVolume);
            if (_fullscreenToggle != null) _fullscreenToggle.isOn = data.fullscreen;
            if (_vSyncToggle != null) _vSyncToggle.isOn = data.vSyncCount > 0;

            BuildResolutionList(data.screenWidth, data.screenHeight);
            UpdateResolutionText();
        }

        private void BuildResolutionList(int savedWidth, int savedHeight)
        {
            _resolutions.Clear();
            foreach (var resolution in Screen.resolutions)
                AddResolution(resolution.width, resolution.height);

            if (_resolutions.Count == 0)
            {
                AddResolution(Screen.currentResolution.width, Screen.currentResolution.height);
                AddResolution(savedWidth, savedHeight);
            }

            _resolutions.Sort((left, right) =>
            {
                var widthComparison = left.x.CompareTo(right.x);
                return widthComparison != 0 ? widthComparison : left.y.CompareTo(right.y);
            });

            _selectedResolution = 0;
            var currentWidth = Screen.currentResolution.width;
            var currentHeight = Screen.currentResolution.height;
            for (var i = 0; i < _resolutions.Count; i++)
            {
                if (_resolutions[i].x == currentWidth && _resolutions[i].y == currentHeight)
                    _selectedResolution = i;
                if (_resolutions[i].x == savedWidth && _resolutions[i].y == savedHeight)
                {
                    _selectedResolution = i;
                    break;
                }
            }
        }

        private void AddResolution(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return;
            foreach (var resolution in _resolutions)
                if (resolution.x == width && resolution.y == height)
                    return;
            _resolutions.Add(new Vector2Int(width, height));
        }

        private void SelectPreviousResolution()
        {
            if (_resolutions.Count == 0)
                return;
            _selectedResolution = (_selectedResolution - 1 + _resolutions.Count) % _resolutions.Count;
            UpdateResolutionText();
        }

        private void SelectNextResolution()
        {
            if (_resolutions.Count == 0)
                return;
            _selectedResolution = (_selectedResolution + 1) % _resolutions.Count;
            UpdateResolutionText();
        }

        private void UpdateResolutionText()
        {
            if (_resolutionText == null || _resolutions.Count == 0)
                return;
            var resolution = _resolutions[_selectedResolution];
            _resolutionText.text = $"{resolution.x} × {resolution.y}";
        }

        private void Apply()
        {
            PreviewAudioSettings(0f);
            var data = SaveService.Data;
            if (_fullscreenToggle != null) data.fullscreen = _fullscreenToggle.isOn;
            if (_vSyncToggle != null) data.vSyncCount = _vSyncToggle.isOn ? 1 : 0;
            if (_resolutions.Count > 0)
            {
                var resolution = _resolutions[_selectedResolution];
                data.screenWidth = resolution.x;
                data.screenHeight = resolution.y;
            }

            SaveService.Save();
            SaveService.ApplyDisplaySettings();
            SaveService.ApplyAudioSettings();
            _previewingAudio = false;
            Close();
        }

        private void PreviewAudioSettings(float _)
        {
            var data = SaveService.Data;
            if (_masterVolume != null) data.masterVolume = _masterVolume.value;
            if (_musicVolume != null) data.musicVolume = _musicVolume.value;
            if (_sfxVolume != null) data.sfxVolume = _sfxVolume.value;
            SaveService.ApplyAudioSettings();
        }

        private void RestoreAudioPreview()
        {
            if (!_previewingAudio)
                return;

            var data = SaveService.Data;
            data.masterVolume = _savedMasterVolume;
            data.musicVolume = _savedMusicVolume;
            data.sfxVolume = _savedSfxVolume;
            _previewingAudio = false;
            SaveService.ApplyAudioSettings();
        }

        private void Close()
        {
            Hide();
            Closed?.Invoke();
        }

        private void BindButtons()
        {
            if (_listenersBound)
                return;
            if (_previousResolutionButton != null) _previousResolutionButton.onClick.AddListener(SelectPreviousResolution);
            if (_nextResolutionButton != null) _nextResolutionButton.onClick.AddListener(SelectNextResolution);
            if (_masterVolume != null) _masterVolume.onValueChanged.AddListener(PreviewAudioSettings);
            if (_musicVolume != null) _musicVolume.onValueChanged.AddListener(PreviewAudioSettings);
            if (_sfxVolume != null) _sfxVolume.onValueChanged.AddListener(PreviewAudioSettings);
            if (_applyButton != null) _applyButton.onClick.AddListener(Apply);
            if (_backButton != null) _backButton.onClick.AddListener(Close);
            if (_controlsButton != null) _controlsButton.onClick.AddListener(ShowControls);
            if (_inputRebindMenu != null) _inputRebindMenu.Closed += HandleControlsClosed;
            _listenersBound = true;
        }

        private void UnbindButtons()
        {
            if (!_listenersBound)
                return;
            if (_previousResolutionButton != null) _previousResolutionButton.onClick.RemoveListener(SelectPreviousResolution);
            if (_nextResolutionButton != null) _nextResolutionButton.onClick.RemoveListener(SelectNextResolution);
            if (_masterVolume != null) _masterVolume.onValueChanged.RemoveListener(PreviewAudioSettings);
            if (_musicVolume != null) _musicVolume.onValueChanged.RemoveListener(PreviewAudioSettings);
            if (_sfxVolume != null) _sfxVolume.onValueChanged.RemoveListener(PreviewAudioSettings);
            if (_applyButton != null) _applyButton.onClick.RemoveListener(Apply);
            if (_backButton != null) _backButton.onClick.RemoveListener(Close);
            if (_controlsButton != null) _controlsButton.onClick.RemoveListener(ShowControls);
            if (_inputRebindMenu != null) _inputRebindMenu.Closed -= HandleControlsClosed;
            _listenersBound = false;
        }
    }
}
