using System;
using System.Collections;
using BlockEscape.Core;
using UnityEngine;

namespace BlockEscape.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1)] private int _maxHp = 3;
        [SerializeField, Min(0f)] private float _iFrameSeconds = 1.2f;
        [SerializeField, Min(0.01f)] private float _flashIntervalSeconds = 0.1f;
        [SerializeField] private SpriteRenderer _spriteRenderer = null;

        private Rigidbody2D _body;
        private Coroutine _iFrameRoutine;
        private bool _isInvulnerable;
        private bool _isDead;

        public event Action<int, int> HealthChanged;
        public event Action Died;

        public int CurrentHp { get; private set; }
        public int MaxHp => _maxHp;
        public bool IsDead => _isDead;
        public bool IsInvulnerable => _isInvulnerable;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            _maxHp = Mathf.Max(1, _maxHp);
            CurrentHp = _maxHp;
        }

        private void OnDisable()
        {
            StopIFrameRoutine();
            SetSpriteAlpha(1f);
        }

        public bool TakeDamage(DamageInfo damage)
        {
            if (_isDead || _isInvulnerable || damage.Amount <= 0)
                return false;

            CurrentHp = Mathf.Clamp(CurrentHp - damage.Amount, 0, _maxHp);
            if (_body != null && damage.Knockback != Vector2.zero)
                _body.linearVelocity = damage.Knockback;

            HealthChanged?.Invoke(CurrentHp, _maxHp);

            if (CurrentHp <= 0)
            {
                Die();
                return true;
            }

            StartIFrames();
            return true;
        }

        public void Heal(int amount)
        {
            if (_isDead || amount <= 0)
                return;

            var previous = CurrentHp;
            CurrentHp = Mathf.Clamp(CurrentHp + amount, 0, _maxHp);
            if (CurrentHp != previous)
                HealthChanged?.Invoke(CurrentHp, _maxHp);
        }

        public void ResetHealth()
        {
            _maxHp = Mathf.Max(1, _maxHp);
            StopIFrameRoutine();
            _isDead = false;
            CurrentHp = _maxHp;
            SetSpriteAlpha(1f);
            HealthChanged?.Invoke(CurrentHp, _maxHp);
        }

        private void Die()
        {
            if (_isDead)
                return;

            _isDead = true;
            StopIFrameRoutine();
            SetSpriteAlpha(1f);
            Died?.Invoke();
        }

        private void StartIFrames()
        {
            StopIFrameRoutine();
            _isInvulnerable = true;
            _iFrameRoutine = StartCoroutine(IFrameRoutine());
        }

        private IEnumerator IFrameRoutine()
        {
            for (var elapsed = 0f; elapsed < _iFrameSeconds; elapsed += _flashIntervalSeconds)
            {
                var alpha = Mathf.Approximately(GetSpriteAlpha(), 1f) ? 0.35f : 1f;
                SetSpriteAlpha(alpha);
                yield return new WaitForSeconds(_flashIntervalSeconds);
            }

            SetSpriteAlpha(1f);
            _isInvulnerable = false;
            _iFrameRoutine = null;
        }

        private void StopIFrameRoutine()
        {
            if (_iFrameRoutine != null)
                StopCoroutine(_iFrameRoutine);
            _iFrameRoutine = null;
            _isInvulnerable = false;
        }

        private float GetSpriteAlpha()
        {
            return _spriteRenderer != null ? _spriteRenderer.color.a : 1f;
        }

        private void SetSpriteAlpha(float alpha)
        {
            if (_spriteRenderer == null)
                return;

            var color = _spriteRenderer.color;
            color.a = alpha;
            _spriteRenderer.color = color;
        }
    }
}
