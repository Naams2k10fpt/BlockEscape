using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BlockEscape.Core;
using BlockEscape.Player;
using NUnit.Framework;
using UnityEngine;

namespace BlockEscape.Tetris.Tests
{
    /// <summary>
    /// Runtime-system contract tests for configs, board views, spawning, pickups,
    /// dynamic events and drone tuning. Coroutines are avoided where a deterministic
    /// public or private tick method can validate the same decision in EditMode.
    /// </summary>
    public sealed class GameplayRuntimeSystemsExhaustiveTests
    {
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Assembly RuntimeAssembly = typeof(BlockBoard).Assembly;
        private readonly List<UnityEngine.Object> _createdObjects = new();

        private static IEnumerable<TestCaseData> BalanceDimensions
        {
            get
            {
                yield return new TestCaseData(-100, -100, 4, 8).SetName("Balance_NegativeDimensionsClampMinimum");
                yield return new TestCaseData(-1, -1, 4, 8).SetName("Balance_MinusOneDimensionsClampMinimum");
                yield return new TestCaseData(0, 0, 4, 8).SetName("Balance_ZeroDimensionsClampMinimum");
                yield return new TestCaseData(1, 1, 4, 8).SetName("Balance_OneDimensionsClampMinimum");
                yield return new TestCaseData(3, 7, 4, 8).SetName("Balance_JustBelowMinimumClamps");
                yield return new TestCaseData(4, 8, 4, 8).SetName("Balance_MinimumDimensionsRemain");
                yield return new TestCaseData(14, 20, 14, 20).SetName("Balance_DefaultDimensionsRemain");
                yield return new TestCaseData(20, 40, 20, 40).SetName("Balance_LargeDimensionsRemain");
            }
        }

        private static IEnumerable<TestCaseData> DangerRowCases
        {
            get
            {
                yield return new TestCaseData(8, -100, 0).SetName("DangerRow_FarBelowClampsZero");
                yield return new TestCaseData(8, -1, 0).SetName("DangerRow_BelowClampsZero");
                yield return new TestCaseData(8, 0, 0).SetName("DangerRow_ZeroRemains");
                yield return new TestCaseData(8, 1, 1).SetName("DangerRow_OneRemains");
                yield return new TestCaseData(8, 6, 6).SetName("DangerRow_InsideRemains");
                yield return new TestCaseData(8, 7, 7).SetName("DangerRow_TopRemains");
                yield return new TestCaseData(8, 8, 7).SetName("DangerRow_AtHeightClampsTop");
                yield return new TestCaseData(8, 100, 7).SetName("DangerRow_FarAboveClampsTop");
                yield return new TestCaseData(20, 18, 18).SetName("DangerRow_DefaultRemains");
                yield return new TestCaseData(20, 19, 19).SetName("DangerRow_DefaultTopRemains");
                yield return new TestCaseData(20, 20, 19).SetName("DangerRow_DefaultAtHeightClamps");
            }
        }

        private static IEnumerable<TestCaseData> FallSpeedCases
        {
            get
            {
                yield return new TestCaseData(2f, 0.5f, 5f, -10, 2f).SetName("FallSpeed_NegativePhaseUsesPhaseOne");
                yield return new TestCaseData(2f, 0.5f, 5f, 0, 2f).SetName("FallSpeed_ZeroPhaseUsesPhaseOne");
                yield return new TestCaseData(2f, 0.5f, 5f, 1, 2f).SetName("FallSpeed_PhaseOneUsesBase");
                yield return new TestCaseData(2f, 0.5f, 5f, 2, 2.5f).SetName("FallSpeed_PhaseTwoAddsIncrement");
                yield return new TestCaseData(2f, 0.5f, 5f, 3, 3f).SetName("FallSpeed_PhaseThreeAddsTwice");
                yield return new TestCaseData(2f, 0.5f, 5f, 7, 5f).SetName("FallSpeed_PhaseSevenHitsCap");
                yield return new TestCaseData(2f, 0.5f, 5f, 100, 5f).SetName("FallSpeed_LargePhaseStaysCapped");
                yield return new TestCaseData(1f, 0f, 4f, 10, 1f).SetName("FallSpeed_ZeroIncrementStaysBase");
                yield return new TestCaseData(3f, 2f, 3f, 5, 3f).SetName("FallSpeed_MaxEqualsBaseStaysBase");
            }
        }

