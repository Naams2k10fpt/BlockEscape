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
        private bool _overdriveVisualActive;
        private int _currentPhase = 1;
        private bool _stopped;

        public event Action<TetrominoKind> PieceSpawned;
        public event Action<TetrominoKind> NextPieceChanged;
        public event Action PlayerCrushed;

        public ActiveTetromino ActivePiece => _activePiece;
        public int Seed { get; private set; }
        public int PiecesSpawned { get; private set; }
        public TetrominoKind NextKind => _nextKind;
        public BlockBoard Board => _board;
        public float CurrentFallSpeed => _currentFallSpeed;
        public float FallSpeedMultiplier => _fallSpeedMultiplier;
        public bool OverdriveVisualActive => _overdriveVisualActive;

        public void Initialize(BlockBoard board, TetrisBalanceConfig config, InputService input, bool startSpawning = true)
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
            _overdriveVisualActive = false;
            RefreshFallSpeed();
            _board.Overflowed += Stop;
            NextPieceChanged?.Invoke(_nextKind);
            if (startSpawning)
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
            if (_spawnRoutine != null)
                return;

            _stopped = false;
            _spawnRoutine = StartCoroutine(SpawnLoop());
        }

        public void Stop()
        {
            _stopped = true;
            StopRoutineOnly();
        }

        public void Restart(bool startSpawning = true)
        {
            Stop();
            foreach (var activePiece in _board.GetComponentsInChildren<ActiveTetromino>(true))
            {
                activePiece.PlayerCrushed -= OnActivePiecePlayerCrushed;
                activePiece.Cancel();
            }
            _activePiece = null;
            PiecesSpawned = 0;
            _bag.Reset(Seed);
            _random = new System.Random(Seed ^ 0x5f3759df);
            _nextKind = _bag.Next();
            _currentPhase = 1;
            _fallSpeedMultiplier = 1f;
            _overdriveVisualActive = false;
            RefreshFallSpeed();
            NextPieceChanged?.Invoke(_nextKind);
            if (startSpawning)
                StartSpawning();
        }

        public void ApplyDifficultyPhase(int phase)
        {
            if (_config == null)
                return;

            _currentPhase = Mathf.Max(1, phase);
            RefreshFallSpeed();
        }

        public void SetFallSpeedMultiplier(float multiplier, bool overdriveVisual = false)
        {
            _fallSpeedMultiplier = Mathf.Max(0.1f, multiplier);
            _overdriveVisualActive = overdriveVisual;
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

                if (_activePiece == null)
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
                _config.lockDelaySeconds,
                _overdriveVisualActive);

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
        BlockOverdrive,
        CutterSweep,
        MeteorShower
    }

    [CreateAssetMenu(menuName = "Block Escape/Dynamic Event Config", fileName = "DynamicEventConfig")]
    internal sealed class DynamicEventConfig : ScriptableObject
    {
        [Min(0.1f)] public float overdriveFallSpeedMultiplier = 1.6f;
        [Min(1)] public int overdrivePieceCount = 3;
        [Min(0.1f)] public float cutterWarningSeconds = 1.2f;
        [Min(0.1f)] public float cutterSpeed = 12f;
        [Range(0f, 1f)] public float meteorEventChance = 0.5f;
        [Min(1)] public int meteorCount = 3;
        [Min(0.05f)] public float meteorWarningSeconds = 0.45f;
        [Min(0.05f)] public float meteorIntervalSeconds = 0.35f;
        [Min(0.1f)] public float meteorFallSpeed = 6.5f;
        [Min(0.05f)] public float meteorExplosionSeconds = 0.3f;
        [Min(0.1f)] public float meteorStartHeight = 1.5f;
        [Min(0)] public int meteorDestroyRadiusCells = 2;
        [Min(0f)] public float meteorBlockFlashSeconds = 0.5f;
        [Min(0f)] public float phase2MinIntervalSeconds = 4f;
        [Min(0f)] public float phase2MaxIntervalSeconds = 6f;
        [Min(0f)] public float phase3MinIntervalSeconds = 6f;
        [Min(0f)] public float phase3MaxIntervalSeconds = 8f;
        [Min(0f)] public float phase4MinIntervalSeconds = 8f;
        [Min(0f)] public float phase4MaxIntervalSeconds = 10f;

        public void Sanitize()
        {
            overdriveFallSpeedMultiplier = Mathf.Max(0.1f, overdriveFallSpeedMultiplier);
            overdrivePieceCount = Mathf.Max(1, overdrivePieceCount);
            cutterWarningSeconds = Mathf.Max(0.1f, cutterWarningSeconds);
            cutterSpeed = Mathf.Max(0.1f, cutterSpeed);
            meteorEventChance = Mathf.Clamp01(meteorEventChance);
            meteorCount = Mathf.Max(1, meteorCount);
            meteorWarningSeconds = Mathf.Max(0.05f, meteorWarningSeconds);
            meteorIntervalSeconds = Mathf.Max(0.05f, meteorIntervalSeconds);
            meteorFallSpeed = Mathf.Max(0.1f, meteorFallSpeed);
            meteorExplosionSeconds = Mathf.Max(0.05f, meteorExplosionSeconds);
            meteorStartHeight = Mathf.Max(0.1f, meteorStartHeight);
            meteorDestroyRadiusCells = Mathf.Max(0, meteorDestroyRadiusCells);
            meteorBlockFlashSeconds = Mathf.Max(0f, meteorBlockFlashSeconds);
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
        private BlockEscape.Tetris.BlockBoard _board;
        private Transform _player;
        private System.Random _random;
        private Coroutine _cutterRoutine;
        private Coroutine _meteorRoutine;
        private Transform _cutterRoot;
        private Transform _meteorRoot;
        private float _timer;
        private int _phase = 1;
        private int _remainingOverdrivePieces;
        private bool _running;
        private bool _hasStartedEvent;

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

        public void Initialize(
            DynamicEventConfig config,
            BlockEscape.Tetris.TetrominoSpawner spawner,
            int seed,
            Transform player)
        {
            _config = config != null ? config : ScriptableObject.CreateInstance<DynamicEventConfig>();
            _config.Sanitize();
            _random = new System.Random(seed ^ 0x6d2b79f5);
            BindSpawner(spawner);
            _board = spawner != null ? spawner.Board : null;
            _player = player;
            ResetDirector(seed);
        }

        public void ResetDirector(int seed)
        {
            StopActiveEvent();
            _random = new System.Random(seed ^ 0x6d2b79f5);
            _hasStartedEvent = false;
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
            return phase >= 1;
        }

        public static int GetCutterTargetRow(BlockEscape.Tetris.BlockBoard board, Vector2 playerPosition)
        {
            if (board == null || board.Height <= 1)
                return -1;

            return Mathf.Clamp(board.CellForWorld(playerPosition).y, 1, board.Height - 1);
        }

        public static float GetNextIntervalForPhase(int phase, DynamicEventConfig config, System.Random random)
        {
            if (config == null)
                config = ScriptableObject.CreateInstance<DynamicEventConfig>();
            config.Sanitize();
            random ??= new System.Random(0);

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

            if (ActiveEvent != DynamicEventKind.None || _board != null && _board.IsResolving)
                return;

            _timer -= deltaTime;
            if (_timer <= 0f)
                StartRandomEvent();
        }

        private void StartRandomEvent()
        {
            if (_board != null && _random.Next(0, 3) == 0 && TryStartCutterSweep())
            {
                _hasStartedEvent = true;
                return;
            }

            if (_board != null && (!_hasStartedEvent || _random.NextDouble() < _config.meteorEventChance))
                StartMeteorShower();
            else
                StartOverdrive();
            _hasStartedEvent = true;
        }

        private void StartOverdrive()
        {
            ActiveEvent = DynamicEventKind.BlockOverdrive;
            _remainingOverdrivePieces = _config.overdrivePieceCount;
            _spawner.SetFallSpeedMultiplier(_config.overdriveFallSpeedMultiplier, overdriveVisual: true);
            StatusChanged?.Invoke(
                $"EVENT: OVERDRIVE x{_config.overdriveFallSpeedMultiplier:0.0} ({_remainingOverdrivePieces} LEFT)");
        }

        private bool TryStartCutterSweep()
        {
            if (_board == null || _player == null)
                return false;

            ActiveEvent = DynamicEventKind.CutterSweep;
            var fromLeft = _random.Next(0, 2) == 0;
            _cutterRoutine = StartCoroutine(CutterSweepRoutine(fromLeft));
            StatusChanged?.Invoke("EVENT: CUTTER TRACKING");
            return true;
        }

        private IEnumerator CutterSweepRoutine(bool fromLeft)
        {
            var boardLeft = _board.transform.position.x;
            var boardRight = boardLeft + _board.Width;
            var row = GetCutterTargetRow(_board, _player.position);
            var rowY = _board.WorldForCell(new Vector2Int(0, row)).y;
            var root = GetCutterRoot();
            var warning = BlockEscape.Tetris.RuntimeVisuals.CreateQuad(
                "Cutter Warning",
                root,
                new Vector3((boardLeft + boardRight) * 0.5f, rowY, 0f),
                new Vector2(_board.Width, 0.14f),
                new Color(1f, 0.1f, 0.15f, 0.6f),
                24);

            for (var elapsed = 0f; elapsed < _config.cutterWarningSeconds; elapsed += Time.deltaTime)
            {
                if (_player != null)
                {
                    row = GetCutterTargetRow(_board, _player.position);
                    rowY = _board.WorldForCell(new Vector2Int(0, row)).y;
                    warning.transform.position = new Vector3((boardLeft + boardRight) * 0.5f, rowY, 0f);
                }

                yield return null;
            }

            if (warning != null)
                Destroy(warning);
            if (_board == null)
                yield break;

            var start = new Vector2(fromLeft ? boardLeft - 0.5f : boardRight + 0.5f, rowY);
            var end = new Vector2(fromLeft ? boardRight + 0.5f : boardLeft - 0.5f, rowY);
            var cutter = BlockEscape.Tetris.RuntimeVisuals.CreateQuad(
                "Cutter",
                root,
                start,
                new Vector2(0.36f, 0.9f),
                new Color(1f, 0.15f, 0.2f),
                26);

            StatusChanged?.Invoke($"EVENT: CUTTER SWEEP ROW {row + 1}");
            var duration = Vector2.Distance(start, end) / _config.cutterSpeed;
            var playerMask = LayerMask.GetMask("Player");
            var touchedPlayer = false;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                var position = Vector2.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
                cutter.transform.position = position;
                if (!touchedPlayer && playerMask != 0)
                {
                    var hit = Physics2D.OverlapBox(position, new Vector2(0.42f, 0.9f), 0f, playerMask);
                    if (hit != null)
                    {
                        touchedPlayer = true;
                        var knockback = new Vector2(fromLeft ? 4f : -4f, 2f);
                        hit.GetComponentInParent<IDamageable>()?.TakeDamage(
                            new DamageInfo(1, knockback, cutter, DamageType.Hazard));
                    }
                }

                yield return null;
            }

            cutter.transform.position = end;
            if (_cutterRoot != null)
                Destroy(_cutterRoot.gameObject);
            _cutterRoot = null;

            while (_board != null && _board.IsResolving)
                yield return null;
            _board?.ForceClearRow(row);

            _cutterRoutine = null;
            ActiveEvent = DynamicEventKind.None;
            _timer = GetNextIntervalForPhase(_phase, _config, _random);
            StatusChanged?.Invoke("EVENT: CUTTER END");
        }

        private void StartMeteorShower()
        {
            ActiveEvent = DynamicEventKind.MeteorShower;
            _meteorRoutine = StartCoroutine(MeteorShowerRoutine());
            StatusChanged?.Invoke("EVENT: METEOR SHOWER");
        }

        private IEnumerator MeteorShowerRoutine()
        {
            for (var i = 0; i < _config.meteorCount; i++)
            {
                yield return SpawnMeteorWarningAndProjectile(i + 1);
                yield return new WaitForSeconds(_config.meteorIntervalSeconds);
            }

            var travelSeconds = (_board != null ? _board.Width + _board.Height + _config.meteorStartHeight + 2f : 2f) / _config.meteorFallSpeed;
            yield return new WaitForSeconds(travelSeconds);

            _meteorRoutine = null;
            ActiveEvent = DynamicEventKind.None;
            _timer = GetNextIntervalForPhase(_phase, _config, _random);
            StatusChanged?.Invoke("EVENT: METEOR END");
        }

        private IEnumerator SpawnMeteorWarningAndProjectile(int index)
        {
            if (_board == null)
                yield break;

            var bottom = _board.transform.position.y;
            var left = _board.transform.position.x;
            var right = left + _board.Width;
            const float edgeInset = 0.65f;
            const float targetInset = 1.0f;
            var startFromLeft = _random.Next(0, 2) == 0;
            var spawnX = startFromLeft ? left + edgeInset : right - edgeInset;
            var targetX = Mathf.Lerp(left + targetInset, right - targetInset, (float)_random.NextDouble());
            var minimumTravelX = _board.Width * 0.35f;
            if (Mathf.Abs(targetX - spawnX) < minimumTravelX)
            {
                targetX = startFromLeft
                    ? Mathf.Min(right - targetInset, spawnX + minimumTravelX + (float)_random.NextDouble() * _board.Width * 0.35f)
                    : Mathf.Max(left + targetInset, spawnX - minimumTravelX - (float)_random.NextDouble() * _board.Width * 0.35f);
            }
            var stackImpactY = GetMeteorImpactY(bottom);
            var spawnMinY = Mathf.Max(bottom + _board.Height * 0.5f, stackImpactY + 3f);
            var spawnMaxY = bottom + _board.Height * 0.95f;
            if (spawnMinY > spawnMaxY)
                spawnMinY = spawnMaxY;
            var start = new Vector2(
                spawnX,
                Mathf.Lerp(spawnMinY, spawnMaxY, (float)_random.NextDouble()));
            var despawnY = bottom - 2f;
            var end = new Vector2(
                targetX,
                bottom - 0.35f);
            var warningEnd = ExtendMeteorPathToY(start, end, despawnY);
            var root = GetMeteorRoot();
            var path = warningEnd - start;
            var pathLength = Mathf.Max(0.1f, path.magnitude);

            var warning = BlockEscape.Tetris.RuntimeVisuals.CreateQuad(
                $"Meteor Warning {index}",
                root,
                (start + warningEnd) * 0.5f,
                new Vector2(0.14f, pathLength),
                new Color(1f, 0.15f, 0.05f, 0.55f),
                18);
            warning.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(path.y, path.x) * Mathf.Rad2Deg - 90f);

            StatusChanged?.Invoke($"EVENT: METEOR {index}/{_config.meteorCount}");
            yield return new WaitForSeconds(_config.meteorWarningSeconds);
            if (warning != null)
                Destroy(warning);

            if (_board == null)
                yield break;

            SpawnMeteorProjectile(start, end, despawnY);
        }

        private float GetMeteorImpactY(float boardBottom)
        {
            var highestRow = _board != null && _board.Model != null ? _board.Model.HighestOccupiedRow() : -1;
            if (highestRow < 0)
                return boardBottom + 0.4f;

            var highestBlockY = _board.WorldForCell(new Vector2Int(_board.Width / 2, highestRow)).y;
            return Mathf.Clamp(highestBlockY + 0.25f, boardBottom + 0.4f, boardBottom + _board.Height - 0.5f);
        }

        private void SpawnMeteorProjectile(Vector2 start, Vector2 end, float despawnY)
        {
            var meteor = new GameObject("Meteor");
            meteor.transform.SetParent(GetMeteorRoot(), false);
            meteor.transform.position = start;
            meteor.transform.localScale = new Vector3(0.72f, 0.72f, 1f);

            var renderer = meteor.AddComponent<SpriteRenderer>();
            renderer.sprite = BlockEscape.Tetris.RuntimeVisuals.Square;
            renderer.color = new Color(1f, 0.38f, 0.05f);
            renderer.sortingOrder = 25;

            var collider = meteor.AddComponent<CircleCollider2D>();
            collider.radius = 0.42f;
            collider.isTrigger = true;

            var body = meteor.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            var projectile = meteor.AddComponent<MeteorProjectile>();
            projectile.Initialize(
                _config.meteorFallSpeed,
                end,
                despawnY,
                _config.meteorExplosionSeconds,
                _board,
                _config.meteorDestroyRadiusCells,
                _config.meteorBlockFlashSeconds);
        }

        private static Vector2 ExtendMeteorPathToY(Vector2 start, Vector2 target, float y)
        {
            var direction = target - start;
            if (direction.sqrMagnitude <= 0.01f || direction.y >= -0.01f)
                return target;

            direction.Normalize();
            var distance = (y - start.y) / direction.y;
            return start + direction * Mathf.Max(0f, distance);
        }

        private Transform GetMeteorRoot()
        {
            if (_meteorRoot != null)
                return _meteorRoot;

            var root = new GameObject("Meteor Events");
            root.transform.SetParent(_board != null ? _board.transform : transform, false);
            _meteorRoot = root.transform;
            return _meteorRoot;
        }

        private Transform GetCutterRoot()
        {
            if (_cutterRoot != null)
                return _cutterRoot;

            var root = new GameObject("Cutter Event");
            root.transform.SetParent(_board != null ? _board.transform : transform, false);
            _cutterRoot = root.transform;
            return _cutterRoot;
        }

        private void OnPieceSpawned(BlockEscape.Tetris.TetrominoKind kind)
        {
            if (ActiveEvent != DynamicEventKind.BlockOverdrive)
                return;

            _remainingOverdrivePieces--;
            if (_remainingOverdrivePieces > 0)
            {
                StatusChanged?.Invoke($"EVENT: OVERDRIVE {_remainingOverdrivePieces} LEFT");
                return;
            }

            StopActiveEvent();
            _timer = GetNextIntervalForPhase(_phase, _config, _random);
            StatusChanged?.Invoke("EVENT: OVERDRIVE END");
        }

        private void StopActiveEvent()
        {
            if (_cutterRoutine != null)
                StopCoroutine(_cutterRoutine);
            _cutterRoutine = null;
            if (_meteorRoutine != null)
                StopCoroutine(_meteorRoutine);
            _meteorRoutine = null;
            if (_cutterRoot != null)
                Destroy(_cutterRoot.gameObject);
            _cutterRoot = null;
            if (_meteorRoot != null)
                Destroy(_meteorRoot.gameObject);
            _meteorRoot = null;
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
            _board = null;
        }
    }

    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    internal sealed class MeteorProjectile : MonoBehaviour
    {
        private float _speed = 11f;
        private Vector2 _direction = Vector2.down;
        private float _despawnY;
        private float _explosionSeconds = 0.3f;
        private BlockEscape.Tetris.BlockBoard _board;
        private int _destroyRadiusCells = 3;
        private float _blockFlashSeconds = 0.5f;
        private bool _finished;

        public void Initialize(
            float speed,
            Vector2 end,
            float despawnY,
            float explosionSeconds,
            BlockEscape.Tetris.BlockBoard board,
            int destroyRadiusCells,
            float blockFlashSeconds)
        {
            _speed = Mathf.Max(0.1f, speed);
            var path = end - (Vector2)transform.position;
            _direction = path.sqrMagnitude > 0.01f ? path.normalized : Vector2.down;
            _despawnY = despawnY;
            _explosionSeconds = Mathf.Max(0.05f, explosionSeconds);
            _board = board;
            _destroyRadiusCells = Mathf.Max(0, destroyRadiusCells);
            _blockFlashSeconds = Mathf.Max(0f, blockFlashSeconds);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg - 90f);
        }

        private void Update()
        {
            if (_finished)
                return;

            var step = _speed * Time.deltaTime;
            transform.position += (Vector3)(_direction * step);
            CheckOverlapHit();
            if (!_finished && transform.position.y <= _despawnY)
                Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            HandleHit(other);
        }

        private void CheckOverlapHit()
        {
            var mask = LayerMask.GetMask("Player", "World", "FallingBlock");
            if (mask == 0)
                return;

            var hits = Physics2D.OverlapCircleAll(transform.position, 0.42f, mask);
            foreach (var hit in hits)
            {
                if (HandleHit(hit))
                    return;
            }
        }

        private bool HandleHit(Collider2D other)
        {
            if (_finished || other == null || !other.enabled || other.isTrigger)
                return false;

            var otherLayer = other.gameObject.layer;
            if (otherLayer == LayerMask.NameToLayer("Player"))
            {
                var damageable = other.GetComponentInParent<IDamageable>();
                damageable?.TakeDamage(new DamageInfo(1, Vector2.down * 4f, gameObject, DamageType.Hazard));
                Explode();
                return true;
            }

            if (otherLayer == LayerMask.NameToLayer("World") || otherLayer == LayerMask.NameToLayer("FallingBlock"))
            {
                if (_board != null && other.GetComponent<BlockEscape.Tetris.BlockCellView>() != null)
                    _board.DestroyCellsInRadius(transform.position, _destroyRadiusCells, _blockFlashSeconds);
                Explode();
                return true;
            }

            return false;
        }

        private void Explode()
        {
            if (_finished)
                return;

            _finished = true;
            var collider = GetComponent<Collider2D>();
            if (collider != null)
                collider.enabled = false;
            StartCoroutine(ExplosionRoutine());
        }

        private IEnumerator ExplosionRoutine()
        {
            var renderer = GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = new Color(1f, 0.75f, 0.1f, 0.95f);
                renderer.sortingOrder = 26;
            }

            var startScale = Vector3.one * 0.75f;
            var endScale = Vector3.one * 1.5f;
            for (var elapsed = 0f; elapsed < _explosionSeconds; elapsed += Time.deltaTime)
            {
                var t = Mathf.Clamp01(elapsed / _explosionSeconds);
                transform.localScale = Vector3.Lerp(startScale, endScale, t);
                if (renderer != null)
                {
                    var color = renderer.color;
                    color.a = 1f - t;
                    renderer.color = color;
                }
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
