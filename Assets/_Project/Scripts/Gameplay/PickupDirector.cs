using System;
using System.Collections.Generic;
using BlockEscape.Player;
using UnityEngine;

namespace BlockEscape.Tetris
{
    public enum PickupKind
    {
        ScoreCrystal,
        JumpBoost,
        HealthPack
    }

    public sealed class PickupDirector : MonoBehaviour
    {
        private const int MaxActivePickups = 2;
        private const float FirstSpawnDelayMin = 12f;
        private const float FirstSpawnDelayMax = 18f;
        private const float SpawnIntervalMin = 18f;
        private const float SpawnIntervalMax = 28f;
        private const float SpawnRetryDelay = 1f;
        private const float LifetimeAfterLandingMin = 10f;
        private const float LifetimeAfterLandingMax = 15f;
        private const int ScoreCrystalWeight = 45;
        private const int JumpBoostWeight = 35;
        private const int HealthPackWeight = 20;

        private readonly List<PickupItem> _pool = new(MaxActivePickups);
        private readonly List<SpawnSlot> _spawnSlots = new();
        private BlockBoard _board;
        private PlayerHealth _playerHealth;
        private System.Random _random;
        private bool _running;
        private float _spawnTimer;

        public event Action<PickupKind> PickupCollected;

        public int ActiveCount
        {
            get
            {
                var count = 0;
                foreach (var item in _pool)
                    if (item.IsActive) count++;
                return count;
            }
        }

        public void Initialize(BlockBoard board, PlayerHealth playerHealth, int seed)
        {
            _board = board;
            _playerHealth = playerHealth;
            EnsurePool();
            ResetDirector(seed);
        }

        public void SetRunning(bool running)
        {
            _running = running;
        }

        public void ResetDirector(int seed)
        {
            _random = new System.Random(seed);
            _spawnTimer = NextDelay(FirstSpawnDelayMin, FirstSpawnDelayMax);
            foreach (var item in _pool)
                item.Deactivate();
        }

        public bool TrySpawnNow()
        {
            if (_board == null || _board.Model == null || _board.IsResolving || _board.IsOverflowed || ActiveCount >= MaxActivePickups)
                return false;

            BuildSpawnSlots();
            if (_spawnSlots.Count == 0)
                return false;

            var item = GetInactiveItem();
            if (item == null)
                return false;

            var slot = _spawnSlots[_random.Next(_spawnSlots.Count)];
            item.Activate(
                ChooseNextKind(),
                slot.spawnPosition,
                slot.landingPosition,
                slot.x,
                slot.supportRow,
                NextDelay(LifetimeAfterLandingMin, LifetimeAfterLandingMax));
            _spawnTimer = NextDelay(SpawnIntervalMin, SpawnIntervalMax);
            return true;
        }

        private void Update()
        {
            ValidateActivePickups();
            if (!_running || _board == null)
                return;

            if (ActiveCount >= MaxActivePickups)
                return;

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer > 0f)
                return;

            if (!TrySpawnNow())
                _spawnTimer = SpawnRetryDelay;
        }

        private void EnsurePool()
        {
            while (_pool.Count < MaxActivePickups)
            {
                var index = _pool.Count + 1;
                var gameObject = RuntimeVisuals.CreateQuad(
                    $"Pickup {index}",
                    transform,
                    Vector3.zero,
                    new Vector2(0.72f, 0.72f),
                    Color.white,
                    31);
                var pickupLayer = LayerMask.NameToLayer("Pickup");
                gameObject.layer = pickupLayer >= 0 ? pickupLayer : 0;
                var collider = gameObject.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                var body = gameObject.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.freezeRotation = true;

                var auraObject = new GameObject("Aura");
                auraObject.transform.SetParent(gameObject.transform, false);
                var aura = auraObject.AddComponent<SpriteRenderer>();
                aura.sortingOrder = 30;

                var labelObject = new GameObject("Label");
                labelObject.transform.SetParent(gameObject.transform, false);
                labelObject.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                var label = labelObject.AddComponent<TextMesh>();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = 32;
                label.characterSize = 0.11f;
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.color = Color.white;
                labelObject.GetComponent<MeshRenderer>().sortingOrder = 32;

                var item = gameObject.AddComponent<PickupItem>();
                item.Initialize(this, gameObject.GetComponent<SpriteRenderer>(), aura, label, body);
                item.Deactivate();
                _pool.Add(item);
            }
        }

