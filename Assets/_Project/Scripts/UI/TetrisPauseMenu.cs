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

        [Header("Pause buttons")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _resetButton;

        [Header("Reset confirmation buttons")]
        [SerializeField] private Button _confirmResetButton;
        [SerializeField] private Button _cancelResetButton;

        public event Action ResumeRequested;
        public event Action ResetConfirmed;

        private bool _listenersBound;

        public bool IsResetConfirmationVisible =>
            _resetConfirmationPanel != null && _resetConfirmationPanel.activeSelf;

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
            Button resumeButton,
            Button resetButton,
            Button confirmResetButton,
            Button cancelResetButton)
        {
            UnbindButtons();
            _pausePanel = pausePanel;
            _resetConfirmationPanel = resetConfirmationPanel;
            _resumeButton = resumeButton;
            _resetButton = resetButton;
            _confirmResetButton = confirmResetButton;
            _cancelResetButton = cancelResetButton;
            if (isActiveAndEnabled) BindButtons();
        }

        private void BindButtons()
        {
            if (_listenersBound)
                return;
            if (_resumeButton != null) _resumeButton.onClick.AddListener(RequestResume);
            if (_resetButton != null) _resetButton.onClick.AddListener(ShowResetConfirmation);
            if (_confirmResetButton != null) _confirmResetButton.onClick.AddListener(ConfirmReset);
            if (_cancelResetButton != null) _cancelResetButton.onClick.AddListener(ShowPause);
            _listenersBound = true;
        }

        private void UnbindButtons()
        {
            if (!_listenersBound)
                return;
            if (_resumeButton != null) _resumeButton.onClick.RemoveListener(RequestResume);
            if (_resetButton != null) _resetButton.onClick.RemoveListener(ShowResetConfirmation);
            if (_confirmResetButton != null) _confirmResetButton.onClick.RemoveListener(ConfirmReset);
            if (_cancelResetButton != null) _cancelResetButton.onClick.RemoveListener(ShowPause);
            _listenersBound = false;
        }

        public void ShowPause()
        {
            if (_pausePanel != null) _pausePanel.SetActive(true);
            if (_resetConfirmationPanel != null) _resetConfirmationPanel.SetActive(false);
            if (_resumeButton != null) _resumeButton.Select();
        }

        public void ShowResetConfirmation()
        {
            if (_pausePanel != null) _pausePanel.SetActive(false);
            if (_resetConfirmationPanel != null) _resetConfirmationPanel.SetActive(true);
            if (_cancelResetButton != null) _cancelResetButton.Select();
        }

        public void HideAll()
        {
            if (_pausePanel != null) _pausePanel.SetActive(false);
            if (_resetConfirmationPanel != null) _resetConfirmationPanel.SetActive(false);
        }

        private void RequestResume()
        {
            ResumeRequested?.Invoke();
        }

        private void ConfirmReset()
        {
            ResetConfirmed?.Invoke();
        }
    }
}