        private static IEnumerable<TestCaseData> CoordinateCases
        {
            get
            {
                yield return new TestCaseData(0, 0).SetName("Coordinates_BottomLeft");
                yield return new TestCaseData(1, 0).SetName("Coordinates_BottomSecond");
                yield return new TestCaseData(0, 1).SetName("Coordinates_LeftSecondRow");
                yield return new TestCaseData(3, 7).SetName("Coordinates_MinimumBoardTopRight");
                yield return new TestCaseData(7, 10).SetName("Coordinates_Middle");
                yield return new TestCaseData(13, 19).SetName("Coordinates_DefaultTopRight");
                yield return new TestCaseData(-1, 0).SetName("Coordinates_LeftOutsideStillMaps");
                yield return new TestCaseData(14, 20).SetName("Coordinates_AboveRightStillMaps");
            }
        }

        private static IEnumerable<TestCaseData> EventPhaseRanges
        {
            get
            {
                yield return new TestCaseData(1, 4f, 6f).SetName("Event_PhaseOneUsesEarlyRange");
                yield return new TestCaseData(2, 4f, 6f).SetName("Event_PhaseTwoUsesEarlyRange");
                yield return new TestCaseData(3, 6f, 8f).SetName("Event_PhaseThreeUsesMiddleRange");
                yield return new TestCaseData(4, 8f, 10f).SetName("Event_PhaseFourUsesLateRange");
                yield return new TestCaseData(5, 8f, 10f).SetName("Event_PhaseFiveUsesLateRange");
                yield return new TestCaseData(10, 8f, 10f).SetName("Event_PhaseTenUsesLateRange");
                yield return new TestCaseData(100, 8f, 10f).SetName("Event_LargePhaseUsesLateRange");
            }
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
        }

        [Test]
        public void TetrisBalanceConfig_DefaultsMatchGameplayContract()
        {
            var config = CreateBalance();

            Assert.That(config.boardWidth, Is.EqualTo(14));
            Assert.That(config.boardHeight, Is.EqualTo(20));
            Assert.That(config.dangerStartRow, Is.EqualTo(18));
            Assert.That(config.overflowGraceSeconds, Is.EqualTo(3f));
            Assert.That(config.initialSpawnDelay, Is.EqualTo(0.5f));
            Assert.That(config.spawnDelay, Is.EqualTo(0.6f));
            Assert.That(config.telegraphSeconds, Is.Zero);
            Assert.That(config.fallSpeedCellsPerSecond, Is.EqualTo(2f));
            Assert.That(config.maxFallSpeedCellsPerSecond, Is.EqualTo(5f));
            Assert.That(config.fallSpeedIncreasePerPhase, Is.EqualTo(0.35f));
            Assert.That(config.lockDelaySeconds, Is.EqualTo(0.12f));
            Assert.That(config.phaseDurationSeconds, Is.EqualTo(45f));
            Assert.That(config.rowClearWarningSeconds, Is.EqualTo(0.6f));
            Assert.That(config.rowCollapseSeconds, Is.EqualTo(0.15f));
            Assert.That(config.seed, Is.Zero);
        }

        [TestCaseSource(nameof(BalanceDimensions))]
        public void TetrisBalanceConfig_SanitizeClampsBoardDimensions(
            int width,
            int height,
            int expectedWidth,
            int expectedHeight)
        {
            var config = CreateBalance();
            config.boardWidth = width;
            config.boardHeight = height;

            config.Sanitize();

            Assert.That(config.boardWidth, Is.EqualTo(expectedWidth));
            Assert.That(config.boardHeight, Is.EqualTo(expectedHeight));
        }

        [TestCaseSource(nameof(DangerRowCases))]
        public void TetrisBalanceConfig_SanitizeClampsDangerRow(int height, int row, int expected)
        {
            var config = CreateBalance();
            config.boardHeight = height;
            config.dangerStartRow = row;

            config.Sanitize();

            Assert.That(config.dangerStartRow, Is.EqualTo(expected));
        }

        [Test]
        public void TetrisBalanceConfig_SanitizeClampsTimingAndSpeedFieldsItOwns()
        {
            var config = CreateBalance();
            config.overflowGraceSeconds = -1f;
            config.fallSpeedCellsPerSecond = -5f;
            config.maxFallSpeedCellsPerSecond = -10f;
            config.fallSpeedIncreasePerPhase = -3f;
            config.phaseDurationSeconds = -20f;

            config.Sanitize();

            Assert.That(config.overflowGraceSeconds, Is.EqualTo(0.1f));
            Assert.That(config.fallSpeedCellsPerSecond, Is.EqualTo(0.1f));
            Assert.That(config.maxFallSpeedCellsPerSecond, Is.EqualTo(0.1f));
            Assert.That(config.fallSpeedIncreasePerPhase, Is.Zero);
            Assert.That(config.phaseDurationSeconds, Is.EqualTo(1f));
        }

