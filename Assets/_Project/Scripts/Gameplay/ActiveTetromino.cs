using System.Collections;
using BlockEscape.Core;
using UnityEngine;

namespace BlockEscape.Tetris
{
    public sealed class ActiveTetromino : MonoBehaviour
    {
        private BlockBoard _board;
        private TetrominoSpawner _owner;
        private InputService _input;
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
        private Transform _ghostRoot;
        private SpriteRenderer[] _ghostRenderers;
        private GameObject _warningBar;
        private bool _playerCrushRaised;
        private int _heldDirection;
        private float _horizontalHoldTime;
        private float _horizontalRepeatTime;
        private float _softDropRepeatTime;
        private Color _visualColor;

        private const float HorizontalRepeatDelay = 0.18f;
        private const float HorizontalRepeatInterval = 0.07f;
        private const float SoftDropRepeatInterval = 0.06f;
        private const float GhostAlpha = 0.10f;
        private const float CellColliderSize = 1.08f;
        private const float CrushProbeSize = 0.86f;
        private const float PlayerBounceVelocity = -8f;
        private const float PlayerBounceSkin = 0.03f;
        private static readonly Color OverdriveColor = new(1f, 0.25f, 0.95f);

        public event System.Action PlayerCrushed;

        public TetrominoKind Kind => _kind;
        public int Rotation => _rotation;
        public Vector2Int GridOrigin => _origin;

        public void Initialize(
            BlockBoard board,
            TetrominoSpawner owner,
            InputService input,
            TetrominoKind kind,
            int rotation,
            Vector2Int origin,
            float fallSpeed,
            float telegraphSeconds,
            float lockDelay,
            bool overdriveVisual = false)
        {
            _board = board;
            _owner = owner;
            _input = input;
            _kind = kind;
            _rotation = rotation;
            _origin = origin;
            _fallSpeed = Mathf.Max(0.1f, fallSpeed);
            _lockDelay = Mathf.Max(0f, lockDelay);
            _visualColor = overdriveVisual ? OverdriveColor : TetrominoCatalog.GetColor(kind);
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
            BuildGhostCells();
            UpdateGhostPiece();
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

            if (_input == null || !_input.GameplayEnabled)
                return;

            HandleHorizontalInput(_input);

            if (_input.TetrisRotate.WasPressedThisFrame())
                TryRotateClockwise();

            if (_input.TetrisSoftDrop.WasPressedThisFrame())
            {
                TryMove(Vector2Int.down);
                _softDropRepeatTime = 0f;
            }
            else if (_input.TetrisSoftDrop.IsPressed())
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
                if (!TryMove(Vector2Int.down))
                {
                    _fallStepTimer = 0f;
                    return;
                }
                return;
            }

            _fallStepTimer = 0f;
            var restingPosition = (Vector2)_board.WorldForCell(_origin);
            _rigidbody.position = restingPosition;
            CheckPlayerCrush();
            _lockTimer += Time.fixedDeltaTime;
            if (_lockTimer >= _lockDelay)
                LockIntoBoard();
        }

