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
        private float _lastJumpStartedTime = float.NegativeInfinity;
        private bool _jumpReleasedThisFrame;
        private bool _wantsCrouch;
        private float _jumpBoostMultiplier = 1f;
        private float _jumpBoostUntil = float.NegativeInfinity;

        private const float BlockBounceJumpWindow = 0.35f;
        private const float GroundedVerticalJitterLimit = 2f;

        public PlayerConfig Config => _config;
        public bool IsGrounded { get; private set; }
        public bool IsCrouching { get; private set; }
        public Vector2 Velocity => _body != null ? _body.linearVelocity : Vector2.zero;
        public bool HasRecentJumpForBlockBounce => !IsGrounded && Time.time - _lastJumpStartedTime <= BlockBounceJumpWindow;
        public float JumpBoostSecondsRemaining => Mathf.Max(0f, _jumpBoostUntil - Time.time);
        public bool JumpBoostActive => JumpBoostSecondsRemaining > 0f;
        public float EffectiveJumpVelocity => _config == null ? 0f : _config.jumpVelocity * (JumpBoostActive ? _jumpBoostMultiplier : 1f);

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
            _collider.sharedMaterial = PhysicsMaterialLibrary.Frictionless;
            if (_config != null)
            {
                _body.gravityScale = _config.gravityScale;
                ApplyCollider(_config.standingColliderSize, _config.standingColliderOffset);
            }
        }

        private void OnEnable()
        {
            _input = InputService.Current;
            _lastGroundedTime = float.NegativeInfinity;
            _lastJumpPressedTime = float.NegativeInfinity;
            _lastJumpStartedTime = float.NegativeInfinity;
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
                _wantsCrouch = false;
                return;
            }

            _moveInput = Mathf.Clamp(_input.PlayerMove.ReadValue<float>(), -1f, 1f);
            _wantsCrouch = _input.PlayerCrouch.IsPressed();

            if (_input.PlayerJump.WasPressedThisFrame())
                _lastJumpPressedTime = Time.time;

            _jumpReleasedThisFrame = _input.PlayerJump.WasReleasedThisFrame();
        }

        private void FixedUpdate()
        {
            if (_config == null)
                return;

            UpdateGroundedState();
            UpdateCrouchState();

            var velocity = _body.linearVelocity;
            velocity.x = _moveInput * _config.moveSpeed;
            var consumeJump = ShouldConsumeJump();

            if (consumeJump)
            {
                velocity.y = EffectiveJumpVelocity;
                _lastJumpPressedTime = float.NegativeInfinity;
                _lastJumpStartedTime = Time.time;
                _lastGroundedTime = float.NegativeInfinity;
                IsGrounded = false;
            }
            else if (_jumpReleasedThisFrame && velocity.y > 0f)
            {
                velocity.y *= _config.variableJumpMultiplier;
            }
            else if (IsGrounded && velocity.y <= GroundedVerticalJitterLimit)
            {
                velocity.y = 0f;
            }

            if (velocity.y < -_config.maxFallSpeed)
                velocity.y = -_config.maxFallSpeed;

            _body.linearVelocity = velocity;
            _jumpReleasedThisFrame = false;
        }

        public void ApplyJumpBoost(float multiplier, float durationSeconds)
        {
            _jumpBoostMultiplier = Mathf.Max(1f, multiplier);
            _jumpBoostUntil = Time.time + Mathf.Max(0f, durationSeconds);
        }

        public void ClearJumpBoost()
        {
            _jumpBoostMultiplier = 1f;
            _jumpBoostUntil = float.NegativeInfinity;
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

        private void UpdateCrouchState()
        {
            if (_wantsCrouch)
            {
                SetCrouching(true);
                return;
            }

            if (IsCrouching && !CanStand())
                return;

            SetCrouching(false);
        }

        private bool CanStand()
        {
            var crouchTop = _config.crouchColliderOffset.y + _config.crouchColliderSize.y * 0.5f;
            var standingTop = _config.standingColliderOffset.y + _config.standingColliderSize.y * 0.5f;
            var headroomHeight = Mathf.Max(0.02f, standingTop - crouchTop);
            var centerY = crouchTop + headroomHeight * 0.5f;
            var center = (Vector2)transform.position + Vector2.up * centerY;
            var size = new Vector2(_config.standingColliderSize.x, headroomHeight);
            var hit = Physics2D.OverlapBox(center, size, 0f, _groundMask);
            return hit == null || hit.attachedRigidbody == _body;
        }

        private void SetCrouching(bool crouching)
        {
            if (IsCrouching == crouching)
                return;

            IsCrouching = crouching;
            if (crouching)
                ApplyCollider(_config.crouchColliderSize, _config.crouchColliderOffset);
            else
                ApplyCollider(_config.standingColliderSize, _config.standingColliderOffset);

        }

        private void ApplyCollider(Vector2 size, Vector2 offset)
        {
            if (_collider == null)
                return;

            _collider.size = size;
            _collider.offset = offset;
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