        [Test]
        public void TetrisBalanceConfig_SanitizeRaisesMaxSpeedToBaseSpeed()
        {
            var config = CreateBalance();
            config.fallSpeedCellsPerSecond = 7f;
            config.maxFallSpeedCellsPerSecond = 2f;

            config.Sanitize();

            Assert.That(config.fallSpeedCellsPerSecond, Is.EqualTo(7f));
            Assert.That(config.maxFallSpeedCellsPerSecond, Is.EqualTo(7f));
        }

        [TestCaseSource(nameof(FallSpeedCases))]
        public void TetrisBalanceConfig_GetFallSpeedForPhaseInterpolatesAndCaps(
            float baseSpeed,
            float increase,
            float maximum,
            int phase,
            float expected)
        {
            var config = CreateBalance();
            config.fallSpeedCellsPerSecond = baseSpeed;
            config.fallSpeedIncreasePerPhase = increase;
            config.maxFallSpeedCellsPerSecond = maximum;
            config.Sanitize();

            Assert.That(config.GetFallSpeedForPhase(phase), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void BlockBoard_InitializeCreatesSanitizedModelAndCellRoot()
        {
            var config = CreateBalance();
            config.boardWidth = 2;
            config.boardHeight = 3;
            var board = CreateBoard(config);

            Assert.That(board.Width, Is.EqualTo(4));
            Assert.That(board.Height, Is.EqualTo(8));
            Assert.That(board.Model, Is.Not.Null);
            Assert.That(board.Model.OccupiedCount, Is.Zero);
            Assert.That(board.IsResolving, Is.False);
            Assert.That(board.IsOverflowed, Is.False);
            Assert.That(board.transform.Find("Locked Cells"), Is.Not.Null);
        }

        [TestCaseSource(nameof(CoordinateCases))]
        public void BlockBoard_CellWorldConversionRoundTrips(int x, int y)
        {
            var config = CreateBalance();
            var board = CreateBoard(config);
            board.transform.position = new Vector3(-7.25f, -10.5f, 3f);
            var cell = new Vector2Int(x, y);

            var world = board.WorldForCell(cell);
            var roundTrip = board.CellForWorld(world);

            Assert.That(roundTrip, Is.EqualTo(cell));
            Assert.That(world.x, Is.EqualTo(board.transform.position.x + x + 0.5f));
            Assert.That(world.y, Is.EqualTo(board.transform.position.y + y + 0.5f));
            Assert.That(world.z, Is.EqualTo(board.transform.position.z));
        }

        [Test]
        public void BlockBoard_CellForWorldUsesFloorAtCellEdges()
        {
            var board = CreateBoard(CreateBalance());
            board.transform.position = new Vector3(-7f, -10f, 0f);

            Assert.That(board.CellForWorld(new Vector2(-7f, -10f)), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(board.CellForWorld(new Vector2(-6.001f, -9.001f)), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(board.CellForWorld(new Vector2(-6f, -9f)), Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(board.CellForWorld(new Vector2(-7.001f, -10.001f)), Is.EqualTo(new Vector2Int(-1, -1)));
        }

        [Test]
        public void BlockBoard_CanPlaceDelegatesToModelOccupancy()
        {
            var board = CreateBoard(CreateBalance());
            var cells = TetrominoCatalog.GetCells(TetrominoKind.O, 0);

            Assert.That(board.CanPlace(cells, Vector2Int.zero), Is.True);
            board.Model.SetOccupied(1, 1);
            Assert.That(board.CanPlace(cells, Vector2Int.zero), Is.False);
            Assert.That(board.CanPlace(cells, new Vector2Int(2, 0)), Is.True);
        }

        [Test]
        public void BlockBoard_CommitPieceCreatesViewsAndPublishesKind()
        {
            var board = CreateBoard(CreateBalance());
            var locked = new List<TetrominoKind>();
            board.PieceLocked += locked.Add;

            var accepted = board.CommitPiece(TetrominoKind.O, 0, new Vector2Int(2, 0));

            Assert.That(accepted, Is.True);
            Assert.That(board.Model.OccupiedCount, Is.EqualTo(4));
            Assert.That(board.GetComponentsInChildren<BlockCellView>(), Has.Length.EqualTo(4));
            CollectionAssert.AreEqual(new[] { TetrominoKind.O }, locked);
        }

        [Test]
        public void BlockBoard_RejectsOverlappingCommitWithoutNewViewsOrEvent()
        {
            var board = CreateBoard(CreateBalance());
            Assert.That(board.CommitPiece(TetrominoKind.O, 0, new Vector2Int(2, 0)), Is.True);
            var events = 0;
            board.PieceLocked += _ => events++;

            var accepted = board.CommitPiece(TetrominoKind.T, 0, new Vector2Int(1, 0));

            Assert.That(accepted, Is.False);
            Assert.That(board.Model.OccupiedCount, Is.EqualTo(4));
            Assert.That(board.GetComponentsInChildren<BlockCellView>(), Has.Length.EqualTo(4));
            Assert.That(events, Is.Zero);
        }

        [Test]
        public void BlockBoard_CommitAboveTopTriggersOverflowExactlyOnce()
        {
            var board = CreateBoard(CreateBalance());
            var overflowEvents = 0;
            board.Overflowed += () => overflowEvents++;

            Assert.That(board.CommitPiece(TetrominoKind.O, 0, new Vector2Int(2, board.Height)), Is.True);
            Assert.That(board.IsOverflowed, Is.True);
            Assert.That(overflowEvents, Is.EqualTo(1));
            Assert.That(board.CommitPiece(TetrominoKind.O, 0, new Vector2Int(4, board.Height)), Is.False);
            Assert.That(overflowEvents, Is.EqualTo(1));
        }

        [Test]
        public void BlockBoard_GetDensestEligibleRowHonorsMinimumAndFirstTie()
        {
            var board = CreateBoard(CreateBalance());
            board.Model.SetOccupied(0, 0);
            board.Model.SetOccupied(0, 2);
            board.Model.SetOccupied(1, 2);
            board.Model.SetOccupied(0, 4);
            board.Model.SetOccupied(1, 4);
            board.Model.SetOccupied(0, 6);

            Assert.That(board.GetDensestEligibleRow(), Is.EqualTo(2));
            Assert.That(board.GetDensestEligibleRow(3), Is.EqualTo(4));
            Assert.That(board.GetDensestEligibleRow(5), Is.EqualTo(6));
            Assert.That(board.GetDensestEligibleRow(7), Is.EqualTo(-1));
        }

        [Test]
        public void BlockBoard_ResetClearsModelViewsOverflowAndResolveState()
        {
            var board = CreateBoard(CreateBalance());
            Assert.That(board.CommitPiece(TetrominoKind.O, 0, Vector2Int.zero), Is.True);
            SetField(board, "_overflowTriggered", true);
            SetAutoProperty(board, "IsResolving", true);
            var overflowChanged = new List<(bool dangerous, float normalized)>();
            board.OverflowChanged += (dangerous, normalized) => overflowChanged.Add((dangerous, normalized));

            board.ResetBoard();

            Assert.That(board.Model.OccupiedCount, Is.Zero);
            Assert.That(board.IsOverflowed, Is.False);
            Assert.That(board.IsResolving, Is.False);
            Assert.That(board.GetComponentsInChildren<BlockCellView>(), Is.Empty);
            Assert.That(overflowChanged.Last(), Is.EqualTo((false, 0f)));
        }

        [Test]
        public void BlockBoard_ForceClearRowRejectsInvalidEmptyAndResolvingCases()
        {
            var board = CreateBoard(CreateBalance());

            Assert.That(board.ForceClearRow(-1), Is.False);
            Assert.That(board.ForceClearRow(board.Height), Is.False);
            Assert.That(board.ForceClearRow(0), Is.False);

            board.Model.SetOccupied(0, 1);
            SetAutoProperty(board, "IsResolving", true);
            Assert.That(board.ForceClearRow(1), Is.False);
        }

        [Test]
        public void BlockBoard_DestroyCellsRejectsInvalidOrMissingViews()
        {
            var board = CreateBoard(CreateBalance());
            board.Model.SetOccupied(0, 0);
            var center = board.WorldForCell(Vector2Int.zero);

            Assert.That(board.DestroyCellsInRadius(center, -1, 0f), Is.Zero);
            Assert.That(board.DestroyCellsInRadius(center, 0, 0f), Is.Zero, "Model-only cells have no runtime view to destroy.");

            SetAutoProperty(board, "IsResolving", true);
            Assert.That(board.DestroyCellsInRadius(center, 5, 0f), Is.Zero);
        }

        [Test]
        public void BlockCellView_InitializeConfiguresRendererColliderLayerAndParent()
        {
            var parent = Track(new GameObject("Cell Parent"));
            var cellObject = Track(new GameObject("Cell View"));
            var view = cellObject.AddComponent<BlockCellView>();

            view.Initialize(parent.transform);

            var renderer = cellObject.GetComponent<SpriteRenderer>();
            var collider = cellObject.GetComponent<BoxCollider2D>();
            Assert.That(view.transform.parent, Is.SameAs(parent.transform));
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(renderer.sortingOrder, Is.EqualTo(10));
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.size, Is.EqualTo(new Vector2(1.08f, 1.08f)));
            var worldLayer = LayerMask.NameToLayer("World");
            if (worldLayer >= 0)
                Assert.That(cellObject.layer, Is.EqualTo(worldLayer));
        }

        [Test]
        public void BlockCellView_ActivateFlashMoveAndDeactivateLifecycle()
        {
            var parent = Track(new GameObject("Cell Lifecycle Parent"));
            var cellObject = Track(new GameObject("Cell Lifecycle"));
            var view = cellObject.AddComponent<BlockCellView>();
            view.Initialize(parent.transform);
            var baseColor = new Color(0.2f, 0.4f, 0.6f, 1f);

            view.Activate(new Vector2Int(3, 4), new Vector3(3.5f, 4.5f, 0f), baseColor);
            Assert.That(view.GridPosition, Is.EqualTo(new Vector2Int(3, 4)));
            Assert.That(view.transform.position, Is.EqualTo(new Vector3(3.5f, 4.5f, 0f)));
            Assert.That(view.transform.localScale, Is.EqualTo(new Vector3(0.92f, 0.92f, 1f)));
            Assert.That(cellObject.GetComponent<BoxCollider2D>().enabled, Is.True);

            view.SetFlash(true);
            Assert.That(cellObject.GetComponent<SpriteRenderer>().color, Is.EqualTo(Color.white));
            view.SetFlash(false);
            Assert.That(cellObject.GetComponent<SpriteRenderer>().color, Is.EqualTo(baseColor));

            view.MoveTo(new Vector2Int(1, 2), new Vector3(1.5f, 2.5f, 0f), 0f);
            Assert.That(view.GridPosition, Is.EqualTo(new Vector2Int(1, 2)));
            Assert.That(view.transform.position, Is.EqualTo(new Vector3(1.5f, 2.5f, 0f)));

            view.Deactivate();
            Assert.That(cellObject.activeSelf, Is.False);
        }

        [Test]
        public void RuntimeVisuals_SquareIsCachedWhitePointFilteredSprite()
        {
            var type = RequireType("BlockEscape.Tetris.RuntimeVisuals");
            var property = type.GetProperty("Square", StaticMembers);

            var first = (Sprite)property.GetValue(null);
            var second = (Sprite)property.GetValue(null);

            Assert.That(second, Is.SameAs(first));
            Assert.That(first.name, Is.EqualTo("Runtime Square Sprite"));
            Assert.That(first.texture.width, Is.EqualTo(1));
            Assert.That(first.texture.height, Is.EqualTo(1));
            Assert.That(first.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(first.texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(first.texture.GetPixel(0, 0), Is.EqualTo(Color.white));
        }

        [Test]
        public void RuntimeVisuals_CreateQuadAppliesAllArguments()
        {
            var type = RequireType("BlockEscape.Tetris.RuntimeVisuals");
            var method = type.GetMethod("CreateQuad", StaticMembers);
            var parent = Track(new GameObject("Quad Parent"));
            var position = new Vector3(2f, 3f, 4f);
            var size = new Vector2(5f, 6f);
            var color = new Color(0.1f, 0.2f, 0.3f, 0.4f);

            var quad = (GameObject)method.Invoke(null, new object[] { "Test Quad", parent.transform, position, size, color, 27 });

            Assert.That(quad.name, Is.EqualTo("Test Quad"));
            Assert.That(quad.transform.parent, Is.SameAs(parent.transform));
            Assert.That(quad.transform.position, Is.EqualTo(position));
            Assert.That(quad.transform.localScale, Is.EqualTo(new Vector3(5f, 6f, 1f)));
            var renderer = quad.GetComponent<SpriteRenderer>();
            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(renderer.color, Is.EqualTo(color));
            Assert.That(renderer.sortingOrder, Is.EqualTo(27));
        }

        [Test]
        public void TetrominoSpawner_InitializeWithoutLoopSetsDeterministicState()
        {
            var config = CreateBalance();
            config.seed = 12345;
            var board = CreateBoard(config);
            var spawner = board.gameObject.AddComponent<TetrominoSpawner>();
            var nextEvents = new List<TetrominoKind>();
            spawner.NextPieceChanged += nextEvents.Add;

            spawner.Initialize(board, config, null, startSpawning: false);

            Assert.That(spawner.Board, Is.SameAs(board));
            Assert.That(spawner.Seed, Is.EqualTo(12345));
            Assert.That(spawner.PiecesSpawned, Is.Zero);
            Assert.That(spawner.ActivePiece, Is.Null);
            Assert.That(spawner.CurrentFallSpeed, Is.EqualTo(config.fallSpeedCellsPerSecond));
            Assert.That(spawner.FallSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(spawner.OverdriveVisualActive, Is.False);
            CollectionAssert.AreEqual(new[] { spawner.NextKind }, nextEvents);
        }

        [Test]
        public void TetrominoSpawner_PhaseAndMultiplierComposeThenRestartDefaults()
        {
            var config = CreateBalance();
            config.seed = 91;
            config.fallSpeedCellsPerSecond = 2f;
            config.fallSpeedIncreasePerPhase = 0.5f;
            config.maxFallSpeedCellsPerSecond = 5f;
            var board = CreateBoard(config);
            var spawner = board.gameObject.AddComponent<TetrominoSpawner>();
            spawner.Initialize(board, config, null, startSpawning: false);

            spawner.ApplyDifficultyPhase(4);
            Assert.That(spawner.CurrentFallSpeed, Is.EqualTo(3.5f));
            spawner.SetFallSpeedMultiplier(1.6f, overdriveVisual: true);
            Assert.That(spawner.CurrentFallSpeed, Is.EqualTo(5.6f).Within(0.001f));
            Assert.That(spawner.OverdriveVisualActive, Is.True);

            spawner.Restart(startSpawning: false);
            Assert.That(spawner.PiecesSpawned, Is.Zero);
            Assert.That(spawner.CurrentFallSpeed, Is.EqualTo(2f));
            Assert.That(spawner.FallSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(spawner.OverdriveVisualActive, Is.False);
        }

        [TestCase(-100f, 0.1f)]
        [TestCase(-1f, 0.1f)]
        [TestCase(0f, 0.1f)]
        [TestCase(0.05f, 0.1f)]
        [TestCase(0.1f, 0.1f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(1f, 1f)]
        [TestCase(1.6f, 1.6f)]
        [TestCase(10f, 10f)]
        public void TetrominoSpawner_MultiplierClampsOnlyMinimum(float input, float expected)
        {
            var config = CreateBalance();
            var board = CreateBoard(config);
            var spawner = board.gameObject.AddComponent<TetrominoSpawner>();
            spawner.Initialize(board, config, null, startSpawning: false);

            spawner.SetFallSpeedMultiplier(input, overdriveVisual: true);

            Assert.That(spawner.FallSpeedMultiplier, Is.EqualTo(expected));
            Assert.That(spawner.CurrentFallSpeed, Is.EqualTo(config.fallSpeedCellsPerSecond * expected).Within(0.001f));
            Assert.That(spawner.OverdriveVisualActive, Is.True);
        }

        [Test]
        public void PickupKind_ContainsAllShippedPowerupsInStableOrder()
        {
            var values = Enum.GetValues(typeof(PickupKind)).Cast<PickupKind>().ToArray();

            CollectionAssert.AreEqual(
                new[] { PickupKind.ScoreCrystal, PickupKind.JumpBoost, PickupKind.HealthPack },
                values);
        }

        [Test]
        public void PickupDirector_UninitializedCannotSpawn()
        {
            var directorObject = Track(new GameObject("Uninitialized Pickup Director"));
            var director = directorObject.AddComponent<PickupDirector>();

            Assert.That(director.TrySpawnNow(), Is.False);
            Assert.That(director.ActiveCount, Is.Zero);
        }

        [Test]
        public void PickupDirector_ResetSameSeedRestoresSameTimerAndClearsPool()
        {
            var config = CreateBalance();
            var board = CreateBoard(config);
            var health = CreateHealth();
            var directorObject = Track(new GameObject("Pickup Determinism"));
            var director = directorObject.AddComponent<PickupDirector>();
            director.Initialize(board, health, 500);
            Assert.That(director.TrySpawnNow(), Is.True);

            director.ResetDirector(777);
            var firstTimer = (float)GetField(director, "_spawnTimer");
            director.ResetDirector(777);
            var secondTimer = (float)GetField(director, "_spawnTimer");

            Assert.That(director.ActiveCount, Is.Zero);
            Assert.That(firstTimer, Is.EqualTo(secondTimer).Within(0.000001f));
            Assert.That(firstTimer, Is.InRange(12f, 18f));
        }

        [Test]
        public void DynamicEventKind_ContainsAllShippedEvents()
        {
            var type = RequireType("BlockEscape.Events.DynamicEventKind");

            CollectionAssert.AreEqual(
                new[] { "None", "BlockOverdrive", "CutterSweep", "MeteorShower" },
                Enum.GetNames(type));
        }

        [Test]
        public void DynamicEventConfig_SanitizeClampsEveryGameplayField()
        {
            var type = RequireType("BlockEscape.Events.DynamicEventConfig");
            var config = Track(ScriptableObject.CreateInstance(type));
            SetPublicField(config, "overdriveFallSpeedMultiplier", -1f);
            SetPublicField(config, "overdrivePieceCount", -1);
            SetPublicField(config, "cutterWarningSeconds", -1f);
            SetPublicField(config, "cutterSpeed", -1f);
            SetPublicField(config, "meteorEventChance", 2f);
            SetPublicField(config, "meteorCount", 0);
            SetPublicField(config, "meteorWarningSeconds", 0f);
            SetPublicField(config, "meteorIntervalSeconds", 0f);
            SetPublicField(config, "meteorFallSpeed", 0f);
            SetPublicField(config, "meteorExplosionSeconds", 0f);
            SetPublicField(config, "meteorStartHeight", 0f);
            SetPublicField(config, "meteorDestroyRadiusCells", -5);
            SetPublicField(config, "meteorBlockFlashSeconds", -5f);
            SetPublicField(config, "phase2MinIntervalSeconds", -2f);
            SetPublicField(config, "phase2MaxIntervalSeconds", -3f);
            type.GetMethod("Sanitize").Invoke(config, null);

            Assert.That(GetPublicField<float>(config, "overdriveFallSpeedMultiplier"), Is.EqualTo(0.1f));
            Assert.That(GetPublicField<int>(config, "overdrivePieceCount"), Is.EqualTo(1));
            Assert.That(GetPublicField<float>(config, "cutterWarningSeconds"), Is.EqualTo(0.1f));
            Assert.That(GetPublicField<float>(config, "cutterSpeed"), Is.EqualTo(0.1f));
            Assert.That(GetPublicField<float>(config, "meteorEventChance"), Is.EqualTo(1f));
            Assert.That(GetPublicField<int>(config, "meteorCount"), Is.EqualTo(1));
            Assert.That(GetPublicField<float>(config, "meteorWarningSeconds"), Is.EqualTo(0.05f));
            Assert.That(GetPublicField<float>(config, "meteorIntervalSeconds"), Is.EqualTo(0.05f));
            Assert.That(GetPublicField<float>(config, "meteorFallSpeed"), Is.EqualTo(0.1f));
            Assert.That(GetPublicField<float>(config, "meteorExplosionSeconds"), Is.EqualTo(0.05f));
            Assert.That(GetPublicField<float>(config, "meteorStartHeight"), Is.EqualTo(0.1f));
            Assert.That(GetPublicField<int>(config, "meteorDestroyRadiusCells"), Is.Zero);
            Assert.That(GetPublicField<float>(config, "meteorBlockFlashSeconds"), Is.Zero);
            Assert.That(GetPublicField<float>(config, "phase2MinIntervalSeconds"), Is.Zero);
            Assert.That(GetPublicField<float>(config, "phase2MaxIntervalSeconds"), Is.Zero);
        }

        [TestCaseSource(nameof(EventPhaseRanges))]
        public void EventDirector_IntervalFallsInsideConfiguredPhaseRange(int phase, float minimum, float maximum)
        {
            var configType = RequireType("BlockEscape.Events.DynamicEventConfig");
            var directorType = RequireType("BlockEscape.Events.EventDirector");
            var config = Track(ScriptableObject.CreateInstance(configType));
            var method = directorType.GetMethod("GetNextIntervalForPhase", StaticMembers);

            for (var seed = 0; seed < 25; seed++)
            {
                var interval = (float)method.Invoke(null, new object[] { phase, config, new System.Random(seed) });
                Assert.That(interval, Is.InRange(minimum, maximum));
            }
        }

        [TestCase(-100, false)]
        [TestCase(-1, false)]
        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(100, true)]
        public void EventDirector_CanRunEventsStartsAtPhaseOne(int phase, bool expected)
        {
            var type = RequireType("BlockEscape.Events.EventDirector");
            var method = type.GetMethod("CanRunEvents", StaticMembers);

            Assert.That((bool)method.Invoke(null, new object[] { phase }), Is.EqualTo(expected));
        }

        [Test]
        public void EventDirector_CutterTargetClampsToPlayableRows()
        {
            var board = CreateBoard(CreateBalance());
            board.transform.position = new Vector3(-7f, -10f, 0f);
            var type = RequireType("BlockEscape.Events.EventDirector");
            var method = type.GetMethod("GetCutterTargetRow", StaticMembers);

            Assert.That((int)method.Invoke(null, new object[] { null, Vector2.zero }), Is.EqualTo(-1));
            Assert.That((int)method.Invoke(null, new object[] { board, new Vector2(0f, -100f) }), Is.EqualTo(1));
            Assert.That((int)method.Invoke(null, new object[] { board, new Vector2(0f, -4.2f) }), Is.EqualTo(5));
            Assert.That((int)method.Invoke(null, new object[] { board, new Vector2(0f, 100f) }), Is.EqualTo(board.Height - 1));
        }

        [Test]
        public void DroneState_ContainsCompleteLifecycle()
        {
            var type = RequireType("BlockEscape.AI.DroneState");

            CollectionAssert.AreEqual(
                new[] { "Disabled", "Patrol", "Detect", "Telegraph", "Dash", "Recover" },
                Enum.GetNames(type));
        }

        [Test]
        public void DroneConfig_SanitizeClampsAllTimingSpeedAndScoreFields()
        {
            var type = RequireType("BlockEscape.AI.DroneConfig");
            var config = Track(ScriptableObject.CreateInstance(type));
            SetPublicField(config, "patrolSpeed", -1f);
            SetPublicField(config, "patrolHeightNormalized", 2f);
            SetPublicField(config, "arenaSidePadding", -1f);
            SetPublicField(config, "detectRange", -1f);
            SetPublicField(config, "detectConfirmSeconds", -1f);
            SetPublicField(config, "telegraphSeconds", -1f);
            SetPublicField(config, "dashSpeed", -1f);
            SetPublicField(config, "dashSeconds", -1f);
            SetPublicField(config, "recoverSeconds", -1f);
            SetPublicField(config, "shootIntervalSeconds", -1f);
            SetPublicField(config, "bulletSpeed", -1f);
            SetPublicField(config, "bulletLifetimeSeconds", -1f);
            SetPublicField(config, "explosionSeconds", -1f);
            SetPublicField(config, "respawnSeconds", -1f);
            SetPublicField(config, "destroyScore", -1);
            type.GetMethod("Sanitize").Invoke(config, null);

            Assert.That(GetPublicField<float>(config, "patrolSpeed"), Is.EqualTo(0.1f));
            Assert.That(GetPublicField<float>(config, "patrolHeightNormalized"), Is.EqualTo(0.95f));
            Assert.That(GetPublicField<float>(config, "arenaSidePadding"), Is.EqualTo(0.1f));
            Assert.That(GetPublicField<float>(config, "detectRange"), Is.EqualTo(0.1f));
            Assert.That(GetPublicField<float>(config, "detectConfirmSeconds"), Is.Zero);
            Assert.That(GetPublicField<float>(config, "telegraphSeconds"), Is.Zero);
            Assert.That(GetPublicField<float>(config, "dashSpeed"), Is.EqualTo(0.1f));
            Assert.That(GetPublicField<float>(config, "dashSeconds"), Is.EqualTo(0.05f));
            Assert.That(GetPublicField<float>(config, "recoverSeconds"), Is.Zero);
            Assert.That(GetPublicField<float>(config, "shootIntervalSeconds"), Is.EqualTo(0.1f));
            Assert.That(GetPublicField<float>(config, "bulletSpeed"), Is.EqualTo(0.1f));
            Assert.That(GetPublicField<float>(config, "bulletLifetimeSeconds"), Is.EqualTo(0.1f));
            Assert.That(GetPublicField<float>(config, "explosionSeconds"), Is.EqualTo(0.05f));
            Assert.That(GetPublicField<float>(config, "respawnSeconds"), Is.Zero);
            Assert.That(GetPublicField<int>(config, "destroyScore"), Is.EqualTo(1));
        }

        private TetrisBalanceConfig CreateBalance()
        {
            return Track(ScriptableObject.CreateInstance<TetrisBalanceConfig>());
        }

        private BlockBoard CreateBoard(TetrisBalanceConfig config)
        {
            var boardObject = Track(new GameObject("Comprehensive Runtime Board"));
            var board = boardObject.AddComponent<BlockBoard>();
            board.Initialize(config);
            return board;
        }

        private PlayerHealth CreateHealth()
        {
            var player = Track(new GameObject("Comprehensive Runtime Player"));
            player.AddComponent<Rigidbody2D>();
            var health = player.AddComponent<PlayerHealth>();
            Invoke(health, "Awake");
            return health;
        }

        private static Type RequireType(string fullName)
        {
            var type = RuntimeAssembly.GetType(fullName);
            Assert.That(type, Is.Not.Null, $"Runtime type {fullName} is missing.");
            return type;
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethods(InstanceMembers)
                .FirstOrDefault(candidate =>
                    candidate.Name == methodName &&
                    candidate.GetParameters().Length == arguments.Length);
            Assert.That(method, Is.Not.Null, $"Method {target.GetType().Name}.{methodName} was not found.");
            return method.Invoke(target, arguments);
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

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            SetField(target, $"<{propertyName}>k__BackingField", value);
        }

        private static void SetPublicField(UnityEngine.Object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"Field {target.GetType().Name}.{fieldName} was not found.");
            field.SetValue(target, value);
        }

        private static T GetPublicField<T>(UnityEngine.Object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"Field {target.GetType().Name}.{fieldName} was not found.");
            return (T)field.GetValue(target);
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            _createdObjects.Add(value);
            return value;
        }
    }
}
