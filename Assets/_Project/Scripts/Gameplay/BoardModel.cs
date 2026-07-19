using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockEscape.Tetris
{
    /// <summary>Pure grid rules. It deliberately has no scene or physics dependency.</summary>
    public sealed class BoardModel
    {
        private bool[,] _occupied;

        public int Width { get; }
        public int Height { get; }
        public int OccupiedCount { get; private set; }

        public BoardModel(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Board dimensions must be positive.");

            Width = width;
            Height = height;
            _occupied = new bool[width, height];
        }

        public bool IsOccupied(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height && _occupied[x, y];
        }

        public bool CanPlace(IReadOnlyList<Vector2Int> localCells, Vector2Int origin, bool allowAboveTop = true)
        {
            foreach (var localCell in localCells)
            {
                var cell = origin + localCell;
                if (cell.x < 0 || cell.x >= Width || cell.y < 0)
                    return false;
                if (cell.y >= Height)
                {
                    if (!allowAboveTop)
                        return false;
                    continue;
                }
                if (_occupied[cell.x, cell.y])
                    return false;
            }

            return true;
        }

        public bool TryLock(IReadOnlyList<Vector2Int> localCells, Vector2Int origin, out bool aboveTop)
        {
            aboveTop = false;
            if (!CanPlace(localCells, origin))
                return false;

            foreach (var localCell in localCells)
            {
                var cell = origin + localCell;
                if (cell.y >= Height)
                {
                    aboveTop = true;
                    continue;
                }

                _occupied[cell.x, cell.y] = true;
                OccupiedCount++;
            }

            return true;
        }

        public void SetOccupied(int x, int y, bool occupied = true)
        {
            ValidateCell(x, y);
            if (_occupied[x, y] == occupied)
                return;

            _occupied[x, y] = occupied;
            OccupiedCount += occupied ? 1 : -1;
        }

        public int GetRowFill(int row)
        {
            if (row < 0 || row >= Height)
                return 0;

            var count = 0;
            for (var x = 0; x < Width; x++)
                if (_occupied[x, row]) count++;
            return count;
        }

        public List<int> GetFullRows()
        {
            var rows = new List<int>(4);
            for (var y = 0; y < Height; y++)
                if (GetRowFill(y) == Width) rows.Add(y);
            return rows;
        }

        public void ClearRows(IReadOnlyCollection<int> rows)
        {
            if (rows == null || rows.Count == 0)
                return;

            var clear = new bool[Height];
            foreach (var row in rows)
                if (row >= 0 && row < Height) clear[row] = true;

            var compacted = new bool[Width, Height];
            var targetY = 0;
            for (var sourceY = 0; sourceY < Height; sourceY++)
            {
                if (clear[sourceY])
                    continue;

                for (var x = 0; x < Width; x++)
                    compacted[x, targetY] = _occupied[x, sourceY];
                targetY++;
            }

            _occupied = compacted;
            Recount();
        }

        public void CollapseColumns()
        {
            var compacted = new bool[Width, Height];
            for (var x = 0; x < Width; x++)
            {
                var targetY = 0;
                for (var sourceY = 0; sourceY < Height; sourceY++)
                {
                    if (!_occupied[x, sourceY])
                        continue;

                    compacted[x, targetY] = true;
                    targetY++;
                }
            }

            _occupied = compacted;
        }

        public bool HasOccupiedAtOrAbove(int row)
        {
            for (var y = Mathf.Clamp(row, 0, Height - 1); y < Height; y++)
                if (GetRowFill(y) > 0) return true;
            return false;
        }

        public int HighestOccupiedRow()
        {
            for (var y = Height - 1; y >= 0; y--)
                if (GetRowFill(y) > 0) return y;
            return -1;
        }

        public void Clear()
        {
            _occupied = new bool[Width, Height];
            OccupiedCount = 0;
        }

        private void Recount()
        {
            OccupiedCount = 0;
            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                if (_occupied[x, y]) OccupiedCount++;
        }

        private void ValidateCell(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                throw new ArgumentOutOfRangeException($"Cell ({x}, {y}) is outside {Width}x{Height} board.");
        }
    }
}
