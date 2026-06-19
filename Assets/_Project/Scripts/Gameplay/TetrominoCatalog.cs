using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockEscape.Tetris
{
    public enum TetrominoKind
    {
        I,
        J,
        L,
        O,
        S,
        T,
        Z
    }

    public static class TetrominoCatalog
    {
        private static readonly Dictionary<TetrominoKind, Vector2Int[]> BaseCells = new()
        {
            { TetrominoKind.I, new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0) } },
            { TetrominoKind.J, new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(2, 0) } },
            { TetrominoKind.L, new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(2, 1) } },
            { TetrominoKind.O, new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) } },
            { TetrominoKind.S, new[] { new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) } },
            { TetrominoKind.T, new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1) } },
            { TetrominoKind.Z, new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 1) } }
        };

        private static readonly Color[] Colors =
        {
            new(0.20f, 0.88f, 0.95f),
            new(0.25f, 0.45f, 0.95f),
            new(1.00f, 0.58f, 0.18f),
            new(1.00f, 0.88f, 0.20f),
            new(0.35f, 0.90f, 0.38f),
            new(0.72f, 0.32f, 0.92f),
            new(0.95f, 0.25f, 0.32f)
        };

        public static Vector2Int[] GetCells(TetrominoKind kind, int rotation)
        {
            var source = BaseCells[kind];
            var result = new Vector2Int[source.Length];
            var turns = kind == TetrominoKind.O ? 0 : Mod(rotation, 4);

            for (var i = 0; i < source.Length; i++)
            {
                var cell = source[i];
                for (var turn = 0; turn < turns; turn++)
                    cell = new Vector2Int(-cell.y, cell.x);

                result[i] = cell;
            }

            Normalize(result);
            return result;
        }

        public static Vector2Int GetSize(TetrominoKind kind, int rotation)
        {
            var cells = GetCells(kind, rotation);
            var maxX = 0;
            var maxY = 0;
            foreach (var cell in cells)
            {
                maxX = Mathf.Max(maxX, cell.x);
                maxY = Mathf.Max(maxY, cell.y);
            }

            return new Vector2Int(maxX + 1, maxY + 1);
        }

        public static Color GetColor(TetrominoKind kind) => Colors[(int)kind];

        private static void Normalize(Vector2Int[] cells)
        {
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            foreach (var cell in cells)
            {
                minX = Mathf.Min(minX, cell.x);
                minY = Mathf.Min(minY, cell.y);
            }

            for (var i = 0; i < cells.Length; i++)
                cells[i] -= new Vector2Int(minX, minY);
        }

        private static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;
    }

    public sealed class SevenBag
    {
        private readonly List<TetrominoKind> _bag = new(7);
        private System.Random _random;

        public SevenBag(int seed)
        {
            Reset(seed);
        }

        public void Reset(int seed)
        {
            _random = new System.Random(seed);
            _bag.Clear();
        }

        public TetrominoKind Next()
        {
            if (_bag.Count == 0)
                Refill();

            var result = _bag[^1];
            _bag.RemoveAt(_bag.Count - 1);
            return result;
        }

        private void Refill()
        {
            _bag.Clear();
            foreach (TetrominoKind kind in Enum.GetValues(typeof(TetrominoKind)))
                _bag.Add(kind);

            for (var i = _bag.Count - 1; i > 0; i--)
            {
                var other = _random.Next(i + 1);
                (_bag[i], _bag[other]) = (_bag[other], _bag[i]);
            }
        }
    }
}
