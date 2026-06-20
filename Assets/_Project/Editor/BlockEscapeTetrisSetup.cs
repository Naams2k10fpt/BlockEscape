using System.IO;
using BlockEscape.Bootstrap;
using BlockEscape.Core;
using BlockEscape.Tetris;
using BlockEscape.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BlockEscape.Editor
{
    [InitializeOnLoad]
    public static class BlockEscapeTetrisSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/TetrisDemo.unity";
        private const string MainMenuScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        private const string ConfigPath = "Assets/_Project/Resources/TetrisBalanceConfig.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string SquareAssetPath = "Assets/_Project/Art/GeneratedSquare.asset";
        private const string ClassroomMarker = "m_Name: Main Menu Confirmation Dialog";

        static BlockEscapeTetrisSetup()
        {
            EditorApplication.delayCall += UpgradeOldDemoOnce;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += UpgradeOldDemoOnce;
        }

        [MenuItem("Block Escape/Build Classroom Tetris Scene")]
        public static void BuildTetrisDemo()
        {
            EnsureLayers();
            var config = CreateOrLoadConfig();
            var squareSprite = CreateOrLoadSquareSprite();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "TetrisDemo";

            var camera = CreateCamera();

            var inputObject = new GameObject("Input Service (Persistent)");
            var inputService = inputObject.AddComponent<InputService>();
            var inputData = new SerializedObject(inputService);
            inputData.FindProperty("_actions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            inputData.ApplyModifiedPropertiesWithoutUndo();

            var gameManagerObject = new GameObject("Game Manager");
            var gameManager = gameManagerObject.AddComponent<TetrisDemoBootstrap>();

            var systemsRoot = new GameObject("Tetris Systems").transform;
            var boardObject = new GameObject("Block Board (14 x 20)");
            boardObject.transform.SetParent(systemsRoot, false);
            boardObject.transform.position = new Vector3(-config.boardWidth * 0.5f, -10f, 0f);
            var board = boardObject.AddComponent<BlockBoard>();
            var spawner = boardObject.AddComponent<TetrominoSpawner>();

            var lockedCells = new GameObject("Locked Block Cells (Runtime)").transform;
            lockedCells.SetParent(boardObject.transform, false);
            var boardData = new SerializedObject(board);
            boardData.FindProperty("_config").objectReferenceValue = config;
            boardData.FindProperty("_cellRoot").objectReferenceValue = lockedCells;
            boardData.ApplyModifiedPropertiesWithoutUndo();

            var arenaRoot = new GameObject("Arena Visuals").transform;
            CreateArenaVisuals(arenaRoot, squareSprite, config);

            var uiRoot = new GameObject("User Interface").transform;
            var hud = CreateHud(uiRoot);

            var managerData = new SerializedObject(gameManager);
            managerData.FindProperty("_config").objectReferenceValue = config;
            managerData.FindProperty("_sceneCamera").objectReferenceValue = camera;
            managerData.FindProperty("_arenaVisuals").objectReferenceValue = arenaRoot;
            managerData.FindProperty("_board").objectReferenceValue = board;
            managerData.FindProperty("_spawner").objectReferenceValue = spawner;
            managerData.FindProperty("_inputService").objectReferenceValue = inputService;
            managerData.FindProperty("_statsText").objectReferenceValue = hud.stats;
            managerData.FindProperty("_statusText").objectReferenceValue = hud.status;
            managerData.FindProperty("_overflowFill").objectReferenceValue = hud.overflowFill;
            managerData.FindProperty("_nextPiecePreview").objectReferenceValue = hud.nextPreview;
            managerData.FindProperty("_pauseMenu").objectReferenceValue = hud.pauseMenu;
            managerData.FindProperty("_gameOverMenu").objectReferenceValue = hud.gameOverMenu;
            managerData.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            CreateMainMenuScene();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true)
            };

            PlayerSettings.companyName = "PRU213 Team";
            PlayerSettings.productName = "Block Escape";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Selection.activeGameObject = GameObject.Find("Game Manager");
            Debug.Log($"Demo scenes built successfully: {MainMenuScenePath}, {ScenePath}");
        }

        private static void UpgradeOldDemoOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;

            if (File.Exists(ScenePath) &&
                File.ReadAllText(ScenePath).Contains(ClassroomMarker) &&
                File.Exists(MainMenuScenePath))
                return;

            BuildTetrisDemo();
        }

        private static void CreateMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "MainMenu";
            CreateCamera();

            var uiRoot = new GameObject("Main Menu UI").transform;
            var canvasObject = new GameObject("Main Menu Canvas");
            canvasObject.transform.SetParent(uiRoot, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var backgroundObject = new GameObject("Menu Background");
            backgroundObject.transform.SetParent(canvasObject.transform, false);
            var background = backgroundObject.AddComponent<Image>();
            background.color = new Color(0.025f, 0.035f, 0.075f, 1f);
            StretchFullScreen(background.rectTransform);

            var accentObject = new GameObject("Title Accent");
            accentObject.transform.SetParent(canvasObject.transform, false);
            var accent = accentObject.AddComponent<Image>();
            accent.color = new Color(0.2f, 0.75f, 1f, 0.9f);
            SetRect(accent.rectTransform, new Vector2(0f, 235f), new Vector2(560f, 5f), new Vector2(0.5f, 0.5f));

            var title = CreateText(canvasObject.transform, "Game Title", "BLOCK ESCAPE", 72, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0f, 330f), new Vector2(1000f, 110f), new Vector2(0.5f, 0.5f));
            title.color = new Color(0.35f, 0.85f, 1f);

            var subtitle = CreateText(canvasObject.transform, "Game Subtitle", "TETRIS PLATFORM SURVIVAL", 28, TextAnchor.MiddleCenter);
            SetRect(subtitle.rectTransform, new Vector2(0f, 270f), new Vector2(800f, 55f), new Vector2(0.5f, 0.5f));
            subtitle.color = new Color(0.72f, 0.8f, 0.92f);

            var startButton = CreateButton(canvasObject.transform, "Start Game Button", "BẮT ĐẦU", new Vector2(0f, 45f), new Vector2(400f, 72f));
            var exitButton = CreateButton(canvasObject.transform, "Exit Game Button", "THOÁT GAME", new Vector2(0f, -55f), new Vector2(400f, 72f));

            var controls = CreateText(canvasObject.transform, "Control Hint", "TETRIS: A / D DI CHUYỂN  •  W XOAY  •  S SOFT DROP", 22, TextAnchor.MiddleCenter);
            SetRect(controls.rectTransform, new Vector2(0f, -250f), new Vector2(1100f, 55f), new Vector2(0.5f, 0.5f));
            controls.color = new Color(0.55f, 0.65f, 0.8f);

            var footer = CreateText(canvasObject.transform, "Project Footer", "PRU213 FINAL PROJECT", 18, TextAnchor.LowerCenter);
            SetRect(footer.rectTransform, new Vector2(0f, 28f), new Vector2(600f, 40f), new Vector2(0.5f, 0f));
            footer.color = new Color(0.42f, 0.5f, 0.65f);

            var controllerObject = new GameObject("Main Menu Controller");
            controllerObject.transform.SetParent(uiRoot, false);
            var controller = controllerObject.AddComponent<MainMenuController>();
            var controllerData = new SerializedObject(controller);
            controllerData.FindProperty("_startButton").objectReferenceValue = startButton;
            controllerData.FindProperty("_exitButton").objectReferenceValue = exitButton;
            controllerData.ApplyModifiedPropertiesWithoutUndo();

            CreateEventSystem(uiRoot);
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 1.5f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 14f;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.09f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        private static void CreateArenaVisuals(Transform root, Sprite sprite, TetrisBalanceConfig config)
        {
            var origin = new Vector2(-config.boardWidth * 0.5f, -10f);
            var center = origin + new Vector2(config.boardWidth * 0.5f, config.boardHeight * 0.5f);

            CreateQuad("Board Background", root, sprite, center, new Vector2(config.boardWidth, config.boardHeight), new Color(0.07f, 0.085f, 0.15f), -20);

            var gridRoot = new GameObject("Grid Lines").transform;
            gridRoot.SetParent(root, false);
            for (var x = 0; x <= config.boardWidth; x++)
                CreateQuad($"Vertical {x:00}", gridRoot, sprite, new Vector2(origin.x + x, center.y), new Vector2(0.025f, config.boardHeight), new Color(0.3f, 0.4f, 0.6f, 0.18f), -10);
            for (var y = 0; y <= config.boardHeight; y++)
                CreateQuad($"Horizontal {y:00}", gridRoot, sprite, new Vector2(center.x, origin.y + y), new Vector2(config.boardWidth, 0.025f), new Color(0.3f, 0.4f, 0.6f, 0.18f), -10);

            var dangerY = origin.y + config.dangerStartRow;
            CreateQuad("Danger Line (Overflow)", root, sprite, new Vector2(center.x, dangerY), new Vector2(config.boardWidth, 0.08f), new Color(1f, 0.2f, 0.25f, 0.85f), 5);

            var borders = new GameObject("Arena Borders").transform;
            borders.SetParent(root, false);
            var borderColor = new Color(0.35f, 0.65f, 1f, 0.9f);
            CreateQuad("Left Wall", borders, sprite, new Vector2(origin.x - 0.08f, center.y), new Vector2(0.16f, config.boardHeight + 0.2f), borderColor, 5);
            CreateQuad("Right Wall", borders, sprite, new Vector2(origin.x + config.boardWidth + 0.08f, center.y), new Vector2(0.16f, config.boardHeight + 0.2f), borderColor, 5);
            CreateQuad("Floor", borders, sprite, new Vector2(center.x, origin.y - 0.08f), new Vector2(config.boardWidth + 0.2f, 0.16f), borderColor, 5);
        }

        private static HudReferences CreateHud(Transform uiRoot)
        {
            var canvasObject = new GameObject("Tetris HUD Canvas");
            canvasObject.transform.SetParent(uiRoot, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var title = CreateText(canvasObject.transform, "Game Title", "BLOCK ESCAPE  /  TETRIS CORE", 34, TextAnchor.UpperLeft);
            SetRect(title.rectTransform, new Vector2(24f, -20f), new Vector2(760f, 60f), new Vector2(0f, 1f));
            title.color = new Color(0.35f, 0.85f, 1f);

            var stats = CreateText(canvasObject.transform, "Game Statistics", "Statistics appear in Play Mode", 24, TextAnchor.UpperLeft);
            SetRect(stats.rectTransform, new Vector2(24f, -80f), new Vector2(520f, 220f), new Vector2(0f, 1f));

            var help = CreateText(canvasObject.transform, "Tetris Controls (WASD)", "A / D  MOVE\nW  ROTATE\nS  SOFT DROP\nR  RESET MENU\nESC  PAUSE", 21, TextAnchor.LowerRight);
            SetRect(help.rectTransform, new Vector2(-24f, 24f), new Vector2(500f, 190f), new Vector2(1f, 0f));
            help.color = new Color(0.7f, 0.78f, 0.9f);

            var status = CreateText(canvasObject.transform, "Game Status", "READY", 30, TextAnchor.UpperCenter);
            SetRect(status.rectTransform, new Vector2(0f, -20f), new Vector2(600f, 60f), new Vector2(0.5f, 1f));

            var previewObject = new GameObject("Next Piece Preview");
            previewObject.transform.SetParent(canvasObject.transform, false);
            var previewBackground = previewObject.AddComponent<Image>();
            previewBackground.color = new Color(0.055f, 0.07f, 0.13f, 0.92f);
            SetRect(previewBackground.rectTransform, new Vector2(-70f, -150f), new Vector2(280f, 240f), new Vector2(1f, 1f));
            var nextPreview = previewObject.AddComponent<NextPiecePreview>();

            var previewTitle = CreateText(previewObject.transform, "Preview Title", "NEXT BLOCK", 26, TextAnchor.UpperCenter);
            SetRect(previewTitle.rectTransform, new Vector2(0f, -16f), new Vector2(250f, 45f), new Vector2(0.5f, 1f));
            previewTitle.color = new Color(0.35f, 0.85f, 1f);

            var previewCellsObject = new GameObject("Preview Cells", typeof(RectTransform));
            previewCellsObject.transform.SetParent(previewObject.transform, false);
            var previewCellsRoot = (RectTransform)previewCellsObject.transform;
            SetRect(previewCellsRoot, new Vector2(0f, -10f), new Vector2(220f, 130f), new Vector2(0.5f, 0.5f));

            var previewCells = new Image[4];
            for (var i = 0; i < previewCells.Length; i++)
            {
                var cellObject = new GameObject($"Preview Cell {i + 1}");
                cellObject.transform.SetParent(previewCellsRoot, false);
                previewCells[i] = cellObject.AddComponent<Image>();
                previewCells[i].color = Color.white;
                SetRect(previewCells[i].rectTransform, Vector2.zero, new Vector2(35f, 35f), new Vector2(0.5f, 0.5f));
            }

            var kindText = CreateText(previewObject.transform, "Piece Name", "?", 22, TextAnchor.LowerCenter);
            SetRect(kindText.rectTransform, new Vector2(0f, 12f), new Vector2(240f, 36f), new Vector2(0.5f, 0f));
            kindText.color = new Color(0.78f, 0.84f, 0.95f);

            var previewData = new SerializedObject(nextPreview);
            previewData.FindProperty("_cellRoot").objectReferenceValue = previewCellsRoot;
            var previewCellsProperty = previewData.FindProperty("_cells");
            previewCellsProperty.arraySize = previewCells.Length;
            for (var i = 0; i < previewCells.Length; i++)
                previewCellsProperty.GetArrayElementAtIndex(i).objectReferenceValue = previewCells[i];
            previewData.FindProperty("_kindText").objectReferenceValue = kindText;
            previewData.ApplyModifiedPropertiesWithoutUndo();

            var overflowRoot = new GameObject("Overflow Meter");
            overflowRoot.transform.SetParent(canvasObject.transform, false);
            var rootImage = overflowRoot.AddComponent<Image>();
            rootImage.color = new Color(0f, 0f, 0f, 0.65f);
            SetRect(rootImage.rectTransform, new Vector2(-24f, -24f), new Vector2(420f, 26f), new Vector2(1f, 1f));

            var fillObject = new GameObject("Danger Fill");
            fillObject.transform.SetParent(overflowRoot.transform, false);
            var overflowFill = fillObject.AddComponent<Image>();
            overflowFill.color = new Color(1f, 0.2f, 0.25f);
            overflowFill.type = Image.Type.Filled;
            overflowFill.fillMethod = Image.FillMethod.Horizontal;
            overflowFill.fillAmount = 0f;
            var fillRect = overflowFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);

            var pauseMenu = CreatePauseMenu(canvasObject.transform);
            var gameOverMenu = CreateGameOverMenu(canvasObject.transform);
            CreateEventSystem(uiRoot);

            return new HudReferences(stats, status, overflowFill, nextPreview, pauseMenu, gameOverMenu);
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

            var message = CreateText(confirmationDialog.transform, "Confirmation Message", "Bạn có muốn reset lại màn chơi không?\nToàn bộ tiến trình của lượt hiện tại sẽ bị mất.", 23, TextAnchor.MiddleCenter);
            SetRect(message.rectTransform, new Vector2(0f, 25f), new Vector2(540f, 100f), new Vector2(0.5f, 0.5f));

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

            var menuData = new SerializedObject(controller);
            menuData.FindProperty("_pausePanel").objectReferenceValue = pausePanel;
            menuData.FindProperty("_resetConfirmationPanel").objectReferenceValue = confirmationPanel;
            menuData.FindProperty("_mainMenuConfirmationPanel").objectReferenceValue = mainMenuConfirmationPanel;
            menuData.FindProperty("_runStatsText").objectReferenceValue = runStats;
            menuData.FindProperty("_resumeButton").objectReferenceValue = resumeButton;
            menuData.FindProperty("_resetButton").objectReferenceValue = resetButton;
            menuData.FindProperty("_mainMenuButton").objectReferenceValue = mainMenuButton;
            menuData.FindProperty("_confirmResetButton").objectReferenceValue = confirmButton;
            menuData.FindProperty("_cancelResetButton").objectReferenceValue = cancelButton;
            menuData.FindProperty("_confirmMainMenuButton").objectReferenceValue = confirmMainMenuButton;
            menuData.FindProperty("_cancelMainMenuButton").objectReferenceValue = cancelMainMenuButton;
            menuData.ApplyModifiedPropertiesWithoutUndo();

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

            var menuData = new SerializedObject(controller);
            menuData.FindProperty("_panel").objectReferenceValue = panel;
            menuData.FindProperty("_summaryText").objectReferenceValue = summary;
            menuData.FindProperty("_restartButton").objectReferenceValue = restartButton;
            menuData.FindProperty("_mainMenuButton").objectReferenceValue = mainMenuButton;
            menuData.ApplyModifiedPropertiesWithoutUndo();

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

        private static void CreateEventSystem(Transform parent)
        {
            var eventSystemObject = new GameObject("Event System");
            eventSystemObject.transform.SetParent(parent, false);
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

        private static Sprite CreateOrLoadSquareSprite()
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(SquareAssetPath))
                if (asset is Sprite sprite) return sprite;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Generated White Pixel",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, SquareAssetPath);

            var square = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            square.name = "Generated Square Sprite";
            AssetDatabase.AddObjectToAsset(square, texture);
            AssetDatabase.SaveAssets();
            return square;
        }

        private static GameObject CreateQuad(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size, Color color, int order)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = new Vector3(position.x, position.y, 0f);
            gameObject.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return gameObject;
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

        private static TetrisBalanceConfig CreateOrLoadConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<TetrisBalanceConfig>(ConfigPath);
            if (config != null)
                return config;

            config = ScriptableObject.CreateInstance<TetrisBalanceConfig>();
            config.name = "Tetris Balance Config";
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        private static void EnsureLayers()
        {
            var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var serializedObject = new SerializedObject(tagManager);
            var layers = serializedObject.FindProperty("layers");
            AddLayer(layers, "World");
            AddLayer(layers, "FallingBlock");
            serializedObject.ApplyModifiedProperties();
        }

        private static void AddLayer(SerializedProperty layers, string layerName)
        {
            for (var i = 8; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return;

            for (var i = 8; i < layers.arraySize; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = layerName;
                return;
            }

            Debug.LogWarning("No free user layer slot for " + layerName);
        }

        private readonly struct HudReferences
        {
            public readonly Text stats;
            public readonly Text status;
            public readonly Image overflowFill;
            public readonly NextPiecePreview nextPreview;
            public readonly TetrisPauseMenu pauseMenu;
            public readonly TetrisGameOverMenu gameOverMenu;

            public HudReferences(
                Text stats,
                Text status,
                Image overflowFill,
                NextPiecePreview nextPreview,
                TetrisPauseMenu pauseMenu,
                TetrisGameOverMenu gameOverMenu)
            {
                this.stats = stats;
                this.status = status;
                this.overflowFill = overflowFill;
                this.nextPreview = nextPreview;
                this.pauseMenu = pauseMenu;
                this.gameOverMenu = gameOverMenu;
            }
        }
    }
}
