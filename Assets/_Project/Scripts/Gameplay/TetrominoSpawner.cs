using System;
using System.Collections;
using BlockEscape.Core;
using UnityEngine;

namespace BlockEscape.Tetris
{
    public sealed class TetrominoSpawner : MonoBehaviour
    {
        private BlockBoard _board;
        private TetrisBalanceConfig _config;
        private InputService _input;
        private SevenBag _bag;
        private System.Random _random;
        private Coroutine _spawnRoutine;
        private ActiveTetromino _activePiece;
        private TetrominoKind _nextKind;
        private bool _stopped;

        public event Action<TetrominoKind> PieceSpawned;
        public event Action<TetrominoKind> NextPieceChanged;
        public event Action PlayerCrushed;

        public ActiveTetromino ActivePiece => _activePiece;
        public int Seed { get; private set; }
        public int PiecesSpawned { get; private set; }
        public TetrominoKind NextKind => _nextKind;

        public void Initialize(BlockBoard board, TetrisBalanceConfig config, InputService input)
        {
            _board = board;
            _config = config;
            _input = input;
            Seed = config.seed == 0 ? Environment.TickCount : config.seed;
            _bag = new SevenBag(Seed);
            _random = new System.Random(Seed ^ 0x5f3759df);
            _nextKind = _bag.Next();
            _board.Overflowed += Stop;
            NextPieceChanged?.Invoke(_nextKind);
            StartSpawning();
        }

        private void OnDestroy()
        {
            if (_board != null)
                _board.Overflowed -= Stop;
            if (_activePiece != null)
                _activePiece.PlayerCrushed -= OnActivePiecePlayerCrushed;
        }

        public void StartSpawning()
        {
            StopRoutineOnly();
            _stopped = false;
            _spawnRoutine = StartCoroutine(SpawnLoop());
        }

        public void Stop()
        {
            _stopped = true;
            StopRoutineOnly();
        }

        public void Restart()
        {
            if (_activePiece != null)
            {
                _activePiece.PlayerCrushed -= OnActivePiecePlayerCrushed;
                _activePiece.Cancel();
            }
            _activePiece = null;
            PiecesSpawned = 0;
            _bag.Reset(Seed);
            _random = new System.Random(Seed ^ 0x5f3759df);
            _nextKind = _bag.Next();
            NextPieceChanged?.Invoke(_nextKind);
            StartSpawning();
        }

        public void NotifyPieceFinished(ActiveTetromino piece)
        {
            piece.PlayerCrushed -= OnActivePiecePlayerCrushed;
            if (_activePiece == piece)
                _activePiece = null;
        }

        private IEnumerator SpawnLoop()
        {
            if (_config.initialSpawnDelay > 0f)
                yield return new WaitForSeconds(_config.initialSpawnDelay);

            while (!_stopped && !_board.IsOverflowed)
            {
                while (_board.IsResolving)
                    yield return null;

                SpawnNext();
                while (_activePiece != null && !_stopped)
                    yield return null;

                if (_config.spawnDelay > 0f && !_stopped)
                    yield return new WaitForSeconds(_config.spawnDelay);
            }

            _spawnRoutine = null;
        }

        private void SpawnNext()
        {
            var kind = _nextKind;
            _nextKind = _bag.Next();
            var rotation = _random.Next(0, 4);
            var size = TetrominoCatalog.GetSize(kind, rotation);
            var maxX = Mathf.Max(0, _board.Width - size.x);
            var x = _random.Next(0, maxX + 1);
            var origin = new Vector2Int(x, _board.Height + 1);

            var gameObject = new GameObject($"Active {kind}");
            gameObject.transform.position = _board.WorldForCell(origin);
            gameObject.transform.SetParent(_board.transform, true);
            _activePiece = gameObject.AddComponent<ActiveTetromino>();
            _activePiece.PlayerCrushed += OnActivePiecePlayerCrushed;
            _activePiece.Initialize(
                _board,
                this,
                _input,
                kind,
                rotation,
                origin,
                _config.fallSpeedCellsPerSecond,
                _config.telegraphSeconds,
                _config.lockDelaySeconds);

            PiecesSpawned++;
            PieceSpawned?.Invoke(kind);
            NextPieceChanged?.Invoke(_nextKind);
        }

        private void OnActivePiecePlayerCrushed()
        {
            PlayerCrushed?.Invoke();
        }

        private void StopRoutineOnly()
        {
            if (_spawnRoutine != null)
                StopCoroutine(_spawnRoutine);
            _spawnRoutine = null;
        }
    }
}
