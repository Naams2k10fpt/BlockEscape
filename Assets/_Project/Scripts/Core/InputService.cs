using UnityEngine;
using UnityEngine.InputSystem;

namespace BlockEscape.Core
{
    [DefaultExecutionOrder(-1000)]
    public sealed class InputService : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _actions;

        private InputActionMap _tetrisMap;
        private InputActionMap _playerMap;
        private InputActionMap _systemMap;
        private bool _initialized;

        public static InputService Current { get; private set; }

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

            _systemMap.Enable();
            SetGameplayEnabled(true);
            _initialized = true;
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
}
