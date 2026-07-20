using UnityEngine;

namespace BlockEscape.Player
{
    public sealed class PlayerPresentation : MonoBehaviour
    {
        [SerializeField] private Transform _visual;
        [SerializeField] private SpriteRenderer _renderer;

        private PlayerController _controller;
        private PlayerHealth _health;
        private Rigidbody2D _body;
        private Vector3 _basePosition;
        private Vector3 _baseScale;
        private float _hurtUntil;
        private int _previousHp;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _health = GetComponent<PlayerHealth>();
            _body = GetComponent<Rigidbody2D>();
            if (_renderer == null)
                _renderer = GetComponentInChildren<SpriteRenderer>();
            if (_visual == null && _renderer != null)
                _visual = _renderer.transform;
            _basePosition = _visual != null ? _visual.localPosition : Vector3.zero;
            _baseScale = _visual != null ? _visual.localScale : Vector3.one;
            _previousHp = _health != null ? _health.CurrentHp : 0;
        }

        private void OnEnable()
        {
            if (_health != null)
                _health.HealthChanged += OnHealthChanged;
        }

        private void OnDisable()
        {
            if (_health != null)
                _health.HealthChanged -= OnHealthChanged;
        }

        private void LateUpdate()
        {
            if (_visual == null || _controller == null || _body == null)
                return;

            var scale = _baseScale;
            var position = _basePosition;
            var rotation = 0f;
            if (_health != null && _health.IsDead)
            {
                scale.y *= 0.35f;
                rotation = 90f;
            }
            else if (Time.time < _hurtUntil)
            {
                var shake = Mathf.Sin(Time.unscaledTime * 70f) * 0.08f;
                position.x += shake;
                scale *= 0.92f;
            }
            else if (_controller.IsCrouching)
            {
                const float crouchScale = 0.72f;
                scale.y *= crouchScale;
                scale.x *= 1.04f;
                if (_renderer.sprite != null)
                    position.y -= _renderer.sprite.bounds.size.y * _baseScale.y * (1f - crouchScale) * 0.5f;
            }
            else if (!_controller.IsGrounded)
            {
                scale.y *= _body.linearVelocity.y > 0f ? 1.08f : 0.92f;
                scale.x *= _body.linearVelocity.y > 0f ? 0.94f : 1.06f;
            }
            else if (Mathf.Abs(_body.linearVelocity.x) > 0.1f)
            {
                scale.y *= 1f + Mathf.Sin(Time.time * 18f) * 0.05f;
                _renderer.flipX = _body.linearVelocity.x < 0f;
            }
            else
            {
                scale.y *= 1f + Mathf.Sin(Time.time * 3f) * 0.015f;
            }

            var alpha = _renderer.color.a;
            _renderer.color = _controller.JumpBoostActive
                ? Color.Lerp(Color.white, new Color(0.3f, 0.95f, 1f), 0.3f + Mathf.Sin(Time.time * 8f) * 0.15f)
                : Color.white;
            _renderer.color = new Color(_renderer.color.r, _renderer.color.g, _renderer.color.b, alpha);
            _visual.localPosition = position;
            _visual.localScale = scale;
            _visual.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void OnHealthChanged(int current, int max)
        {
            if (current < _previousHp)
                _hurtUntil = Time.time + 0.25f;
            _previousHp = current;
        }
    }
}
