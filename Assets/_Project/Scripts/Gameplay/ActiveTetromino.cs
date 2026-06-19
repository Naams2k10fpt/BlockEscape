using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlockEscape.Tetris
{
    public sealed class ActiveTetromino : MonoBehaviour
    {
        private BlockBoard _board;
        private TetrominoSpawner _owner;
        private TetrominoKind _kind;
        private int _rotation;
        private Vector2Int[] _localCells;
        private Vector2Int _origin;
        private float _fallSpeed;
        private float _lockDelay;
        private float _lockTimer;
        private float _fallStepTimer;
        private bool _telegraphing;
        private bool _finished;
        private Rigidbody2D _rigidbody;
        private SpriteRenderer[] _renderers;
        private GameObject _warningBar;
        private int _heldDirection;
        private float _horizontalHoldTime;
        private float _horizontalRepeatTime;
        private float _softDropRepeatTime;

        private const float HorizontalRepeatDelay = 0.18f;
        private const float HorizontalRepeatInterval = 0.07f;
        private const float SoftDropRepeatInterval = 0.06f;

        public TetrominoKind Kind => _kind;
        public int Rotation => _rotation;
        public Vector2Int GridOrigin => _origin;

        public void Initialize(
            BlockBoard board,
            TetrominoSpawner owner,
            TetrominoKind kind,
            int rotation,
            Vector2Int origin,
            float fallSpeed,
            float telegraphSeconds,
            float lockDelay)
        {
            _board = board;
            _owner = owner;
            _kind = kind;
            _rotation = rotation;
            _origin = origin;
            _fallSpeed = Mathf.Max(0.1f, fallSpeed);
            _lockDelay = Mathf.Max(0f, lockDelay);
            _localCells = TetrominoCatalog.GetCells(kind, rotation);
            var spawnWorldPosition = _board.WorldForCell(_origin);
            transform.position = spawnWorldPosition;

            gameObject.name = $"Active {kind}";
            var fallingLayer = LayerMask.NameToLayer("FallingBlock");
            if (fallingLayer >= 0)
                gameObject.layer = fallingLayer;

            _rigidbody = gameObject.AddComponent<Rigidbody2D>();
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody.gravityScale = 0f;
            _rigidbody.freezeRotation = true;
            _rigidbody.interpolation = RigidbodyInterpolation2D.None;
            _rigidbody.position = spawnWorldPosition;

            BuildCells();
            if (telegraphSeconds > 0f)
            {
                BuildWarningBar();
                StartCoroutine(TelegraphRoutine(telegraphSeconds));
            }
        }

        private void Update()
        {
            if (_telegraphing || _finished || _board == null || _board.IsOverflowed || Time.timeScale <= 0f)
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            HandleHorizontalInput(keyboard);

            if (keyboard.wKey.wasPressedThisFrame)
                TryRotateClockwise();

            if (keyboard.sKey.wasPressedThisFrame)
            {
                TryMove(Vector2Int.down);
                _softDropRepeatTime = 0f;
            }
            else if (keyboard.sKey.isPressed)
            {
                _softDropRepeatTime += Time.deltaTime;
                while (_softDropRepeatTime >= SoftDropRepeatInterval)
                {
                    _softDropRepeatTime -= SoftDropRepeatInterval;
                    if (!TryMove(Vector2Int.down)) break;
                }
            }
            else
            {
                _softDropRepeatTime = 0f;
            }
        }

        private void FixedUpdate()
        {
            if (_telegraphing || _finished || _board == null || _board.IsOverflowed)
                return;

            var nextOrigin = _origin + Vector2Int.down;
            if (_board.CanPlace(_localCells, nextOrigin))
            {
                _lockTimer = 0f;
                _fallStepTimer += Time.fixedDeltaTime;
                var stepInterval = 1f / _fallSpeed;
                if (_fallStepTimer < stepInterval)
                    return;

                _fallStepTimer -= stepInterval;
                _origin = nextOrigin;
                _rigidbody.position = _board.WorldForCell(_origin);
                return;
            }

            _fallStepTimer = 0f;
            var restingPosition = (Vector2)_board.WorldForCell(_origin);
            _rigidbody.position = restingPosition;
            _lockTimer += Time.fixedDeltaTime;
            if (_lockTimer >= _lockDelay)
                LockIntoBoard();
        }

        private IEnumerator TelegraphRoutine(float duration)
        {
            _telegraphing = true;
            var baseColor = TetrominoCatalog.GetColor(_kind);
            var elapsed = 0f;
            while (elapsed < duration && !_finished)
            {
                var pulse = 0.25f + Mathf.PingPong(elapsed * 2.5f, 0.45f);
                SetVisualColor(new Color(baseColor.r, baseColor.g, baseColor.b, pulse));
                if (_warningBar != null)
                {
                    var renderer = _warningBar.GetComponent<SpriteRenderer>();
                    renderer.color = new Color(1f, 0.25f, 0.25f, 0.35f + Mathf.PingPong(elapsed * 3f, 0.45f));
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            SetVisualColor(baseColor);
            if (_warningBar != null)
                Destroy(_warningBar);
            _warningBar = null;
            _telegraphing = false;
        }

        private void BuildCells()
        {
            _renderers = new SpriteRenderer[_localCells.Length];
            var color = TetrominoCatalog.GetColor(_kind);
            for (var i = 0; i < _localCells.Length; i++)
            {
                var cell = new GameObject($"Cell {i}");
                cell.transform.SetParent(transform, false);
                cell.transform.localPosition = new Vector3(_localCells[i].x, _localCells[i].y, 0f);
                cell.transform.localScale = new Vector3(0.92f, 0.92f, 1f);
                cell.layer = gameObject.layer;

                var renderer = cell.AddComponent<SpriteRenderer>();
                renderer.sprite = RuntimeVisuals.Square;
                renderer.color = color;
                renderer.sortingOrder = 20;
                _renderers[i] = renderer;

                var collider = cell.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.94f, 0.94f);
                collider.isTrigger = true;
            }
        }

        private void BuildWarningBar()
        {
            var size = TetrominoCatalog.GetSize(_kind, _rotation);
            var x = _board.transform.position.x + _origin.x + size.x * 0.5f;
            var y = _board.transform.position.y + _board.Height + 0.15f;
            _warningBar = RuntimeVisuals.CreateQuad(
                $"Spawn Warning {_kind}",
                _board.transform,
                new Vector3(x, y, 0f),
                new Vector2(size.x - 0.1f, 0.16f),
                new Color(1f, 0.25f, 0.25f, 0.7f),
                30);
        }

        private void SetVisualColor(Color color)
        {
            foreach (var renderer in _renderers)
                renderer.color = color;
        }

        private void HandleHorizontalInput(Keyboard keyboard)
        {
            var direction = keyboard.aKey.isPressed == keyboard.dKey.isPressed
                ? 0
                : keyboard.aKey.isPressed ? -1 : 1;

            if (direction == 0)
            {
                _heldDirection = 0;
                _horizontalHoldTime = 0f;
                _horizontalRepeatTime = 0f;
                return;
            }

            if (_heldDirection != direction)
            {
                _heldDirection = direction;
                _horizontalHoldTime = 0f;
                _horizontalRepeatTime = 0f;
                TryMove(new Vector2Int(direction, 0));
                return;
            }

            _horizontalHoldTime += Time.deltaTime;
            if (_horizontalHoldTime < HorizontalRepeatDelay)
                return;

            _horizontalRepeatTime += Time.deltaTime;
            while (_horizontalRepeatTime >= HorizontalRepeatInterval)
            {
                _horizontalRepeatTime -= HorizontalRepeatInterval;
                if (!TryMove(new Vector2Int(direction, 0))) break;
            }
        }

        private bool TryMove(Vector2Int offset)
        {
            var candidate = _origin + offset;
            if (!_board.CanPlace(_localCells, candidate))
                return false;

            _origin = candidate;
            _rigidbody.position = _board.WorldForCell(_origin);
            _lockTimer = 0f;
            if (offset.y < 0)
                _fallStepTimer = 0f;
            return true;
        }

        private void TryRotateClockwise()
        {
            var candidateRotation = (_rotation + 1) % 4;
            var candidateCells = TetrominoCatalog.GetCells(_kind, candidateRotation);
            var wallKicks = new[] { 0, 1, -1, 2, -2 };

            foreach (var kick in wallKicks)
            {
                var candidateOrigin = _origin + new Vector2Int(kick, 0);
                if (!_board.CanPlace(candidateCells, candidateOrigin))
                    continue;

                _rotation = candidateRotation;
                _origin = candidateOrigin;
                _localCells = candidateCells;
                _rigidbody.position = _board.WorldForCell(_origin);
                for (var i = 0; i < _localCells.Length; i++)
                    _renderers[i].transform.localPosition = new Vector3(_localCells[i].x, _localCells[i].y, 0f);
                _lockTimer = 0f;
                return;
            }
        }

        private void LockIntoBoard()
        {
            if (_finished)
                return;

            _finished = true;
            _rigidbody.position = _board.WorldForCell(_origin);
            _board.CommitPiece(_kind, _rotation, _origin);
            _owner.NotifyPieceFinished(this);
            Destroy(gameObject);
        }

        public void Cancel()
        {
            if (_finished)
                return;
            _finished = true;
            if (_warningBar != null)
                Destroy(_warningBar);
            Destroy(gameObject);
        }
    }
}
