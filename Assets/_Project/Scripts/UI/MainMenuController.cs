using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BlockEscape.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _exitButton;

        private void OnEnable()
        {
            if (_startButton != null) _startButton.onClick.AddListener(StartGame);
            if (_exitButton != null) _exitButton.onClick.AddListener(ExitGame);
            if (_startButton != null) _startButton.Select();
        }

        private void OnDisable()
        {
            if (_startButton != null) _startButton.onClick.RemoveListener(StartGame);
            if (_exitButton != null) _exitButton.onClick.RemoveListener(ExitGame);
        }

        private void Start()
        {
            if (_startButton != null) _startButton.Select();
        }

        public void Configure(Button startButton, Button exitButton)
        {
            if (isActiveAndEnabled)
                OnDisable();

            _startButton = startButton;
            _exitButton = exitButton;

            if (isActiveAndEnabled)
                OnEnable();
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
