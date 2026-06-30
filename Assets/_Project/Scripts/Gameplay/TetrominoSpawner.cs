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
        private float _currentFallSpeed;
        private float _fallSpeedMultiplier = 1f;
        private int _currentPhase = 1;
        private bool _stopped;

        public event Action<TetrominoKind> PieceSpawned;
        public event Action<TetrominoKind> NextPieceChanged;
        public event Action PlayerCrushed;

        public ActiveTetromino ActivePiece => _activePiece;
        public int Seed { get; private set; }
        public int PiecesSpawned { get; private set; }
        public TetrominoKind NextKind => _nextKind;
        public float CurrentFallSpeed => _currentFallSpeed;
        public float FallSpeedMultiplier => _fallSpeedMultiplier;

        public void Initialize(BlockBoard board, TetrisBalanceConfig config, InputService input)
        {
            _board = board;
            _config = config;
            _input = input;
            Seed = config.seed == 0 ? Environment.TickCount : config.seed;
            _bag = new SevenBag(Seed);
            _random = new System.Random(Seed ^ 0x5f3759df);
            _nextKind = _bag.Next();
            _currentPhase = 1;
            _fallSpeedMultiplier = 1f;
            RefreshFallSpeed();
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
            _currentPhase = 1;
            _fallSpeedMultiplier = 1f;
            RefreshFallSpeed();
            NextPieceChanged?.Invoke(_nextKind);
            StartSpawning();
        }

        public void ApplyDifficultyPhase(int phase)
        {
            if (_config == null)
                return;

            _currentPhase = Mathf.Max(1, phase);
            RefreshFallSpeed();
        }

        public void SetFallSpeedMultiplier(float multiplier)
        {
            _fallSpeedMultiplier = Mathf.Max(0.1f, multiplier);
            RefreshFallSpeed();
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
                _currentFallSpeed,
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

        private void RefreshFallSpeed()
        {
            if (_config == null)
                return;

            _currentFallSpeed = _config.GetFallSpeedForPhase(_currentPhase) * _fallSpeedMultiplier;
        }

        private void StopRoutineOnly()
        {
            if (_spawnRoutine != null)
                StopCoroutine(_spawnRoutine);
            _spawnRoutine = null;
        }
    }
}

namespace BlockEscape.Events
{
    internal enum DynamicEventKind
    {
        None,
        BlockOverdrive
    }

    [CreateAssetMenu(menuName = "Block Escape/Dynamic Event Config", fileName = "DynamicEventConfig")]
    internal sealed class DynamicEventConfig : ScriptableObject
    {
        [Min(0.1f)] public float overdriveFallSpeedMultiplier = 1.6f;
        [Min(1)] public int overdrivePieceCount = 3;
        [Min(0f)] public float phase2MinIntervalSeconds = 25f;
        [Min(0f)] public float phase2MaxIntervalSeconds = 30f;
        [Min(0f)] public float phase3MinIntervalSeconds = 18f;
        [Min(0f)] public float phase3MaxIntervalSeconds = 24f;
        [Min(0f)] public float phase4MinIntervalSeconds = 14f;
        [Min(0f)] public float phase4MaxIntervalSeconds = 20f;

        public void Sanitize()
        {
            overdriveFallSpeedMultiplier = Mathf.Max(0.1f, overdriveFallSpeedMultiplier);
            overdrivePieceCount = Mathf.Max(1, overdrivePieceCount);
            NormalizeRange(ref phase2MinIntervalSeconds, ref phase2MaxIntervalSeconds);
            NormalizeRange(ref phase3MinIntervalSeconds, ref phase3MaxIntervalSeconds);
            NormalizeRange(ref phase4MinIntervalSeconds, ref phase4MaxIntervalSeconds);
        }

        private static void NormalizeRange(ref float min, ref float max)
        {
            min = Mathf.Max(0f, min);
            max = Mathf.Max(min, max);
        }
    }

    internal sealed class EventDirector : MonoBehaviour
    {
        [SerializeField] private DynamicEventConfig _config;

        private BlockEscape.Tetris.TetrominoSpawner _spawner;
        private System.Random _random;
        private float _timer;
        private int _phase = 1;
        private int _remainingOverdrivePieces;
        private bool _running;

