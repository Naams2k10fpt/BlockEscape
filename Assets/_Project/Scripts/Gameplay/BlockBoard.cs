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
                    if (hit != null && hit.enabled && !hit.isTrigger)
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

        public void ResetBoard()
        {
            if (_resolveRoutine != null)
                StopCoroutine(_resolveRoutine);
            _resolveRoutine = null;

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
}
