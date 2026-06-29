using System;
using UnityEngine;
using UnityEngine.UI;

namespace BlockEscape.UI
{
    public sealed class TetrisPauseMenu : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject _pausePanel;
        [SerializeField] private GameObject _resetConfirmationPanel;
        [SerializeField] private GameObject _mainMenuConfirmationPanel;

        [Header("Run statistics")]
        [SerializeField] private Text _runStatsText;

        [Header("Pause buttons")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _mainMenuButton;

        [Header("Reset confirmation buttons")]
        [SerializeField] private Button _confirmResetButton;
        [SerializeField] private Button _cancelResetButton;

        [Header("Main menu confirmation buttons")]
        [SerializeField] private Button _confirmMainMenuButton;
        [SerializeField] private Button _cancelMainMenuButton;

        public event Action ResumeRequested;
        public event Action ResetConfirmed;
        public event Action MainMenuConfirmed;

        private bool _listenersBound;

        public bool IsConfirmationVisible =>
            (_resetConfirmationPanel != null && _resetConfirmationPanel.activeSelf) ||
            (_mainMenuConfirmationPanel != null && _mainMenuConfirmationPanel.activeSelf);

        private void OnEnable()
        {
            BindButtons();
        }

        private void OnDisable()
        {
            UnbindButtons();
        }

        public void Configure(
            GameObject pausePanel,
            GameObject resetConfirmationPanel,
            GameObject mainMenuConfirmationPanel,
            Text runStatsText,
            Button resumeButton,
            Button resetButton,
            Button mainMenuButton,
            Button confirmResetButton,
            Button cancelResetButton,
            Button confirmMainMenuButton,
            Button cancelMainMenuButton)
        {
            UnbindButtons();
            _pausePanel = pausePanel;
            _resetConfirmationPanel = resetConfirmationPanel;
            _mainMenuConfirmationPanel = mainMenuConfirmationPanel;
            _runStatsText = runStatsText;
            _resumeButton = resumeButton;
            _resetButton = resetButton;
            _mainMenuButton = mainMenuButton;
            _confirmResetButton = confirmResetButton;
            _cancelResetButton = cancelResetButton;
            _confirmMainMenuButton = confirmMainMenuButton;
            _cancelMainMenuButton = cancelMainMenuButton;
            if (isActiveAndEnabled) BindButtons();
        }

        public void SetRunStatistics(int piecesSpawned, int rowsCleared, int score, int seed, float survivalTime = 0f)
        {
            if (_runStatsText == null)
                return;

            _runStatsText.text =
                $"BLOCK ĐÃ THẢ  {piecesSpawned}\n" +
                $"THỜI GIAN  {FormatTime(survivalTime)}\n" +
                $"HÀNG ĐÃ XÓA  {rowsCleared}     ĐIỂM  {score}\n" +
                $"SEED  {seed}";
        }

        private static string FormatTime(float seconds)
        {
            var totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        private void BindButtons()
        {
            if (_listenersBound)
                return;
            if (_resumeButton != null) _resumeButton.onClick.AddListener(RequestResume);
            if (_resetButton != null) _resetButton.onClick.AddListener(ShowResetConfirmation);
            if (_mainMenuButton != null) _mainMenuButton.onClick.AddListener(ShowMainMenuConfirmation);
            if (_confirmResetButton != null) _confirmResetButton.onClick.AddListener(ConfirmReset);
            if (_cancelResetButton != null) _cancelResetButton.onClick.AddListener(ShowPause);
            if (_confirmMainMenuButton != null) _confirmMainMenuButton.onClick.AddListener(ConfirmMainMenu);
            if (_cancelMainMenuButton != null) _cancelMainMenuButton.onClick.AddListener(ShowPause);
            _listenersBound = true;
        }

        private void UnbindButtons()
        {
            if (!_listenersBound)
                return;
            if (_resumeButton != null) _resumeButton.onClick.RemoveListener(RequestResume);
            if (_resetButton != null) _resetButton.onClick.RemoveListener(ShowResetConfirmation);
            if (_mainMenuButton != null) _mainMenuButton.onClick.RemoveListener(ShowMainMenuConfirmation);
            if (_confirmResetButton != null) _confirmResetButton.onClick.RemoveListener(ConfirmReset);
            if (_cancelResetButton != null) _cancelResetButton.onClick.RemoveListener(ShowPause);
            if (_confirmMainMenuButton != null) _confirmMainMenuButton.onClick.RemoveListener(ConfirmMainMenu);
            if (_cancelMainMenuButton != null) _cancelMainMenuButton.onClick.RemoveListener(ShowPause);
            _listenersBound = false;
        }

        public void ShowPause()
        {
            if (_pausePanel != null) _pausePanel.SetActive(true);
            if (_resetConfirmationPanel != null) _resetConfirmationPanel.SetActive(false);
            if (_mainMenuConfirmationPanel != null) _mainMenuConfirmationPanel.SetActive(false);
            if (_resumeButton != null) _resumeButton.Select();
        }

        public void ShowResetConfirmation()
        {
            if (_pausePanel != null) _pausePanel.SetActive(false);
            if (_resetConfirmationPanel != null) _resetConfirmationPanel.SetActive(true);
            if (_mainMenuConfirmationPanel != null) _mainMenuConfirmationPanel.SetActive(false);
            if (_cancelResetButton != null) _cancelResetButton.Select();
        }

        public void ShowMainMenuConfirmation()
        {
            if (_pausePanel != null) _pausePanel.SetActive(false);
            if (_resetConfirmationPanel != null) _resetConfirmationPanel.SetActive(false);
            if (_mainMenuConfirmationPanel != null) _mainMenuConfirmationPanel.SetActive(true);
            if (_cancelMainMenuButton != null) _cancelMainMenuButton.Select();
        }

        public void HideAll()
        {
            if (_pausePanel != null) _pausePanel.SetActive(false);
            if (_resetConfirmationPanel != null) _resetConfirmationPanel.SetActive(false);
            if (_mainMenuConfirmationPanel != null) _mainMenuConfirmationPanel.SetActive(false);
        }

        private void RequestResume()
        {
            ResumeRequested?.Invoke();
        }

        private void ConfirmReset()
        {
            ResetConfirmed?.Invoke();
        }

        private void ConfirmMainMenu()
        {
            MainMenuConfirmed?.Invoke();
        }
    }
}