        private IEnumerator TelegraphRoutine(float duration)
        {
            _telegraphing = true;
            var elapsed = 0f;
            while (elapsed < duration && !_finished)
            {
                var pulse = 0.25f + Mathf.PingPong(elapsed * 2.5f, 0.45f);
                SetVisualColor(new Color(_visualColor.r, _visualColor.g, _visualColor.b, pulse));
                if (_warningBar != null)
                {
                    var renderer = _warningBar.GetComponent<SpriteRenderer>();
                    renderer.color = new Color(1f, 0.25f, 0.25f, 0.35f + Mathf.PingPong(elapsed * 3f, 0.45f));
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            SetVisualColor(_visualColor);
            if (_warningBar != null)
                DestroySafely(_warningBar);
            _warningBar = null;
            _telegraphing = false;
        }

        private void BuildCells()
        {
            _renderers = new SpriteRenderer[_localCells.Length];
            for (var i = 0; i < _localCells.Length; i++)
            {
                var cell = new GameObject($"Cell {i}");
                cell.transform.SetParent(transform, false);
                cell.transform.localPosition = new Vector3(_localCells[i].x, _localCells[i].y, 0f);
                cell.transform.localScale = new Vector3(0.92f, 0.92f, 1f);
                cell.layer = gameObject.layer;

                var renderer = cell.AddComponent<SpriteRenderer>();
                renderer.sprite = RuntimeVisuals.Square;
                renderer.color = _visualColor;
                renderer.sortingOrder = 20;
                _renderers[i] = renderer;

                var collider = cell.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(CellColliderSize, CellColliderSize);
                collider.isTrigger = true;
                collider.sharedMaterial = PhysicsMaterialLibrary.Frictionless;
            }
        }

        public bool CheckPlayerCrush()
        {
            if (!DetectPlayerCrush())
                return false;

            return RaisePlayerCrush();
        }

        private bool RaisePlayerCrush()
        {
            if (_playerCrushRaised)
                return false;

            _playerCrushRaised = true;
            PlayerCrushed?.Invoke();
            return true;
        }

        private bool DetectPlayerCrush()
        {
            var playerMask = LayerMask.GetMask("Player");
            if (playerMask == 0 || _localCells == null)
                return false;

            Physics2D.SyncTransforms();
            var rootPosition = _rigidbody != null ? _rigidbody.position : (Vector2)transform.position;
            foreach (var localCell in _localCells)
            {
                var center = rootPosition + localCell;
                var hits = Physics2D.OverlapBoxAll(center, new Vector2(CrushProbeSize, CrushProbeSize), 0f, playerMask);
                foreach (var hit in hits)
                {
                    if (hit == null || !hit.enabled || hit.isTrigger)
                        continue;
                    if (PlayerCrushEscape.ShouldCrush(
                            hit,
                            center,
                            _board,
                            ignoreRisingPlayer: true,
                            _localCells,
                            _origin,
                            new Vector2(CrushProbeSize, CrushProbeSize)))
                        return true;
                }
            }

            return false;
        }

        private void BuildGhostCells()
        {
            var ghostObject = new GameObject("Ghost Piece (Landing Preview)");
            ghostObject.transform.SetParent(_board.transform, false);
            ghostObject.layer = gameObject.layer;
            _ghostRoot = ghostObject.transform;

            _ghostRenderers = new SpriteRenderer[_localCells.Length];
            var ghostColor = new Color(_visualColor.r, _visualColor.g, _visualColor.b, GhostAlpha);

            for (var i = 0; i < _localCells.Length; i++)
            {
                var cell = new GameObject($"Ghost Cell {i}");
                cell.transform.SetParent(_ghostRoot, false);
                cell.transform.localPosition = new Vector3(_localCells[i].x, _localCells[i].y, 0f);
                cell.transform.localScale = new Vector3(0.86f, 0.86f, 1f);
                cell.layer = gameObject.layer;

                var renderer = cell.AddComponent<SpriteRenderer>();
                renderer.sprite = RuntimeVisuals.Square;
                renderer.color = ghostColor;
                renderer.sortingOrder = 15;
                _ghostRenderers[i] = renderer;
            }
        }

        private void UpdateGhostPiece()
        {
            if (_ghostRoot == null || _ghostRenderers == null)
                return;

            var landingOrigin = _origin;
            while (_board.CanPlace(_localCells, landingOrigin + Vector2Int.down))
                landingOrigin += Vector2Int.down;

            _ghostRoot.position = _board.WorldForCell(landingOrigin);
            var showGhost = landingOrigin != _origin;

            for (var i = 0; i < _ghostRenderers.Length; i++)
            {
                _ghostRenderers[i].enabled = showGhost;
                _ghostRenderers[i].transform.localPosition = new Vector3(_localCells[i].x, _localCells[i].y, 0f);
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

        private void HandleHorizontalInput(InputService input)
        {
            var moveValue = input.TetrisMove.ReadValue<float>();
            var direction = moveValue < -0.5f ? -1 : moveValue > 0.5f ? 1 : 0;

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
            if (PlayerCrushEscape.TryEvaluateCellOverlap(
                    _localCells,
                    candidate,
                    _board,
                    new Vector2(CellColliderSize, CellColliderSize),
                    ignoreRisingPlayer: true,
                    out _,
                    out _,
                    out _))
            {
                if (offset.y < 0)
                {
                    PlayerCrushEscape.TryEvaluateCellOverlap(
                        _localCells,
                        candidate,
                        _board,
                        new Vector2(CrushProbeSize, CrushProbeSize),
                        ignoreRisingPlayer: true,
                        out var shouldCrush,
                        out _,
                        out _);

                    ApplyMove(candidate, offset, checkCrush: false);
                    if (shouldCrush)
                    {
                        RaisePlayerCrush();
                    }
                    else
                    {
                        PlayerCrushEscape.DeflectOverlappedPlayersDownInCells(
                            _localCells,
                            _origin,
                            _board,
                            new Vector2(CellColliderSize, CellColliderSize),
                            PlayerBounceVelocity,
                            PlayerBounceSkin);
                    }

                    return true;
                }

                return false;
            }

            ApplyMove(candidate, offset);
            return true;
        }

        private void ApplyMove(Vector2Int origin, Vector2Int offset, bool checkCrush = true)
        {
            _origin = origin;
            _rigidbody.position = _board.WorldForCell(_origin);
            _lockTimer = 0f;
            if (offset.y < 0)
                _fallStepTimer = 0f;
            UpdateGhostPiece();
            if (checkCrush)
                CheckPlayerCrush();
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
                if (PlayerCrushEscape.TryEvaluateCellOverlap(
                        candidateCells,
                        candidateOrigin,
                        _board,
                        new Vector2(CellColliderSize, CellColliderSize),
                        ignoreRisingPlayer: true,
                        out _,
                        out _,
                        out _))
                    continue;

                _rotation = candidateRotation;
                _origin = candidateOrigin;
                _localCells = candidateCells;
                _rigidbody.position = _board.WorldForCell(_origin);
                for (var i = 0; i < _localCells.Length; i++)
                    _renderers[i].transform.localPosition = new Vector3(_localCells[i].x, _localCells[i].y, 0f);
                _lockTimer = 0f;
                UpdateGhostPiece();
                CheckPlayerCrush();
                return;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            ResolvePlayerCollisionAtCurrentCells();
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            ResolvePlayerCollisionAtCurrentCells();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            ResolvePlayerCollisionAtCurrentCells();
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            ResolvePlayerCollisionAtCurrentCells();
        }

        private void ResolvePlayerCollisionAtCurrentCells()
        {
            if (CheckPlayerCrush())
                return;

            PlayerCrushEscape.DeflectOverlappedPlayersDownInCells(
                _localCells,
                _origin,
                _board,
                new Vector2(CellColliderSize, CellColliderSize),
                PlayerBounceVelocity,
                PlayerBounceSkin);
        }

        private void LockIntoBoard()
        {
            if (_finished)
                return;

            _finished = true;
            DestroyGhostPiece();
            _rigidbody.position = _board.WorldForCell(_origin);
            _board.CommitPiece(_kind, _rotation, _origin);
            _owner.NotifyPieceFinished(this);
            DestroySafely(gameObject);
        }

        public void Cancel()
        {
            if (_finished)
                return;
            _finished = true;
            DestroyGhostPiece();
            if (_warningBar != null)
                DestroySafely(_warningBar);
            DestroySafely(gameObject);
        }

        private void OnDestroy()
        {
            DestroyGhostPiece();
        }

        private void DestroyGhostPiece()
        {
            if (_ghostRoot == null)
                return;

            var ghostObject = _ghostRoot.gameObject;
            ghostObject.SetActive(false);
            _ghostRoot = null;
            _ghostRenderers = null;
            DestroySafely(ghostObject);
        }

        private static void DestroySafely(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
