using System.IO;
using BlockEscape.Bootstrap;
using BlockEscape.Tetris;
using BlockEscape.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BlockEscape.Editor
{
    [InitializeOnLoad]
    public static class BlockEscapeTetrisSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/TetrisDemo.unity";
        private const string ConfigPath = "Assets/_Project/Resources/TetrisBalanceConfig.asset";
        private const string SquareAssetPath = "Assets/_Project/Art/GeneratedSquare.asset";
        private const string ClassroomMarker = "m_Name: Tetris Controls (WASD)";

        static BlockEscapeTetrisSetup()
        {
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
            managerData.FindProperty("_statsText").objectReferenceValue = hud.stats;
            managerData.FindProperty("_statusText").objectReferenceValue = hud.status;
            managerData.FindProperty("_overflowFill").objectReferenceValue = hud.overflowFill;
            managerData.FindProperty("_nextPiecePreview").objectReferenceValue = hud.nextPreview;
            managerData.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = "PRU213 Team";
            PlayerSettings.productName = "Block Escape";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Selection.activeGameObject = gameManagerObject;
            Debug.Log("Classroom-ready Tetris scene built successfully: " + ScenePath);
        }

        private static void UpgradeOldDemoOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;

            if (File.Exists(ScenePath) && File.ReadAllText(ScenePath).Contains(ClassroomMarker))
                return;

            BuildTetrisDemo();
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

            var help = CreateText(canvasObject.transform, "Tetris Controls (WASD)", "A / D  MOVE\nW  ROTATE\nS  SOFT DROP\nR  RESET\nP  PAUSE", 21, TextAnchor.LowerRight);
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

            return new HudReferences(stats, status, overflowFill, nextPreview);
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

            public HudReferences(Text stats, Text status, Image overflowFill, NextPiecePreview nextPreview)
            {
                this.stats = stats;
                this.status = status;
                this.overflowFill = overflowFill;
                this.nextPreview = nextPreview;
            }
        }
    }
}
