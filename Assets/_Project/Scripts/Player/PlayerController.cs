using BlockEscape.Core;
using UnityEngine;

namespace BlockEscape.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerConfig _config = null;
        [SerializeField] private Transform _groundCheck = null;
        [SerializeField] private LayerMask _groundMask;

        private Rigidbody2D _body;
        private CapsuleCollider2D _collider;
        private InputService _input;
        private float _moveInput;
        private float _lastGroundedTime = float.NegativeInfinity;
        private float _lastJumpPressedTime = float.NegativeInfinity;
        private bool _jumpReleasedThisFrame;

        public PlayerConfig Config => _config;
        public bool IsGrounded { get; private set; }
        public Vector2 Velocity => _body != null ? _body.linearVelocity : Vector2.zero;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CapsuleCollider2D>();
            _input = InputService.Current;

            if (_config == null)
                _config = Resources.Load<PlayerConfig>("PlayerConfig");

            if (_config != null)
                _config.Sanitize();

            if (_groundMask.value == 0)
                _groundMask = LayerMask.GetMask("World");

            _body.freezeRotation = true;
        }

        private void OnEnable()
        {
            _input = InputService.Current;
            _lastGroundedTime = float.NegativeInfinity;
            _lastJumpPressedTime = float.NegativeInfinity;
        }

        private void Update()
        {
            if (_config == null)
                return;

            _input ??= InputService.Current;
            if (!CanReadGameplayInput())
            {
                _moveInput = 0f;
                _jumpReleasedThisFrame = false;
                return;
            }

            _moveInput = Mathf.Clamp(_input.PlayerMove.ReadValue<float>(), -1f, 1f);

            if (_input.PlayerJump.WasPressedThisFrame())
                _lastJumpPressedTime = Time.time;

            _jumpReleasedThisFrame = _input.PlayerJump.WasReleasedThisFrame();
        }

        private void FixedUpdate()
        {
            if (_config == null)
                return;

            UpdateGroundedState();

            var velocity = _body.linearVelocity;
            velocity.x = _moveInput * _config.moveSpeed;

            if (ShouldConsumeJump())
            {
                velocity.y = _config.jumpVelocity;
                _lastJumpPressedTime = float.NegativeInfinity;
                _lastGroundedTime = float.NegativeInfinity;
                IsGrounded = false;
            }
            else if (_jumpReleasedThisFrame && velocity.y > 0f)
            {
                velocity.y *= _config.variableJumpMultiplier;
            }

            if (velocity.y < -_config.maxFallSpeed)
                velocity.y = -_config.maxFallSpeed;

            _body.linearVelocity = velocity;
            _jumpReleasedThisFrame = false;
        }

        private bool CanReadGameplayInput()
        {
            return _input != null && _input.GameplayEnabled;
        }

        private bool ShouldConsumeJump()
        {
            var hasBufferedJump = Time.time - _lastJumpPressedTime <= _config.jumpBufferTime;
            var hasCoyoteTime = Time.time - _lastGroundedTime <= _config.coyoteTime;
            return hasBufferedJump && hasCoyoteTime;
        }

        private void UpdateGroundedState()
        {
            var center = _groundCheck != null
                ? (Vector2)_groundCheck.position
                : (Vector2)transform.position + Vector2.up * _config.groundCheckOffsetY;

            IsGrounded = Physics2D.OverlapBox(center, _config.groundCheckSize, 0f, _groundMask) != null;
            if (IsGrounded)
                _lastGroundedTime = Time.time;
        }

        private void OnDrawGizmosSelected()
        {
            if (_config == null)
                return;

            var center = _groundCheck != null
                ? _groundCheck.position
                : transform.position + Vector3.up * _config.groundCheckOffsetY;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(center, _config.groundCheckSize);
        }
    }
}
