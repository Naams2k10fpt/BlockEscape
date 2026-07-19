using System.Collections;
using BlockEscape.AI;
using BlockEscape.Core;
using BlockEscape.Events;
using BlockEscape.Player;
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
        [SerializeField] private Text _healthText;
        [SerializeField] private NextPiecePreview _nextPiecePreview;
        [SerializeField] private TetrisPauseMenu _pauseMenu;
        [SerializeField] private TetrisGameOverMenu _gameOverMenu;

        private readonly GameSession _session = new();
        private TetrominoKind _lastSpawned;
        private bool _paused;
        private bool _gameOver;
        private Transform _player;
        private PlayerController _playerController;
        private PlayerHealth _playerHealth;
        private SpriteRenderer _overflowWarningRenderer;
        private Coroutine _respawnHoverRoutine;
        private DroneController _drone;
        private EventDirector _eventDirector;
        private PickupDirector _pickupDirector;
        private string _lastDynamicEvent = "NONE";

        private static readonly Vector3 PlayerSpawnPosition = new(0f, -4.6f, 0f);
        private const float CrushRespawnHeightAboveHighestBlock = 5f;
        private const float CrushRespawnInvulnerabilitySeconds = 3f;
        private const float CrushRespawnHoverSeconds = 0.75f;
        private const float PlayerUnstuckPenetrationThreshold = 0.05f;
        private const float RespawnProbeStep = 1f;
        private const int RespawnProbeSteps = 12;
        private const float CountdownSeconds = 3f;

        private void Awake()
        {
            Time.timeScale = 1f;
            SaveService.Load();
            SaveService.ApplyDisplaySettings();
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
            EnsureOverflowWarningVisual();
            EnsureArenaBoundaryColliders();
            EnsurePlayerTestRig();
            if (_statsText == null || _statusText == null)
                CreateHud();
            else
            {
                var existingCanvas = FindAnyObjectByType<Canvas>();
                if (existingCanvas != null)
                {
                    if (_healthText == null)
                        _healthText = CreateHealthText(existingCanvas.transform);
                    if (_pauseMenu == null)
                        _pauseMenu = CreatePauseMenu(existingCanvas.transform);
                    if (_gameOverMenu == null)
                        _gameOverMenu = CreateGameOverMenu(existingCanvas.transform);
                    if (_pauseMenu != null || _gameOverMenu != null)
                        EnsureEventSystem();
                }
            }
            InitializeSystems();
            InitializeAiAndEvents();
            InitializePickups();
            _session.StateChanged += OnSessionStateChanged;
            _session.PhaseChanged += OnSessionPhaseChanged;
            StartCountdown();
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

            _session.Tick(Time.deltaTime);
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
            if (_board != null)
            {
                _board.PieceLocked -= OnPieceLocked;
                _board.RowsCleared -= OnRowsCleared;
                _board.OverflowChanged -= OnOverflowChanged;
                _board.Overflowed -= OnOverflowed;
                _board.PlayerCrushed -= OnPlayerCrushed;
            }
            if (_spawner != null)
                _spawner.PlayerCrushed -= OnPlayerCrushed;
            UnbindAiAndEvents();
            _session.StateChanged -= OnSessionStateChanged;
            _session.PhaseChanged -= OnSessionPhaseChanged;
            UnbindPlayerHealth();
            StopRespawnHover(restoreGravity: true);
            Time.timeScale = 1f;
        }

        private void FixedUpdate()
        {
            if (!_gameOver)
            {
                ClampPlayerToArena();
                ResolvePlayerBlockOverlap();
            }
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
            _board.PlayerCrushed += OnPlayerCrushed;

            _spawner.PieceSpawned += kind => _lastSpawned = kind;
            _spawner.NextPieceChanged += OnNextPieceChanged;
            _spawner.PlayerCrushed += OnPlayerCrushed;
            _spawner.Initialize(_board, _config, _inputService, startSpawning: false);
        }

        private void InitializeAiAndEvents()
        {
            if (_board == null)
                return;

            if (_drone == null)
                _drone = CreateRuntimeDrone();
            if (_drone != null)
            {
                var droneConfig = Resources.Load<DroneConfig>("DroneConfig");
                _drone.Initialize(droneConfig, _player, _board);
                _drone.DestroyedByFallingBlock += OnDroneDestroyedByFallingBlock;
            }

            if (_eventDirector == null)
            {
                var eventObject = new GameObject("Dynamic Event Director");
                eventObject.transform.SetParent(transform, false);
                _eventDirector = eventObject.AddComponent<EventDirector>();
            }

            var eventConfig = Resources.Load<DynamicEventConfig>("DynamicEventConfig");
            _eventDirector.Initialize(eventConfig, _spawner, _spawner != null ? _spawner.Seed : 0, _player);
            _eventDirector.StatusChanged += OnDynamicEventStatusChanged;
            _lastDynamicEvent = "NONE";
        }

        private void ResetAiAndEvents()
        {
            _lastDynamicEvent = "NONE";
            if (_drone != null)
            {
                _drone.SetPhase(_session.Phase);
                _drone.ResetDrone();
            }

            if (_eventDirector != null)
            {
                _eventDirector.SetPhase(_session.Phase);
                _eventDirector.ResetDirector(_spawner != null ? _spawner.Seed : 0);
            }

            _playerController?.ClearJumpBoost();
            _pickupDirector?.ResetDirector(_spawner != null ? _spawner.Seed : 0);
        }

        private void UnbindAiAndEvents()
        {
            if (_drone != null)
                _drone.DestroyedByFallingBlock -= OnDroneDestroyedByFallingBlock;
            if (_eventDirector != null)
                _eventDirector.StatusChanged -= OnDynamicEventStatusChanged;
            if (_pickupDirector != null)
                _pickupDirector.PickupCollected -= OnPickupCollected;
        }

        private void SetAuxiliaryGameplayRunning(bool running)
        {
            if (_drone != null)
                _drone.SetRunning(running);
            if (_eventDirector != null)
                _eventDirector.SetRunning(running);
            if (_pickupDirector != null)
                _pickupDirector.SetRunning(running);
        }

        private void InitializePickups()
        {
            if (_board == null)
                return;

            if (_pickupDirector == null)
            {
                var pickupObject = new GameObject("Pickup Director");
                pickupObject.transform.SetParent(transform, false);
                _pickupDirector = pickupObject.AddComponent<PickupDirector>();
            }

            _pickupDirector.PickupCollected -= OnPickupCollected;
            _pickupDirector.Initialize(_board, _playerHealth, _spawner != null ? _spawner.Seed : 0);
            _pickupDirector.PickupCollected += OnPickupCollected;
        }

        private DroneController CreateRuntimeDrone()
        {
            var droneObject = new GameObject("Drone");
            droneObject.transform.SetParent(transform, false);
            var enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
                droneObject.layer = enemyLayer;

            var body = droneObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            var collider = droneObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.45f;
            collider.isTrigger = true;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(droneObject.transform, false);
            visual.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeVisuals.Square;
            renderer.color = new Color(0.75f, 0.25f, 1f);
            renderer.sortingOrder = 24;

            return droneObject.AddComponent<DroneController>();
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
            if (_player == null)
            {
                var player = FindAnyObjectByType<PlayerController>();
                if (player != null) _player = player.transform;
            }

            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (var canvas in canvases)
            {
                if (_statsText == null)
                    _statsText = FindNamedComponent<Text>(canvas.transform, "Game Statistics");
                if (_statusText == null)
                    _statusText = FindNamedComponent<Text>(canvas.transform, "Game Status");
                if (_healthText == null)
                    _healthText = FindNamedComponent<Text>(canvas.transform, "Player Health");
                if (_nextPiecePreview == null)
                    _nextPiecePreview = FindNamedComponent<NextPiecePreview>(canvas.transform, "Next Piece Preview");
                if (_pauseMenu == null)
                    _pauseMenu = canvas.GetComponentInChildren<TetrisPauseMenu>(true);
                if (_gameOverMenu == null)
                    _gameOverMenu = canvas.GetComponentInChildren<TetrisGameOverMenu>(true);
            }

            var legacyOverflowMeter = GameObject.Find("Overflow Meter");
            if (legacyOverflowMeter != null)
                legacyOverflowMeter.SetActive(false);
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

            var warningHeight = _config.boardHeight - _config.dangerStartRow;
            _overflowWarningRenderer = RuntimeVisuals.CreateQuad(
                "Overflow Warning",
                root,
                new Vector3(center.x, dangerY + warningHeight * 0.5f, 0f),
                new Vector2(_config.boardWidth, warningHeight),
                new Color(1f, 0.05f, 0.08f, 0.3f),
                30).GetComponent<SpriteRenderer>();
            _overflowWarningRenderer.enabled = false;

            var borderColor = new Color(0.35f, 0.65f, 1f, 0.9f);
            AddWorldCollider(RuntimeVisuals.CreateQuad("Border Left", root, new Vector3(origin.x - 0.08f, center.y, 0f), new Vector2(0.16f, _config.boardHeight + 0.2f), borderColor, 5));
            AddWorldCollider(RuntimeVisuals.CreateQuad("Border Right", root, new Vector3(origin.x + _config.boardWidth + 0.08f, center.y, 0f), new Vector2(0.16f, _config.boardHeight + 0.2f), borderColor, 5));
            AddWorldCollider(RuntimeVisuals.CreateQuad("Border Bottom", root, new Vector3(center.x, origin.y - 0.08f, 0f), new Vector2(_config.boardWidth + 0.2f, 0.16f), borderColor, 5));
        }

        private void EnsureOverflowWarningVisual()
        {
            if (_overflowWarningRenderer == null && _arenaVisuals != null)
                _overflowWarningRenderer = FindNamedComponent<SpriteRenderer>(_arenaVisuals, "Overflow Warning");
            if (_overflowWarningRenderer != null || _arenaVisuals == null)
                return;

            var origin = new Vector2(-_config.boardWidth * 0.5f, -10f);
            var dangerY = origin.y + _config.dangerStartRow;
            var warningHeight = _config.boardHeight - _config.dangerStartRow;
            _overflowWarningRenderer = RuntimeVisuals.CreateQuad(
                "Overflow Warning",
                _arenaVisuals,
                new Vector3(0f, dangerY + warningHeight * 0.5f, 0f),
                new Vector2(_config.boardWidth, warningHeight),
                new Color(1f, 0.05f, 0.08f, 0.3f),
                30).GetComponent<SpriteRenderer>();
            _overflowWarningRenderer.enabled = false;
        }

        private static void EnsureArenaBoundaryColliders()
        {
            AddWorldCollider(GameObject.Find("Left Wall"));
            AddWorldCollider(GameObject.Find("Right Wall"));
            AddWorldCollider(GameObject.Find("Floor"));
            AddWorldCollider(GameObject.Find("Border Left"));
            AddWorldCollider(GameObject.Find("Border Right"));
            AddWorldCollider(GameObject.Find("Border Bottom"));
        }

        private static void AddWorldCollider(GameObject gameObject)
        {
            if (gameObject == null)
                return;

            var worldLayer = LayerMask.NameToLayer("World");
            if (worldLayer >= 0)
                gameObject.layer = worldLayer;

            var collider = gameObject.GetComponent<BoxCollider2D>();
            if (collider == null)
                collider = gameObject.AddComponent<BoxCollider2D>();
            collider.sharedMaterial = PhysicsMaterialLibrary.Frictionless;
        }

        private void EnsurePlayerTestRig()
        {
            var platform = GameObject.Find("Player Test Platform");
            if (platform != null)
                Destroy(platform);

            var player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                SetPlayer(player.transform);
                _player.position = PlayerSpawnPosition;
                EnsurePlayerPhysicsSetup(_player.gameObject);
                return;
            }

            SetPlayer(CreateRuntimePlayer().transform);
        }

        private static void EnsurePlayerPhysicsSetup(GameObject player)
        {
            var collider = player.GetComponent<Collider2D>();
            if (collider != null)
                collider.sharedMaterial = PhysicsMaterialLibrary.Frictionless;

            var body = player.GetComponent<Rigidbody2D>();
            if (body == null)
                return;

            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private static GameObject CreateRuntimePlayer()
        {
            var player = new GameObject("Player");
            var playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
                player.layer = playerLayer;
            player.transform.position = PlayerSpawnPosition;

            var body = player.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            var playerConfig = Resources.Load<PlayerConfig>("PlayerConfig");
            body.gravityScale = playerConfig != null ? playerConfig.gravityScale : 3f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = player.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.72f, 1.45f);
            collider.offset = new Vector2(0f, -0.02f);
            collider.sharedMaterial = PhysicsMaterialLibrary.Frictionless;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(player.transform, false);
            visual.transform.localScale = new Vector3(0.72f, 1.45f, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeVisuals.Square;
            renderer.color = new Color(0.95f, 0.86f, 0.30f);
            renderer.sortingOrder = 25;
            visual.AddComponent<Animator>();

            player.AddComponent<PlayerController>();
            player.AddComponent<PlayerHealth>();
            return player;
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

            _healthText = CreateHealthText(canvasObject.transform);

            _pauseMenu = CreatePauseMenu(canvasObject.transform);
            _gameOverMenu = CreateGameOverMenu(canvasObject.transform);
            EnsureEventSystem();
        }

        private void UpdateHud()
        {
            if (_session.State == GameSessionState.Countdown && _statusText != null && !_paused)
            {
                _statusText.text = $"GET READY  {Mathf.CeilToInt(_session.CountdownRemaining)}";
                _statusText.color = new Color(1f, 0.85f, 0.25f);
            }

            if (_healthText != null)
                _healthText.text = FormatHealth();
            if (_statsText == null || _board == null || _spawner == null)
                return;

            _statsText.text =
                $"SEED       {_spawner.Seed}\n" +
                $"TIME       {FormatTime(_session.SurvivalTime)}\n" +
                $"PHASE      {_session.Phase} ({FormatTime(_session.TimeUntilNextPhase)})\n" +
                $"SPAWNED    {_spawner.PiecesSpawned}\n" +
                $"LAST PIECE {_lastSpawned}\n" +
                $"LOCKED     {_board.Model.OccupiedCount} CELLS\n" +
                $"DRONE      {FormatDroneState()}\n" +
                $"EVENT      {_lastDynamicEvent}\n" +
                $"POWER      {FormatPowerUpState()}\n" +
                $"ROWS       {_session.RowsCleared}\n" +
                $"SCORE      {_session.Score}";
        }

        private void ResetDemo()
        {
            Time.timeScale = 1f;
            _paused = false;
            _gameOver = false;
            StopRespawnHover(restoreGravity: true);
            if (_pauseMenu != null) _pauseMenu.HideAll();
            if (_gameOverMenu != null) _gameOverMenu.Hide();
            _board.ResetBoard();
            if (_player != null)
                _player.position = PlayerSpawnPosition;
            if (_playerHealth != null)
                _playerHealth.ResetHealth();
            ClampPlayerToArena();
            _spawner.Restart(startSpawning: false);
            ResetAiAndEvents();
            StartCountdown();
        }

        private void ClampPlayerToArena()
        {
            if (_player == null || _board == null)
                return;

            var collider = _player.GetComponent<Collider2D>();
            if (collider == null)
                return;

            var boardOrigin = _board.transform.position;
            var left = boardOrigin.x;
            var right = boardOrigin.x + _board.Width;
            var bottom = boardOrigin.y;
            var bounds = collider.bounds;
            var delta = Vector2.zero;
            const float skin = 0.01f;

            if (bounds.min.x < left)
                delta.x = left - bounds.min.x + skin;
            else if (bounds.max.x > right)
                delta.x = right - bounds.max.x - skin;

            if (bounds.min.y < bottom)
                delta.y = bottom - bounds.min.y + skin;

            if (delta == Vector2.zero)
                return;

            var target = (Vector2)_player.position + delta;
            var body = _player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                var velocity = body.linearVelocity;
                if ((delta.x < 0f && velocity.x > 0f) || (delta.x > 0f && velocity.x < 0f))
                    velocity.x = 0f;
                if (delta.y > 0f && velocity.y < 0f)
                    velocity.y = 0f;
                body.linearVelocity = velocity;
                body.position = target;
                return;
            }

            _player.position = new Vector3(target.x, target.y, _player.position.z);
        }

        private void ResolvePlayerBlockOverlap()
        {
            if (_player == null || _board == null)
                return;

            var playerCollider = _player.GetComponent<Collider2D>();
            if (playerCollider == null)
                return;

            var bounds = playerCollider.bounds;
            var probeSize = new Vector2(
                Mathf.Max(0.05f, bounds.size.x - 0.04f),
                Mathf.Max(0.05f, bounds.size.y - 0.04f));
            if (!HasDeepWorldOverlap(playerCollider))
                return;

            var boardOrigin = (Vector2)_board.transform.position;
            var minX = boardOrigin.x + probeSize.x * 0.5f;
            var maxX = boardOrigin.x + _board.Width - probeSize.x * 0.5f;
            var body = _player.GetComponent<Rigidbody2D>();
            if (TryResolveLockedBlockSideOverlap(playerCollider, probeSize, body, minX, maxX))
                return;

            var offsetsX = new[] { 0f, -1f, 1f, -2f, 2f, -3f, 3f };

            for (var step = 1; step <= RespawnProbeSteps; step++)
            {
                var y = bounds.center.y + step * RespawnProbeStep;
                foreach (var offsetX in offsetsX)
                {
                    var candidate = new Vector2(Mathf.Clamp(bounds.center.x + offsetX, minX, maxX), y);
                    if (!IsPlayerClearAt(candidate, probeSize, playerCollider, includeFallingBlocks: false))
                        continue;

                    if (body != null)
                    {
                        body.linearVelocity = Vector2.zero;
                        body.position = candidate;
                    }
                    else
                    {
                        _player.position = new Vector3(candidate.x, candidate.y, _player.position.z);
                    }
                    Physics2D.SyncTransforms();
                    return;
                }
            }

            var fallback = GetCrushRespawnPosition();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.position = fallback;
            }
            else
            {
                _player.position = new Vector3(fallback.x, fallback.y, _player.position.z);
            }
            Physics2D.SyncTransforms();
        }

        private static bool HasDeepWorldOverlap(Collider2D playerCollider)
        {
            var worldMask = LayerMask.GetMask("World");
            if (playerCollider == null || worldMask == 0)
                return false;

            Physics2D.SyncTransforms();
            var bounds = playerCollider.bounds;
            var hits = Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f, worldMask);
            foreach (var hit in hits)
            {
                if (hit == null || !hit.enabled || hit.isTrigger || hit == playerCollider)
                    continue;
                if (hit.transform.IsChildOf(playerCollider.transform))
                    continue;

                var distance = playerCollider.Distance(hit);
                if (distance.isOverlapped && distance.distance < -PlayerUnstuckPenetrationThreshold)
                    return true;
            }

            return false;
        }

        private bool TryResolveLockedBlockSideOverlap(
            Collider2D playerCollider,
            Vector2 probeSize,
            Rigidbody2D body,
            float minX,
            float maxX)
        {
            var blockingMask = LayerMask.GetMask("World");
            if (blockingMask == 0)
                return false;

            const float skin = 0.03f;
            const float sideBias = 0.06f;
            var playerBounds = playerCollider.bounds;
            var hits = Physics2D.OverlapBoxAll(playerBounds.center, probeSize, 0f, blockingMask);
            foreach (var hit in hits)
            {
                if (hit == null || !hit.enabled || hit.isTrigger || hit == playerCollider)
                    continue;
                if (hit.GetComponent<BlockCellView>() == null)
                    continue;

                var hitBounds = hit.bounds;
                var overlapX = Mathf.Min(playerBounds.max.x, hitBounds.max.x) - Mathf.Max(playerBounds.min.x, hitBounds.min.x);
                var overlapY = Mathf.Min(playerBounds.max.y, hitBounds.max.y) - Mathf.Max(playerBounds.min.y, hitBounds.min.y);
                if (overlapX <= 0f || overlapY <= 0f || overlapX > overlapY + sideBias)
                    continue;

                var direction = playerBounds.center.x < hitBounds.center.x ? -1f : 1f;
                for (var extra = 0; extra <= 3; extra++)
                {
                    var push = direction * (overlapX + skin + extra * 0.08f);
                    var candidate = new Vector2(
                        Mathf.Clamp(playerBounds.center.x + push, minX, maxX),
                        playerBounds.center.y);
                    if (!IsPlayerClearAt(candidate, probeSize, playerCollider, includeFallingBlocks: false))
                        continue;

                    MovePlayerToOverlapResolution(candidate, body, push);
                    return true;
                }
            }

            return false;
        }

        private void MovePlayerToOverlapResolution(Vector2 targetCenter, Rigidbody2D body, float horizontalPush)
        {
            if (body != null)
            {
                var velocity = body.linearVelocity;
                if ((horizontalPush < 0f && velocity.x > 0f) || (horizontalPush > 0f && velocity.x < 0f))
                    velocity.x = 0f;
                body.linearVelocity = velocity;
                body.position = targetCenter;
            }
            else if (_player != null)
            {
                _player.position = new Vector3(targetCenter.x, targetCenter.y, _player.position.z);
            }

            Physics2D.SyncTransforms();
        }

        private static bool IsPlayerClearAt(
            Vector2 center,
            Vector2 size,
            Collider2D playerCollider,
            bool includeFallingBlocks = true)
        {
            var blockingMask = includeFallingBlocks
                ? LayerMask.GetMask("World", "FallingBlock")
                : LayerMask.GetMask("World");
            if (blockingMask == 0)
                return true;

            Physics2D.SyncTransforms();
            var hits = Physics2D.OverlapBoxAll(center, size, 0f, blockingMask);
            foreach (var hit in hits)
            {
                if (hit == null || !hit.enabled || hit.isTrigger || hit == playerCollider)
                    continue;
                if (hit.transform.IsChildOf(playerCollider.transform))
                    continue;
                return false;
            }

            return true;
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
            _session.Pause();
            SetAuxiliaryGameplayRunning(false);
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
            _session.Resume();
            var playing = _session.State == GameSessionState.Playing;
            SetAuxiliaryGameplayRunning(playing);
            Time.timeScale = 1f;
            if (_inputService != null)
                _inputService.SetGameplayEnabled(playing && _board != null && !_board.IsOverflowed);
            if (_pauseMenu != null) _pauseMenu.HideAll();
            if (_statusText != null)
            {
                _statusText.text = !playing
                    ? $"GET READY  {Mathf.CeilToInt(_session.CountdownRemaining)}"
                    : _board != null && _board.IsOverflowed ? "BOARD OVERFLOW — PRESS R" : "RUNNING";
                _statusText.color = !playing
                    ? new Color(1f, 0.85f, 0.25f)
                    : _board != null && _board.IsOverflowed ? new Color(1f, 0.2f, 0.25f) : Color.white;
            }
        }

        private void OpenResetConfirmation()
        {
            _paused = true;
            _session.Pause();
            SetAuxiliaryGameplayRunning(false);
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
                _session.RowsCleared,
                _session.Score,
                _spawner.Seed,
                _session.SurvivalTime,
                _session.Phase);
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
            var points = _session.AddRowsCleared(rows.Length);
            _statusText.text = $"{rows.Length} ROW{(rows.Length > 1 ? "S" : string.Empty)} CLEARED";
            _statusText.color = new Color(0.35f, 1f, 0.55f);
            if (points > 0)
                _statusText.text += $" +{points}";
        }

        private void OnDroneDestroyedByFallingBlock(DroneController drone)
        {
            var points = _session.AddBonusScore(drone != null ? drone.DestroyScore : 300);
            if (_statusText != null && !_paused && !_gameOver)
            {
                _statusText.text = points > 0 ? $"DRONE DESTROYED +{points}" : "DRONE DESTROYED";
                _statusText.color = new Color(0.75f, 0.35f, 1f);
            }
        }

        private void OnDynamicEventStatusChanged(string message)
        {
            _lastDynamicEvent = string.IsNullOrEmpty(message) ? "NONE" : message;
            if (_statusText != null && !_paused && !_gameOver)
            {
                _statusText.text = _lastDynamicEvent;
                _statusText.color = new Color(1f, 0.45f, 0.2f);
            }
        }

        private void OnPickupCollected(PickupKind kind)
        {
            var message = kind switch
            {
                PickupKind.ScoreCrystal => $"SCORE CRYSTAL +{_session.AddBonusScore(100)}",
                PickupKind.HealthPack => "HEALTH +1",
                _ => "JUMP BOOST 8s"
            };

            if (kind == PickupKind.JumpBoost)
                _playerController?.ApplyJumpBoost(1.2f, 8f);

            if (_statusText != null && !_paused && !_gameOver)
            {
                _statusText.text = message;
                _statusText.color = kind == PickupKind.HealthPack
                    ? new Color(1f, 0.3f, 0.4f)
                    : new Color(1f, 0.8f, 0.2f);
            }
        }

        private void OnOverflowChanged(bool dangerous, float normalized)
        {
            if (_overflowWarningRenderer != null)
            {
                _overflowWarningRenderer.enabled = dangerous;
                if (dangerous)
                {
                    var color = _overflowWarningRenderer.color;
                    color.a = Mathf.Lerp(0.08f, 0.35f, Mathf.PingPong(Time.unscaledTime * 2.5f, 1f));
                    _overflowWarningRenderer.color = color;
                }
            }
            if (dangerous && !_paused && _statusText != null)
            {
                _statusText.text = "DANGER — CLEAR THE TOP";
                _statusText.color = new Color(1f, 0.25f, 0.25f);
            }
        }

        private void OnOverflowed()
        {
            EndRun("BLOCK CHẠM VÙNG NGUY HIỂM");
        }

        private void OnPlayerCrushed()
        {
            if (_playerHealth == null)
            {
                EndRun("PLAYER BỊ BLOCK ĐÈ");
                return;
            }

            if (_playerHealth.IsInvulnerable)
            {
                RespawnPlayerAfterCrush();
                return;
            }

            var accepted = _playerHealth.TakeDamage(new DamageInfo(1, Vector2.zero, _board != null ? _board.gameObject : null, DamageType.Crush));
            if (!accepted || _playerHealth.IsDead)
                return;

            RespawnPlayerAfterCrush();
        }

        private void OnPlayerDied()
        {
            EndRun("PLAYER HẾT MÁU");
        }

        private void OnSessionPhaseChanged(int phase)
        {
            ApplySessionPhase();
            if (_statusText != null && !_paused && !_gameOver)
            {
                _statusText.text = $"PHASE {phase}";
                _statusText.color = new Color(1f, 0.75f, 0.25f);
            }
        }

        private void OnSessionStateChanged(GameSessionState state)
        {
            if (state != GameSessionState.Playing || _paused || _gameOver)
                return;

            SetPlayerRunning(true);
            if (_inputService != null)
                _inputService.SetGameplayEnabled(true);
            _spawner?.StartSpawning();
            SetAuxiliaryGameplayRunning(true);
            if (_statusText != null)
            {
                _statusText.text = "RUNNING";
                _statusText.color = Color.white;
            }
        }

        private void StartCountdown()
        {
            _session.StartCountdown(CountdownSeconds, _config != null ? _config.phaseDurationSeconds : 45f);
            ApplySessionPhase();
            SetPlayerRunning(false);
            if (_inputService != null)
                _inputService.SetGameplayEnabled(false);
            SetAuxiliaryGameplayRunning(false);
            UpdateHud();
        }

        private void SetPlayerRunning(bool running)
        {
            if (_playerController != null)
                _playerController.enabled = running;
            if (_player == null || !_player.TryGetComponent<Rigidbody2D>(out var body))
                return;

            if (!running)
                body.linearVelocity = Vector2.zero;
            body.simulated = running;
        }

        private void EndRun(string reason)
        {
            if (_gameOver)
                return;

            _gameOver = true;
            _paused = false;
            if (_overflowWarningRenderer != null)
                _overflowWarningRenderer.enabled = false;
            StopRespawnHover(restoreGravity: true);
            SetAuxiliaryGameplayRunning(false);
            Time.timeScale = 0f;
            if (_inputService != null) _inputService.SetGameplayEnabled(false);
            if (_spawner != null) _spawner.Stop();
            if (_pauseMenu != null) _pauseMenu.HideAll();
            var result = _session.EndRun(
                reason,
                _spawner != null ? _spawner.PiecesSpawned : 0,
                _spawner != null ? _spawner.Seed : 0);
            SaveService.RecordRun(result.Score, result.SurvivalTime);
            if (_gameOverMenu != null && _spawner != null)
            {
                _gameOverMenu.Show(
                    result.PiecesSpawned,
                    result.RowsCleared,
                    result.Score,
                    result.Seed,
                    result.Reason,
                    result.SurvivalTime,
                    result.Phase);
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

        private static string FormatTime(float seconds)
        {
            var totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        private string FormatHealth()
        {
            if (_playerHealth == null)
                return "♡ ♡ ♡";

            var hearts = string.Empty;
            for (var i = 0; i < _playerHealth.MaxHp; i++)
                hearts += (i == 0 ? string.Empty : " ") + (i < _playerHealth.CurrentHp ? "♥" : "♡");
            return hearts;
        }

        private void RespawnPlayerAfterCrush()
        {
            if (_player == null || _board == null)
                return;

            var target = GetCrushRespawnPosition();
            var body = _player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.position = target;
                StartRespawnHover(body);
            }
            else
            {
                _player.position = new Vector3(target.x, target.y, _player.position.z);
            }

            _playerHealth?.StartInvulnerability(CrushRespawnInvulnerabilitySeconds);
            Physics2D.SyncTransforms();

            if (_statusText != null && !_gameOver)
            {
                _statusText.text = "PLAYER RESPAWN - INVINCIBLE";
                _statusText.color = new Color(1f, 0.75f, 0.25f);
            }
        }

        private void StartRespawnHover(Rigidbody2D body)
        {
            if (body == null)
                return;

            StopRespawnHover(restoreGravity: true);
            _respawnHoverRoutine = StartCoroutine(RespawnHoverRoutine(body));
        }

        private IEnumerator RespawnHoverRoutine(Rigidbody2D body)
        {
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;

            var elapsed = 0f;
            while (elapsed < CrushRespawnHoverSeconds && !_gameOver)
            {
                if (body == null)
                {
                    _respawnHoverRoutine = null;
                    yield break;
                }

                var velocity = body.linearVelocity;
                velocity.y = 0f;
                body.linearVelocity = velocity;
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (body != null)
                body.gravityScale = GetPlayerGravityScale();
            _respawnHoverRoutine = null;
        }

        private void StopRespawnHover(bool restoreGravity)
        {
            if (_respawnHoverRoutine != null)
                StopCoroutine(_respawnHoverRoutine);
            _respawnHoverRoutine = null;

            if (!restoreGravity || _player == null)
                return;

            var body = _player.GetComponent<Rigidbody2D>();
            if (body != null)
                body.gravityScale = GetPlayerGravityScale();
        }

        private float GetPlayerGravityScale()
        {
            var controller = _player != null ? _player.GetComponent<PlayerController>() : null;
            if (controller != null && controller.Config != null)
                return controller.Config.gravityScale;
            return 3f;
        }

        private Vector2 GetCrushRespawnPosition()
        {
            var boardOrigin = (Vector2)_board.transform.position;
            var x = boardOrigin.x + _board.Width * 0.5f;
            var y = PlayerSpawnPosition.y;
            var highestRow = _board.Model != null ? _board.Model.HighestOccupiedRow() : -1;
            if (highestRow >= 0)
                y = _board.WorldForCell(new Vector2Int(_board.Width / 2, highestRow)).y + CrushRespawnHeightAboveHighestBlock;

            return FindClearRespawnPosition(new Vector2(x, y));
        }

        private Vector2 FindClearRespawnPosition(Vector2 preferredCenter)
        {
            var playerCollider = _player != null ? _player.GetComponent<Collider2D>() : null;
            if (playerCollider == null || _board == null)
                return preferredCenter;

            var probeSize = new Vector2(
                Mathf.Max(0.05f, playerCollider.bounds.size.x - 0.04f),
                Mathf.Max(0.05f, playerCollider.bounds.size.y - 0.04f));
            var boardOrigin = (Vector2)_board.transform.position;
            var centerX = boardOrigin.x + _board.Width * 0.5f;
            var xOffsets = new[] { 0f, -1f, 1f, -2f, 2f, -3f, 3f };

            for (var step = 0; step <= RespawnProbeSteps; step++)
            {
                var y = preferredCenter.y + step * RespawnProbeStep;
                foreach (var xOffset in xOffsets)
                {
                    var x = Mathf.Clamp(
                        centerX + xOffset,
                        boardOrigin.x + probeSize.x * 0.5f,
                        boardOrigin.x + _board.Width - probeSize.x * 0.5f);
                    var candidate = new Vector2(x, y);
                    if (IsRespawnPositionClear(candidate, probeSize, playerCollider))
                        return candidate;
                }
            }

            return preferredCenter + Vector2.up * (RespawnProbeSteps + 1) * RespawnProbeStep;
        }

        private static bool IsRespawnPositionClear(Vector2 center, Vector2 size, Collider2D playerCollider)
        {
            var blockingMask = LayerMask.GetMask("World", "FallingBlock");
            if (blockingMask == 0)
                return true;

            Physics2D.SyncTransforms();
            var hits = Physics2D.OverlapBoxAll(center, size, 0f, blockingMask);
            foreach (var hit in hits)
            {
                if (hit == null || !hit.enabled || hit.isTrigger || hit == playerCollider)
                    continue;
                if (hit.transform.IsChildOf(playerCollider.transform))
                    continue;
                return false;
            }

            return true;
        }

        private void ApplySessionPhase()
        {
            if (_spawner != null)
                _spawner.ApplyDifficultyPhase(_session.Phase);
            if (_drone != null)
                _drone.SetPhase(_session.Phase);
            if (_eventDirector != null)
                _eventDirector.SetPhase(_session.Phase);
        }

        private string FormatDroneState()
        {
            if (_drone == null)
                return "---";
            if (_drone.IsDestroyed)
                return $"RESPAWN {Mathf.CeilToInt(_drone.RespawnSecondsRemaining)}s";
            return _drone.State.ToString().ToUpperInvariant();
        }

        private string FormatPowerUpState()
        {
            return _playerController != null && _playerController.JumpBoostActive
                ? $"JUMP {Mathf.CeilToInt(_playerController.JumpBoostSecondsRemaining)}s"
                : "NONE";
        }

        private void SetPlayer(Transform player)
        {
            UnbindPlayerHealth();
            _player = player;
            _playerController = _player != null ? _player.GetComponent<PlayerController>() : null;
            _playerHealth = _player != null ? _player.GetComponent<PlayerHealth>() : null;
            if (_playerHealth != null)
                _playerHealth.Died += OnPlayerDied;
        }

        private void UnbindPlayerHealth()
        {
            if (_playerHealth != null)
                _playerHealth.Died -= OnPlayerDied;
            _playerController = null;
            _playerHealth = null;
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
                null,
                mainMenuButton,
                null,
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

        private static Text CreateHealthText(Transform parent)
        {
            var health = CreateText(parent, "Player Health", "♥ ♥ ♥", 46, TextAnchor.UpperRight);
            SetRect(health.rectTransform, new Vector2(-24f, -16f), new Vector2(420f, 70f), new Vector2(1f, 1f));
            health.color = new Color(1f, 0.2f, 0.25f);
            return health;
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

namespace BlockEscape.AI
{
    internal enum DroneState
    {
        Disabled,
        Patrol,
        Detect,
        Telegraph,
        Dash,
        Recover
    }

    [CreateAssetMenu(menuName = "Block Escape/Drone Config", fileName = "DroneConfig")]
    internal sealed class DroneConfig : ScriptableObject
    {
        [Header("Patrol")]
        [Min(0.1f)] public float patrolSpeed = 2.2f;
        [Range(0.5f, 0.95f)] public float patrolHeightNormalized = 0.72f;
        [Min(0.1f)] public float arenaSidePadding = 1f;

        [Header("Attack")]
        [Min(0.1f)] public float detectRange = 8f;
        [Min(0f)] public float detectConfirmSeconds = 0.15f;
        [Min(0f)] public float telegraphSeconds = 0.8f;
        [Min(0.1f)] public float dashSpeed = 13f;
        [Min(0.05f)] public float dashSeconds = 0.45f;
        [Min(0f)] public float recoverSeconds = 1.5f;
        public Vector2 knockback = new(8f, 3f);

        [Header("Projectile")]
        [Min(0.1f)] public float shootIntervalSeconds = 1.6f;
        [Min(0.1f)] public float bulletSpeed = 8f;
        [Min(0.1f)] public float bulletLifetimeSeconds = 4f;
        [Min(0.05f)] public float explosionSeconds = 0.28f;

        [Header("Lifecycle")]
        [Min(0f)] public float respawnSeconds = 6f;
        [Min(1)] public int destroyScore = 300;

        public void Sanitize()
        {
            patrolSpeed = Mathf.Max(0.1f, patrolSpeed);
            patrolHeightNormalized = Mathf.Clamp(patrolHeightNormalized, 0.5f, 0.95f);
            arenaSidePadding = Mathf.Max(0.1f, arenaSidePadding);
            detectRange = Mathf.Max(0.1f, detectRange);
            detectConfirmSeconds = Mathf.Max(0f, detectConfirmSeconds);
            telegraphSeconds = Mathf.Max(0f, telegraphSeconds);
            dashSpeed = Mathf.Max(0.1f, dashSpeed);
            dashSeconds = Mathf.Max(0.05f, dashSeconds);
            recoverSeconds = Mathf.Max(0f, recoverSeconds);
            shootIntervalSeconds = Mathf.Max(0.1f, shootIntervalSeconds);
            bulletSpeed = Mathf.Max(0.1f, bulletSpeed);
            bulletLifetimeSeconds = Mathf.Max(0.1f, bulletLifetimeSeconds);
            explosionSeconds = Mathf.Max(0.05f, explosionSeconds);
            respawnSeconds = Mathf.Max(0f, respawnSeconds);
            destroyScore = Mathf.Max(1, destroyScore);
        }
    }

    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    internal sealed class DroneController : MonoBehaviour
    {
        [SerializeField] private DroneConfig _config;
        [SerializeField] private SpriteRenderer _renderer;

        private Rigidbody2D _body;
        private Collider2D _collider;
        private Transform _player;
        private BlockEscape.Tetris.BlockBoard _board;
        private Rect _arena;
        private Vector2 _spawnPosition;
        private Vector2 _dashDirection = Vector2.right;
        private Vector2 _dashTarget;
        private float _stateTimer;
        private float _respawnTimer;
        private int _phase = 1;
        private int _patrolDirection = 1;
        private bool _running;
        private bool _destroyed;
        private bool _damagedThisDash;
        private float _shootTimer;

        public event System.Action<DroneController> DestroyedByFallingBlock;

        public DroneState State { get; private set; } = DroneState.Disabled;
        public int DestroyScore => _config != null ? _config.destroyScore : 300;
        public bool IsDestroyed => _destroyed;
        public float RespawnSecondsRemaining => Mathf.Max(0f, _respawnTimer);

        private void Awake()
        {
            EnsureComponents();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            HandleTrigger(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            HandleTrigger(other);
        }

        public void Initialize(DroneConfig config, Transform player, BlockEscape.Tetris.BlockBoard board)
        {
            EnsureComponents();
            _config = config != null ? config : ScriptableObject.CreateInstance<DroneConfig>();
            _config.Sanitize();
            _player = player;
            _board = board;
            RefreshArena();
            _spawnPosition = GetPatrolPoint(0.5f);
            _body.position = _spawnPosition;
            _patrolDirection = 1;
            _shootTimer = _config.shootIntervalSeconds * 0.5f;
            SetPhase(_phase);
        }

        public void SetRunning(bool running)
        {
            _running = running;
            if (!running && _body != null)
                _body.linearVelocity = Vector2.zero;
        }

        public void SetPhase(int phase)
        {
            _phase = Mathf.Max(1, phase);
            if (_phase < 1)
            {
                _destroyed = false;
                _respawnTimer = 0f;
                ChangeState(DroneState.Disabled);
                SetVisible(false);
                return;
            }

            if (State == DroneState.Disabled && !_destroyed)
                ResetDrone();
        }

        public void ResetDrone()
        {
            EnsureComponents();
            RefreshArena();
            _destroyed = false;
            _respawnTimer = 0f;
            _damagedThisDash = false;
            _spawnPosition = GetPatrolPoint(0.5f);
            _body.position = _spawnPosition;
            _body.linearVelocity = Vector2.zero;
            _patrolDirection = 1;
            _shootTimer = _config.shootIntervalSeconds * 0.5f;
            SetVisible(_phase >= 1);
            ChangeState(_phase >= 1 ? DroneState.Patrol : DroneState.Disabled);
        }

        public void ManualTick(float deltaTime)
        {
            Tick(deltaTime);
        }

        private void Tick(float deltaTime)
        {
            if (!_running || deltaTime <= 0f || _config == null)
                return;

            if (_destroyed)
            {
                TickRespawn(deltaTime);
                return;
            }

            if (_phase < 1 || State == DroneState.Disabled)
                return;

            TickShooting(deltaTime);
            switch (State)
            {
                case DroneState.Patrol:
                    TickPatrol(deltaTime);
                    break;
                case DroneState.Detect:
                    TickDetect(deltaTime);
                    break;
                case DroneState.Telegraph:
                    TickTelegraph(deltaTime);
                    break;
                case DroneState.Dash:
                    TickDash(deltaTime);
                    break;
                case DroneState.Recover:
                    TickRecover(deltaTime);
                    break;
            }
        }

        private void TickRespawn(float deltaTime)
        {
            if (_phase < 1)
                return;

            _respawnTimer -= deltaTime;
            if (_respawnTimer <= 0f)
                ResetDrone();
        }

        private void TickPatrol(float deltaTime)
        {
            var position = _body.position;
            position.x += _patrolDirection * _config.patrolSpeed * deltaTime;
            if (position.x <= _arena.xMin + _config.arenaSidePadding)
            {
                position.x = _arena.xMin + _config.arenaSidePadding;
                _patrolDirection = 1;
            }
            else if (position.x >= _arena.xMax - _config.arenaSidePadding)
            {
                position.x = _arena.xMax - _config.arenaSidePadding;
                _patrolDirection = -1;
            }

            position.y = GetPatrolY();
            _body.position = position;
            if (CanDetectPlayer())
                ChangeState(DroneState.Detect);
        }

        private void TickShooting(float deltaTime)
        {
            if (State == DroneState.Dash || State == DroneState.Disabled)
                return;

            _shootTimer -= deltaTime;
            if (_shootTimer > 0f)
                return;

            _shootTimer = _config.shootIntervalSeconds;
            FireBullet();
        }

        private void FireBullet()
        {
            var bulletObject = new GameObject("Drone Bullet");
            bulletObject.transform.SetParent(transform.parent, false);
            bulletObject.transform.position = _body.position + Vector2.down * 0.55f;
            var hazardLayer = LayerMask.NameToLayer("Hazard");
            if (hazardLayer >= 0)
                bulletObject.layer = hazardLayer;

            var renderer = bulletObject.AddComponent<SpriteRenderer>();
            renderer.sprite = BlockEscape.Tetris.RuntimeVisuals.Square;
            renderer.color = new Color(1f, 0.25f, 0.2f);
            renderer.sortingOrder = 23;
            bulletObject.transform.localScale = new Vector3(0.18f, 0.34f, 1f);

            var collider = bulletObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.14f;
            collider.isTrigger = true;

            var body = bulletObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;

            var bullet = bulletObject.AddComponent<DroneBullet>();
            bullet.Initialize(Vector2.down, _config.bulletSpeed, _config.bulletLifetimeSeconds, _config.explosionSeconds);
        }

        private void TickDetect(float deltaTime)
        {
            if (!CanDetectPlayer())
            {
                ChangeState(DroneState.Patrol);
                return;
            }

            _stateTimer += deltaTime;
            if (_stateTimer >= _config.detectConfirmSeconds)
            {
                _dashTarget = _player.position;
                ChangeState(DroneState.Telegraph);
            }
        }

        private void TickTelegraph(float deltaTime)
        {
            _stateTimer += deltaTime;
            if (_stateTimer < _config.telegraphSeconds)
                return;

            _dashDirection = (_dashTarget - _body.position).normalized;
            if (_dashDirection.sqrMagnitude < 0.01f)
                _dashDirection = Vector2.down;
            ChangeState(DroneState.Dash);
        }

        private void TickDash(float deltaTime)
        {
            _stateTimer += deltaTime;
            _body.position += _dashDirection * _config.dashSpeed * deltaTime;
            if (!IsInsideArena(_body.position) || _stateTimer >= _config.dashSeconds)
                ChangeState(DroneState.Recover);
        }

        private void TickRecover(float deltaTime)
        {
            _stateTimer += deltaTime;
            if (_stateTimer >= _config.recoverSeconds)
                ChangeState(DroneState.Patrol);
        }

        private void HandleTrigger(Collider2D other)
        {
            if (_destroyed || other == null || !other.enabled)
                return;

            if (other.gameObject.layer == LayerMask.NameToLayer("FallingBlock"))
            {
                DestroyByFallingBlock();
                return;
            }

            if (State != DroneState.Dash || _damagedThisDash)
                return;

            var damageable = other.GetComponentInParent<BlockEscape.Core.IDamageable>();
            if (damageable == null)
                return;

            var knockbackDirection = _dashDirection.x < 0f ? -1f : 1f;
            var knockback = new Vector2(_config.knockback.x * knockbackDirection, _config.knockback.y);
            if (damageable.TakeDamage(new BlockEscape.Core.DamageInfo(1, knockback, gameObject, BlockEscape.Core.DamageType.Enemy)))
                _damagedThisDash = true;
        }

        private void DestroyByFallingBlock()
        {
            _destroyed = true;
            _respawnTimer = _config.respawnSeconds;
            _body.linearVelocity = Vector2.zero;
            ChangeState(DroneState.Disabled);
            SetVisible(false);
            DestroyedByFallingBlock?.Invoke(this);
        }

        private bool CanDetectPlayer()
        {
            if (_player == null)
                return false;

            var toPlayer = (Vector2)_player.position - _body.position;
            return toPlayer.sqrMagnitude <= _config.detectRange * _config.detectRange;
        }

        private void ChangeState(DroneState state)
        {
            State = state;
            _stateTimer = 0f;
            if (state == DroneState.Dash)
                _damagedThisDash = false;
            ApplyStateColor();
        }

        private void ApplyStateColor()
        {
            if (_renderer == null)
                return;

            _renderer.color = State switch
            {
                DroneState.Detect => new Color(1f, 0.75f, 0.25f),
                DroneState.Telegraph => new Color(1f, 0.2f, 0.25f),
                DroneState.Dash => new Color(1f, 0.45f, 0.15f),
                DroneState.Recover => new Color(0.55f, 0.6f, 0.7f),
                _ => new Color(0.75f, 0.25f, 1f)
            };
        }

        private void SetVisible(bool visible)
        {
            if (_renderer != null)
                _renderer.enabled = visible;
            if (_collider != null)
                _collider.enabled = visible;
        }

        private void RefreshArena()
        {
            if (_board == null)
            {
                _arena = new Rect(-7f, -10f, 14f, 20f);
                return;
            }

            var origin = (Vector2)_board.transform.position;
            _arena = new Rect(origin.x, origin.y, _board.Width, _board.Height);
        }

        private Vector2 GetPatrolPoint(float normalizedX)
        {
            var x = Mathf.Lerp(_arena.xMin + _config.arenaSidePadding, _arena.xMax - _config.arenaSidePadding, normalizedX);
            return new Vector2(x, GetPatrolY());
        }

        private float GetPatrolY()
        {
            return Mathf.Lerp(_arena.yMin, _arena.yMax, _config.patrolHeightNormalized);
        }

        private bool IsInsideArena(Vector2 position)
        {
            return position.x >= _arena.xMin &&
                   position.x <= _arena.xMax &&
                   position.y >= _arena.yMin &&
                   position.y <= _arena.yMax;
        }

        private void EnsureComponents()
        {
            if (_body == null)
                _body = GetComponent<Rigidbody2D>();
            if (_collider == null)
                _collider = GetComponent<Collider2D>();
            if (_renderer == null)
                _renderer = GetComponentInChildren<SpriteRenderer>();

            if (_body != null)
            {
                _body.bodyType = RigidbodyType2D.Kinematic;
                _body.gravityScale = 0f;
                _body.freezeRotation = true;
            }

            if (_collider != null)
                _collider.isTrigger = true;
        }
    }

    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    internal sealed class DroneBullet : MonoBehaviour
    {
        private Vector2 _direction = Vector2.down;
        private float _speed = 8f;
        private float _lifeSeconds = 4f;
        private float _explosionSeconds = 0.28f;
        private bool _finished;

        public void Initialize(Vector2 direction, float speed, float lifeSeconds, float explosionSeconds)
        {
            _direction = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.down;
            _speed = Mathf.Max(0.1f, speed);
            _lifeSeconds = Mathf.Max(0.1f, lifeSeconds);
            _explosionSeconds = Mathf.Max(0.05f, explosionSeconds);
        }

        private void Update()
        {
            if (_finished)
                return;

            transform.position += (Vector3)(_direction * _speed * Time.deltaTime);
            _lifeSeconds -= Time.deltaTime;
            if (_lifeSeconds <= 0f)
                Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_finished || other == null || !other.enabled || other.isTrigger)
                return;

            var otherLayer = other.gameObject.layer;
            if (otherLayer == LayerMask.NameToLayer("Player"))
            {
                var damageable = other.GetComponentInParent<BlockEscape.Core.IDamageable>();
                damageable?.TakeDamage(new BlockEscape.Core.DamageInfo(1, Vector2.down * 3f, gameObject, BlockEscape.Core.DamageType.Enemy));
                Explode();
                return;
            }

            if (otherLayer == LayerMask.NameToLayer("World") || otherLayer == LayerMask.NameToLayer("FallingBlock"))
                Explode();
        }

        private void Explode()
        {
            if (_finished)
                return;

            _finished = true;
            var collider = GetComponent<Collider2D>();
            if (collider != null)
                collider.enabled = false;
            StartCoroutine(ExplosionRoutine());
        }

        private IEnumerator ExplosionRoutine()
        {
            var renderer = GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = new Color(1f, 0.7f, 0.15f, 0.9f);
                renderer.sortingOrder = 26;
            }

            var startScale = Vector3.one * 0.3f;
            var endScale = Vector3.one * 0.9f;
            for (var elapsed = 0f; elapsed < _explosionSeconds; elapsed += Time.deltaTime)
            {
                var t = Mathf.Clamp01(elapsed / _explosionSeconds);
                transform.localScale = Vector3.Lerp(startScale, endScale, t);
                if (renderer != null)
                {
                    var color = renderer.color;
                    color.a = 1f - t;
                    renderer.color = color;
                }
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