        public event System.Action<string> StatusChanged;

        public DynamicEventKind ActiveEvent { get; private set; } = DynamicEventKind.None;
        public int RemainingOverdrivePieces => _remainingOverdrivePieces;

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            StopActiveEvent();
            UnbindSpawner();
        }

        public void Initialize(DynamicEventConfig config, BlockEscape.Tetris.TetrominoSpawner spawner, int seed)
        {
            _config = config != null ? config : ScriptableObject.CreateInstance<DynamicEventConfig>();
            _config.Sanitize();
            _random = new System.Random(seed ^ 0x6d2b79f5);
            BindSpawner(spawner);
            ResetDirector(seed);
        }

        public void ResetDirector(int seed)
        {
            StopActiveEvent();
            _random = new System.Random(seed ^ 0x6d2b79f5);
            _timer = GetNextIntervalForPhase(_phase, _config, _random);
        }

        public void SetRunning(bool running)
        {
            _running = running;
        }

        public void SetPhase(int phase)
        {
            _phase = Mathf.Max(1, phase);
            if (!CanRunEvents(_phase))
            {
                StopActiveEvent();
                _timer = GetNextIntervalForPhase(_phase, _config, _random);
                return;
            }

            if (_timer <= 0f)
                _timer = GetNextIntervalForPhase(_phase, _config, _random);
        }

        public void ManualTick(float deltaTime)
        {
            Tick(deltaTime);
        }

        public static bool CanRunEvents(int phase)
        {
            return phase >= 2;
        }

        public static float GetNextIntervalForPhase(int phase, DynamicEventConfig config, System.Random random)
        {
            if (config == null)
                config = ScriptableObject.CreateInstance<DynamicEventConfig>();
            config.Sanitize();
            random ??= new System.Random(0);

            if (phase <= 1)
                return float.PositiveInfinity;

            var min = config.phase2MinIntervalSeconds;
            var max = config.phase2MaxIntervalSeconds;
            if (phase == 3)
            {
                min = config.phase3MinIntervalSeconds;
                max = config.phase3MaxIntervalSeconds;
            }
            else if (phase >= 4)
            {
                min = config.phase4MinIntervalSeconds;
                max = config.phase4MaxIntervalSeconds;
            }

            return Mathf.Lerp(min, max, (float)random.NextDouble());
        }

        private void Tick(float deltaTime)
        {
            if (!_running || deltaTime <= 0f || _spawner == null || _config == null || !CanRunEvents(_phase))
                return;

            if (ActiveEvent != DynamicEventKind.None)
                return;

            _timer -= deltaTime;
            if (_timer <= 0f)
                StartOverdrive();
        }

        private void StartOverdrive()
        {
            ActiveEvent = DynamicEventKind.BlockOverdrive;
            _remainingOverdrivePieces = _config.overdrivePieceCount;
            _spawner.SetFallSpeedMultiplier(_config.overdriveFallSpeedMultiplier);
            StatusChanged?.Invoke($"EVENT: OVERDRIVE x{_config.overdriveFallSpeedMultiplier:0.0}");
        }

        private void OnPieceSpawned(BlockEscape.Tetris.TetrominoKind kind)
        {
            if (ActiveEvent != DynamicEventKind.BlockOverdrive)
                return;

            _remainingOverdrivePieces--;
            if (_remainingOverdrivePieces > 0)
                return;

            StopActiveEvent();
            _timer = GetNextIntervalForPhase(_phase, _config, _random);
            StatusChanged?.Invoke("EVENT: OVERDRIVE END");
        }

        private void StopActiveEvent()
        {
            if (_spawner != null)
                _spawner.SetFallSpeedMultiplier(1f);
            ActiveEvent = DynamicEventKind.None;
            _remainingOverdrivePieces = 0;
        }

        private void BindSpawner(BlockEscape.Tetris.TetrominoSpawner spawner)
        {
            UnbindSpawner();
            _spawner = spawner;
            if (_spawner != null)
                _spawner.PieceSpawned += OnPieceSpawned;
        }

        private void UnbindSpawner()
        {
            if (_spawner != null)
                _spawner.PieceSpawned -= OnPieceSpawned;
            _spawner = null;
        }
    }
}