        private void BuildSpawnSlots()
        {
            _spawnSlots.Clear();
            Physics2D.SyncTransforms();
            var blockingMask = LayerMask.GetMask("World", "FallingBlock", "Player", "Pickup");
            for (var x = 0; x < _board.Width; x++)
            {
                if (_pool.Exists(item => item.IsActive && item.SupportX == x))
                    continue;

                if (!TryGetSurface(x, out var supportRow, out var landingPosition))
                    continue;

                if (blockingMask != 0 && Physics2D.OverlapBox(landingPosition, new Vector2(0.7f, 0.7f), 0f, blockingMask) != null)
                    continue;

                var spawnPosition = _board.WorldForCell(new Vector2Int(x, _board.Height - 1));
                _spawnSlots.Add(new SpawnSlot(x, supportRow, spawnPosition, landingPosition));
            }
        }

        private bool TryGetSurface(int x, out int supportRow, out Vector3 landingPosition)
        {
            supportRow = -1;
            landingPosition = default;
            if (_board == null || _board.Model == null || x < 0 || x >= _board.Width)
                return false;

            for (var row = _board.Height - 1; row >= 0; row--)
            {
                if (!_board.Model.IsOccupied(x, row))
                    continue;

                supportRow = row;
                break;
            }

            if (!IsSurfaceValid(x, supportRow))
                return false;

            landingPosition = _board.WorldForCell(new Vector2Int(x, supportRow + 1));
            return true;
        }

        private bool IsSurfaceValid(int x, int supportRow)
        {
            if (_board == null || _board.Model == null || x < 0 || x >= _board.Width || supportRow < -1 || supportRow > _board.Height - 3)
                return false;
            if (supportRow >= 0 && !_board.Model.IsOccupied(x, supportRow))
                return false;
            return !_board.Model.IsOccupied(x, supportRow + 1) && !_board.Model.IsOccupied(x, supportRow + 2);
        }

        private void ValidateActivePickups()
        {
            foreach (var item in _pool)
            {
                if (!item.IsActive || IsSurfaceValid(item.SupportX, item.SupportRow))
                    continue;

                if (TryGetSurface(item.SupportX, out var supportRow, out var landingPosition) &&
                    landingPosition.y <= item.transform.position.y + 0.01f)
                {
                    item.RetargetLanding(landingPosition, supportRow);
                    continue;
                }

                item.Deactivate();
            }
        }

        private PickupKind ChooseNextKind()
        {
            var canSpawnHealth = _playerHealth != null &&
                                 !_playerHealth.IsDead &&
                                 _playerHealth.CurrentHp < _playerHealth.MaxHp;
            var totalWeight = ScoreCrystalWeight + JumpBoostWeight + (canSpawnHealth ? HealthPackWeight : 0);
            var roll = _random.Next(totalWeight);
            if (roll < ScoreCrystalWeight)
                return PickupKind.ScoreCrystal;
            if (roll < ScoreCrystalWeight + JumpBoostWeight)
                return PickupKind.JumpBoost;

            return PickupKind.HealthPack;
        }

        private float NextDelay(float minimum, float maximum)
        {
            if (_random == null)
                _random = new System.Random(0);
            return Mathf.Lerp(minimum, maximum, (float)_random.NextDouble());
        }

        private PickupItem GetInactiveItem()
        {
            foreach (var item in _pool)
                if (!item.IsActive) return item;
            return null;
        }

        internal bool Collect(PickupItem item)
        {
            if (item == null || !item.IsActive)
                return false;

            var kind = item.Kind;
            if (kind == PickupKind.HealthPack && (_playerHealth == null || !_playerHealth.TryHeal(1)))
                return false;

            item.Deactivate();
            PickupCollected?.Invoke(kind);
            return true;
        }

        internal void Expire(PickupItem item)
        {
            if (item == null || !item.IsActive)
                return;

            item.Deactivate();
        }

