using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BlockEscape.Tetris.Tests
{
    /// <summary>
    /// Exhaustive, asset-independent coverage for the deterministic Tetris rules.
    /// These tests intentionally exercise many board sizes and seeds because this
    /// layer is the foundation used by spawning, row clearing, events and pickups.
    /// </summary>
    public sealed class BoardAndTetrominoExhaustiveTests
    {
        private static readonly TetrominoKind[] AllKinds =
        {
            TetrominoKind.I,
            TetrominoKind.J,
            TetrominoKind.L,
            TetrominoKind.O,
            TetrominoKind.S,
            TetrominoKind.T,
            TetrominoKind.Z
        };

        private static IEnumerable<TestCaseData> ValidBoardDimensions
        {
            get
            {
                yield return new TestCaseData(1, 1).SetName("Board_1x1_IsValid");
                yield return new TestCaseData(1, 8).SetName("Board_1x8_IsValid");
                yield return new TestCaseData(4, 1).SetName("Board_4x1_IsValid");
                yield return new TestCaseData(4, 8).SetName("Board_4x8_IsValid");
                yield return new TestCaseData(10, 20).SetName("Board_10x20_IsValid");
                yield return new TestCaseData(14, 20).SetName("Board_Default14x20_IsValid");
                yield return new TestCaseData(20, 40).SetName("Board_20x40_IsValid");
                yield return new TestCaseData(64, 64).SetName("Board_64x64_IsValid");
            }
        }

        private static IEnumerable<TestCaseData> InvalidBoardDimensions
        {
            get
            {
                yield return new TestCaseData(0, 1).SetName("Board_ZeroWidth_IsRejected");
                yield return new TestCaseData(1, 0).SetName("Board_ZeroHeight_IsRejected");
                yield return new TestCaseData(0, 0).SetName("Board_ZeroSize_IsRejected");
                yield return new TestCaseData(-1, 1).SetName("Board_NegativeWidth_IsRejected");
                yield return new TestCaseData(1, -1).SetName("Board_NegativeHeight_IsRejected");
                yield return new TestCaseData(-10, 20).SetName("Board_LargeNegativeWidth_IsRejected");
                yield return new TestCaseData(14, -20).SetName("Board_LargeNegativeHeight_IsRejected");
                yield return new TestCaseData(int.MinValue, 1).SetName("Board_MinimumWidth_IsRejected");
                yield return new TestCaseData(1, int.MinValue).SetName("Board_MinimumHeight_IsRejected");
            }
        }

        private static IEnumerable<TestCaseData> OutsideCells
        {
            get
            {
                yield return new TestCaseData(-1, 0).SetName("Occupied_LeftOfBoard_IsFalse");
                yield return new TestCaseData(0, -1).SetName("Occupied_BelowBoard_IsFalse");
                yield return new TestCaseData(-1, -1).SetName("Occupied_BelowLeft_IsFalse");
                yield return new TestCaseData(4, 0).SetName("Occupied_AtWidth_IsFalse");
                yield return new TestCaseData(0, 8).SetName("Occupied_AtHeight_IsFalse");
                yield return new TestCaseData(4, 8).SetName("Occupied_AboveRight_IsFalse");
                yield return new TestCaseData(100, 4).SetName("Occupied_FarRight_IsFalse");
                yield return new TestCaseData(2, 100).SetName("Occupied_FarAbove_IsFalse");
                yield return new TestCaseData(int.MinValue, 0).SetName("Occupied_MinimumX_IsFalse");
                yield return new TestCaseData(0, int.MinValue).SetName("Occupied_MinimumY_IsFalse");
                yield return new TestCaseData(int.MaxValue, 0).SetName("Occupied_MaximumX_IsFalse");
                yield return new TestCaseData(0, int.MaxValue).SetName("Occupied_MaximumY_IsFalse");
            }
        }

        private static IEnumerable<TestCaseData> SingleCellPlacements
        {
            get
            {
                yield return new TestCaseData(0, 0, true).SetName("CanPlace_BottomLeft_IsTrue");
                yield return new TestCaseData(3, 0, true).SetName("CanPlace_BottomRight_IsTrue");
                yield return new TestCaseData(0, 7, true).SetName("CanPlace_TopLeft_IsTrue");
                yield return new TestCaseData(3, 7, true).SetName("CanPlace_TopRight_IsTrue");
                yield return new TestCaseData(-1, 0, false).SetName("CanPlace_LeftOutside_IsFalse");
                yield return new TestCaseData(4, 0, false).SetName("CanPlace_RightOutside_IsFalse");
                yield return new TestCaseData(0, -1, false).SetName("CanPlace_BelowOutside_IsFalse");
                yield return new TestCaseData(0, 8, true).SetName("CanPlace_AboveTop_Default_IsTrue");
                yield return new TestCaseData(3, 20, true).SetName("CanPlace_FarAboveTop_Default_IsTrue");
            }
        }

        private static IEnumerable<TestCaseData> RotationAliases
        {
            get
            {
                foreach (var kind in AllKinds)
                {
                    yield return new TestCaseData(kind, 0, 4).SetName($"{kind}_Rotation0_EqualsRotation4");
                    yield return new TestCaseData(kind, 1, 5).SetName($"{kind}_Rotation1_EqualsRotation5");
                    yield return new TestCaseData(kind, 2, 6).SetName($"{kind}_Rotation2_EqualsRotation6");
                    yield return new TestCaseData(kind, 3, 7).SetName($"{kind}_Rotation3_EqualsRotation7");
                    yield return new TestCaseData(kind, 0, -4).SetName($"{kind}_Rotation0_EqualsRotationMinus4");
                    yield return new TestCaseData(kind, 1, -3).SetName($"{kind}_Rotation1_EqualsRotationMinus3");
                    yield return new TestCaseData(kind, 2, -2).SetName($"{kind}_Rotation2_EqualsRotationMinus2");
                    yield return new TestCaseData(kind, 3, -1).SetName($"{kind}_Rotation3_EqualsRotationMinus1");
                }
            }
        }

        private static IEnumerable<TestCaseData> SevenBagSeeds
        {
            get
            {
                yield return new TestCaseData(0).SetName("SevenBag_Seed_0");
                yield return new TestCaseData(1).SetName("SevenBag_Seed_1");
                yield return new TestCaseData(2).SetName("SevenBag_Seed_2");
                yield return new TestCaseData(3).SetName("SevenBag_Seed_3");
                yield return new TestCaseData(7).SetName("SevenBag_Seed_7");
                yield return new TestCaseData(11).SetName("SevenBag_Seed_11");
                yield return new TestCaseData(17).SetName("SevenBag_Seed_17");
                yield return new TestCaseData(23).SetName("SevenBag_Seed_23");
                yield return new TestCaseData(31).SetName("SevenBag_Seed_31");
                yield return new TestCaseData(42).SetName("SevenBag_Seed_42");
                yield return new TestCaseData(64).SetName("SevenBag_Seed_64");
                yield return new TestCaseData(99).SetName("SevenBag_Seed_99");
                yield return new TestCaseData(100).SetName("SevenBag_Seed_100");
                yield return new TestCaseData(255).SetName("SevenBag_Seed_255");
                yield return new TestCaseData(1024).SetName("SevenBag_Seed_1024");
                yield return new TestCaseData(2026).SetName("SevenBag_Seed_2026");
                yield return new TestCaseData(12345).SetName("SevenBag_Seed_12345");
                yield return new TestCaseData(54321).SetName("SevenBag_Seed_54321");
                yield return new TestCaseData(-1).SetName("SevenBag_Seed_Minus1");
                yield return new TestCaseData(-2026).SetName("SevenBag_Seed_Minus2026");
                yield return new TestCaseData(int.MinValue).SetName("SevenBag_Seed_MinimumInt");
                yield return new TestCaseData(int.MaxValue).SetName("SevenBag_Seed_MaximumInt");
            }
        }

        [TestCaseSource(nameof(ValidBoardDimensions))]
        public void Constructor_ValidDimensionsCreateEmptyBoard(int width, int height)
        {
            var board = new BoardModel(width, height);

            Assert.That(board.Width, Is.EqualTo(width));
            Assert.That(board.Height, Is.EqualTo(height));
            Assert.That(board.OccupiedCount, Is.Zero);
            Assert.That(board.HighestOccupiedRow(), Is.EqualTo(-1));
            Assert.That(board.GetFullRows(), Is.Empty);

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                Assert.That(board.IsOccupied(x, y), Is.False, $"Cell ({x}, {y}) should start empty.");
        }

        [TestCaseSource(nameof(InvalidBoardDimensions))]
        public void Constructor_InvalidDimensionsThrow(int width, int height)
        {
            Assert.That(
                () => new BoardModel(width, height),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCaseSource(nameof(OutsideCells))]
        public void IsOccupied_OutsideBoardReturnsFalse(int x, int y)
        {
            var board = new BoardModel(4, 8);

            Assert.That(board.IsOccupied(x, y), Is.False);
        }

        [TestCaseSource(nameof(OutsideCells))]
        public void SetOccupied_OutsideBoardThrows(int x, int y)
        {
            var board = new BoardModel(4, 8);

            Assert.That(
                () => board.SetOccupied(x, y),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(board.OccupiedCount, Is.Zero);
        }

        [Test]
        public void SetOccupied_TransitionsMaintainExactCount()
        {
            var board = new BoardModel(4, 8);

            board.SetOccupied(0, 0);
            Assert.That(board.OccupiedCount, Is.EqualTo(1));

            board.SetOccupied(0, 0);
            Assert.That(board.OccupiedCount, Is.EqualTo(1), "Setting the same state must be idempotent.");

            board.SetOccupied(3, 7);
            Assert.That(board.OccupiedCount, Is.EqualTo(2));

            board.SetOccupied(0, 0, false);
            Assert.That(board.OccupiedCount, Is.EqualTo(1));

            board.SetOccupied(0, 0, false);
            Assert.That(board.OccupiedCount, Is.EqualTo(1), "Clearing an empty cell must be idempotent.");

            board.SetOccupied(3, 7, false);
            Assert.That(board.OccupiedCount, Is.Zero);
        }

        [TestCaseSource(nameof(SingleCellPlacements))]
        public void CanPlace_SingleCellHonorsBoundaries(int x, int y, bool expected)
        {
            var board = new BoardModel(4, 8);
            var cells = new[] { Vector2Int.zero };

            Assert.That(board.CanPlace(cells, new Vector2Int(x, y)), Is.EqualTo(expected));
        }

        [Test]
        public void CanPlace_AllowAboveTopFlagChangesOnlyTopBoundary()
        {
            var board = new BoardModel(4, 8);
            var cells = new[] { Vector2Int.zero };

            Assert.That(board.CanPlace(cells, new Vector2Int(0, 8), allowAboveTop: true), Is.True);
            Assert.That(board.CanPlace(cells, new Vector2Int(0, 8), allowAboveTop: false), Is.False);
            Assert.That(board.CanPlace(cells, new Vector2Int(-1, 8), allowAboveTop: true), Is.False);
            Assert.That(board.CanPlace(cells, new Vector2Int(4, 8), allowAboveTop: true), Is.False);
            Assert.That(board.CanPlace(cells, new Vector2Int(0, -1), allowAboveTop: true), Is.False);
        }

        [Test]
        public void CanPlace_CompositeShapeChecksEveryCell()
        {
            var board = new BoardModel(6, 8);
            var tShape = TetrominoCatalog.GetCells(TetrominoKind.T, 0);

            Assert.That(board.CanPlace(tShape, new Vector2Int(0, 0)), Is.True);
            Assert.That(board.CanPlace(tShape, new Vector2Int(3, 0)), Is.True);
            Assert.That(board.CanPlace(tShape, new Vector2Int(4, 0)), Is.False);
            Assert.That(board.CanPlace(tShape, new Vector2Int(-1, 0)), Is.False);

            board.SetOccupied(1, 0);
            Assert.That(board.CanPlace(tShape, new Vector2Int(0, 0)), Is.False);
            Assert.That(board.CanPlace(tShape, new Vector2Int(3, 0)), Is.True);
        }

        [Test]
        public void TryLock_ValidPieceOccupiesExactlyFourCells()
        {
            var board = new BoardModel(10, 20);
            var cells = TetrominoCatalog.GetCells(TetrominoKind.L, 0);

            var accepted = board.TryLock(cells, new Vector2Int(3, 4), out var aboveTop);

            Assert.That(accepted, Is.True);
            Assert.That(aboveTop, Is.False);
            Assert.That(board.OccupiedCount, Is.EqualTo(4));
            foreach (var cell in cells)
                Assert.That(board.IsOccupied(cell.x + 3, cell.y + 4), Is.True);
        }

        [Test]
        public void TryLock_OverlappingPieceIsRejectedWithoutMutation()
        {
            var board = new BoardModel(10, 20);
            var cells = TetrominoCatalog.GetCells(TetrominoKind.O, 0);
            Assert.That(board.TryLock(cells, new Vector2Int(2, 2), out _), Is.True);
            var countBefore = board.OccupiedCount;

            var accepted = board.TryLock(cells, new Vector2Int(3, 3), out var aboveTop);

            Assert.That(accepted, Is.False);
            Assert.That(aboveTop, Is.False);
            Assert.That(board.OccupiedCount, Is.EqualTo(countBefore));
            Assert.That(board.IsOccupied(4, 4), Is.False, "Rejected pieces must not partially write cells.");
        }

        [Test]
        public void TryLock_PiecePartlyAboveTopReportsOverflowAndWritesVisibleCells()
        {
            var board = new BoardModel(6, 8);
            var verticalI = TetrominoCatalog.GetCells(TetrominoKind.I, 1);

            var accepted = board.TryLock(verticalI, new Vector2Int(2, 6), out var aboveTop);

            Assert.That(accepted, Is.True);
            Assert.That(aboveTop, Is.True);
            Assert.That(board.OccupiedCount, Is.EqualTo(2));
            Assert.That(board.IsOccupied(2, 6), Is.True);
            Assert.That(board.IsOccupied(2, 7), Is.True);
        }

        [Test]
        public void TryLock_PieceFullyAboveTopReportsOverflowWithoutVisibleCells()
        {
            var board = new BoardModel(6, 8);
            var cells = TetrominoCatalog.GetCells(TetrominoKind.O, 0);

            var accepted = board.TryLock(cells, new Vector2Int(2, 8), out var aboveTop);

            Assert.That(accepted, Is.True);
            Assert.That(aboveTop, Is.True);
            Assert.That(board.OccupiedCount, Is.Zero);
            Assert.That(board.HighestOccupiedRow(), Is.EqualTo(-1));
        }

        [TestCase(-10)]
        [TestCase(-1)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(100)]
        public void GetRowFill_InvalidRowReturnsZero(int row)
        {
            var board = new BoardModel(4, 8);
            for (var x = 0; x < board.Width; x++)
                board.SetOccupied(x, 3);

            Assert.That(board.GetRowFill(row), Is.Zero);
        }

        [Test]
        public void GetRowFill_TracksSparseAndFullRows()
        {
            var board = new BoardModel(5, 8);

            board.SetOccupied(0, 0);
            board.SetOccupied(2, 0);
            board.SetOccupied(4, 0);
            Assert.That(board.GetRowFill(0), Is.EqualTo(3));

            for (var x = 0; x < board.Width; x++)
                board.SetOccupied(x, 2);
            Assert.That(board.GetRowFill(2), Is.EqualTo(5));

            board.SetOccupied(2, 0, false);
            Assert.That(board.GetRowFill(0), Is.EqualTo(2));
            Assert.That(board.GetRowFill(1), Is.Zero);
        }

        [Test]
        public void GetFullRows_ReturnsAscendingRowsOnly()
        {
            var board = new BoardModel(4, 8);
            FillRow(board, 6);
            FillRow(board, 1);
            FillRow(board, 4);
            board.SetOccupied(0, 2);
            board.SetOccupied(1, 2);
            board.SetOccupied(2, 2);

            var rows = board.GetFullRows();

            CollectionAssert.AreEqual(new[] { 1, 4, 6 }, rows);
        }

        [Test]
        public void ClearRows_NullAndEmptyCollectionsAreNoOps()
        {
            var board = CreatePatternBoard();
            var before = Snapshot(board);

            board.ClearRows(null);
            AssertSnapshot(board, before);

            board.ClearRows(Array.Empty<int>());
            AssertSnapshot(board, before);
        }

        [Test]
        public void ClearRows_SingleBottomRowCompactsEverythingDown()
        {
            var board = new BoardModel(4, 8);
            FillRow(board, 0);
            board.SetOccupied(0, 1);
            board.SetOccupied(1, 2);
            board.SetOccupied(2, 3);

            board.ClearRows(new[] { 0 });

            Assert.That(board.OccupiedCount, Is.EqualTo(3));
            Assert.That(board.IsOccupied(0, 0), Is.True);
            Assert.That(board.IsOccupied(1, 1), Is.True);
            Assert.That(board.IsOccupied(2, 2), Is.True);
            Assert.That(board.GetRowFill(3), Is.Zero);
        }

        [Test]
        public void ClearRows_MultipleNonAdjacentRowsPreservesRelativeOrder()
        {
            var board = new BoardModel(4, 10);
            FillRow(board, 1);
            FillRow(board, 4);
            board.SetOccupied(0, 0);
            board.SetOccupied(1, 2);
            board.SetOccupied(2, 3);
            board.SetOccupied(3, 5);
            board.SetOccupied(0, 7);

            board.ClearRows(new[] { 4, 1 });

            Assert.That(board.OccupiedCount, Is.EqualTo(5));
            Assert.That(board.IsOccupied(0, 0), Is.True);
            Assert.That(board.IsOccupied(1, 1), Is.True);
            Assert.That(board.IsOccupied(2, 2), Is.True);
            Assert.That(board.IsOccupied(3, 3), Is.True);
            Assert.That(board.IsOccupied(0, 5), Is.True);
        }

        [Test]
        public void ClearRows_DuplicateAndInvalidRowsDoNotOverCompact()
        {
            var board = new BoardModel(4, 8);
            FillRow(board, 2);
            board.SetOccupied(0, 3);
            board.SetOccupied(1, 4);

            board.ClearRows(new[] { -1, 2, 2, 2, 8, 100 });

            Assert.That(board.OccupiedCount, Is.EqualTo(2));
            Assert.That(board.IsOccupied(0, 2), Is.True);
            Assert.That(board.IsOccupied(1, 3), Is.True);
            Assert.That(board.GetFullRows(), Is.Empty);
        }

        [Test]
        public void CollapseColumns_DropsEachColumnIndependently()
        {
            var board = new BoardModel(5, 10);
            board.SetOccupied(0, 2);
            board.SetOccupied(0, 5);
            board.SetOccupied(0, 9);
            board.SetOccupied(1, 7);
            board.SetOccupied(3, 1);
            board.SetOccupied(3, 8);
            board.SetOccupied(4, 0);

            board.CollapseColumns();

            Assert.That(board.OccupiedCount, Is.EqualTo(7));
            AssertColumn(board, 0, true, true, true, false, false, false, false, false, false, false);
            AssertColumn(board, 1, true, false, false, false, false, false, false, false, false, false);
            AssertColumn(board, 2, false, false, false, false, false, false, false, false, false, false);
            AssertColumn(board, 3, true, true, false, false, false, false, false, false, false, false);
            AssertColumn(board, 4, true, false, false, false, false, false, false, false, false, false);
        }

        [Test]
        public void CollapseColumns_IsIdempotent()
        {
            var board = CreatePatternBoard();
            board.CollapseColumns();
            var once = Snapshot(board);

            board.CollapseColumns();

            AssertSnapshot(board, once);
        }

        [TestCase(-100, true)]
        [TestCase(-1, true)]
        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(4, true)]
        [TestCase(5, false)]
        [TestCase(7, false)]
        [TestCase(8, false)]
        [TestCase(100, false)]
        public void HasOccupiedAtOrAbove_ClampsProbeAndFindsHighestStack(int row, bool expected)
        {
            var board = new BoardModel(4, 8);
            board.SetOccupied(2, 4);

            Assert.That(board.HasOccupiedAtOrAbove(row), Is.EqualTo(expected));
        }

        [Test]
        public void HighestOccupiedRow_TracksAddRemoveClearAndCollapse()
        {
            var board = new BoardModel(4, 8);
            Assert.That(board.HighestOccupiedRow(), Is.EqualTo(-1));

            board.SetOccupied(0, 2);
            Assert.That(board.HighestOccupiedRow(), Is.EqualTo(2));

            board.SetOccupied(3, 7);
            Assert.That(board.HighestOccupiedRow(), Is.EqualTo(7));

            board.SetOccupied(3, 7, false);
            Assert.That(board.HighestOccupiedRow(), Is.EqualTo(2));

            board.CollapseColumns();
            Assert.That(board.HighestOccupiedRow(), Is.EqualTo(0));

            board.Clear();
            Assert.That(board.HighestOccupiedRow(), Is.EqualTo(-1));
        }

        [Test]
        public void Clear_ResetsAllDerivedStateAndCanBeRepeated()
        {
            var board = CreatePatternBoard();
            Assert.That(board.OccupiedCount, Is.GreaterThan(0));

            board.Clear();
            board.Clear();

            Assert.That(board.OccupiedCount, Is.Zero);
            Assert.That(board.HighestOccupiedRow(), Is.EqualTo(-1));
            Assert.That(board.GetFullRows(), Is.Empty);
            for (var y = 0; y < board.Height; y++)
            for (var x = 0; x < board.Width; x++)
                Assert.That(board.IsOccupied(x, y), Is.False);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(20)]
        [TestCase(30)]
        [TestCase(40)]
        [TestCase(50)]
        public void RandomizedSetOccupied_MatchesReferenceGrid(int seed)
        {
            const int width = 14;
            const int height = 20;
            var board = new BoardModel(width, height);
            var reference = new bool[width, height];
            var random = new System.Random(seed);

            for (var operation = 0; operation < 500; operation++)
            {
                var x = random.Next(width);
                var y = random.Next(height);
                var occupied = random.Next(2) == 0;
                reference[x, y] = occupied;
                board.SetOccupied(x, y, occupied);

                if (operation % 25 == 0)
                    AssertBoardMatchesReference(board, reference);
            }

            AssertBoardMatchesReference(board, reference);
        }

        [TestCase(101)]
        [TestCase(202)]
        [TestCase(303)]
        [TestCase(404)]
        [TestCase(505)]
        [TestCase(606)]
        [TestCase(707)]
        [TestCase(808)]
        public void RandomizedCollapseColumns_MatchesReferenceCompaction(int seed)
        {
            const int width = 10;
            const int height = 16;
            var board = new BoardModel(width, height);
            var reference = new bool[width, height];
            var random = new System.Random(seed);

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var occupied = random.NextDouble() < 0.35;
                reference[x, y] = occupied;
                board.SetOccupied(x, y, occupied);
            }

            var compacted = CollapseReferenceColumns(reference, width, height);
            board.CollapseColumns();

            AssertBoardMatchesReference(board, compacted);
        }

        [TestCase(1001)]
        [TestCase(1002)]
        [TestCase(1003)]
        [TestCase(1004)]
        [TestCase(1005)]
        [TestCase(1006)]
        [TestCase(1007)]
        [TestCase(1008)]
        public void RandomizedClearRows_MatchesReferenceCompaction(int seed)
        {
            const int width = 8;
            const int height = 12;
            var board = new BoardModel(width, height);
            var reference = new bool[width, height];
            var random = new System.Random(seed);

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var occupied = random.NextDouble() < 0.45;
                reference[x, y] = occupied;
                board.SetOccupied(x, y, occupied);
            }

            var rows = Enumerable.Range(0, height)
                .Where(_ => random.NextDouble() < 0.25)
                .ToArray();
            var compacted = ClearReferenceRows(reference, width, height, rows);
            board.ClearRows(rows);

            AssertBoardMatchesReference(board, compacted);
        }

        [Test]
        public void TetrominoKind_ContainsExactlySevenCanonicalKinds()
        {
            var values = Enum.GetValues(typeof(TetrominoKind)).Cast<TetrominoKind>().ToArray();

            CollectionAssert.AreEqual(AllKinds, values);
            Assert.That(values.Distinct().Count(), Is.EqualTo(7));
        }

        [TestCase(TetrominoKind.I)]
        [TestCase(TetrominoKind.J)]
        [TestCase(TetrominoKind.L)]
        [TestCase(TetrominoKind.O)]
        [TestCase(TetrominoKind.S)]
        [TestCase(TetrominoKind.T)]
        [TestCase(TetrominoKind.Z)]
        public void GetCells_EveryRotationHasFourUniqueNormalizedCells(TetrominoKind kind)
        {
            for (var rotation = -8; rotation <= 8; rotation++)
            {
                var cells = TetrominoCatalog.GetCells(kind, rotation);

                Assert.That(cells, Has.Length.EqualTo(4));
                Assert.That(cells.Distinct().Count(), Is.EqualTo(4));
                Assert.That(cells.Min(cell => cell.x), Is.Zero);
                Assert.That(cells.Min(cell => cell.y), Is.Zero);
                Assert.That(cells.All(cell => cell.x >= 0 && cell.y >= 0), Is.True);
            }
        }

        [TestCase(TetrominoKind.I)]
        [TestCase(TetrominoKind.J)]
        [TestCase(TetrominoKind.L)]
        [TestCase(TetrominoKind.O)]
        [TestCase(TetrominoKind.S)]
        [TestCase(TetrominoKind.T)]
        [TestCase(TetrominoKind.Z)]
        public void GetSize_ExactlyBoundsReturnedCells(TetrominoKind kind)
        {
            for (var rotation = 0; rotation < 4; rotation++)
            {
                var cells = TetrominoCatalog.GetCells(kind, rotation);
                var size = TetrominoCatalog.GetSize(kind, rotation);

                Assert.That(size.x, Is.EqualTo(cells.Max(cell => cell.x) + 1));
                Assert.That(size.y, Is.EqualTo(cells.Max(cell => cell.y) + 1));
                Assert.That(cells.All(cell => cell.x < size.x && cell.y < size.y), Is.True);
            }
        }

        [TestCaseSource(nameof(RotationAliases))]
        public void GetCells_RotationAliasesArePeriodic(TetrominoKind kind, int firstRotation, int secondRotation)
        {
            var first = TetrominoCatalog.GetCells(kind, firstRotation);
            var second = TetrominoCatalog.GetCells(kind, secondRotation);

            CollectionAssert.AreEquivalent(first, second);
        }

        [Test]
        public void GetCells_OPieceIgnoresEveryRotation()
        {
            var expected = TetrominoCatalog.GetCells(TetrominoKind.O, 0);

            for (var rotation = -20; rotation <= 20; rotation++)
                CollectionAssert.AreEqual(expected, TetrominoCatalog.GetCells(TetrominoKind.O, rotation));
        }

        [Test]
        public void GetCells_ReturnsDefensiveArrayCopies()
        {
            var first = TetrominoCatalog.GetCells(TetrominoKind.T, 0);
            var expectedFirstCell = first[0];
            first[0] = new Vector2Int(99, 99);

            var second = TetrominoCatalog.GetCells(TetrominoKind.T, 0);

            Assert.That(second[0], Is.EqualTo(expectedFirstCell));
            Assert.That(second[0], Is.Not.EqualTo(first[0]));
        }

        [Test]
        public void GetColor_ReturnsOpaqueDistinctColorForEveryKind()
        {
            var colors = AllKinds.Select(TetrominoCatalog.GetColor).ToArray();

            foreach (var color in colors)
            {
                Assert.That(color.r, Is.InRange(0f, 1f));
                Assert.That(color.g, Is.InRange(0f, 1f));
                Assert.That(color.b, Is.InRange(0f, 1f));
                Assert.That(color.a, Is.EqualTo(1f));
            }

            Assert.That(colors.Distinct().Count(), Is.EqualTo(AllKinds.Length));
        }

        [TestCaseSource(nameof(SevenBagSeeds))]
        public void SevenBag_EveryConsecutiveBagContainsAllSevenKinds(int seed)
        {
            var bag = new SevenBag(seed);

            for (var bagIndex = 0; bagIndex < 25; bagIndex++)
            {
                var draw = new TetrominoKind[7];
                for (var index = 0; index < draw.Length; index++)
                    draw[index] = bag.Next();

                CollectionAssert.AreEquivalent(AllKinds, draw, $"Bag {bagIndex} for seed {seed} was invalid.");
            }
        }

        [TestCaseSource(nameof(SevenBagSeeds))]
        public void SevenBag_SameSeedProducesSameLongSequence(int seed)
        {
            var first = new SevenBag(seed);
            var second = new SevenBag(seed);

            for (var draw = 0; draw < 350; draw++)
                Assert.That(second.Next(), Is.EqualTo(first.Next()), $"Sequence diverged at draw {draw}.");
        }

        [TestCaseSource(nameof(SevenBagSeeds))]
        public void SevenBag_ResetRewindsSequence(int seed)
        {
            var bag = new SevenBag(seed);
            var expected = new TetrominoKind[70];
            for (var index = 0; index < expected.Length; index++)
                expected[index] = bag.Next();

            for (var index = 0; index < 13; index++)
                bag.Next();
            bag.Reset(seed);

            var actual = new TetrominoKind[70];
            for (var index = 0; index < actual.Length; index++)
                actual[index] = bag.Next();
            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void SevenBag_SelectedDifferentSeedsProduceDifferentSequences()
        {
            var first = new SevenBag(100);
            var second = new SevenBag(200);
            var firstSequence = new TetrominoKind[35];
            var secondSequence = new TetrominoKind[35];

            for (var index = 0; index < firstSequence.Length; index++)
            {
                firstSequence[index] = first.Next();
                secondSequence[index] = second.Next();
            }

            CollectionAssert.AreNotEqual(firstSequence, secondSequence);
        }

        private static BoardModel CreatePatternBoard()
        {
            var board = new BoardModel(6, 10);
            board.SetOccupied(0, 0);
            board.SetOccupied(1, 1);
            board.SetOccupied(2, 2);
            board.SetOccupied(3, 3);
            board.SetOccupied(4, 5);
            board.SetOccupied(5, 7);
            board.SetOccupied(0, 9);
            return board;
        }

        private static void FillRow(BoardModel board, int row)
        {
            for (var x = 0; x < board.Width; x++)
                board.SetOccupied(x, row);
        }

        private static bool[,] Snapshot(BoardModel board)
        {
            var snapshot = new bool[board.Width, board.Height];
            for (var y = 0; y < board.Height; y++)
            for (var x = 0; x < board.Width; x++)
                snapshot[x, y] = board.IsOccupied(x, y);
            return snapshot;
        }

        private static void AssertSnapshot(BoardModel board, bool[,] expected)
        {
            Assert.That(expected.GetLength(0), Is.EqualTo(board.Width));
            Assert.That(expected.GetLength(1), Is.EqualTo(board.Height));
            AssertBoardMatchesReference(board, expected);
        }

        private static void AssertColumn(BoardModel board, int x, params bool[] expected)
        {
            Assert.That(expected, Has.Length.EqualTo(board.Height));
            for (var y = 0; y < board.Height; y++)
                Assert.That(board.IsOccupied(x, y), Is.EqualTo(expected[y]), $"Unexpected cell ({x}, {y}).");
        }

        private static void AssertBoardMatchesReference(BoardModel board, bool[,] expected)
        {
            var occupiedCount = 0;
            for (var y = 0; y < board.Height; y++)
            for (var x = 0; x < board.Width; x++)
            {
                var expectedValue = expected[x, y];
                Assert.That(board.IsOccupied(x, y), Is.EqualTo(expectedValue), $"Unexpected cell ({x}, {y}).");
                if (expectedValue)
                    occupiedCount++;
            }

            Assert.That(board.OccupiedCount, Is.EqualTo(occupiedCount));
        }

        private static bool[,] CollapseReferenceColumns(bool[,] source, int width, int height)
        {
            var result = new bool[width, height];
            for (var x = 0; x < width; x++)
            {
                var targetY = 0;
                for (var sourceY = 0; sourceY < height; sourceY++)
                {
                    if (!source[x, sourceY])
                        continue;
                    result[x, targetY] = true;
                    targetY++;
                }
            }

            return result;
        }

        private static bool[,] ClearReferenceRows(bool[,] source, int width, int height, IReadOnlyCollection<int> rows)
        {
            var clear = new bool[height];
            foreach (var row in rows)
            {
                if (row >= 0 && row < height)
                    clear[row] = true;
            }

            var result = new bool[width, height];
            var targetY = 0;
            for (var sourceY = 0; sourceY < height; sourceY++)
            {
                if (clear[sourceY])
                    continue;
                for (var x = 0; x < width; x++)
                    result[x, targetY] = source[x, sourceY];
                targetY++;
            }

            return result;
        }
    }
}
