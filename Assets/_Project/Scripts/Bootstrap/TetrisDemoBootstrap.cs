using BlockEscape.Tetris;
using BlockEscape.UI;
using UnityEngine;
using UnityEngine.InputSystem;
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

        [Header("HUD references")]
        [SerializeField] private Text _statsText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Image _overflowFill;
        [SerializeField] private NextPiecePreview _nextPiecePreview;

        private int _rowsCleared;
        private int _lineScore;
        private TetrominoKind _lastSpawned;
        private bool _paused;

        private void Awake()
        {
            Time.timeScale = 1f;
            if (_config == null)
                _config = Resources.Load<TetrisBalanceConfig>("TetrisBalanceConfig");
            if (_config == null)
                _config = CreateRuntimeDefaults();

            _config.Sanitize();
            ConfigureCamera();
            if (_arenaVisuals == null)
                CreateArenaVisuals();
            InitializeSystems();
            if (_statsText == null || _statusText == null || _overflowFill == null)
                CreateHud();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame)
                    ResetDemo();
                if (keyboard.pKey.wasPressedThisFrame)
                    TogglePause();
            }

            UpdateHud();
        }

        private void OnDestroy()
        {
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
            _spawner.Initialize(_board, _config);
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

            var help = CreateText(canvasObject.transform, "Tetris Controls (WASD)", "A / D  MOVE\nW  ROTATE\nS  SOFT DROP\nR  RESET\nP  PAUSE", 21, TextAnchor.LowerRight);
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
            _rowsCleared = 0;
            _lineScore = 0;
            _board.ResetBoard();
            _spawner.Restart();
            _statusText.text = "RUNNING";
            _statusText.color = Color.white;
        }

        private void TogglePause()
        {
            if (_board.IsOverflowed)
                return;
            _paused = !_paused;
            Time.timeScale = _paused ? 0f : 1f;
            _statusText.text = _paused ? "PAUSED" : "RUNNING";
            _statusText.color = _paused ? new Color(1f, 0.85f, 0.25f) : Color.white;
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
            if (_statusText == null)
                return;
            _statusText.text = "BOARD OVERFLOW — PRESS R";
            _statusText.color = new Color(1f, 0.2f, 0.25f);
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
