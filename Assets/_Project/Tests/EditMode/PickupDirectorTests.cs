using System.Reflection;
using BlockEscape.Core;
using BlockEscape.Player;
using NUnit.Framework;
using UnityEngine;

namespace BlockEscape.Tetris.Tests
{
    public sealed class PickupDirectorTests
    {
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void PickupDirector_SpawnsKinematicPickupsWithinConfiguredTimingAndLifetime()
        {
            var config = ScriptableObject.CreateInstance<TetrisBalanceConfig>();
            var boardObject = new GameObject("Pickup Board Test");
            var playerObject = new GameObject("Pickup Player Test");
            var directorObject = new GameObject("Pickup Director Test");
            var pickupLayer = LayerMask.NameToLayer("Pickup");
            var worldLayer = LayerMask.NameToLayer("World");
            var playerLayer = LayerMask.NameToLayer("Player");
            var ignoredWorldBefore = pickupLayer >= 0 && worldLayer >= 0 &&
                                     Physics2D.GetIgnoreLayerCollision(pickupLayer, worldLayer);
            var ignoredPlayerBefore = pickupLayer >= 0 && playerLayer >= 0 &&
                                      Physics2D.GetIgnoreLayerCollision(pickupLayer, playerLayer);
            try
            {
                boardObject.transform.position = new Vector3(-config.boardWidth * 0.5f, -10f);
                var board = boardObject.AddComponent<BlockBoard>();
                board.Initialize(config);
                var health = CreateHealth(playerObject);
                var director = directorObject.AddComponent<PickupDirector>();
                director.Initialize(board, health, 1234);

                var spawnTimer = typeof(PickupDirector).GetField("_spawnTimer", InstanceFields);
                Assert.That((float)spawnTimer.GetValue(director), Is.InRange(12f, 18f));
                Assert.That(director.TrySpawnNow(), Is.True);
                Assert.That((float)spawnTimer.GetValue(director), Is.InRange(18f, 28f));
                Assert.That(director.TrySpawnNow(), Is.True);
                Assert.That(director.TrySpawnNow(), Is.False);
                Assert.That(director.ActiveCount, Is.EqualTo(2));

                if (pickupLayer >= 0 && worldLayer >= 0)
                    Assert.That(Physics2D.GetIgnoreLayerCollision(pickupLayer, worldLayer), Is.EqualTo(ignoredWorldBefore));
                if (pickupLayer >= 0 && playerLayer >= 0)
                    Assert.That(Physics2D.GetIgnoreLayerCollision(pickupLayer, playerLayer), Is.EqualTo(ignoredPlayerBefore));

                var pickups = director.GetComponentsInChildren<BoxCollider2D>(false);
                Assert.That(pickups, Has.Length.EqualTo(2));
                foreach (var pickup in pickups)
                {
                    Assert.That(pickup.isTrigger, Is.True);
                    Assert.That(pickup.gameObject.layer, Is.EqualTo(pickupLayer));
                    Assert.That(pickup.transform.position.x, Is.InRange(board.transform.position.x, board.transform.position.x + board.Width));
                    Assert.That(pickup.transform.position.y,
                        Is.EqualTo(board.WorldForCell(new Vector2Int(0, board.Height - 1)).y).Within(0.001f));

                    var body = pickup.GetComponent<Rigidbody2D>();
                    Assert.That(body, Is.Not.Null);
                    Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
                    Assert.That(body.gravityScale, Is.Zero);
                    Assert.That(body.freezeRotation, Is.True);
                }

                var itemType = GetItemType();
                var item = pickups[0].GetComponent(itemType);
                var lifetime = (float)itemType.GetField("_lifetimeAfterLanding", InstanceFields).GetValue(item);
                Assert.That(lifetime, Is.InRange(10f, 15f));
                itemType.GetField("_landedAt", InstanceFields).SetValue(item, Time.time - lifetime - 0.01f);
                itemType.GetMethod("Update", InstanceFields).Invoke(item, null);

                Assert.That(pickups[0].gameObject.activeSelf, Is.False);
                Assert.That(director.ActiveCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(boardObject);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void PickupDirector_UsesWeightedKindsAndSkipsHealthAtFullHp()
        {
            var config = ScriptableObject.CreateInstance<TetrisBalanceConfig>();
            var boardObject = new GameObject("Pickup Weight Board");
            var playerObject = new GameObject("Pickup Weight Player");
            var directorObject = new GameObject("Pickup Weight Director");
            try
            {
                var board = boardObject.AddComponent<BlockBoard>();
                board.Initialize(config);
                var health = CreateHealth(playerObject);
                var director = directorObject.AddComponent<PickupDirector>();
                director.Initialize(board, health, 4321);
                var chooseKind = typeof(PickupDirector).GetMethod("ChooseNextKind", InstanceFields);

                for (var i = 0; i < 300; i++)
                    Assert.That((PickupKind)chooseKind.Invoke(director, null), Is.Not.EqualTo(PickupKind.HealthPack));

                Assert.That(health.TakeDamage(new DamageInfo(1, Vector2.zero, null, DamageType.Enemy)), Is.True);
                director.ResetDirector(4321);
                var counts = new int[3];
                for (var i = 0; i < 1000; i++)
                    counts[(int)(PickupKind)chooseKind.Invoke(director, null)]++;

                Assert.That(counts[(int)PickupKind.ScoreCrystal], Is.InRange(380, 520));
                Assert.That(counts[(int)PickupKind.JumpBoost], Is.InRange(280, 430));
                Assert.That(counts[(int)PickupKind.HealthPack], Is.InRange(130, 270));
            }
            finally
            {
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(boardObject);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void HealthPickup_RemainsActiveUntilTryHealSucceeds()
        {
            var config = ScriptableObject.CreateInstance<TetrisBalanceConfig>();
            var boardObject = new GameObject("Health Pickup Board");
            var playerObject = new GameObject("Health Pickup Player");
            var directorObject = new GameObject("Health Pickup Director");
            try
            {
                var board = boardObject.AddComponent<BlockBoard>();
                board.Initialize(config);
                var health = CreateHealth(playerObject);
                var director = directorObject.AddComponent<PickupDirector>();
                director.Initialize(board, health, 50);
                Assert.That(director.TrySpawnNow(), Is.True);

                var itemType = GetItemType();
                var item = director.GetComponentsInChildren<BoxCollider2D>(false)[0].GetComponent(itemType);
                itemType.GetField("<Kind>k__BackingField", InstanceFields).SetValue(item, PickupKind.HealthPack);
                var collect = typeof(PickupDirector).GetMethod("Collect", InstanceFields);
                var collectedCount = 0;
                director.PickupCollected += kind =>
                {
                    Assert.That(kind, Is.EqualTo(PickupKind.HealthPack));
                    collectedCount++;
                };

                Assert.That((bool)collect.Invoke(director, new[] { item }), Is.False);
                Assert.That(((Component)item).gameObject.activeSelf, Is.True);
                Assert.That(collectedCount, Is.Zero);

                Assert.That(health.TakeDamage(new DamageInfo(1, Vector2.zero, null, DamageType.Enemy)), Is.True);
                Assert.That((bool)collect.Invoke(director, new[] { item }), Is.True);
                Assert.That(((Component)item).gameObject.activeSelf, Is.False);
                Assert.That(health.CurrentHp, Is.EqualTo(health.MaxHp));
                Assert.That(collectedCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(boardObject);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void PickupDirector_RetargetsPickupWhenItsSupportIsDestroyed()
        {
            var config = ScriptableObject.CreateInstance<TetrisBalanceConfig>();
            var boardObject = new GameObject("Pickup Retarget Board");
            var playerObject = new GameObject("Pickup Retarget Player");
            var directorObject = new GameObject("Pickup Retarget Director");
            try
            {
                var board = boardObject.AddComponent<BlockBoard>();
                board.Initialize(config);
                for (var x = 0; x < board.Width; x++)
                    board.Model.SetOccupied(x, 2);

                var director = directorObject.AddComponent<PickupDirector>();
                director.Initialize(board, CreateHealth(playerObject), 99);
                Assert.That(director.TrySpawnNow(), Is.True);

                var itemType = GetItemType();
                var item = director.GetComponentsInChildren<BoxCollider2D>(false)[0].GetComponent(itemType);
                var supportX = (int)itemType.GetProperty("SupportX").GetValue(item);
                var supportRow = (int)itemType.GetProperty("SupportRow").GetValue(item);
                Assert.That(supportRow, Is.EqualTo(2));

                var landingField = itemType.GetField("_landingPosition", InstanceFields);
                var oldLanding = (Vector3)landingField.GetValue(item);
                ((Component)item).transform.position = oldLanding;
                ((Component)item).GetComponent<Rigidbody2D>().position = oldLanding;
                board.Model.SetOccupied(supportX, supportRow, false);

                typeof(PickupDirector).GetMethod("ValidateActivePickups", InstanceFields).Invoke(director, null);

                Assert.That(((Component)item).gameObject.activeSelf, Is.True);
                Assert.That((int)itemType.GetProperty("SupportRow").GetValue(item), Is.EqualTo(-1));
                Assert.That((Vector3)landingField.GetValue(item),
                    Is.EqualTo(board.WorldForCell(new Vector2Int(supportX, 0))));
                Assert.That(float.IsPositiveInfinity((float)itemType.GetField("_landedAt", InstanceFields).GetValue(item)), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(boardObject);
                Object.DestroyImmediate(config);
            }
        }

        private static System.Type GetItemType()
        {
            var itemType = typeof(PickupDirector).Assembly.GetType("BlockEscape.Tetris.PickupItem");
            Assert.That(itemType, Is.Not.Null);
            return itemType;
        }

        private static PlayerHealth CreateHealth(GameObject playerObject)
        {
            playerObject.AddComponent<Rigidbody2D>();
            var health = playerObject.AddComponent<PlayerHealth>();
            var awake = typeof(PlayerHealth).GetMethod("Awake", InstanceFields);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(health, null);
            return health;
        }
    }
}
