using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BlockEscape.Core;
using BlockEscape.Player;
using BlockEscape.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace BlockEscape.Tetris.Tests
{
    /// <summary>
    /// Integration coverage for shipped assets and all UI presentation contracts.
    /// These tests catch missing prefab references, renamed input actions and menu
    /// listener regressions without entering Play Mode or changing project assets.
    /// </summary>
    public sealed class UiAssetsAndIntegrationExhaustiveTests
    {
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player.prefab";
        private const string ArenaPrefabPath = "Assets/_Project/Prefabs/Arena/Arena.prefab";
        private const string TetrisScenePath = "Assets/_Project/Scenes/TetrisDemo.unity";
        private const string SandboxScenePath = "Assets/_Project/Scenes/Sandbox/ArenaSandbox.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string PlayerConfigPath = "Assets/_Project/Resources/PlayerConfig.asset";
        private const string TetrisConfigPath = "Assets/_Project/Resources/TetrisBalanceConfig.asset";

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private string _temporaryDirectory;

        private static IEnumerable<TestCaseData> RequiredFolders
        {
            get
            {
                yield return new TestCaseData("Assets/_Project").SetName("Folder_ProjectRootExists");
                yield return new TestCaseData("Assets/_Project/Animations").SetName("Folder_AnimationsExists");
                yield return new TestCaseData("Assets/_Project/Art").SetName("Folder_ArtExists");
                yield return new TestCaseData("Assets/_Project/Audio").SetName("Folder_AudioExists");
                yield return new TestCaseData("Assets/_Project/Editor").SetName("Folder_EditorExists");
                yield return new TestCaseData("Assets/_Project/Prefabs").SetName("Folder_PrefabsExists");
                yield return new TestCaseData("Assets/_Project/Resources").SetName("Folder_ResourcesExists");
                yield return new TestCaseData("Assets/_Project/Scenes").SetName("Folder_ScenesExists");
                yield return new TestCaseData("Assets/_Project/Scripts").SetName("Folder_ScriptsExists");
                yield return new TestCaseData("Assets/_Project/Tests").SetName("Folder_TestsExists");
                yield return new TestCaseData("Assets/_Project/Scripts/Bootstrap").SetName("Folder_BootstrapScriptsExists");
                yield return new TestCaseData("Assets/_Project/Scripts/Core").SetName("Folder_CoreScriptsExists");
                yield return new TestCaseData("Assets/_Project/Scripts/Gameplay").SetName("Folder_GameplayScriptsExists");
                yield return new TestCaseData("Assets/_Project/Scripts/Player").SetName("Folder_PlayerScriptsExists");
                yield return new TestCaseData("Assets/_Project/Scripts/UI").SetName("Folder_UiScriptsExists");
                yield return new TestCaseData("Assets/_Project/Tests/EditMode").SetName("Folder_EditModeTestsExists");
            }
        }

        private static IEnumerable<TestCaseData> RequiredAssets
        {
            get
            {
                yield return new TestCaseData(PlayerPrefabPath, typeof(GameObject)).SetName("Asset_PlayerPrefabExists");
                yield return new TestCaseData(ArenaPrefabPath, typeof(GameObject)).SetName("Asset_ArenaPrefabExists");
                yield return new TestCaseData(TetrisScenePath, typeof(SceneAsset)).SetName("Asset_TetrisSceneExists");
                yield return new TestCaseData(SandboxScenePath, typeof(SceneAsset)).SetName("Asset_SandboxSceneExists");
                yield return new TestCaseData(InputActionsPath, typeof(InputActionAsset)).SetName("Asset_InputActionsExists");
                yield return new TestCaseData(PlayerConfigPath, typeof(PlayerConfig)).SetName("Asset_PlayerConfigExists");
                yield return new TestCaseData(TetrisConfigPath, typeof(TetrisBalanceConfig)).SetName("Asset_TetrisConfigExists");
                yield return new TestCaseData("Assets/_Project/Scripts/BlockEscape.Runtime.asmdef", typeof(TextAsset)).SetName("Asset_RuntimeAsmdefExists");
                yield return new TestCaseData("Assets/_Project/Editor/BlockEscape.Editor.asmdef", typeof(TextAsset)).SetName("Asset_EditorAsmdefExists");
                yield return new TestCaseData("Assets/_Project/Tests/EditMode/BlockEscape.Tetris.Tests.asmdef", typeof(TextAsset)).SetName("Asset_TestAsmdefExists");
                yield return new TestCaseData("Assets/_Project/Editor/BlockEscapeTetrisSetup.cs", typeof(MonoScript)).SetName("Asset_TetrisBuilderExists");
                yield return new TestCaseData("Assets/_Project/Editor/BlockEscapePlayerSetup.cs", typeof(MonoScript)).SetName("Asset_PlayerBuilderExists");
                yield return new TestCaseData("Assets/_Project/Editor/BlockEscapeArenaSetup.cs", typeof(MonoScript)).SetName("Asset_ArenaBuilderExists");
            }
        }

        private static IEnumerable<TestCaseData> RequiredLayers
        {
            get
            {
                yield return new TestCaseData("Default").SetName("Layer_DefaultExists");
                yield return new TestCaseData("World").SetName("Layer_WorldExists");
                yield return new TestCaseData("Player").SetName("Layer_PlayerExists");
                yield return new TestCaseData("FallingBlock").SetName("Layer_FallingBlockExists");
                yield return new TestCaseData("Pickup").SetName("Layer_PickupExists");
            }
        }

        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(
                Application.temporaryCachePath,
                "BlockEscapeUiIntegrationTests",
                Guid.NewGuid().ToString("N"));
            SaveService.Load(Path.Combine(_temporaryDirectory, "save.json"));
        }

        [TearDown]
        public void TearDown()
        {
            for (var index = _createdObjects.Count - 1; index >= 0; index--)
            {
                if (_createdObjects[index] != null)
                    UnityEngine.Object.DestroyImmediate(_createdObjects[index]);
            }

            _createdObjects.Clear();
            if (Directory.Exists(_temporaryDirectory))
                Directory.Delete(_temporaryDirectory, true);
        }

        [TestCaseSource(nameof(RequiredFolders))]
        public void ProjectStructure_RequiredFolderExists(string assetPath)
        {
            Assert.That(AssetDatabase.IsValidFolder(assetPath), Is.True, $"Required folder is missing: {assetPath}");
        }

        [TestCaseSource(nameof(RequiredAssets))]
        public void ProjectStructure_RequiredAssetExistsWithExpectedType(string assetPath, Type expectedType)
        {
            var asset = AssetDatabase.LoadAssetAtPath(assetPath, expectedType);

            Assert.That(asset, Is.Not.Null, $"Required {expectedType.Name} asset is missing: {assetPath}");
        }

        [TestCaseSource(nameof(RequiredLayers))]
        public void ProjectSettings_RequiredPhysicsLayerExists(string layerName)
        {
            Assert.That(LayerMask.NameToLayer(layerName), Is.GreaterThanOrEqualTo(0), $"Layer {layerName} is missing.");
        }

        [Test]
        public void RuntimeAssemblyDefinition_HasStableNameAndRequiredReferences()
        {
            var json = ReadAssetText("Assets/_Project/Scripts/BlockEscape.Runtime.asmdef");

            Assert.That(json, Does.Contain("\"name\": \"BlockEscape.Runtime\""));
            Assert.That(json, Does.Contain("Unity.InputSystem"));
            Assert.That(json, Does.Contain("Unity.ugui"));
        }

        [Test]
        public void TestAssemblyDefinition_TargetsEditorAndReferencesRuntime()
        {
            var json = ReadAssetText("Assets/_Project/Tests/EditMode/BlockEscape.Tetris.Tests.asmdef");

            Assert.That(json, Does.Contain("\"name\": \"BlockEscape.Tetris.Tests\""));
            Assert.That(json, Does.Contain("BlockEscape.Runtime"));
            Assert.That(json, Does.Contain("Unity.InputSystem"));
            Assert.That(json, Does.Contain("\"Editor\""));
            Assert.That(json, Does.Contain("TestAssemblies"));
        }

        [Test]
        public void EditorAssemblyDefinition_TargetsEditorAndReferencesRuntime()
        {
            var json = ReadAssetText("Assets/_Project/Editor/BlockEscape.Editor.asmdef");

            Assert.That(json, Does.Contain("\"name\": \"BlockEscape.Editor\""));
            Assert.That(json, Does.Contain("BlockEscape.Runtime"));
            Assert.That(json, Does.Contain("\"Editor\""));
        }

        [Test]
        public void PlayerPrefab_ContainsCompletePlayableComponentGraph()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            Assert.That(player, Is.Not.Null);
            var body = player.GetComponent<Rigidbody2D>();
            var collider = player.GetComponent<CapsuleCollider2D>();
            var controller = player.GetComponent<PlayerController>();
            var health = player.GetComponent<PlayerHealth>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
            Assert.That(body.freezeRotation, Is.True);
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.direction, Is.EqualTo(CapsuleDirection2D.Vertical));
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Config, Is.Not.Null);
            Assert.That(health, Is.Not.Null);
            Assert.That(player.transform.Find("Ground Check"), Is.Not.Null);
            Assert.That(player.transform.Find("Visual"), Is.Not.Null);
            Assert.That(player.transform.Find("Visual").GetComponent<SpriteRenderer>(), Is.Not.Null);
            Assert.That(player.transform.Find("Visual").GetComponent<Animator>(), Is.Not.Null);
        }

        [Test]
        public void PlayerPrefab_ConfigAndPhysicsValuesStaySynchronized()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var controller = player.GetComponent<PlayerController>();
            var body = player.GetComponent<Rigidbody2D>();
            var collider = player.GetComponent<CapsuleCollider2D>();

            Assert.That(body.gravityScale, Is.EqualTo(controller.Config.gravityScale));
            Assert.That(collider.size, Is.EqualTo(controller.Config.standingColliderSize));
            Assert.That(collider.offset, Is.EqualTo(controller.Config.standingColliderOffset));
            Assert.That(controller.Config.jumpVelocity, Is.EqualTo(12.5f));
            Assert.That(controller.Config.moveSpeed, Is.EqualTo(7f));
        }

        [Test]
        public void ArenaPrefab_ContainsGridTilemapsSpawnsAndKillZone()
        {
            var arena = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath);

            Assert.That(arena, Is.Not.Null);
            Assert.That(arena.transform.Find("Grid"), Is.Not.Null);
            Assert.That(arena.transform.Find("Grid/Ground Tilemap"), Is.Not.Null);
            Assert.That(arena.transform.Find("Grid/Platform Tilemap"), Is.Not.Null);
            Assert.That(arena.transform.Find("Grid/Obstacle Tilemap"), Is.Not.Null);
            Assert.That(arena.transform.Find("Grid/Decoration Tilemap"), Is.Not.Null);
            Assert.That(arena.transform.Find("Spawn Points"), Is.Not.Null);
            Assert.That(arena.transform.Find("Spawn Points/Player Spawn"), Is.Not.Null);
            Assert.That(arena.transform.Find("Spawn Points/Drone Left"), Is.Not.Null);
            Assert.That(arena.transform.Find("Spawn Points/Drone Right"), Is.Not.Null);
            Assert.That(arena.transform.Find("Kill Zone"), Is.Not.Null);
        }

        [Test]
        public void ArenaPrefab_TilemapsHaveExpectedColliderRoles()
        {
            var arena = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath);
            var ground = arena.transform.Find("Grid/Ground Tilemap");
            var platform = arena.transform.Find("Grid/Platform Tilemap");
            var obstacle = arena.transform.Find("Grid/Obstacle Tilemap");
            var decoration = arena.transform.Find("Grid/Decoration Tilemap");

            AssertSolidTilemap(ground);
            AssertSolidTilemap(platform);
            Assert.That(obstacle.GetComponent<Tilemap>(), Is.Not.Null);
            Assert.That(obstacle.GetComponent<CompositeCollider2D>(), Is.Not.Null);
            Assert.That(obstacle.GetComponent<CompositeCollider2D>().isTrigger, Is.True);
            Assert.That(decoration.GetComponent<Tilemap>(), Is.Not.Null);
            Assert.That(decoration.GetComponent<Collider2D>(), Is.Null);
        }

        [Test]
        public void InputActionAsset_HasExactlyRequiredGameplayMaps()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            var names = asset.actionMaps.Select(map => map.name).ToArray();

            Assert.That(names, Does.Contain("Tetris"));
            Assert.That(names, Does.Contain("Player"));
            Assert.That(names, Does.Contain("System"));
            Assert.That(names.Distinct().Count(), Is.EqualTo(names.Length));
        }

        [Test]
        public void InputActionAsset_TetrisBindingsUseWasdOnly()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            var map = asset.FindActionMap("Tetris", true);

            AssertActionPaths(map, "Move", "<Keyboard>/a", "<Keyboard>/d");
            AssertActionPaths(map, "Rotate", "<Keyboard>/w");
            AssertActionPaths(map, "SoftDrop", "<Keyboard>/s");
            Assert.That(map.bindings.Any(binding => binding.path.Contains("Arrow", StringComparison.OrdinalIgnoreCase)), Is.False);
        }

        [Test]
        public void InputActionAsset_PlayerBindingsUseArrowKeysOnly()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            var map = asset.FindActionMap("Player", true);

            AssertActionPaths(map, "Move", "<Keyboard>/leftArrow", "<Keyboard>/rightArrow");
            AssertActionPaths(map, "Jump", "<Keyboard>/upArrow");
            AssertActionPaths(map, "Crouch", "<Keyboard>/downArrow");
        }

        [Test]
        public void InputActionAsset_SystemBindingsRemainAvailableDuringPause()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            var map = asset.FindActionMap("System", true);

            AssertActionPaths(map, "Pause", "<Keyboard>/escape");
            AssertActionPaths(map, "ResetRun", "<Keyboard>/r");
        }

        [Test]
        public void UiInputActions_AssignsCompleteUiActionMap()
        {
            var eventSystemObject = Track(new GameObject("UI Input Event System"));
            eventSystemObject.AddComponent<EventSystem>();
            var module = eventSystemObject.AddComponent<InputSystemUIInputModule>();

            UiInputActions.AssignTo(module);
            var asset = module.actionsAsset;
            Track(asset);

            Assert.That(asset, Is.Not.Null);
            var ui = asset.FindActionMap("UI", true);
            Assert.That(ui.FindAction("Point", false), Is.Not.Null);
            Assert.That(ui.FindAction("Navigate", false), Is.Not.Null);
            Assert.That(ui.FindAction("Submit", false), Is.Not.Null);
            Assert.That(ui.FindAction("Cancel", false), Is.Not.Null);
            Assert.That(ui.FindAction("Click", false), Is.Not.Null);
            Assert.That(ui.FindAction("RightClick", false), Is.Not.Null);
            Assert.That(ui.FindAction("MiddleClick", false), Is.Not.Null);
            Assert.That(ui.FindAction("ScrollWheel", false), Is.Not.Null);
            Assert.That(module.point.action, Is.SameAs(ui.FindAction("Point", true)));
            Assert.That(module.move.action, Is.SameAs(ui.FindAction("Navigate", true)));
            Assert.That(module.submit.action, Is.SameAs(ui.FindAction("Submit", true)));
            Assert.That(module.cancel.action, Is.SameAs(ui.FindAction("Cancel", true)));
        }

        [Test]
        public void UiInputActions_HasMouseKeyboardNavigationAndSubmitBindings()
        {
            var eventSystemObject = Track(new GameObject("UI Binding Event System"));
            eventSystemObject.AddComponent<EventSystem>();
            var module = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            UiInputActions.AssignTo(module);
            Track(module.actionsAsset);
            var ui = module.actionsAsset.FindActionMap("UI", true);

            AssertActionPaths(ui, "Point", "<Mouse>/position");
            AssertActionPaths(ui, "Navigate", "<Keyboard>/upArrow", "<Keyboard>/downArrow");
            AssertActionPaths(ui, "Navigate", "<Keyboard>/leftArrow", "<Keyboard>/rightArrow");
            AssertActionPaths(ui, "Submit", "<Keyboard>/enter", "<Keyboard>/space");
            AssertActionPaths(ui, "Cancel", "<Keyboard>/escape");
            AssertActionPaths(ui, "Click", "<Mouse>/leftButton");
            AssertActionPaths(ui, "RightClick", "<Mouse>/rightButton");
            AssertActionPaths(ui, "MiddleClick", "<Mouse>/middleButton");
            AssertActionPaths(ui, "ScrollWheel", "<Mouse>/scroll");
        }

        [Test]
        public void NextPiecePreview_ShowRendersEveryTetrominoWithFourCells()
        {
            var previewObject = Track(new GameObject("Next Piece Preview", typeof(RectTransform)));
            var rootObject = Track(new GameObject("Cell Root", typeof(RectTransform)));
            rootObject.transform.SetParent(previewObject.transform, false);
            var preview = previewObject.AddComponent<NextPiecePreview>();
            var cells = new Image[4];
            for (var index = 0; index < cells.Length; index++)
            {
                var cellObject = Track(new GameObject($"Preview Cell {index}", typeof(RectTransform), typeof(CanvasRenderer)));
                cellObject.transform.SetParent(rootObject.transform, false);
                cells[index] = cellObject.AddComponent<Image>();
            }
            var kindText = CreateText("Kind Text");
            kindText.transform.SetParent(previewObject.transform, false);
            SetField(preview, "_cellRoot", rootObject.GetComponent<RectTransform>());
            SetField(preview, "_cells", cells);
            SetField(preview, "_kindText", kindText);
            SetField(preview, "_cellSize", 38f);

            foreach (TetrominoKind kind in Enum.GetValues(typeof(TetrominoKind)))
            {
                preview.Show(kind);
                var color = TetrominoCatalog.GetColor(kind);
                Assert.That(kindText.text, Is.EqualTo(kind.ToString()));
                foreach (var cell in cells)
                {
                    Assert.That(cell.gameObject.activeSelf, Is.True);
                    Assert.That(cell.color, Is.EqualTo(color));
                    Assert.That(cell.rectTransform.sizeDelta, Is.EqualTo(new Vector2(35f, 35f)));
                    Assert.That(cell.rectTransform.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                    Assert.That(cell.rectTransform.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                }
            }
        }

        [Test]
        public void NextPiecePreview_MissingReferencesSafelyNoOp()
        {
            var previewObject = Track(new GameObject("Incomplete Preview"));
            var preview = previewObject.AddComponent<NextPiecePreview>();

            Assert.That(() => preview.Show(TetrominoKind.T), Throws.Nothing);
        }

        [Test]
        public void GameOverMenu_ShowFormatsSummaryAndHideClosesPanel()
        {
            var menuObject = Track(new GameObject("Game Over Menu"));
            var panel = Track(new GameObject("Game Over Panel"));
            var summary = CreateText("Game Over Summary");
            var restart = CreateButton("Restart");
            var mainMenu = CreateButton("Main Menu");
            var menu = menuObject.AddComponent<TetrisGameOverMenu>();
            menu.Configure(panel, summary, restart, mainMenu);

            menu.Show(17, 4, 2650, 12345, "PLAYER HP 0", 125.9f, 3);

            Assert.That(panel.activeSelf, Is.True);
            Assert.That(summary.text, Does.Contain("PLAYER HP 0"));
            Assert.That(summary.text, Does.Contain("17"));
            Assert.That(summary.text, Does.Contain("02:05"));
            Assert.That(summary.text, Does.Contain("3"));
            Assert.That(summary.text, Does.Contain("4"));
            Assert.That(summary.text, Does.Contain("2650"));
            Assert.That(summary.text, Does.Contain("12345"));

            menu.Hide();
            Assert.That(panel.activeSelf, Is.False);
        }

        [Test]
        public void GameOverMenu_ButtonsPublishOneEventPerClick()
        {
            var menuObject = Track(new GameObject("Game Over Events"));
            var panel = Track(new GameObject("Game Over Event Panel"));
            var restart = CreateButton("Restart Event");
            var mainMenu = CreateButton("Main Event");
            var menu = menuObject.AddComponent<TetrisGameOverMenu>();
            menu.Configure(panel, CreateText("Summary Event"), restart, mainMenu);
            var restarts = 0;
            var exits = 0;
            menu.RestartRequested += () => restarts++;
            menu.MainMenuRequested += () => exits++;

            restart.onClick.Invoke();
            mainMenu.onClick.Invoke();

            Assert.That(restarts, Is.EqualTo(1));
            Assert.That(exits, Is.EqualTo(1));
        }

        [Test]
        public void PauseMenu_PanelMethodsAreMutuallyExclusive()
        {
            var harness = CreatePauseMenu();

            harness.Menu.ShowPause();
            AssertPanelStates(harness, pause: true, reset: false, main: false, options: false);

            harness.Menu.ShowResetConfirmation();
            AssertPanelStates(harness, pause: false, reset: true, main: false, options: false);
            Assert.That(harness.Menu.IsConfirmationVisible, Is.True);

            harness.Menu.ShowMainMenuConfirmation();
            AssertPanelStates(harness, pause: false, reset: false, main: true, options: false);
            Assert.That(harness.Menu.IsConfirmationVisible, Is.True);

            harness.Menu.ShowOptions();
            AssertPanelStates(harness, pause: false, reset: false, main: false, options: true);
            Assert.That(harness.Menu.IsConfirmationVisible, Is.True);

            harness.Menu.HideAll();
            AssertPanelStates(harness, pause: false, reset: false, main: false, options: false);
            Assert.That(harness.Menu.IsConfirmationVisible, Is.False);
        }

        [Test]
        public void PauseMenu_SetRunStatisticsFormatsAllValuesAndClampsTime()
        {
            var harness = CreatePauseMenu();

            harness.Menu.SetRunStatistics(20, 5, 3200, 9876, 185.9f, 4);

            Assert.That(harness.Stats.text, Does.Contain("20"));
            Assert.That(harness.Stats.text, Does.Contain("03:05"));
            Assert.That(harness.Stats.text, Does.Contain("4"));
            Assert.That(harness.Stats.text, Does.Contain("5"));
            Assert.That(harness.Stats.text, Does.Contain("3200"));
            Assert.That(harness.Stats.text, Does.Contain("9876"));

            harness.Menu.SetRunStatistics(0, 0, 0, 0, -10f, 1);
            Assert.That(harness.Stats.text, Does.Contain("00:00"));
        }

        [Test]
        public void PauseMenu_ActionButtonsPublishExpectedEvents()
        {
            var harness = CreatePauseMenu();
            var resumes = 0;
            var resets = 0;
            var mainMenus = 0;
            harness.Menu.ResumeRequested += () => resumes++;
            harness.Menu.ResetConfirmed += () => resets++;
            harness.Menu.MainMenuConfirmed += () => mainMenus++;

            harness.Resume.onClick.Invoke();
            harness.ConfirmReset.onClick.Invoke();
            harness.ConfirmMain.onClick.Invoke();

            Assert.That(resumes, Is.EqualTo(1));
            Assert.That(resets, Is.EqualTo(1));
            Assert.That(mainMenus, Is.EqualTo(1));
        }

        [Test]
        public void OptionsMenu_ConfigureNormalizesAllSliderRanges()
        {
            var harness = CreateOptionsMenu();

            foreach (var slider in new[] { harness.Master, harness.Music, harness.Sfx })
            {
                Assert.That(slider.minValue, Is.Zero);
                Assert.That(slider.maxValue, Is.EqualTo(1f));
                Assert.That(slider.wholeNumbers, Is.False);
            }
        }

        [Test]
        public void OptionsMenu_ShowPopulatesSavedValuesAndHideClosesPanel()
        {
            SaveService.Data.masterVolume = 0.2f;
            SaveService.Data.musicVolume = 0.4f;
            SaveService.Data.sfxVolume = 0.6f;
            SaveService.Data.fullscreen = false;
            SaveService.Data.vSyncCount = 0;
            SaveService.Data.screenWidth = 1280;
            SaveService.Data.screenHeight = 720;
            var harness = CreateOptionsMenu();

            harness.Menu.Show();

            Assert.That(harness.Panel.activeSelf, Is.True);
            Assert.That(harness.Menu.IsVisible, Is.True);
            Assert.That(harness.Master.value, Is.EqualTo(0.2f));
            Assert.That(harness.Music.value, Is.EqualTo(0.4f));
            Assert.That(harness.Sfx.value, Is.EqualTo(0.6f));
            Assert.That(harness.Fullscreen.isOn, Is.False);
            Assert.That(harness.VSync.isOn, Is.False);
            Assert.That(harness.Resolution.text, Is.Not.Empty);

            harness.Menu.Hide();
            Assert.That(harness.Menu.IsVisible, Is.False);
        }

        [Test]
        public void OptionsMenu_AddResolutionRejectsInvalidAndDuplicateEntries()
        {
            var harness = CreateOptionsMenu();
            var add = typeof(OptionsMenu).GetMethod("AddResolution", InstanceMembers);
            var list = (IList<Vector2Int>)GetField(harness.Menu, "_resolutions");

            add.Invoke(harness.Menu, new object[] { 0, 1080 });
            add.Invoke(harness.Menu, new object[] { 1920, 0 });
            add.Invoke(harness.Menu, new object[] { -1, -1 });
            Assert.That(list, Is.Empty);

            add.Invoke(harness.Menu, new object[] { 1280, 720 });
            add.Invoke(harness.Menu, new object[] { 1280, 720 });
            add.Invoke(harness.Menu, new object[] { 1920, 1080 });
            Assert.That(list, Has.Count.EqualTo(2));
            CollectionAssert.AreEqual(new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) }, list);
        }

        [Test]
        public void OptionsMenu_BackButtonClosesAndPublishesExactlyOnce()
        {
            var harness = CreateOptionsMenu();
            harness.Menu.Show();
            var closed = 0;
            harness.Menu.Closed += () => closed++;

            harness.Back.onClick.Invoke();

            Assert.That(harness.Panel.activeSelf, Is.False);
            Assert.That(closed, Is.EqualTo(1));
        }

        [Test]
        public void MainMenu_ConfigureRefreshesRecordsAndOptionsButtonOpensMenu()
        {
            SaveService.Data.highScore = 5432;
            SaveService.Data.bestSurvivalTime = 125.9f;
            var mainObject = Track(new GameObject("Main Menu Controller"));
            var main = mainObject.AddComponent<MainMenuController>();
            var start = CreateButton("Main Start");
            var options = CreateButton("Main Options");
            var exit = CreateButton("Main Exit");
            var records = CreateText("Main Records");
            var optionsHarness = CreateOptionsMenu();

            main.Configure(start, options, exit, records, optionsHarness.Menu);

            Assert.That(records.text, Does.Contain("5432"));
            Assert.That(records.text, Does.Contain("02:05"));
            options.onClick.Invoke();
            Assert.That(optionsHarness.Menu.IsVisible, Is.True);
        }

        private PauseHarness CreatePauseMenu()
        {
            var menuObject = Track(new GameObject("Comprehensive Pause Menu"));
            var menu = menuObject.AddComponent<TetrisPauseMenu>();
            var pause = Track(new GameObject("Pause Panel"));
            var reset = Track(new GameObject("Reset Panel"));
            var main = Track(new GameObject("Main Confirmation Panel"));
            var stats = CreateText("Pause Stats");
            var resume = CreateButton("Resume");
            var resetButton = CreateButton("Reset");
            var optionsButton = CreateButton("Options");
            var mainButton = CreateButton("Main Menu");
            var options = CreateOptionsMenu();
            var confirmReset = CreateButton("Confirm Reset");
            var cancelReset = CreateButton("Cancel Reset");
            var confirmMain = CreateButton("Confirm Main");
            var cancelMain = CreateButton("Cancel Main");
            menu.Configure(
                pause,
                reset,
                main,
                stats,
                resume,
                resetButton,
                optionsButton,
                mainButton,
                options.Menu,
                confirmReset,
                cancelReset,
                confirmMain,
                cancelMain);
            return new PauseHarness(
                menu,
                pause,
                reset,
                main,
                stats,
                resume,
                confirmReset,
                confirmMain,
                options);
        }

        private OptionsHarness CreateOptionsMenu()
        {
            var menuObject = Track(new GameObject("Comprehensive Options Menu"));
            var panel = Track(new GameObject("Options Panel"));
            var menu = menuObject.AddComponent<OptionsMenu>();
            var master = CreateSlider("Master Volume");
            var music = CreateSlider("Music Volume");
            var sfx = CreateSlider("SFX Volume");
            var resolution = CreateText("Resolution");
            var previous = CreateButton("Previous Resolution");
            var next = CreateButton("Next Resolution");
            var fullscreen = CreateToggle("Fullscreen");
            var vSync = CreateToggle("VSync");
            var apply = CreateButton("Apply Options");
            var back = CreateButton("Back Options");
            menu.Configure(
                panel,
                master,
                music,
                sfx,
                resolution,
                previous,
                next,
                fullscreen,
                vSync,
                apply,
                back);
            return new OptionsHarness(menu, panel, master, music, sfx, resolution, fullscreen, vSync, back);
        }

        private Button CreateButton(string name)
        {
            var gameObject = Track(new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)));
            return gameObject.AddComponent<Button>();
        }

        private Slider CreateSlider(string name)
        {
            var gameObject = Track(new GameObject(name, typeof(RectTransform)));
            return gameObject.AddComponent<Slider>();
        }

        private Toggle CreateToggle(string name)
        {
            var gameObject = Track(new GameObject(name, typeof(RectTransform)));
            return gameObject.AddComponent<Toggle>();
        }

        private Text CreateText(string name)
        {
            var gameObject = Track(new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)));
            return gameObject.AddComponent<Text>();
        }

        private static string ReadAssetText(string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            Assert.That(asset, Is.Not.Null, $"Text asset is missing: {assetPath}");
            return asset.text;
        }

        private static void AssertSolidTilemap(Transform transform)
        {
            Assert.That(transform, Is.Not.Null);
            Assert.That(transform.GetComponent<Tilemap>(), Is.Not.Null);
            Assert.That(transform.GetComponent<TilemapCollider2D>(), Is.Not.Null);
            Assert.That(transform.GetComponent<CompositeCollider2D>(), Is.Not.Null);
            Assert.That(transform.GetComponent<CompositeCollider2D>().isTrigger, Is.False);
            Assert.That(transform.GetComponent<Rigidbody2D>(), Is.Not.Null);
            Assert.That(transform.GetComponent<Rigidbody2D>().bodyType, Is.EqualTo(RigidbodyType2D.Static));
        }

        private static void AssertActionPaths(InputActionMap map, string actionName, params string[] paths)
        {
            var action = map.FindAction(actionName, true);
            var actual = action.bindings.Select(binding => binding.path).ToArray();
            foreach (var path in paths)
                Assert.That(actual, Does.Contain(path), $"{map.name}/{actionName} must contain {path}.");
        }

        private static object GetField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstanceMembers);
            Assert.That(field, Is.Not.Null, $"Field {target.GetType().Name}.{fieldName} was not found.");
            return field.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstanceMembers);
            Assert.That(field, Is.Not.Null, $"Field {target.GetType().Name}.{fieldName} was not found.");
            field.SetValue(target, value);
        }

        private static void AssertPanelStates(
            PauseHarness harness,
            bool pause,
            bool reset,
            bool main,
            bool options)
        {
            Assert.That(harness.PausePanel.activeSelf, Is.EqualTo(pause));
            Assert.That(harness.ResetPanel.activeSelf, Is.EqualTo(reset));
            Assert.That(harness.MainPanel.activeSelf, Is.EqualTo(main));
            Assert.That(harness.Options.Menu.IsVisible, Is.EqualTo(options));
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            _createdObjects.Add(value);
            return value;
        }

        private sealed class OptionsHarness
        {
            public OptionsHarness(
                OptionsMenu menu,
                GameObject panel,
                Slider master,
                Slider music,
                Slider sfx,
                Text resolution,
                Toggle fullscreen,
                Toggle vSync,
                Button back)
            {
                Menu = menu;
                Panel = panel;
                Master = master;
                Music = music;
                Sfx = sfx;
                Resolution = resolution;
                Fullscreen = fullscreen;
                VSync = vSync;
                Back = back;
            }

            public OptionsMenu Menu { get; }
            public GameObject Panel { get; }
            public Slider Master { get; }
            public Slider Music { get; }
            public Slider Sfx { get; }
            public Text Resolution { get; }
            public Toggle Fullscreen { get; }
            public Toggle VSync { get; }
            public Button Back { get; }
        }

        private sealed class PauseHarness
        {
            public PauseHarness(
                TetrisPauseMenu menu,
                GameObject pausePanel,
                GameObject resetPanel,
                GameObject mainPanel,
                Text stats,
                Button resume,
                Button confirmReset,
                Button confirmMain,
                OptionsHarness options)
            {
                Menu = menu;
                PausePanel = pausePanel;
                ResetPanel = resetPanel;
                MainPanel = mainPanel;
                Stats = stats;
                Resume = resume;
                ConfirmReset = confirmReset;
                ConfirmMain = confirmMain;
                Options = options;
            }

            public TetrisPauseMenu Menu { get; }
            public GameObject PausePanel { get; }
            public GameObject ResetPanel { get; }
            public GameObject MainPanel { get; }
            public Text Stats { get; }
            public Button Resume { get; }
            public Button ConfirmReset { get; }
            public Button ConfirmMain { get; }
            public OptionsHarness Options { get; }
        }
    }
}
