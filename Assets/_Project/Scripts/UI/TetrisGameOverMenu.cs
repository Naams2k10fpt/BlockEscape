using System;
using UnityEngine;
using UnityEngine.UI;

namespace BlockEscape.UI
{
    public sealed class TetrisGameOverMenu : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _summaryText;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _mainMenuButton;

        public event Action RestartRequested;
        public event Action MainMenuRequested;

        private bool _listenersBound;

        private void OnEnable()
        {
            BindButtons();
        }

        private void OnDisable()
        {
            UnbindButtons();
        }

        public void Configure(GameObject panel, Text summaryText, Button restartButton, Button mainMenuButton)
        {
            UnbindButtons();
            _panel = panel;
            _summaryText = summaryText;
            _restartButton = restartButton;
            _mainMenuButton = mainMenuButton;
            if (isActiveAndEnabled) BindButtons();
        }

        public void Show(int piecesSpawned, int rowsCleared, int score, int seed, string reason, float survivalTime = 0f, int phase = 1)
        {
            if (_summaryText != null)
            {
                _summaryText.text =
                    $"LÝ DO: {reason}\n\n" +
                    $"BLOCK ĐÃ THẢ  {piecesSpawned}\n" +
                    $"THỜI GIAN  {FormatTime(survivalTime)}\n" +
                    $"PHASE  {phase}\n" +
                    $"HÀNG ĐÃ XÓA  {rowsCleared}\n" +
                    $"TỔNG ĐIỂM  {score}\n" +
                    $"SEED  {seed}";
            }

            if (_panel != null) _panel.SetActive(true);
            if (_restartButton != null) _restartButton.Select();
        }

        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
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
            if (_restartButton != null) _restartButton.onClick.AddListener(RequestRestart);
            if (_mainMenuButton != null) _mainMenuButton.onClick.AddListener(RequestMainMenu);
            _listenersBound = true;
        }

        private void UnbindButtons()
        {
            if (!_listenersBound)
                return;
            if (_restartButton != null) _restartButton.onClick.RemoveListener(RequestRestart);
            if (_mainMenuButton != null) _mainMenuButton.onClick.RemoveListener(RequestMainMenu);
            _listenersBound = false;
        }

        private void RequestRestart()
        {
            RestartRequested?.Invoke();
        }

        private void RequestMainMenu()
        {
            MainMenuRequested?.Invoke();
        }
    }
}
