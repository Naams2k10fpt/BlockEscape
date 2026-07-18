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

        private void Awake()
        {
            SaveService.Load();
            SaveService.ApplyDisplaySettings();
        }

        private void OnEnable()
        {
            if (_startButton != null) _startButton.onClick.AddListener(StartGame);
            if (_optionsButton != null) _optionsButton.onClick.AddListener(OpenOptions);
            if (_exitButton != null) _exitButton.onClick.AddListener(ExitGame);
            if (_optionsMenu != null) _optionsMenu.Closed += HandleOptionsClosed;
            RefreshRecords();
            if (_startButton != null) _startButton.Select();
        }

        private void OnDisable()
        {
            if (_startButton != null) _startButton.onClick.RemoveListener(StartGame);
            if (_optionsButton != null) _optionsButton.onClick.RemoveListener(OpenOptions);
            if (_exitButton != null) _exitButton.onClick.RemoveListener(ExitGame);
            if (_optionsMenu != null) _optionsMenu.Closed -= HandleOptionsClosed;
        }

        private void Start()
        {
            if (_startButton != null) _startButton.Select();
        }

        public void Configure(Button startButton, Button optionsButton, Button exitButton, Text recordsText, OptionsMenu optionsMenu)
        {
            if (isActiveAndEnabled)
                OnDisable();

            _startButton = startButton;
            _optionsButton = optionsButton;
            _exitButton = exitButton;
            _recordsText = recordsText;
            _optionsMenu = optionsMenu;

            if (isActiveAndEnabled)
                OnEnable();
        }

        private void OpenOptions()
        {
            _optionsMenu?.Show();
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
