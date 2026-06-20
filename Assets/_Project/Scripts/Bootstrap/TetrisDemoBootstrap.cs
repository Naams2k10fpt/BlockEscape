using BlockEscape.Core;
using BlockEscape.Tetris;
using BlockEscape.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BlockEscape.Bootstrap
{
    public sealed class TetrisDemoBootstrap : MonoBehaviour
    {
        [Header("Game configuration")]
        [SerializeField] private TetrisBalanceConfig _config;

        [Header("Scene references")]
        [SerializeField] private Camera _sceneCamera;
        [SerializeField] private Transform _arenaVisuals;
        [SerializeField] private BlockBoard _board;
        [SerializeField] private TetrominoSpawner _spawner;
        [SerializeField] private InputService _inputService;

        [Header("HUD references")]
        [SerializeField] private Text _statsText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Image _overflowFill;
        [SerializeField] private NextPiecePreview _nextPiecePreview;
        [SerializeField] private TetrisPauseMenu _pauseMenu;
        [SerializeField] private TetrisGameOverMenu _gameOverMenu;

        private int _rowsCleared;
        private int _lineScore;
        private TetrominoKind _lastSpawned;
        private bool _paused;
        private bool _gameOver;

        private void Awake()
        {
            Time.timeScale = 1f;
            if (_config == null)
                _config = Resources.Load<TetrisBalanceConfig>("TetrisBalanceConfig");
            if (_config == null)
                _config = CreateRuntimeDefaults();

            _config.Sanitize();
            RecoverSceneReferences();
            ConfigureCamera();
            InitializeInput();
            if (_arenaVisuals == null)
                CreateArenaVisuals();
            if (_statsText == null || _statusText == null || _overflowFill == null)
                CreateHud();
            else
            {
                var existingCanvas = FindAnyObjectByType<Canvas>();
                if (existingCanvas != null)
                {
                    if (_pauseMenu == null)
                        _pauseMenu = CreatePauseMenu(existingCanvas.transform);
                    if (_gameOverMenu == null)
                        _gameOverMenu = CreateGameOverMenu(existingCanvas.transform);
                    if (_pauseMenu != null || _gameOverMenu != null)
                        EnsureEventSystem();
                }
            }
            InitializeSystems();
            if (_statusText != null)
            {
                _statusText.text = "RUNNING";
                _statusText.color = Color.white;
            }
            InitializePauseFlow();
            InitializeGameOverFlow();
        }

        private void Update()
        {
            if (!_gameOver)
            {
                if (_inputService != null && _inputService.ResetRun.WasPressedThisFrame())
                    OpenResetConfirmation();
                else if (_inputService != null && _inputService.Pause.WasPressedThisFrame())
                    HandleEscape();
            }

            UpdateHud();
        }

        private void OnDestroy()
        {
            if (_pauseMenu != null)
            {
                _pauseMenu.ResumeRequested -= ResumeGame;
                _pauseMenu.ResetConfirmed -= ResetDemo;
                _pauseMenu.MainMenuConfirmed -= ReturnToMainMenu;
            }
            if (_gameOverMenu != null)
            {
                _gameOverMenu.RestartRequested -= ResetDemo;
                _gameOverMenu.MainMenuRequested -= ReturnToMainMenu;
            }
            Time.timeScale = 1f;
        }

        private void InitializeSystems()
        {
            if (_board == null || _spawner == null)
            {
                var boardObject = new GameObject("Block Board 14x20");
                boardObject.transform.SetParent(transform, false);
                boardObject.transform.position = new Vector3(-_config.boardWidth * 0.5f, -10f, 0f);
                _board = boardObject.AddComponent<BlockBoard>();
                _spawner = boardObject.AddComponent<TetrominoSpawner>();
            }

            _board.Initialize(_config);
            _board.PieceLocked += OnPieceLocked;
            _board.RowsCleared += OnRowsCleared;
            _board.OverflowChanged += OnOverflowChanged;
            _board.Overflowed += OnOverflowed;

            _spawner.PieceSpawned += kind => _lastSpawned = kind;
            _spawner.NextPieceChanged += OnNextPieceChanged;
            _spawner.Initialize(_board, _config, _inputService);
        }

        private void RecoverSceneReferences()
        {
            if (_board == null)
                _board = FindAnyObjectByType<BlockBoard>();
            if (_spawner == null)
                _spawner = FindAnyObjectByType<TetrominoSpawner>();
            if (_arenaVisuals == null)
            {
                var arenaObject = GameObject.Find("Arena Visuals");
                if (arenaObject != null) _arenaVisuals = arenaObject.transform;
            }

            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (var canvas in canvases)
            {
                if (_statsText == null)
                    _statsText = FindNamedComponent<Text>(canvas.transform, "Game Statistics");
                if (_statusText == null)
                    _statusText = FindNamedComponent<Text>(canvas.transform, "Game Status");
                if (_overflowFill == null)
                    _overflowFill = FindNamedComponent<Image>(canvas.transform, "Danger Fill");
                if (_nextPiecePreview == null)
                    _nextPiecePreview = FindNamedComponent<NextPiecePreview>(canvas.transform, "Next Piece Preview");
                if (_pauseMenu == null)
                    _pauseMenu = canvas.GetComponentInChildren<TetrisPauseMenu>(true);
                if (_gameOverMenu == null)
                    _gameOverMenu = canvas.GetComponentInChildren<TetrisGameOverMenu>(true);
            }
        }

        private static T FindNamedComponent<T>(Transform root, string objectName) where T : Component
        {
            var components = root.GetComponentsInChildren<T>(true);
            foreach (var component in components)
                if (component.gameObject.name == objectName)
                    return component;
            return null;
        }

        private void InitializeInput()
        {
            if (InputService.Current != null)
                _inputService = InputService.Current;

            if (_inputService == null)
            {
                var inputObject = new GameObject("Input Service (Persistent)");
                _inputService = inputObject.AddComponent<InputService>();
            }

            _inputService.EnsureInitialized();
            _inputService.SetGameplayEnabled(true);
        }

        private void ConfigureCamera()
        {
            var camera = _sceneCamera != null ? _sceneCamera : Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            _sceneCamera = camera;

            camera.orthographic = true;
            camera.orthographicSize = 14f;
            camera.transform.position = new Vector3(0f, 1.5f, -10f);
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.09f);
            camera.clearFlags = CameraClearFlags.SolidColor;
        }

        private void CreateArenaVisuals()
        {
            var root = new GameObject("Arena Visuals").transform;
            root.SetParent(transform, false);
            _arenaVisuals = root;
            var origin = new Vector2(-_config.boardWidth * 0.5f, -10f);
            var center = origin + new Vector2(_config.boardWidth * 0.5f, _config.boardHeight * 0.5f);

            RuntimeVisuals.CreateQuad("Board Background", root, center, new Vector2(_config.boardWidth, _config.boardHeight), new Color(0.07f, 0.085f, 0.15f), -20);

            for (var x = 0; x <= _config.boardWidth; x++)
            {
                var position = new Vector3(origin.x + x, center.y, 0f);
                RuntimeVisuals.CreateQuad($"Grid X {x}", root, position, new Vector2(0.025f, _config.boardHeight), new Color(0.3f, 0.4f, 0.6f, 0.18f), -10);
            }

            for (var y = 0; y <= _config.boardHeight; y++)
            {
                var position = new Vector3(center.x, origin.y + y, 0f);
                RuntimeVisuals.CreateQuad($"Grid Y {y}", root, position, new Vector2(_config.boardWidth, 0.025f), new Color(0.3f, 0.4f, 0.6f, 0.18f), -10);
            }

            var dangerY = origin.y + _config.dangerStartRow;
            RuntimeVisuals.CreateQuad("Danger Line", root, new Vector3(center.x, dangerY, 0f), new Vector2(_config.boardWidth, 0.08f), new Color(1f, 0.2f, 0.25f, 0.85f), 5);

            var borderColor = new Color(0.35f, 0.65f, 1f, 0.9f);
            RuntimeVisuals.CreateQuad("Border Left", root, new Vector3(origin.x - 0.08f, center.y, 0f), new Vector2(0.16f, _config.boardHeight + 0.2f), borderColor, 5);
            RuntimeVisuals.CreateQuad("Border Right", root, new Vector3(origin.x + _config.boardWidth + 0.08f, center.y, 0f), new Vector2(0.16f, _config.boardHeight + 0.2f), borderColor, 5);
            RuntimeVisuals.CreateQuad("Border Bottom", root, new Vector3(center.x, origin.y - 0.08f, 0f), new Vector2(_config.boardWidth + 0.2f, 0.16f), borderColor, 5);
        }

        private void CreateHud()
        {
            var canvasObject = new GameObject("Tetris Demo HUD");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var title = CreateText(canvasObject.transform, "Title", "BLOCK ESCAPE  /  TETRIS CORE", 34, TextAnchor.UpperLeft);
            SetRect(title.rectTransform, new Vector2(24f, -20f), new Vector2(760f, 60f), new Vector2(0f, 1f));
            title.color = new Color(0.35f, 0.85f, 1f);

            _statsText = CreateText(canvasObject.transform, "Stats", string.Empty, 24, TextAnchor.UpperLeft);
            SetRect(_statsText.rectTransform, new Vector2(24f, -80f), new Vector2(520f, 220f), new Vector2(0f, 1f));

            var help = CreateText(canvasObject.transform, "Tetris Controls (WASD)", "A / D  MOVE\nW  ROTATE\nS  SOFT DROP\nR  RESET MENU\nESC  PAUSE", 21, TextAnchor.LowerRight);
            SetRect(help.rectTransform, new Vector2(-24f, 24f), new Vector2(500f, 190f), new Vector2(1f, 0f));
            help.color = new Color(0.7f, 0.78f, 0.9f);

            _statusText = CreateText(canvasObject.transform, "Status", "RUNNING", 30, TextAnchor.UpperCenter);
            SetRect(_statusText.rectTransform, new Vector2(0f, -20f), new Vector2(600f, 60f), new Vector2(0.5f, 1f));

            var overflowRoot = new GameObject("Overflow Meter");
            overflowRoot.transform.SetParent(canvasObject.transform, false);
            var rootImage = overflowRoot.AddComponent<Image>();
            rootImage.color = new Color(0f, 0f, 0f, 0.65f);
            SetRect(rootImage.rectTransform, new Vector2(-24f, -24f), new Vector2(420f, 26f), new Vector2(1f, 1f));

            var fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(overflowRoot.transform, false);
            _overflowFill = fillObject.AddComponent<Image>();
            _overflowFill.color = new Color(1f, 0.2f, 0.25f);
            _overflowFill.type = Image.Type.Filled;
            _overflowFill.fillMethod = Image.FillMethod.Horizontal;
            _overflowFill.fillAmount = 0f;
            var fillRect = _overflowFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);

            _pauseMenu = CreatePauseMenu(canvasObject.transform);
            _gameOverMenu = CreateGameOverMenu(canvasObject.transform);
            EnsureEventSystem();
        }

        private void UpdateHud()
        {
            if (_statsText == null || _board == null || _spawner == null)
                return;

            _statsText.text =
                $"SEED       {_spawner.Seed}\n" +
                $"SPAWNED    {_spawner.PiecesSpawned}\n" +
                $"LAST PIECE {_lastSpawned}\n" +
                $"LOCKED     {_board.Model.OccupiedCount} CELLS\n" +
                $"ROWS       {_rowsCleared}\n" +
                $"LINE SCORE {_lineScore}";
        }

        private void ResetDemo()
        {
            Time.timeScale = 1f;
            _paused = false;
            _gameOver = false;
            if (_inputService != null) _inputService.SetGameplayEnabled(true);
            if (_pauseMenu != null) _pauseMenu.HideAll();
            if (_gameOverMenu != null) _gameOverMenu.Hide();
            _rowsCleared = 0;
            _lineScore = 0;
            _board.ResetBoard();
            _spawner.Restart();
            _statusText.text = "RUNNING";
            _statusText.color = Color.white;
        }

        private void InitializePauseFlow()
        {
            if (_pauseMenu == null)
                return;

            _pauseMenu.ResumeRequested += ResumeGame;
            _pauseMenu.ResetConfirmed += ResetDemo;
            _pauseMenu.MainMenuConfirmed += ReturnToMainMenu;
            UpdatePauseStatistics();
            _pauseMenu.HideAll();
        }

        private void InitializeGameOverFlow()
        {
            if (_gameOverMenu == null)
                return;

            _gameOverMenu.RestartRequested += ResetDemo;
            _gameOverMenu.MainMenuRequested += ReturnToMainMenu;
            _gameOverMenu.Hide();
        }

        private void HandleEscape()
        {
            if (_pauseMenu != null && _pauseMenu.IsConfirmationVisible)
            {
                UpdatePauseStatistics();
                _pauseMenu.ShowPause();
                return;
            }

            if (_paused)
                ResumeGame();
            else
                PauseGame();
        }

        private void PauseGame()
        {
            _paused = true;
            Time.timeScale = 0f;
            if (_inputService != null) _inputService.SetGameplayEnabled(false);
            if (_pauseMenu != null)
            {
                UpdatePauseStatistics();
                _pauseMenu.ShowPause();
            }
            if (_statusText != null)
            {
                _statusText.text = "PAUSED";
                _statusText.color = new Color(1f, 0.85f, 0.25f);
            }
        }

        private void ResumeGame()
        {
            _paused = false;
            Time.timeScale = 1f;
            if (_inputService != null)
                _inputService.SetGameplayEnabled(_board != null && !_board.IsOverflowed);
            if (_pauseMenu != null) _pauseMenu.HideAll();
            if (_statusText != null)
            {
                _statusText.text = _board != null && _board.IsOverflowed ? "BOARD OVERFLOW — PRESS R" : "RUNNING";
                _statusText.color = _board != null && _board.IsOverflowed
                    ? new Color(1f, 0.2f, 0.25f)
                    : Color.white;
            }
        }

        private void OpenResetConfirmation()
        {
            _paused = true;
            Time.timeScale = 0f;
            if (_inputService != null) _inputService.SetGameplayEnabled(false);
            if (_statusText != null)
            {
                _statusText.text = "PAUSED";
                _statusText.color = new Color(1f, 0.85f, 0.25f);
            }
            if (_pauseMenu != null)
                _pauseMenu.ShowResetConfirmation();
            else
                ResetDemo();
        }

        private void UpdatePauseStatistics()
        {
            if (_pauseMenu == null || _spawner == null)
                return;

            _pauseMenu.SetRunStatistics(
                _spawner.PiecesSpawned,
                _rowsCleared,
                _lineScore,
                _spawner.Seed);
        }

        private void OnPieceLocked(TetrominoKind kind)
        {
            if (_statusText != null && !_paused)
                _statusText.text = $"{kind} LOCKED";
        }

        private void OnNextPieceChanged(TetrominoKind kind)
        {
            if (_nextPiecePreview != null)
                _nextPiecePreview.Show(kind);
        }

        private void OnRowsCleared(int[] rows)
        {
            _rowsCleared += rows.Length;
            _lineScore += rows.Length switch
            {
                1 => 250,
                2 => 600,
                3 => 1000,
                _ => 1500
            };
            _statusText.text = $"{rows.Length} ROW{(rows.Length > 1 ? "S" : string.Empty)} CLEARED";
            _statusText.color = new Color(0.35f, 1f, 0.55f);
        }

        private void OnOverflowChanged(bool dangerous, float normalized)
        {
            if (_overflowFill != null)
                _overflowFill.fillAmount = normalized;
            if (dangerous && !_paused && _statusText != null)
            {
                _statusText.text = "DANGER — CLEAR THE TOP";
                _statusText.color = new Color(1f, 0.25f, 0.25f);
            }
        }

        private void OnOverflowed()
        {
            _gameOver = true;
            _paused = false;
            Time.timeScale = 0f;
            if (_inputService != null) _inputService.SetGameplayEnabled(false);
            if (_pauseMenu != null) _pauseMenu.HideAll();
            if (_gameOverMenu != null && _spawner != null)
            {
                _gameOverMenu.Show(
                    _spawner.PiecesSpawned,
                    _rowsCleared,
                    _lineScore,
                    _spawner.Seed,
                    "BLOCK CHẠM VÙNG NGUY HIỂM");
            }
            if (_statusText == null)
                return;
            _statusText.text = "GAME OVER";
            _statusText.color = new Color(1f, 0.2f, 0.25f);
        }

        private void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }

        private static TetrisPauseMenu CreatePauseMenu(Transform canvasRoot)
        {
            var controllerObject = new GameObject("Pause Menu Controller");
            controllerObject.transform.SetParent(canvasRoot, false);
            var controller = controllerObject.AddComponent<TetrisPauseMenu>();

            var pausePanel = CreateOverlay(canvasRoot, "Pause Menu Overlay", new Color(0f, 0f, 0f, 0.72f));
            var pauseDialog = CreatePanel(pausePanel.transform, "Pause Dialog", new Vector2(560f, 580f));
            var pauseTitle = CreateText(pauseDialog.transform, "Pause Title", "TẠM DỪNG", 40, TextAnchor.UpperCenter);
            SetRect(pauseTitle.rectTransform, new Vector2(0f, -30f), new Vector2(460f, 60f), new Vector2(0.5f, 1f));
            pauseTitle.color = new Color(0.35f, 0.85f, 1f);

            var runStats = CreateText(pauseDialog.transform, "Pause Run Statistics", "BLOCK ĐÃ THẢ  0\nHÀNG ĐÃ XÓA  0     ĐIỂM  0\nSEED  0", 22, TextAnchor.MiddleCenter);
            SetRect(runStats.rectTransform, new Vector2(0f, 100f), new Vector2(500f, 105f), new Vector2(0.5f, 0.5f));
            runStats.color = new Color(0.45f, 1f, 0.65f);

            var controls = CreateText(pauseDialog.transform, "Pause Controls", "A/D: Di chuyển   W: Xoay   S: Soft drop\nESC: Tiếp tục", 20, TextAnchor.MiddleCenter);
            SetRect(controls.rectTransform, new Vector2(0f, 15f), new Vector2(480f, 60f), new Vector2(0.5f, 0.5f));
            controls.color = new Color(0.72f, 0.8f, 0.92f);

            var resumeButton = CreateButton(pauseDialog.transform, "Resume Button", "TIẾP TỤC", new Vector2(0f, -75f), new Vector2(320f, 58f));
            var resetButton = CreateButton(pauseDialog.transform, "Reset Button", "CHƠI LẠI", new Vector2(0f, -150f), new Vector2(320f, 58f));
            var mainMenuButton = CreateButton(pauseDialog.transform, "Pause Main Menu Button", "TRỞ VỀ MAIN MENU", new Vector2(0f, -225f), new Vector2(320f, 58f));

            var confirmationPanel = CreateOverlay(canvasRoot, "Reset Confirmation Overlay", new Color(0f, 0f, 0f, 0.82f));
            var confirmationDialog = CreatePanel(confirmationPanel.transform, "Reset Confirmation Dialog", new Vector2(620f, 330f));
            var confirmationTitle = CreateText(confirmationDialog.transform, "Confirmation Title", "XÁC NHẬN CHƠI LẠI", 34, TextAnchor.UpperCenter);
            SetRect(confirmationTitle.rectTransform, new Vector2(0f, -28f), new Vector2(560f, 55f), new Vector2(0.5f, 1f));
            confirmationTitle.color = new Color(1f, 0.72f, 0.25f);

            var confirmationMessage = CreateText(confirmationDialog.transform, "Confirmation Message", "Bạn có muốn reset lại màn chơi không?\nToàn bộ tiến trình của lượt hiện tại sẽ bị mất.", 23, TextAnchor.MiddleCenter);
            SetRect(confirmationMessage.rectTransform, new Vector2(0f, 25f), new Vector2(540f, 100f), new Vector2(0.5f, 0.5f));

            var confirmButton = CreateButton(confirmationDialog.transform, "Confirm Reset Button", "CÓ, RESET", new Vector2(-145f, -105f), new Vector2(250f, 58f));
            var cancelButton = CreateButton(confirmationDialog.transform, "Cancel Reset Button", "HỦY", new Vector2(145f, -105f), new Vector2(250f, 58f));

            var mainMenuConfirmationPanel = CreateOverlay(canvasRoot, "Main Menu Confirmation Overlay", new Color(0f, 0f, 0f, 0.82f));
            var mainMenuConfirmationDialog = CreatePanel(mainMenuConfirmationPanel.transform, "Main Menu Confirmation Dialog", new Vector2(660f, 350f));
            var mainMenuConfirmationTitle = CreateText(mainMenuConfirmationDialog.transform, "Main Menu Confirmation Title", "TRỞ VỀ MAIN MENU?", 34, TextAnchor.UpperCenter);
            SetRect(mainMenuConfirmationTitle.rectTransform, new Vector2(0f, -28f), new Vector2(600f, 55f), new Vector2(0.5f, 1f));
            mainMenuConfirmationTitle.color = new Color(1f, 0.72f, 0.25f);

            var mainMenuMessage = CreateText(mainMenuConfirmationDialog.transform, "Main Menu Confirmation Message", "Bạn có chắc muốn rời lượt chơi hiện tại?\nToàn bộ tiến trình chưa lưu sẽ bị mất.", 23, TextAnchor.MiddleCenter);
            SetRect(mainMenuMessage.rectTransform, new Vector2(0f, 28f), new Vector2(580f, 105f), new Vector2(0.5f, 0.5f));

            var confirmMainMenuButton = CreateButton(mainMenuConfirmationDialog.transform, "Confirm Main Menu Button", "ĐỒNG Ý", new Vector2(-155f, -112f), new Vector2(270f, 58f));
            var cancelMainMenuButton = CreateButton(mainMenuConfirmationDialog.transform, "Cancel Main Menu Button", "HỦY", new Vector2(155f, -112f), new Vector2(270f, 58f));

            controller.Configure(
                pausePanel,
                confirmationPanel,
                mainMenuConfirmationPanel,
                runStats,
                resumeButton,
                resetButton,
                mainMenuButton,
                confirmButton,
                cancelButton,
                confirmMainMenuButton,
                cancelMainMenuButton);

            pausePanel.SetActive(false);
            confirmationPanel.SetActive(false);
            mainMenuConfirmationPanel.SetActive(false);
            return controller;
        }

        private static TetrisGameOverMenu CreateGameOverMenu(Transform canvasRoot)
        {
            var controllerObject = new GameObject("Game Over Menu Controller");
            controllerObject.transform.SetParent(canvasRoot, false);
            var controller = controllerObject.AddComponent<TetrisGameOverMenu>();

            var panel = CreateOverlay(canvasRoot, "Game Over Overlay", new Color(0f, 0f, 0f, 0.88f));
            var dialog = CreatePanel(panel.transform, "Game Over Summary Dialog", new Vector2(680f, 620f));

            var title = CreateText(dialog.transform, "Game Over Title", "GAME OVER", 52, TextAnchor.UpperCenter);
            SetRect(title.rectTransform, new Vector2(0f, -35f), new Vector2(600f, 75f), new Vector2(0.5f, 1f));
            title.color = new Color(1f, 0.25f, 0.3f);

            var summary = CreateText(dialog.transform, "Run Summary", "KẾT QUẢ LƯỢT CHƠI", 25, TextAnchor.MiddleCenter);
            SetRect(summary.rectTransform, new Vector2(0f, 55f), new Vector2(590f, 280f), new Vector2(0.5f, 0.5f));
            summary.color = new Color(0.82f, 0.9f, 1f);

            var restartButton = CreateButton(dialog.transform, "Restart Run Button", "CHƠI LẠI", new Vector2(0f, -155f), new Vector2(360f, 62f));
            var mainMenuButton = CreateButton(dialog.transform, "Return Main Menu Button", "MAIN MENU", new Vector2(0f, -235f), new Vector2(360f, 62f));

            controller.Configure(panel, summary, restartButton, mainMenuButton);
            panel.SetActive(false);
            return controller;
        }

        private static GameObject CreateOverlay(Transform parent, string name, Color color)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.AddComponent<Image>();
            image.color = color;
            StretchFullScreen(image.rectTransform);
            return gameObject;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 size)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.AddComponent<Image>();
            image.color = new Color(0.055f, 0.07f, 0.13f, 0.98f);
            SetRect(image.rectTransform, Vector2.zero, size, new Vector2(0.5f, 0.5f));
            return gameObject;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.AddComponent<Image>();
            image.color = new Color(0.16f, 0.34f, 0.62f, 1f);
            SetRect(image.rectTransform, position, size, new Vector2(0.5f, 0.5f));

            var button = gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.75f, 0.9f, 1f);
            colors.pressedColor = new Color(0.55f, 0.75f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            var text = CreateText(gameObject.transform, "Label", label, 24, TextAnchor.MiddleCenter);
            StretchFullScreen(text.rectTransform);
            return button;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var eventSystemObject = new GameObject("Event System");
            eventSystemObject.SetActive(false);
            eventSystemObject.AddComponent<EventSystem>();
            var inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            UiInputActions.AssignTo(inputModule);
            eventSystemObject.SetActive(true);
        }

        private static void StretchFullScreen(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Text CreateText(Transform parent, string name, string value, int fontSize, TextAnchor alignment)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static TetrisBalanceConfig CreateRuntimeDefaults()
        {
            var config = ScriptableObject.CreateInstance<TetrisBalanceConfig>();
            config.name = "Runtime Tetris Defaults";
            return config;
        }
    }
}
