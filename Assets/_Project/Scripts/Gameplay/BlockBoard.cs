using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlockEscape.Tetris
{
    public sealed class BlockBoard : MonoBehaviour
    {
        [SerializeField] private TetrisBalanceConfig _config;
        [SerializeField, Tooltip("Parent that keeps runtime block cells tidy in the Hierarchy.")]
        private Transform _cellRoot;

        private readonly Queue<BlockCellView> _pool = new();
        private readonly HashSet<Vector2Int> _pendingDestroyedCells = new();
        private BoardModel _model;
        private BlockCellView[,] _views;
        private Coroutine _resolveRoutine;
        private float _overflowTime;
        private bool _overflowTriggered;

        public event Action<TetrominoKind> PieceLocked;
        public event Action<int[]> RowsCleared;
        public event Action<bool, float> OverflowChanged;
        public event Action Overflowed;
        public event Action PlayerCrushed;

        public TetrisBalanceConfig Config => _config;
        public BoardModel Model => _model;
        public int Width => _model?.Width ?? (_config != null ? _config.boardWidth : 14);
        public int Height => _model?.Height ?? (_config != null ? _config.boardHeight : 20);
        public bool IsResolving { get; private set; }
        public bool IsOverflowed => _overflowTriggered;
        public float OverflowNormalized => _config == null ? 0f : Mathf.Clamp01(_overflowTime / _config.overflowGraceSeconds);

        public void Initialize(TetrisBalanceConfig config)
        {
            _config = config;
            _config.Sanitize();

            if (_cellRoot == null)
            {
                var root = new GameObject("Locked Cells");
                root.transform.SetParent(transform, false);
                _cellRoot = root.transform;
            }

            _model = new BoardModel(_config.boardWidth, _config.boardHeight);
            _views = new BlockCellView[Width, Height];
            _overflowTime = 0f;
            _overflowTriggered = false;
            IsResolving = false;
        }

        private void Update()
        {
            if (_model == null || _overflowTriggered)
                return;

            var dangerous = _model.HasOccupiedAtOrAbove(_config.dangerStartRow);
            if (dangerous)
                _overflowTime += Time.deltaTime;
            else
                _overflowTime = 0f;

            OverflowChanged?.Invoke(dangerous, OverflowNormalized);
            if (_overflowTime >= _config.overflowGraceSeconds)
                TriggerOverflow();
        }

        public Vector3 WorldForCell(Vector2Int cell)
        {
            return transform.position + new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
        }

        public Vector2Int CellForWorld(Vector2 worldPosition)
        {
            var local = worldPosition - (Vector2)transform.position;
            return new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
        }

        public bool CanPlace(IReadOnlyList<Vector2Int> localCells, Vector2Int origin)
        {
            return _model != null && _model.CanPlace(localCells, origin);
        }

        public bool CommitPiece(TetrominoKind kind, int rotation, Vector2Int origin)
        {
            if (_model == null || IsResolving || _overflowTriggered)
                return false;

            var localCells = TetrominoCatalog.GetCells(kind, rotation);
            if (!_model.TryLock(localCells, origin, out var aboveTop))
                return false;

            var color = TetrominoCatalog.GetColor(kind);
            foreach (var localCell in localCells)
            {
                var cell = origin + localCell;
                if (cell.y >= Height)
                    continue;
                var view = AcquireView();
                view.Activate(cell, WorldForCell(cell), color);
                _views[cell.x, cell.y] = view;
            }

            PieceLocked?.Invoke(kind);
            if (DetectPlayerCrush(localCells, origin))
                PlayerCrushed?.Invoke();

            if (aboveTop)
            {
                TriggerOverflow();
                return true;
            }

            var fullRows = _model.GetFullRows();
            if (fullRows.Count > 0)
                _resolveRoutine = StartCoroutine(ResolveRowsRoutine(fullRows));
            return true;
        }

        private bool DetectPlayerCrush(IReadOnlyList<Vector2Int> localCells, Vector2Int origin)
        {
            var playerMask = LayerMask.GetMask("Player");
            if (playerMask == 0)
                return false;

            Physics2D.SyncTransforms();
            foreach (var localCell in localCells)
            {
                var cell = origin + localCell;
                if (cell.y < 0 || cell.y >= Height)
                    continue;

                var hits = Physics2D.OverlapBoxAll(WorldForCell(cell), new Vector2(0.96f, 0.96f), 0f, playerMask);
                foreach (var hit in hits)
                    if (hit != null &&
                        hit.enabled &&
                        !hit.isTrigger &&
                        PlayerCrushEscape.ShouldCrush(hit, WorldForCell(cell), this, ignoreRisingPlayer: false))
                        return true;
            }

            return false;
        }

        public bool ForceClearRow(int row)
        {
            if (row < 0 || row >= Height || IsResolving || _model.GetRowFill(row) == 0)
                return false;

            _resolveRoutine = StartCoroutine(ResolveRowsRoutine(new List<int> { row }));
            return true;
        }

        public int GetDensestEligibleRow(int minimumRow = 1)
        {
            var bestRow = -1;
            var bestFill = 0;
            for (var y = Mathf.Max(0, minimumRow); y < Height; y++)
            {
                var fill = _model.GetRowFill(y);
                if (fill <= bestFill)
                    continue;
                bestFill = fill;
                bestRow = y;
            }

            return bestRow;
        }

        public int DestroyCellsInRadius(Vector2 worldCenter, int radiusCells, float flashSeconds)
        {
            if (_model == null || _views == null || IsResolving || radiusCells < 0)
                return 0;

            var centerCell = CellForWorld(worldCenter);
            var radiusSqr = radiusCells * radiusCells;
            var cells = new List<Vector2Int>();
            for (var y = centerCell.y - radiusCells; y <= centerCell.y + radiusCells; y++)
            for (var x = centerCell.x - radiusCells; x <= centerCell.x + radiusCells; x++)
            {
                if (x < 0 || x >= Width || y < 0 || y >= Height)
                    continue;

                var offset = new Vector2Int(x - centerCell.x, y - centerCell.y);
                if (offset.sqrMagnitude > radiusSqr)
                    continue;

                var cell = new Vector2Int(x, y);
                if (!_model.IsOccupied(x, y) || _views[x, y] == null || _pendingDestroyedCells.Contains(cell))
                    continue;

                _pendingDestroyedCells.Add(cell);
                cells.Add(cell);
            }

            if (cells.Count == 0)
                return 0;

            StartCoroutine(DestroyCellsRoutine(cells, flashSeconds));
            return cells.Count;
        }

        public void ResetBoard()
        {
            if (_resolveRoutine != null)
                StopCoroutine(_resolveRoutine);
            _resolveRoutine = null;
            _pendingDestroyedCells.Clear();

            if (_views != null)
            {
                for (var y = 0; y < Height; y++)
                for (var x = 0; x < Width; x++)
                {
                    if (_views[x, y] == null) continue;
                    ReleaseView(_views[x, y]);
                    _views[x, y] = null;
                }
            }

            _model?.Clear();
            _overflowTime = 0f;
            _overflowTriggered = false;
            IsResolving = false;
            OverflowChanged?.Invoke(false, 0f);
        }

        private IEnumerator DestroyCellsRoutine(List<Vector2Int> cells, float flashSeconds)
        {
            var duration = Mathf.Max(0f, flashSeconds);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                var flash = Mathf.FloorToInt(elapsed * 12f) % 2 == 0;
                foreach (var cell in cells)
                    if (IsValidCell(cell) && _views[cell.x, cell.y] != null)
                        _views[cell.x, cell.y].SetFlash(flash);

                elapsed += Time.deltaTime;
                yield return null;
            }

            foreach (var cell in cells)
            {
                if (!IsValidCell(cell))
                    continue;

                var view = _views[cell.x, cell.y];
                if (view != null)
                {
                    ReleaseView(view);
                    _views[cell.x, cell.y] = null;
                }

                if (_model.IsOccupied(cell.x, cell.y))
                    _model.SetOccupied(cell.x, cell.y, false);
                _pendingDestroyedCells.Remove(cell);
            }
        }

        private bool IsValidCell(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
        }

        private IEnumerator ResolveRowsRoutine(List<int> rows)
        {
            IsResolving = true;
            rows.Sort();

            var elapsed = 0f;
            while (elapsed < _config.rowClearWarningSeconds)
            {
                var flash = Mathf.FloorToInt(elapsed * 10f) % 2 == 0;
                foreach (var row in rows)
                for (var x = 0; x < Width; x++)
                    if (_views[x, row] != null) _views[x, row].SetFlash(flash);

                elapsed += Time.deltaTime;
                yield return null;
            }

            var clear = new bool[Height];
            foreach (var row in rows)
            {
                if (row < 0 || row >= Height) continue;
                clear[row] = true;
                for (var x = 0; x < Width; x++)
                {
                    if (_views[x, row] == null) continue;
                    ReleaseView(_views[x, row]);
                    _views[x, row] = null;
                }
            }

            var compacted = new BlockCellView[Width, Height];
            var targetY = 0;
            for (var sourceY = 0; sourceY < Height; sourceY++)
            {
                if (clear[sourceY])
                    continue;

                for (var x = 0; x < Width; x++)
                {
                    var view = _views[x, sourceY];
                    if (view == null) continue;
                    compacted[x, targetY] = view;
                    view.SetFlash(false);
                    view.MoveTo(new Vector2Int(x, targetY), WorldForCell(new Vector2Int(x, targetY)), _config.rowCollapseSeconds);
                }
                targetY++;
            }

            _views = compacted;
            _model.ClearRows(rows);
            RowsCleared?.Invoke(rows.ToArray());

            if (_config.rowCollapseSeconds > 0f)
                yield return new WaitForSeconds(_config.rowCollapseSeconds);

            IsResolving = false;
            _resolveRoutine = null;
        }

        private BlockCellView AcquireView()
        {
            if (_pool.Count > 0)
                return _pool.Dequeue();

            var gameObject = new GameObject("Block Cell");
            var view = gameObject.AddComponent<BlockCellView>();
            view.Initialize(_cellRoot);
            return view;
        }

        private void ReleaseView(BlockCellView view)
        {
            view.Deactivate();
            _pool.Enqueue(view);
        }

        private void TriggerOverflow()
        {
            if (_overflowTriggered)
                return;
            _overflowTriggered = true;
            Overflowed?.Invoke();
        }
    }

    internal static class PlayerCrushEscape
    {
        private const float EscapeStep = 1f;
        private const float ProbeSkin = 0.04f;
        private const float MinimumCrushOverlapX = 0.12f;
        private const float CrushContactTolerance = 0.02f;
        private const float RisingVelocityThreshold = 0.05f;

        private static readonly Vector2[] EscapeOffsets =
        {
            Vector2.left * EscapeStep,
            Vector2.right * EscapeStep,
            Vector2.left * EscapeStep * 2f,
            Vector2.right * EscapeStep * 2f
        };

        public static bool TryEvaluateCellOverlap(
            IReadOnlyList<Vector2Int> localCells,
            Vector2Int origin,
            BlockBoard board,
            Vector2 probeSize,
            bool ignoreRisingPlayer,
            out bool shouldCrush)
        {
            return TryEvaluateCellOverlap(
                localCells,
                origin,
                board,
                probeSize,
                ignoreRisingPlayer,
                out shouldCrush,
                out _,
                out _);
        }

        public static bool TryEvaluateCellOverlap(
            IReadOnlyList<Vector2Int> localCells,
            Vector2Int origin,
            BlockBoard board,
            Vector2 probeSize,
            bool ignoreRisingPlayer,
            out bool shouldCrush,
            out bool hasRisingPlayer,
            out bool hasPlayerPinnedFromAbove)
        {
            shouldCrush = false;
            hasRisingPlayer = false;
            hasPlayerPinnedFromAbove = false;
            var playerMask = LayerMask.GetMask("Player");
            if (playerMask == 0 || localCells == null || board == null)
                return false;

            var blockedByPlayer = false;
            Physics2D.SyncTransforms();
            foreach (var localCell in localCells)
            {
                var cell = origin + localCell;
                if (cell.y < 0 || cell.y >= board.Height)
                    continue;

                var center = board.WorldForCell(cell);
                var hits = Physics2D.OverlapBoxAll(center, probeSize, 0f, playerMask);
                foreach (var hit in hits)
                {
                    if (hit == null || !hit.enabled || hit.isTrigger)
                        continue;

                    blockedByPlayer = true;
                    if (IsPlayerMovingUp(hit))
                        hasRisingPlayer = true;
                    else if (IsPinnedFromAbove(hit, center))
                        hasPlayerPinnedFromAbove = true;
                    if (ShouldCrush(hit, center, board, ignoreRisingPlayer, localCells, origin, probeSize))
                        shouldCrush = true;
                }
            }

            return blockedByPlayer;
        }

        public static bool BounceRisingPlayersInCells(
            IReadOnlyList<Vector2Int> localCells,
            Vector2Int origin,
            BlockBoard board,
            Vector2 cellSize,
            float bounceVelocity,
            float skin)
        {
            var playerMask = LayerMask.GetMask("Player");
            if (playerMask == 0 || localCells == null || board == null)
                return false;

            var bounced = false;
            Physics2D.SyncTransforms();
            foreach (var localCell in localCells)
            {
                var cell = origin + localCell;
                if (cell.y < 0 || cell.y >= board.Height)
                    continue;

                var center = board.WorldForCell(cell);
                var hits = Physics2D.OverlapBoxAll(center, cellSize, 0f, playerMask);
                foreach (var hit in hits)
                {
                    if (hit == null || !hit.enabled || hit.isTrigger || !IsPlayerMovingUp(hit))
                        continue;
                    if (!IsPlayerBelowCell(hit.bounds, center, cellSize))
                        continue;

                    BouncePlayerBelowCells(hit, localCells, origin, board, cellSize, bounceVelocity, skin);
                    bounced = true;
                }
            }

            return bounced;
        }

        public static bool DeflectOverlappedPlayersDownInCells(
            IReadOnlyList<Vector2Int> localCells,
            Vector2Int origin,
            BlockBoard board,
            Vector2 cellSize,
            float bounceVelocity,
            float skin)
        {
            var playerMask = LayerMask.GetMask("Player");
            if (playerMask == 0 || localCells == null || board == null)
                return false;

            var deflected = false;
            Physics2D.SyncTransforms();
            foreach (var localCell in localCells)
            {
                var cell = origin + localCell;
                if (cell.y < 0 || cell.y >= board.Height)
                    continue;

                var center = board.WorldForCell(cell);
                var hits = Physics2D.OverlapBoxAll(center, cellSize, 0f, playerMask);
                foreach (var hit in hits)
                {
                    if (hit == null || !hit.enabled || hit.isTrigger)
                        continue;

                    BouncePlayerBelowCells(hit, localCells, origin, board, cellSize, bounceVelocity, skin);
                    deflected = true;
                }
            }

            return deflected;
        }

        public static bool ShouldCrush(
            Collider2D playerCollider,
            Vector2 crushSourceCenter,
            BlockBoard board,
            bool ignoreRisingPlayer,
            IReadOnlyList<Vector2Int> predictedCells = null,
            Vector2Int predictedOrigin = default,
            Vector2 predictedCellSize = default)
        {
            if (playerCollider == null || board == null)
                return false;

            var playerBounds = playerCollider.bounds;
            if (!IsCrushingFromAbove(crushSourceCenter, playerBounds))
                return false;

            if (ignoreRisingPlayer && IsPlayerMovingUp(playerCollider))
                return false;

            return !HasEscapeSpace(playerCollider, board, predictedCells, predictedOrigin, predictedCellSize);
        }

        public static bool IsPinnedFromAbove(
            Collider2D playerCollider,
            Vector2 crushSourceCenter,
            bool ignoreRisingPlayer = true)
        {
            if (playerCollider == null)
                return false;

            if (!IsCrushingFromAbove(crushSourceCenter, playerCollider.bounds))
                return false;

            return !ignoreRisingPlayer || !IsPlayerMovingUp(playerCollider);
        }

        private static bool IsCrushingFromAbove(Vector2 blockCenter, Bounds playerBounds)
        {
            var halfCell = 0.46f;
            var blockMinX = blockCenter.x - halfCell;
            var blockMaxX = blockCenter.x + halfCell;
            var horizontalOverlap = Mathf.Min(blockMaxX, playerBounds.max.x) - Mathf.Max(blockMinX, playerBounds.min.x);
            if (horizontalOverlap < MinimumCrushOverlapX)
                return false;

            var blockBottom = blockCenter.y - halfCell;
            if (playerBounds.max.y < blockBottom - CrushContactTolerance)
                return false;

            return blockCenter.y > playerBounds.center.y;
        }

        private static bool IsPlayerMovingUp(Collider2D playerCollider)
        {
            var body = playerCollider.attachedRigidbody;
            if (body != null && body.linearVelocity.y > RisingVelocityThreshold)
                return true;

            var player = playerCollider.GetComponentInParent<BlockEscape.Player.PlayerController>();
            return player != null && player.HasRecentJumpForBlockBounce;
        }

        private static bool IsPlayerBelowCell(Bounds playerBounds, Vector2 cellCenter, Vector2 cellSize)
        {
            return playerBounds.max.y <= cellCenter.y + cellSize.y * 0.08f;
        }

        private static void BouncePlayerBelowCells(
            Collider2D playerCollider,
            IReadOnlyList<Vector2Int> localCells,
            Vector2Int origin,
            BlockBoard board,
            Vector2 cellSize,
            float bounceVelocity,
            float skin)
        {
            var body = playerCollider.attachedRigidbody;
            if (body == null)
                return;

            var playerBounds = playerCollider.bounds;
            var targetTop = float.PositiveInfinity;
            foreach (var localCell in localCells)
            {
                var cell = origin + localCell;
                if (cell.y < 0 || cell.y >= board.Height)
                    continue;

                var cellCenter = (Vector2)board.WorldForCell(cell);
                var horizontalOverlap = Mathf.Min(cellCenter.x + cellSize.x * 0.5f, playerBounds.max.x) -
                                        Mathf.Max(cellCenter.x - cellSize.x * 0.5f, playerBounds.min.x);
                if (horizontalOverlap <= 0f)
                    continue;

                targetTop = Mathf.Min(targetTop, cellCenter.y - cellSize.y * 0.5f - skin);
            }

            var velocity = body.linearVelocity;
            velocity.y = Mathf.Min(velocity.y, bounceVelocity);
            body.linearVelocity = velocity;

            if (float.IsPositiveInfinity(targetTop))
                return;

            var targetCenterY = targetTop - playerBounds.extents.y;
            var boardBottom = board.transform.position.y;
            targetCenterY = Mathf.Max(targetCenterY, boardBottom + playerBounds.extents.y + skin);
            if (targetCenterY >= playerBounds.center.y)
                return;

            var deltaY = targetCenterY - playerBounds.center.y;
            body.position += Vector2.up * deltaY;
        }

        private static bool HasEscapeSpace(
            Collider2D playerCollider,
            BlockBoard board,
            IReadOnlyList<Vector2Int> predictedCells,
            Vector2Int predictedOrigin,
            Vector2 predictedCellSize)
        {
            var bounds = playerCollider.bounds;
            var probeSize = new Vector2(
                Mathf.Max(0.05f, bounds.size.x - ProbeSkin),
                Mathf.Max(0.05f, bounds.size.y - ProbeSkin));
            var blockingMask = LayerMask.GetMask("World", "FallingBlock");

            foreach (var offset in EscapeOffsets)
            {
                var center = (Vector2)bounds.center + offset;
                if (!IsInsideBoard(center, probeSize, board))
                    continue;
                if (OverlapsPredictedCells(center, probeSize, predictedCells, predictedOrigin, predictedCellSize, board))
                    continue;
                if (!OverlapsBlockingCollider(center, probeSize, blockingMask, playerCollider))
                    return true;
            }

            return false;
        }

        private static bool OverlapsPredictedCells(
            Vector2 center,
            Vector2 size,
            IReadOnlyList<Vector2Int> predictedCells,
            Vector2Int predictedOrigin,
            Vector2 predictedCellSize,
            BlockBoard board)
        {
            if (predictedCells == null || predictedCells.Count == 0)
                return false;

            if (predictedCellSize == default)
                predictedCellSize = new Vector2(0.96f, 0.96f);

            foreach (var localCell in predictedCells)
            {
                var cell = predictedOrigin + localCell;
                if (cell.y < 0 || cell.y >= board.Height)
                    continue;

                var cellCenter = (Vector2)board.WorldForCell(cell);
                if (BoxesOverlap(center, size, cellCenter, predictedCellSize))
                    return true;
            }

            return false;
        }

        private static bool BoxesOverlap(Vector2 aCenter, Vector2 aSize, Vector2 bCenter, Vector2 bSize)
        {
            return Mathf.Abs(aCenter.x - bCenter.x) * 2f < aSize.x + bSize.x &&
                   Mathf.Abs(aCenter.y - bCenter.y) * 2f < aSize.y + bSize.y;
        }

        private static bool IsInsideBoard(Vector2 center, Vector2 size, BlockBoard board)
        {
            var half = size * 0.5f;
            var boardOrigin = (Vector2)board.transform.position;
            return center.x - half.x >= boardOrigin.x &&
                   center.x + half.x <= boardOrigin.x + board.Width &&
                   center.y - half.y >= boardOrigin.y &&
                   center.y + half.y <= boardOrigin.y + board.Height;
        }

        private static bool OverlapsBlockingCollider(
            Vector2 center,
            Vector2 size,
            int blockingMask,
            Collider2D playerCollider)
        {
            if (blockingMask == 0)
                return false;

            var hits = Physics2D.OverlapBoxAll(center, size, 0f, blockingMask);
            foreach (var hit in hits)
            {
                if (hit == null || !hit.enabled || hit.isTrigger || hit == playerCollider)
                    continue;
                if (hit.transform.IsChildOf(playerCollider.transform))
                    continue;
                return true;
            }

            return false;
        }
    }
}
