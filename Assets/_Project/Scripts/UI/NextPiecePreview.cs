using BlockEscape.Tetris;
using UnityEngine;
using UnityEngine.UI;

namespace BlockEscape.UI
{
    public sealed class NextPiecePreview : MonoBehaviour
    {
        [SerializeField] private RectTransform _cellRoot = null;
        [SerializeField] private Image[] _cells = null;
        [SerializeField] private Text _kindText = null;
        [SerializeField, Min(8f)] private float _cellSize = 38f;

        public void Show(TetrominoKind kind)
        {
            if (_cellRoot == null || _cells == null || _cells.Length < 4)
                return;

            var shape = TetrominoCatalog.GetCells(kind, 0);
            var size = TetrominoCatalog.GetSize(kind, 0);
            var color = TetrominoCatalog.GetColor(kind);
            var centerOffset = new Vector2((size.x - 1) * 0.5f, (size.y - 1) * 0.5f);

            for (var i = 0; i < _cells.Length; i++)
            {
                var visible = i < shape.Length;
                _cells[i].gameObject.SetActive(visible);
                if (!visible) continue;

                var rect = _cells[i].rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(_cellSize - 3f, _cellSize - 3f);
                rect.anchoredPosition = new Vector2(
                    (shape[i].x - centerOffset.x) * _cellSize,
                    (shape[i].y - centerOffset.y) * _cellSize);
                _cells[i].color = color;
            }

            if (_kindText != null)
                _kindText.text = kind.ToString();
        }
    }
}
