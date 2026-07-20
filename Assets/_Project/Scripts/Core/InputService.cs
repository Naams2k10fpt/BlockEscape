using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlockEscape.Core
{
    public enum RebindAction
    {
        TetrisLeft,
        TetrisRight,
        TetrisRotate,
        TetrisSoftDrop,
        PlayerLeft,
        PlayerRight,
        PlayerJump,
        PlayerCrouch
    }

    [DefaultExecutionOrder(-1000)]
    public sealed class InputService : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _actions;

        private InputActionMap _tetrisMap;
        private InputActionMap _playerMap;
        private InputActionMap _systemMap;
        private InputActionRebindingExtensions.RebindingOperation _rebindOperation;
        private InputAction _rebindAction;
        private bool _rebindActionWasEnabled;
        private bool _initialized;

        public static InputService Current { get; private set; }
        public event Action BindingsChanged;

        public InputAction TetrisMove { get; private set; }
        public InputAction TetrisRotate { get; private set; }
        public InputAction TetrisSoftDrop { get; private set; }

        public InputAction PlayerMove { get; private set; }
        public InputAction PlayerJump { get; private set; }
        public InputAction PlayerCrouch { get; private set; }

        public InputAction Pause { get; private set; }
        public InputAction ResetRun { get; private set; }

        public bool GameplayEnabled => _tetrisMap != null && _tetrisMap.enabled;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Destroy(gameObject);
                return;
            }

            Current = this;
            DontDestroyOnLoad(gameObject);
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            if (Current != this)
                return;

            _tetrisMap?.Disable();
            _playerMap?.Disable();
            _systemMap?.Disable();
            _rebindOperation?.Cancel();
            _rebindOperation?.Dispose();
            Current = null;
        }

        public void EnsureInitialized()
        {
            if (_initialized)
                return;

            if (_actions == null)
                _actions = CreateRuntimeDefaults();

            _tetrisMap = _actions.FindActionMap("Tetris", true);
            _playerMap = _actions.FindActionMap("Player", true);
            _systemMap = _actions.FindActionMap("System", true);

            TetrisMove = _tetrisMap.FindAction("Move", true);
            TetrisRotate = _tetrisMap.FindAction("Rotate", true);
            TetrisSoftDrop = _tetrisMap.FindAction("SoftDrop", true);

            PlayerMove = _playerMap.FindAction("Move", true);
            PlayerJump = _playerMap.FindAction("Jump", true);
            PlayerCrouch = _playerMap.FindAction("Crouch", true);

            Pause = _systemMap.FindAction("Pause", true);
            ResetRun = _systemMap.FindAction("ResetRun", true);

            LoadSavedBindings();
            _systemMap.Enable();
            SetGameplayEnabled(true);
            _initialized = true;
        }

        public void LoadSavedBindings()
        {
            if (_actions == null)
                return;

            _actions.RemoveAllBindingOverrides();
            var json = SaveService.Data.inputBindingOverridesJson;
            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                _actions.LoadBindingOverridesFromJson(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Input bindings could not be loaded. Defaults will be used. {exception.Message}");
                _actions.RemoveAllBindingOverrides();
            }

            BindingsChanged?.Invoke();
        }

        public string GetBindingDisplayString(RebindAction target)
        {
            if (!TryResolveBinding(target, out var action, out var bindingIndex))
                return "UNBOUND";

            return action.GetBindingDisplayString(bindingIndex);
        }

        public bool StartInteractiveRebind(RebindAction target, Action<bool> completed)
        {
            if (_rebindOperation != null || !TryResolveBinding(target, out var action, out var bindingIndex))
                return false;

            _rebindAction = action;
            _rebindActionWasEnabled = action.enabled;
            action.Disable();
            _rebindOperation = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("<Mouse>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(_ => FinishInteractiveRebind(false, completed))
                .OnComplete(_ => FinishInteractiveRebind(true, completed));
            _rebindOperation.Start();
            return true;
        }

        public void ResetBindingsToDefault()
        {
            if (_actions == null)
                return;

            _actions.RemoveAllBindingOverrides();
            SaveService.Data.inputBindingOverridesJson = string.Empty;
            SaveService.Save();
            BindingsChanged?.Invoke();
        }

        public void SetGameplayEnabled(bool enabled)
        {
            if (!_initialized && _actions == null)
                EnsureInitialized();

            if (enabled)
            {
                _tetrisMap?.Enable();
                _playerMap?.Enable();
            }
            else
            {
                _tetrisMap?.Disable();
                _playerMap?.Disable();
            }
        }

        private void FinishInteractiveRebind(bool changed, Action<bool> completed)
        {
            var operation = _rebindOperation;
            _rebindOperation = null;
            operation?.Dispose();

            if (_rebindActionWasEnabled)
                _rebindAction?.Enable();
            _rebindAction = null;

            if (changed)
            {
                SaveService.Data.inputBindingOverridesJson = _actions.SaveBindingOverridesAsJson();
                SaveService.Save();
                BindingsChanged?.Invoke();
            }

            completed?.Invoke(changed);
        }

        private bool TryResolveBinding(RebindAction target, out InputAction action, out int bindingIndex)
        {
            EnsureInitialized();
            action = target switch
            {
                RebindAction.TetrisLeft or RebindAction.TetrisRight => TetrisMove,
                RebindAction.TetrisRotate => TetrisRotate,
                RebindAction.TetrisSoftDrop => TetrisSoftDrop,
                RebindAction.PlayerLeft or RebindAction.PlayerRight => PlayerMove,
                RebindAction.PlayerJump => PlayerJump,
                RebindAction.PlayerCrouch => PlayerCrouch,
                _ => null
            };

            if (action == null)
            {
                bindingIndex = -1;
                return false;
            }

            var partName = target switch
            {
                RebindAction.TetrisLeft or RebindAction.PlayerLeft => "negative",
                RebindAction.TetrisRight or RebindAction.PlayerRight => "positive",
                _ => null
            };
            bindingIndex = partName == null ? 0 : FindBindingPart(action, partName);
            return bindingIndex >= 0;
        }

        private static int FindBindingPart(InputAction action, string partName)
        {
            for (var i = 0; i < action.bindings.Count; i++)
                if (string.Equals(action.bindings[i].name, partName, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        private static InputActionAsset CreateRuntimeDefaults()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();

            var tetris = asset.AddActionMap("Tetris");
            var tetrisMove = tetris.AddAction("Move", InputActionType.Value, expectedControlLayout: "Axis");
            tetrisMove.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a")
                .With("Positive", "<Keyboard>/d");
            tetris.AddAction("Rotate", InputActionType.Button, "<Keyboard>/w");
            tetris.AddAction("SoftDrop", InputActionType.Button, "<Keyboard>/s");

            var player = asset.AddActionMap("Player");
            var playerMove = player.AddAction("Move", InputActionType.Value, expectedControlLayout: "Axis");
            playerMove.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow")
                .With("Positive", "<Keyboard>/rightArrow");
            player.AddAction("Jump", InputActionType.Button, "<Keyboard>/upArrow");
            player.AddAction("Crouch", InputActionType.Button, "<Keyboard>/downArrow");

            var system = asset.AddActionMap("System");
            system.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            system.AddAction("ResetRun", InputActionType.Button, "<Keyboard>/r");

            return asset;
        }
    }

    internal enum GameSessionState
    {
        Countdown,
        Playing,
        Paused,
        GameOver
    }

    internal readonly struct RunResult
    {
        public RunResult(int piecesSpawned, int rowsCleared, int score, float survivalTime, int phase, int seed, string reason)
        {
            PiecesSpawned = piecesSpawned;
            RowsCleared = rowsCleared;
            Score = score;
            SurvivalTime = survivalTime;
            Phase = phase;
            Seed = seed;
            Reason = reason;
        }

        public int PiecesSpawned { get; }
        public int RowsCleared { get; }
        public int Score { get; }
        public float SurvivalTime { get; }
        public int Phase { get; }
        public int Seed { get; }
        public string Reason { get; }
    }

    internal sealed class ScoreService
    {
        private const int SurvivalScorePerSecond = 10;
        private float _survivalScoreAccumulator;

        public int Score { get; private set; }
        public int RowsCleared { get; private set; }

        public void Reset()
        {
            Score = 0;
            RowsCleared = 0;
            _survivalScoreAccumulator = 0f;
        }

        public void AddSurvivalTime(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            _survivalScoreAccumulator += deltaTime;
            var wholeSeconds = Mathf.FloorToInt(_survivalScoreAccumulator);
            if (wholeSeconds <= 0)
                return;

            Score += wholeSeconds * SurvivalScorePerSecond;
            _survivalScoreAccumulator -= wholeSeconds;
        }

        public int AddRowsCleared(int rowCount)
        {
            if (rowCount <= 0)
                return 0;

            RowsCleared += rowCount;
            var points = rowCount switch
            {
                1 => 250,
                2 => 600,
                3 => 1000,
                _ => 1500
            };
            Score += points;
            return points;
        }

        public int AddBonusScore(int points)
        {
            if (points <= 0)
                return 0;

            Score += points;
            return points;
        }
    }

    internal sealed class GameSession
    {
        private readonly ScoreService _scoreService = new();
        private float _phaseDurationSeconds = 45f;

        public event Action<GameSessionState> StateChanged;
        public event Action<int> PhaseChanged;
        public event Action<RunResult> RunEnded;

        public GameSessionState State { get; private set; } = GameSessionState.Countdown;
        public int Score => _scoreService.Score;
        public int RowsCleared => _scoreService.RowsCleared;
        public float SurvivalTime { get; private set; }
        public int Phase { get; private set; } = 1;
        public float TimeUntilNextPhase { get; private set; } = 45f;
        public float CountdownRemaining { get; private set; }
        public RunResult LastResult { get; private set; }

        public void StartRun()
        {
            StartRun(_phaseDurationSeconds);
        }

        public void StartRun(float phaseDurationSeconds)
        {
            ResetRun(phaseDurationSeconds);
            SetState(GameSessionState.Playing);
        }

        public void StartCountdown(float countdownSeconds, float phaseDurationSeconds)
        {
            ResetRun(phaseDurationSeconds);
            CountdownRemaining = Mathf.Max(0f, countdownSeconds);
            SetState(GameSessionState.Countdown);
            if (CountdownRemaining <= 0f)
                SetState(GameSessionState.Playing);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            if (State == GameSessionState.Countdown)
            {
                CountdownRemaining = Mathf.Max(0f, CountdownRemaining - deltaTime);
                if (CountdownRemaining <= 0f)
                    SetState(GameSessionState.Playing);
                return;
            }

            if (State != GameSessionState.Playing)
                return;

            SurvivalTime += deltaTime;
            _scoreService.AddSurvivalTime(deltaTime);
            UpdatePhase();
        }

        public int AddRowsCleared(int rowCount)
        {
            if (State != GameSessionState.Playing)
                return 0;

            return _scoreService.AddRowsCleared(rowCount);
        }

        public int AddBonusScore(int points)
        {
            if (State != GameSessionState.Playing)
                return 0;

            return _scoreService.AddBonusScore(points);
        }

        public void Pause()
        {
            if (State == GameSessionState.Playing)
                SetState(GameSessionState.Paused);
        }

        public void Resume()
        {
            if (State == GameSessionState.Paused)
                SetState(GameSessionState.Playing);
        }

        public RunResult EndRun(string reason, int piecesSpawned, int seed)
        {
            if (State == GameSessionState.GameOver)
                return LastResult;

            LastResult = new RunResult(piecesSpawned, RowsCleared, Score, SurvivalTime, Phase, seed, reason);
            SetState(GameSessionState.GameOver);
            RunEnded?.Invoke(LastResult);
            return LastResult;
        }

        private void UpdatePhase()
        {
            var previousPhase = Phase;
            Phase = Mathf.FloorToInt(SurvivalTime / _phaseDurationSeconds) + 1;
            var nextPhaseAt = Phase * _phaseDurationSeconds;
            TimeUntilNextPhase = Mathf.Max(0f, nextPhaseAt - SurvivalTime);
            if (Phase != previousPhase)
                PhaseChanged?.Invoke(Phase);
        }

        private void ResetRun(float phaseDurationSeconds)
        {
            _scoreService.Reset();
            _phaseDurationSeconds = Mathf.Max(1f, phaseDurationSeconds);
            SurvivalTime = 0f;
            Phase = 1;
            TimeUntilNextPhase = _phaseDurationSeconds;
            CountdownRemaining = 0f;
            LastResult = default;
        }

        private void SetState(GameSessionState state)
        {
            if (State == state)
                return;

            State = state;
            StateChanged?.Invoke(State);
        }
    }
}
