using System.Collections;
using UnityEngine;

namespace BlockEscape.Tetris
{
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public sealed class BlockCellView : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private BoxCollider2D _collider;
        private Coroutine _moveRoutine;
        private Color _baseColor;

        public Vector2Int GridPosition { get; private set; }

        public void Initialize(Transform parent)
        {
            transform.SetParent(parent, false);
            _renderer = gameObject.GetComponent<SpriteRenderer>();
            if (_renderer == null)
                _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sprite = RuntimeVisuals.Square;
            _renderer.sortingOrder = 10;

            _collider = gameObject.GetComponent<BoxCollider2D>();
            if (_collider == null)
                _collider = gameObject.AddComponent<BoxCollider2D>();
            _collider.size = new Vector2(0.96f, 0.96f);

            var worldLayer = LayerMask.NameToLayer("World");
            if (worldLayer >= 0)
                gameObject.layer = worldLayer;
        }

        public void Activate(Vector2Int gridPosition, Vector3 worldPosition, Color color)
        {
            if (_renderer == null)
                Initialize(transform.parent);

            gameObject.SetActive(true);
            GridPosition = gridPosition;
            transform.position = worldPosition;
            transform.localScale = new Vector3(0.92f, 0.92f, 1f);
            _baseColor = color;
            _renderer.color = color;
            _collider.enabled = true;
        }

        public void SetFlash(bool highlighted)
        {
            if (_renderer != null)
                _renderer.color = highlighted ? Color.white : _baseColor;
        }

        public void MoveTo(Vector2Int gridPosition, Vector3 worldPosition, float duration)
        {
            GridPosition = gridPosition;
            if (_moveRoutine != null)
                StopCoroutine(_moveRoutine);

            if (duration <= 0f)
            {
                transform.position = worldPosition;
                return;
            }

            _moveRoutine = StartCoroutine(MoveRoutine(worldPosition, duration));
        }

        public void Deactivate()
        {
            if (_moveRoutine != null)
                StopCoroutine(_moveRoutine);
            _moveRoutine = null;
            _collider.enabled = false;
            gameObject.SetActive(false);
        }

        private IEnumerator MoveRoutine(Vector3 target, float duration)
        {
            var start = transform.position;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                transform.position = Vector3.Lerp(start, target, 1f - Mathf.Pow(1f - t, 3f));
                yield return null;
            }

            transform.position = target;
            _moveRoutine = null;
        }
    }
}
