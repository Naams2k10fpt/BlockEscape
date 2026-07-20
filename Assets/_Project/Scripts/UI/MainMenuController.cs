using BlockEscape.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BlockEscape.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _optionsButton;
        [SerializeField] private Button _exitButton;
        [SerializeField] private Text _recordsText;
        [SerializeField] private OptionsMenu _optionsMenu;
        [SerializeField] private GameObject _tutorialPanel;
        [SerializeField] private Button _tutorialContinueButton;

        private void Awake()
        {
            BlockEscape.Bootstrap.AppBootstrap.EnsureExists();
            SaveService.Load();
            SaveService.ApplyDisplaySettings();
            InputService.Current?.LoadSavedBindings();
            InputService.Current?.SetGameplayEnabled(false);
        }

        private void OnEnable()
        {
            if (_startButton != null) _startButton.onClick.AddListener(RequestStartGame);
            if (_optionsButton != null) _optionsButton.onClick.AddListener(OpenOptions);
            if (_exitButton != null) _exitButton.onClick.AddListener(ExitGame);
            if (_tutorialContinueButton != null) _tutorialContinueButton.onClick.AddListener(CompleteTutorial);
            if (_optionsMenu != null) _optionsMenu.Closed += HandleOptionsClosed;
            if (_tutorialPanel != null) _tutorialPanel.SetActive(false);
            RefreshRecords();
            if (_startButton != null) _startButton.Select();
        }

        private void OnDisable()
        {
            if (_startButton != null) _startButton.onClick.RemoveListener(RequestStartGame);
            if (_optionsButton != null) _optionsButton.onClick.RemoveListener(OpenOptions);
            if (_exitButton != null) _exitButton.onClick.RemoveListener(ExitGame);
            if (_tutorialContinueButton != null) _tutorialContinueButton.onClick.RemoveListener(CompleteTutorial);
            if (_optionsMenu != null) _optionsMenu.Closed -= HandleOptionsClosed;
        }

        private void Start()
        {
            if (_startButton != null) _startButton.Select();
        }

        public void Configure(
            Button startButton,
            Button optionsButton,
            Button exitButton,
            Text recordsText,
            OptionsMenu optionsMenu,
            GameObject tutorialPanel = null,
            Button tutorialContinueButton = null)
        {
            if (isActiveAndEnabled)
                OnDisable();

            _startButton = startButton;
            _optionsButton = optionsButton;
            _exitButton = exitButton;
            _recordsText = recordsText;
            _optionsMenu = optionsMenu;
            _tutorialPanel = tutorialPanel;
            _tutorialContinueButton = tutorialContinueButton;

            if (isActiveAndEnabled)
                OnEnable();
        }

        private void OpenOptions()
        {
            _optionsMenu?.Show();
        }

        private void RequestStartGame()
        {
            if (!SaveService.Data.hasSeenTutorial && _tutorialPanel != null)
            {
                _tutorialPanel.SetActive(true);
                _tutorialContinueButton?.Select();
                return;
            }

            StartGame();
        }

        private void CompleteTutorial()
        {
            SaveService.Data.hasSeenTutorial = true;
            SaveService.Save();
            _tutorialPanel?.SetActive(false);
            StartGame();
        }

        private void HandleOptionsClosed()
        {
            RefreshRecords();
            if (_startButton != null) _startButton.Select();
        }

        private void RefreshRecords()
        {
            if (_recordsText == null)
                return;
            var totalSeconds = Mathf.Max(0, Mathf.FloorToInt(SaveService.Data.bestSurvivalTime));
            _recordsText.text =
                $"HIGH SCORE  {SaveService.Data.highScore}\n" +
                $"BEST TIME   {totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        private static void StartGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("TetrisDemo");
        }

        private static void ExitGame()
        {
#if UNITY_EDITOR
            Debug.Log("Exit is only available in a built game.");
#else
            Application.Quit();
#endif
        }
    }
}
