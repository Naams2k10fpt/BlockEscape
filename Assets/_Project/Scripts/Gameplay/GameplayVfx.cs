using UnityEngine;

namespace BlockEscape.Tetris
{
    public sealed class GameplayVfx : MonoBehaviour
    {
        private SpriteRenderer[] _particles;
        private Vector2[] _velocities;
        private float _duration;
        private float _elapsed;

        public static void Burst(Vector3 position, Color color, int count = 10, float duration = 0.45f)
        {
            var root = new GameObject("Gameplay VFX Burst");
            root.transform.position = position;
            root.AddComponent<GameplayVfx>().Initialize(color, Mathf.Clamp(count, 1, 24), duration);
        }

        private void Initialize(Color color, int count, float duration)
        {
            _duration = Mathf.Max(0.05f, duration);
            _particles = new SpriteRenderer[count];
            _velocities = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                var particle = new GameObject($"Particle {i + 1}");
                particle.transform.SetParent(transform, false);
                particle.transform.localScale = Vector3.one * Random.Range(0.08f, 0.2f);
                var renderer = particle.AddComponent<SpriteRenderer>();
                renderer.sprite = RuntimeVisuals.Square;
                renderer.color = color;
                renderer.sortingOrder = 80;
                _particles[i] = renderer;

                var angle = i * Mathf.PI * 2f / count + Random.Range(-0.18f, 0.18f);
                _velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(1.8f, 4.2f);
            }
        }

        private void Update()
        {
            var deltaTime = Time.unscaledDeltaTime;
            _elapsed += deltaTime;
            var normalized = Mathf.Clamp01(_elapsed / _duration);
            for (var i = 0; i < _particles.Length; i++)
            {
                if (_particles[i] == null)
                    continue;
                _particles[i].transform.localPosition += (Vector3)(_velocities[i] * deltaTime);
                var color = _particles[i].color;
                color.a = 1f - normalized;
                _particles[i].color = color;
            }

            if (_elapsed >= _duration)
                Destroy(gameObject);
        }
    }
}
