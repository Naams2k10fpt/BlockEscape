using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BlockEscape.Tetris.Tests
{
    public sealed class BoardModelTests
    {
        [Test]
        public void CanPlace_RejectsWallsFloorAndOccupiedCells()
        {
            var board = new BoardModel(10, 20);
            var square = TetrominoCatalog.GetCells(TetrominoKind.O, 0);

            Assert.That(board.CanPlace(square, new Vector2Int(0, 0)), Is.True);
            Assert.That(board.CanPlace(square, new Vector2Int(-1, 0)), Is.False);
            Assert.That(board.CanPlace(square, new Vector2Int(9, 0)), Is.False);
            Assert.That(board.CanPlace(square, new Vector2Int(0, -1)), Is.False);

            board.SetOccupied(1, 0);
            Assert.That(board.CanPlace(square, new Vector2Int(0, 0)), Is.False);
        }

        [Test]
        public void CanPlace_AllowsCellsAboveTopUntilTheyLock()
        {
            var board = new BoardModel(10, 20);
            var line = TetrominoCatalog.GetCells(TetrominoKind.I, 1);

            Assert.That(board.CanPlace(line, new Vector2Int(4, 20)), Is.True);
            Assert.That(board.TryLock(line, new Vector2Int(4, 20), out var aboveTop), Is.True);
            Assert.That(aboveTop, Is.True);
            Assert.That(board.OccupiedCount, Is.EqualTo(0));
        }

        [Test]
        public void GetFullRows_FindsEveryCompletedRow()
        {
            var board = new BoardModel(4, 8);
            for (var x = 0; x < board.Width; x++)
            {
                board.SetOccupied(x, 0);
                board.SetOccupied(x, 3);
            }

            CollectionAssert.AreEqual(new[] { 0, 3 }, board.GetFullRows());
        }

        [Test]
        public void ClearRows_CompactsAllRowsAboveThem()
        {
            var board = new BoardModel(4, 8);
            for (var x = 0; x < board.Width; x++)
            {
                board.SetOccupied(x, 0);
                board.SetOccupied(x, 2);
            }
            board.SetOccupied(1, 1);
            board.SetOccupied(2, 3);

            board.ClearRows(new List<int> { 0, 2 });

            Assert.That(board.IsOccupied(1, 0), Is.True, "Old row 1 must move to row 0.");
            Assert.That(board.IsOccupied(2, 1), Is.True, "Old row 3 must move to row 1.");
            Assert.That(board.OccupiedCount, Is.EqualTo(2));
        }

        [Test]
        public void SevenBag_ContainsAllSevenKindsBeforeRepeatingBag()
        {
            var bag = new SevenBag(12345);
            var first = new HashSet<TetrominoKind>();
            var second = new HashSet<TetrominoKind>();

            for (var i = 0; i < 7; i++) first.Add(bag.Next());
            for (var i = 0; i < 7; i++) second.Add(bag.Next());

            Assert.That(first.Count, Is.EqualTo(7));
            Assert.That(second.Count, Is.EqualTo(7));
        }

        [TestCase(TetrominoKind.I)]
        [TestCase(TetrominoKind.J)]
        [TestCase(TetrominoKind.L)]
        [TestCase(TetrominoKind.O)]
        [TestCase(TetrominoKind.S)]
        [TestCase(TetrominoKind.T)]
        [TestCase(TetrominoKind.Z)]
        public void EveryRotation_HasFourNormalizedUniqueCells(TetrominoKind kind)
        {
            for (var rotation = 0; rotation < 4; rotation++)
            {
                var cells = TetrominoCatalog.GetCells(kind, rotation);
                var unique = new HashSet<Vector2Int>(cells);
                Assert.That(cells.Length, Is.EqualTo(4));
                Assert.That(unique.Count, Is.EqualTo(4));
                Assert.That(TetrominoCatalog.GetSize(kind, rotation).x, Is.GreaterThan(0));
                foreach (var cell in cells)
                {
                    Assert.That(cell.x, Is.GreaterThanOrEqualTo(0));
                    Assert.That(cell.y, Is.GreaterThanOrEqualTo(0));
                }
            }
        }

        [Test]
        public void DangerQuery_OnlyTriggersAtConfiguredRowOrHigher()
        {
            var board = new BoardModel(14, 20);
            board.SetOccupied(4, 17);
            Assert.That(board.HasOccupiedAtOrAbove(18), Is.False);

            board.SetOccupied(7, 18);
            Assert.That(board.HasOccupiedAtOrAbove(18), Is.True);
            Assert.That(board.HighestOccupiedRow(), Is.EqualTo(18));
        }

    }
}
