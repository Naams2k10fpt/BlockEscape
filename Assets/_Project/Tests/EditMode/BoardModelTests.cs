using System.Collections.Generic;
using System.Reflection;
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

        [Test]
        public void BlockBoard_RaisesPlayerCrushedWhenLockedCellOverlapsPlayer()
        {
            var boardObject = new GameObject("Board");
            var player = new GameObject("Player Probe");
            try
            {
                boardObject.transform.position = Vector3.zero;
                var config = ScriptableObject.CreateInstance<TetrisBalanceConfig>();
                config.boardWidth = 2;
                config.boardHeight = 8;
                config.rowClearWarningSeconds = 0f;
                config.rowCollapseSeconds = 0f;

                var board = boardObject.AddComponent<BlockBoard>();
                board.Initialize(config);

                player.layer = LayerMask.NameToLayer("Player");
                player.transform.position = board.WorldForCell(new Vector2Int(1, 0)) + Vector3.up * 0.5f;
                var collider = player.AddComponent<CapsuleCollider2D>();
                collider.size = new Vector2(0.72f, 1.45f);
                Physics2D.SyncTransforms();

                var crushed = false;
                board.PlayerCrushed += () => crushed = true;

                Assert.That(board.CommitPiece(TetrominoKind.O, 0, Vector2Int.zero), Is.True);
                Assert.That(crushed, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(boardObject);
            }
        }

        [Test]
        public void BlockBoard_DoesNotRaisePlayerCrushedWhenPlayerHasSideEscape()
        {
            var boardObject = new GameObject("Board");
            var player = new GameObject("Player Probe");
            try
            {
                boardObject.transform.position = Vector3.zero;
                var config = ScriptableObject.CreateInstance<TetrisBalanceConfig>();
                config.boardWidth = 4;
                config.boardHeight = 8;
                config.rowClearWarningSeconds = 0f;
                config.rowCollapseSeconds = 0f;

                var board = boardObject.AddComponent<BlockBoard>();
                board.Initialize(config);

                player.layer = LayerMask.NameToLayer("Player");
                player.transform.position = board.WorldForCell(new Vector2Int(1, 0)) + Vector3.up * 0.5f;
                var collider = player.AddComponent<CapsuleCollider2D>();
                collider.size = new Vector2(0.72f, 1.45f);
                Physics2D.SyncTransforms();

                var crushed = false;
                board.PlayerCrushed += () => crushed = true;

                Assert.That(board.CommitPiece(TetrominoKind.O, 0, Vector2Int.zero), Is.True);
                Assert.That(crushed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(boardObject);
            }
        }

        [Test]
        public void ActiveTetromino_FallingCellsUseSolidColliders()
        {
            var boardObject = new GameObject("Board");
            var pieceObject = new GameObject("Active Test Piece");
            try
            {
                var config = ScriptableObject.CreateInstance<TetrisBalanceConfig>();
                config.boardWidth = 4;
                config.boardHeight = 8;
                config.fallSpeedCellsPerSecond = 1f;

                var board = boardObject.AddComponent<BlockBoard>();
                board.Initialize(config);

                var piece = pieceObject.AddComponent<ActiveTetromino>();
                piece.Initialize(board, null, null, TetrominoKind.O, 0, new Vector2Int(1, 4), 1f, 0f, 0f);

                var colliders = pieceObject.GetComponentsInChildren<BoxCollider2D>();
                Assert.That(colliders.Length, Is.EqualTo(4));
                foreach (var collider in colliders)
                {
                    Assert.That(collider.isTrigger, Is.False);
                    Assert.That(collider.sharedMaterial, Is.Not.Null);
                    Assert.That(collider.sharedMaterial.friction, Is.EqualTo(0f));
                }
            }
            finally
            {
                Object.DestroyImmediate(pieceObject);
                Object.DestroyImmediate(boardObject);
            }
        }

        [Test]
        public void ActiveTetromino_RaisesPlayerCrushedWhenFallingBlockPinsPlayerWithoutSideEscape()
        {
            var boardObject = new GameObject("Board");
            var pieceObject = new GameObject("Active Crush Test Piece");
            var player = new GameObject("Player Crush Probe");
            var leftBlocker = new GameObject("Left Escape Blocker");
            try
            {
                var config = ScriptableObject.CreateInstance<TetrisBalanceConfig>();
                config.boardWidth = 3;
                config.boardHeight = 10;
                config.fallSpeedCellsPerSecond = 1f;

                var board = boardObject.AddComponent<BlockBoard>();
                board.Initialize(config);

                CreateBlockingBox(leftBlocker, new Vector2(0.5f, 1.75f), new Vector2(0.96f, 1.6f));

                player.layer = LayerMask.NameToLayer("Player");
                player.transform.position = new Vector3(1.5f, 1.75f, 0f);
                var playerCollider = player.AddComponent<CapsuleCollider2D>();
                playerCollider.size = new Vector2(0.72f, 1.45f);

                var piece = pieceObject.AddComponent<ActiveTetromino>();
                piece.Initialize(board, null, null, TetrominoKind.O, 0, new Vector2Int(1, 2), 1f, 0f, 0f);

                var crushed = false;
                piece.PlayerCrushed += () => crushed = true;

                Assert.That(piece.CheckPlayerCrush(), Is.True);
                Assert.That(crushed, Is.True);
                Assert.That(piece.CheckPlayerCrush(), Is.False, "A single falling piece should only raise crush once.");
            }
            finally
            {
                Object.DestroyImmediate(leftBlocker);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(pieceObject);
                Object.DestroyImmediate(boardObject);
            }
        }

        [Test]
        public void ActiveTetromino_DoesNotCrushWhenPlayerHasSideEscape()
        {
            var boardObject = new GameObject("Board");
            var pieceObject = new GameObject("Active Escape Test Piece");
            var player = new GameObject("Player Escape Probe");
            try
            {
                var config = ScriptableObject.CreateInstance<TetrisBalanceConfig>();
                config.boardWidth = 4;
                config.boardHeight = 10;
                config.fallSpeedCellsPerSecond = 1f;

                var board = boardObject.AddComponent<BlockBoard>();
                board.Initialize(config);

                player.layer = LayerMask.NameToLayer("Player");
                player.transform.position = new Vector3(1.5f, 1.75f, 0f);
                var playerCollider = player.AddComponent<CapsuleCollider2D>();
                playerCollider.size = new Vector2(0.72f, 1.45f);

                var piece = pieceObject.AddComponent<ActiveTetromino>();
                piece.Initialize(board, null, null, TetrominoKind.O, 0, new Vector2Int(1, 2), 1f, 0f, 0f);

                var crushed = false;
                piece.PlayerCrushed += () => crushed = true;

                Assert.That(piece.CheckPlayerCrush(), Is.False);
                Assert.That(crushed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(pieceObject);
                Object.DestroyImmediate(boardObject);
            }
        }

        [Test]
        public void ActiveTetromino_DownStepStopsBeforeOverlappingPlayerWithSideEscape()
        {
            var boardObject = new GameObject("Board");
            var pieceObject = new GameObject("Active Step Stop Test Piece");
            var player = new GameObject("Player Step Probe");
            try
            {
                var config = ScriptableObject.CreateInstance<TetrisBalanceConfig>();
                config.boardWidth = 4;
                config.boardHeight = 10;
                config.fallSpeedCellsPerSecond = 1f;

                var board = boardObject.AddComponent<BlockBoard>();
                board.Initialize(config);

                player.layer = LayerMask.NameToLayer("Player");
                player.transform.position = new Vector3(1.5f, 1.75f, 0f);
                var playerCollider = player.AddComponent<CapsuleCollider2D>();
                playerCollider.size = new Vector2(0.72f, 1.45f);

                var piece = pieceObject.AddComponent<ActiveTetromino>();
                var startOrigin = new Vector2Int(1, 3);
                piece.Initialize(board, null, null, TetrominoKind.O, 0, startOrigin, 1f, 0f, 0f);

                var crushed = false;
                piece.PlayerCrushed += () => crushed = true;

                Assert.That(InvokeTryMove(piece, Vector2Int.down), Is.False);
                Assert.That(piece.GridOrigin, Is.EqualTo(startOrigin));
                Assert.That(crushed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(pieceObject);
                Object.DestroyImmediate(boardObject);
            }
        }

        [Test]
        public void ActiveTetromino_DownStepCrushesWhenPlayerHasNoSideEscape()
        {
            var boardObject = new GameObject("Board");
            var pieceObject = new GameObject("Active Step Crush Test Piece");
            var player = new GameObject("Player Step Crush Probe");
            var leftBlocker = new GameObject("Left Escape Blocker");
            try
            {
                var config = ScriptableObject.CreateInstance<TetrisBalanceConfig>();
                config.boardWidth = 3;
                config.boardHeight = 10;
                config.fallSpeedCellsPerSecond = 1f;

                var board = boardObject.AddComponent<BlockBoard>();
                board.Initialize(config);

                CreateBlockingBox(leftBlocker, new Vector2(0.5f, 1.75f), new Vector2(0.96f, 1.6f));

                player.layer = LayerMask.NameToLayer("Player");
                player.transform.position = new Vector3(1.5f, 1.75f, 0f);
                var playerCollider = player.AddComponent<CapsuleCollider2D>();
                playerCollider.size = new Vector2(0.72f, 1.45f);

                var piece = pieceObject.AddComponent<ActiveTetromino>();
                var startOrigin = new Vector2Int(1, 3);
                piece.Initialize(board, null, null, TetrominoKind.O, 0, startOrigin, 1f, 0f, 0f);

                var crushed = false;
                piece.PlayerCrushed += () => crushed = true;

                Assert.That(InvokeTryMove(piece, Vector2Int.down), Is.False);
                Assert.That(piece.GridOrigin, Is.EqualTo(startOrigin));
                Assert.That(crushed, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(leftBlocker);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(pieceObject);
                Object.DestroyImmediate(boardObject);
            }
        }

        [Test]
        public void ActiveTetromino_DoesNotCrushWhenPlayerJumpsIntoBlock()
        {
            var boardObject = new GameObject("Board");
            var pieceObject = new GameObject("Active Jump Test Piece");
            var player = new GameObject("Player Jump Probe");
            var leftBlocker = new GameObject("Left Escape Blocker");
            try
            {
                var config = ScriptableObject.CreateInstance<TetrisBalanceConfig>();
                config.boardWidth = 3;
                config.boardHeight = 10;
                config.fallSpeedCellsPerSecond = 1f;

                var board = boardObject.AddComponent<BlockBoard>();
                board.Initialize(config);

                CreateBlockingBox(leftBlocker, new Vector2(0.5f, 1.75f), new Vector2(0.96f, 1.6f));

                player.layer = LayerMask.NameToLayer("Player");
                player.transform.position = new Vector3(1.5f, 1.75f, 0f);
                var body = player.AddComponent<Rigidbody2D>();
                body.linearVelocity = new Vector2(0f, 6f);
                var playerCollider = player.AddComponent<CapsuleCollider2D>();
                playerCollider.size = new Vector2(0.72f, 1.45f);

                var piece = pieceObject.AddComponent<ActiveTetromino>();
                piece.Initialize(board, null, null, TetrominoKind.O, 0, new Vector2Int(1, 2), 1f, 0f, 0f);

                var crushed = false;
                piece.PlayerCrushed += () => crushed = true;

                Assert.That(piece.CheckPlayerCrush(), Is.False);
                Assert.That(crushed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(leftBlocker);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(pieceObject);
                Object.DestroyImmediate(boardObject);
            }
        }

        private static void CreateBlockingBox(GameObject gameObject, Vector2 center, Vector2 size)
        {
            var worldLayer = LayerMask.NameToLayer("World");
            if (worldLayer >= 0)
                gameObject.layer = worldLayer;
            gameObject.transform.position = center;
            var collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        private static bool InvokeTryMove(ActiveTetromino piece, Vector2Int offset)
        {
            var method = typeof(ActiveTetromino).GetMethod("TryMove", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(piece, new object[] { offset });
        }

    }
}