        private readonly struct SpawnSlot
        {
            public readonly int x;
            public readonly int supportRow;
            public readonly Vector3 spawnPosition;
            public readonly Vector3 landingPosition;

            public SpawnSlot(int x, int supportRow, Vector3 spawnPosition, Vector3 landingPosition)
            {
                this.x = x;
                this.supportRow = supportRow;
                this.spawnPosition = spawnPosition;
                this.landingPosition = landingPosition;
            }
        }
    }

    internal sealed class PickupItem : MonoBehaviour
    {
        private const float FallSpeed = 5f;

        private PickupDirector _owner;
        private SpriteRenderer _renderer;
        private SpriteRenderer _aura;
        private TextMesh _label;
        private Rigidbody2D _body;
        private bool _collected;
        private Vector3 _landingPosition;
        private float _landedAt;
        private float _lifetimeAfterLanding;

        public PickupKind Kind { get; private set; }
        public int SupportX { get; private set; }
        public int SupportRow { get; private set; }
        public bool IsActive => gameObject.activeSelf;

        public void Initialize(
            PickupDirector owner,
            SpriteRenderer spriteRenderer,
            SpriteRenderer aura,
            TextMesh label,
            Rigidbody2D body)
        {
            _owner = owner;
            _renderer = spriteRenderer;
            _aura = aura;
            _label = label;
            _body = body;
        }

        public void Activate(
            PickupKind kind,
            Vector3 spawnPosition,
            Vector3 landingPosition,
            int supportX,
            int supportRow,
            float lifetimeAfterLanding)
        {
            Kind = kind;
            SupportX = supportX;
            SupportRow = supportRow;
            _collected = false;
            _landingPosition = landingPosition;
            _landedAt = float.PositiveInfinity;
            _lifetimeAfterLanding = Mathf.Max(0f, lifetimeAfterLanding);
            transform.position = spawnPosition;
            gameObject.SetActive(true);
            if (_body != null)
                _body.position = spawnPosition;
            gameObject.name = $"Pickup {kind}";
            var sprite = Resources.Load<Sprite>(GetSpritePath(kind));
            _renderer.sprite = sprite != null ? sprite : RuntimeVisuals.Square;
            _renderer.color = sprite != null ? Color.white : GetColor(kind);
            _aura.sprite = _renderer.sprite;
            _aura.color = WithAlpha(GetColor(kind), 0.2f);
            _label.gameObject.SetActive(sprite == null);
            _label.text = GetLabel(kind);
        }

        private void FixedUpdate()
        {
            var currentPosition = _body != null ? _body.position : (Vector2)transform.position;
            var nextPosition = Vector2.MoveTowards(currentPosition, _landingPosition, FallSpeed * Time.fixedDeltaTime);
            if (_body != null)
                _body.MovePosition(nextPosition);
            else
                transform.position = nextPosition;

            if (nextPosition != (Vector2)_landingPosition || !float.IsPositiveInfinity(_landedAt))
                return;

            _landedAt = Time.time;
        }

        private void Update()
        {
            if (!float.IsPositiveInfinity(_landedAt) && Time.time - _landedAt >= _lifetimeAfterLanding)
                _owner.Expire(this);

            var pulse = 1.12f + Mathf.Sin(Time.time * 5f) * 0.08f;
            _aura.transform.localScale = Vector3.one * pulse;
            _aura.color = WithAlpha(GetColor(Kind), 0.12f + (pulse - 1.04f) * 0.5f);
        }

        public void RetargetLanding(Vector3 landingPosition, int supportRow)
        {
            _landingPosition = landingPosition;
            SupportRow = supportRow;
            _landedAt = float.PositiveInfinity;
        }

        public void Deactivate()
        {
            if (_body != null)
                _body.linearVelocity = Vector2.zero;
            gameObject.SetActive(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryCollect(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryCollect(other);
        }

        private void TryCollect(Collider2D other)
        {
            if (_collected || other == null || other.gameObject.layer != LayerMask.NameToLayer("Player"))
                return;

            _collected = _owner.Collect(this);
        }

        private static Color GetColor(PickupKind kind)
        {
            return kind switch
            {
                PickupKind.ScoreCrystal => new Color(0.2f, 0.85f, 1f),
                PickupKind.HealthPack => new Color(1f, 0.2f, 0.3f),
                _ => new Color(1f, 0.75f, 0.15f)
            };
        }

        private static string GetSpritePath(PickupKind kind)
        {
            return kind switch
            {
                PickupKind.ScoreCrystal => "Art/PickupScoreCrystal",
                PickupKind.HealthPack => "Art/PickupHealthPack",
                _ => "Art/PickupJumpBoost"
            };
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static string GetLabel(PickupKind kind)
        {
            return kind switch
            {
                PickupKind.ScoreCrystal => "100",
                PickupKind.HealthPack => "HP",
                _ => "JUMP"
            };
        }
    }
}
