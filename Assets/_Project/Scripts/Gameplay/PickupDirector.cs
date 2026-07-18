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
        private const float FirstSpawnDelay = 1f;
        private const float SpawnInterval = 8f;

        private readonly List<PickupItem> _pool = new(MaxActivePickups);
        private readonly List<SpawnSlot> _spawnSlots = new();
        private BlockBoard _board;
        private PlayerHealth _playerHealth;
        private System.Random _random;
        private bool _running;
        private float _spawnTimer;
        private int _nextKindIndex;

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
            ConfigureLayerCollisions();
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
            _spawnTimer = FirstSpawnDelay;
            _nextKindIndex = 0;
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
            item.Activate(ChooseNextKind(), slot.spawnPosition, slot.landingPosition, slot.x, slot.supportRow);
            return true;
        }

        private void Update()
        {
            ValidateActivePickups();
            if (!_running || _board == null)
                return;

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer > 0f || ActiveCount >= MaxActivePickups)
                return;

            _spawnTimer = TrySpawnNow() ? SpawnInterval : 1f;
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
                item.Initialize(this, gameObject.GetComponent<SpriteRenderer>(), label);
                item.Deactivate();
                _pool.Add(item);
            }
        }

        private static void ConfigureLayerCollisions()
        {
            var pickupLayer = LayerMask.NameToLayer("Pickup");
            var playerLayer = LayerMask.NameToLayer("Player");
            if (pickupLayer < 0 || playerLayer < 0)
                return;

            for (var layer = 0; layer < 32; layer++)
                Physics2D.IgnoreLayerCollision(pickupLayer, layer, layer != playerLayer);
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

                var supportRow = -1;
                for (var row = _board.Height - 1; row >= 0; row--)
                {
                    if (!_board.Model.IsOccupied(x, row))
                        continue;

                    supportRow = row;
                    break;
                }

                if (!IsSurfaceValid(x, supportRow))
                    continue;

                var landingPosition = _board.WorldForCell(new Vector2Int(x, supportRow + 1));
                if (blockingMask != 0 && Physics2D.OverlapBox(landingPosition, new Vector2(0.7f, 0.7f), 0f, blockingMask) != null)
                    continue;

                var spawnPosition = _board.WorldForCell(new Vector2Int(x, _board.Height - 1));
                _spawnSlots.Add(new SpawnSlot(x, supportRow, spawnPosition, landingPosition));
            }
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
                item.Deactivate();
                _spawnTimer = Mathf.Min(_spawnTimer, 1f);
            }
        }

        private PickupKind ChooseNextKind()
        {
            for (var i = 0; i < 3; i++)
            {
                var kind = (PickupKind)_nextKindIndex;
                _nextKindIndex = (_nextKindIndex + 1) % 3;
                if (kind != PickupKind.HealthPack || _playerHealth != null && _playerHealth.CurrentHp < _playerHealth.MaxHp)
                    return kind;
            }

            return PickupKind.ScoreCrystal;
        }

        private PickupItem GetInactiveItem()
        {
            foreach (var item in _pool)
                if (!item.IsActive) return item;
            return null;
        }

        internal void Collect(PickupItem item)
        {
            if (item == null || !item.IsActive)
                return;

            var kind = item.Kind;
            item.Deactivate();
            _spawnTimer = Mathf.Min(_spawnTimer, 1f);
            PickupCollected?.Invoke(kind);
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
        private TextMesh _label;
        private bool _collected;
        private Vector3 _landingPosition;

        public PickupKind Kind { get; private set; }
        public int SupportX { get; private set; }
        public int SupportRow { get; private set; }
        public bool IsActive => gameObject.activeSelf;

        public void Initialize(PickupDirector owner, SpriteRenderer spriteRenderer, TextMesh label)
        {
            _owner = owner;
            _renderer = spriteRenderer;
            _label = label;
        }

        public void Activate(PickupKind kind, Vector3 spawnPosition, Vector3 landingPosition, int supportX, int supportRow)
        {
            Kind = kind;
            SupportX = supportX;
            SupportRow = supportRow;
            _collected = false;
            _landingPosition = landingPosition;
            transform.position = spawnPosition;
            gameObject.name = $"Pickup {kind}";
            _renderer.color = GetColor(kind);
            _label.text = GetLabel(kind);
            gameObject.SetActive(true);
        }

        private void Update()
        {
            transform.position = Vector3.MoveTowards(transform.position, _landingPosition, FallSpeed * Time.deltaTime);
        }

        public void Deactivate()
        {
            gameObject.SetActive(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_collected || other == null || other.gameObject.layer != LayerMask.NameToLayer("Player"))
                return;

            _collected = true;
            _owner.Collect(this);
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
