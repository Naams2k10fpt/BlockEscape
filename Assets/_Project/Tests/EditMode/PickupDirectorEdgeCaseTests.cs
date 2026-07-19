using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BlockEscape.Core;
using BlockEscape.Player;
using NUnit.Framework;
using UnityEngine;

namespace BlockEscape.Tetris.Tests
{
    public sealed class PickupDirectorEdgeCaseTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<UnityEngine.Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (var i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void Initialize_CreatesFixedInactiveKinematicPool()
        {
            var harness = CreateHarness(seed: 11);

            var pickups = GetPooledPickups(harness.Director);

            Assert.That(pickups, Has.Length.EqualTo(2));
            Assert.That(harness.Director.ActiveCount, Is.Zero);
            Assert.That(pickups.All(pickup => !pickup.gameObject.activeSelf), Is.True);
            foreach (var pickup in pickups)
            {
                var body = pickup.GetComponent<Rigidbody2D>();
                Assert.That(body, Is.Not.Null);
                Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
                Assert.That(body.gravityScale, Is.Zero);
                Assert.That(body.freezeRotation, Is.True);
                Assert.That(pickup.isTrigger, Is.True);
            }
        }

        [Test]
        public void TrySpawnNow_StopsAtCapacityAndResetReusesPool()
        {
            var harness = CreateHarness(seed: 23);
            var poolBeforeReset = GetPooledPickups(harness.Director)
                .Select(pickup => pickup.gameObject)
                .ToArray();

            Assert.That(harness.Director.TrySpawnNow(), Is.True);
            Assert.That(harness.Director.TrySpawnNow(), Is.True);
            Assert.That(harness.Director.ActiveCount, Is.EqualTo(2));
            Assert.That(harness.Director.TrySpawnNow(), Is.False, "The two-item pool is also the active pickup limit.");

            harness.Director.ResetDirector(23);

            Assert.That(harness.Director.ActiveCount, Is.Zero);
            Assert.That(poolBeforeReset.All(item => !item.activeSelf), Is.True);
            Assert.That(harness.Director.TrySpawnNow(), Is.True);
            var activeAfterReset = GetPooledPickups(harness.Director)
                .Single(pickup => pickup.gameObject.activeSelf)
                .gameObject;
            CollectionAssert.Contains(poolBeforeReset, activeAfterReset);
        }

        [Test]
        public void ResetDirector_UsesDeterministicFirstDelayAndRunningGate()
        {
            var harness = CreateHarness(seed: 101);
            var timerField = RequireField(typeof(PickupDirector), "_spawnTimer");
            var updateMethod = RequireMethod(typeof(PickupDirector), "Update");

            harness.Director.ResetDirector(700);
            var firstDelay = (float)timerField.GetValue(harness.Director);
            harness.Director.ResetDirector(700);
            var repeatedDelay = (float)timerField.GetValue(harness.Director);

            Assert.That(firstDelay, Is.InRange(12f, 18f));
            Assert.That(repeatedDelay, Is.EqualTo(firstDelay).Within(0.000001f));

            timerField.SetValue(harness.Director, -1f);
            updateMethod.Invoke(harness.Director, null);
            Assert.That(harness.Director.ActiveCount, Is.Zero, "A stopped director must not spawn pickups.");

            harness.Director.SetRunning(true);
            updateMethod.Invoke(harness.Director, null);
            Assert.That(harness.Director.ActiveCount, Is.EqualTo(1));
            Assert.That((float)timerField.GetValue(harness.Director), Is.InRange(18f, 28f));
        }

        [Test]
        public void Collect_ScoreAndJumpPickupsDeactivateAndPublishExactlyOnce()
        {
            var harness = CreateHarness(seed: 37);
            var receivedKinds = new List<PickupKind>();
            harness.Director.PickupCollected += receivedKinds.Add;

            Assert.That(harness.Director.TrySpawnNow(), Is.True);
            var firstItem = GetActiveItem(harness.Director);
            SetKind(firstItem, PickupKind.ScoreCrystal);

            Assert.That(InvokeCollect(harness.Director, firstItem), Is.True);
            Assert.That(InvokeCollect(harness.Director, firstItem), Is.False);
            Assert.That(harness.Director.ActiveCount, Is.Zero);

            Assert.That(harness.Director.TrySpawnNow(), Is.True);
            var secondItem = GetActiveItem(harness.Director);
            SetKind(secondItem, PickupKind.JumpBoost);

            Assert.That(InvokeCollect(harness.Director, secondItem), Is.True);
            Assert.That(harness.Director.ActiveCount, Is.Zero);
            CollectionAssert.AreEqual(
                new[] { PickupKind.ScoreCrystal, PickupKind.JumpBoost },
                receivedKinds);
        }

        [Test]
        public void HealthPickup_OnTriggerStayRetriesAfterPlayerLosesHp()
        {
            var playerLayer = LayerMask.NameToLayer("Player");
            Assert.That(playerLayer, Is.GreaterThanOrEqualTo(0), "The Player layer must exist for pickup collision tests.");

            var harness = CreateHarness(seed: 41);
            harness.PlayerObject.layer = playerLayer;
            var playerCollider = harness.PlayerObject.AddComponent<BoxCollider2D>();
            var collected = 0;
            harness.Director.PickupCollected += kind =>
            {
                Assert.That(kind, Is.EqualTo(PickupKind.HealthPack));
                collected++;
            };

            Assert.That(harness.Director.TrySpawnNow(), Is.True);
            var item = GetActiveItem(harness.Director);
            SetKind(item, PickupKind.HealthPack);
            var stayMethod = RequireMethod(GetPickupItemType(), "OnTriggerStay2D");

            stayMethod.Invoke(item, new object[] { playerCollider });
            Assert.That(((Component)item).gameObject.activeSelf, Is.True, "Full health must leave the pickup available.");
            Assert.That(collected, Is.Zero);

            Assert.That(
                harness.Health.TakeDamage(new DamageInfo(1, Vector2.zero, null, DamageType.Enemy)),
                Is.True);
            stayMethod.Invoke(item, new object[] { playerCollider });

            Assert.That(((Component)item).gameObject.activeSelf, Is.False);
            Assert.That(harness.Health.CurrentHp, Is.EqualTo(harness.Health.MaxHp));
            Assert.That(collected, Is.EqualTo(1));
        }

        [Test]
        public void HealthPickup_WithoutPlayerHealthRemainsAvailable()
        {
            var harness = CreateHarness(seed: 53, attachHealth: false);
            var collected = 0;
            harness.Director.PickupCollected += _ => collected++;

            Assert.That(harness.Director.TrySpawnNow(), Is.True);
            var item = GetActiveItem(harness.Director);
            SetKind(item, PickupKind.HealthPack);

            Assert.That(InvokeCollect(harness.Director, item), Is.False);
            Assert.That(((Component)item).gameObject.activeSelf, Is.True);
            Assert.That(harness.Director.ActiveCount, Is.EqualTo(1));
            Assert.That(collected, Is.Zero);
        }

        [Test]
        public void ValidateActivePickups_DeactivatesInsteadOfMovingPickupUpward()
        {
            var harness = CreateHarness(seed: 67);
            for (var x = 0; x < harness.Board.Width; x++)
                harness.Board.Model.SetOccupied(x, 2);

            Assert.That(harness.Director.TrySpawnNow(), Is.True);
            var item = GetActiveItem(harness.Director);
            var itemType = GetPickupItemType();
            var component = (Component)item;
            var supportX = (int)RequireProperty(itemType, "SupportX").GetValue(item);
            var oldLanding = (Vector3)RequireField(itemType, "_landingPosition").GetValue(item);
            component.transform.position = oldLanding;
            component.GetComponent<Rigidbody2D>().position = oldLanding;

            harness.Board.Model.SetOccupied(supportX, 4);
            RequireMethod(typeof(PickupDirector), "ValidateActivePickups").Invoke(harness.Director, null);

            Assert.That(component.gameObject.activeSelf, Is.False);
            Assert.That(harness.Director.ActiveCount, Is.Zero);
        }

        private PickupHarness CreateHarness(int seed, bool attachHealth = true)
        {
            var config = Track(ScriptableObject.CreateInstance<TetrisBalanceConfig>());
            config.boardWidth = 4;
            config.boardHeight = 8;
            config.dangerStartRow = 7;

            var boardObject = Track(new GameObject("Pickup Edge Board"));
            var board = boardObject.AddComponent<BlockBoard>();
            board.Initialize(config);

            var playerObject = Track(new GameObject("Pickup Edge Player"));
            playerObject.AddComponent<Rigidbody2D>();
            PlayerHealth health = null;
            if (attachHealth)
            {
                health = playerObject.AddComponent<PlayerHealth>();
                RequireMethod(typeof(PlayerHealth), "Awake").Invoke(health, null);
            }

            var directorObject = Track(new GameObject("Pickup Edge Director"));
            var director = directorObject.AddComponent<PickupDirector>();
            director.Initialize(board, health, seed);
            return new PickupHarness(board, playerObject, health, director);
        }

        private static BoxCollider2D[] GetPooledPickups(PickupDirector director)
        {
            return director.GetComponentsInChildren<BoxCollider2D>(true);
        }

        private static object GetActiveItem(PickupDirector director)
        {
            var itemType = GetPickupItemType();
            var activeCollider = GetPooledPickups(director).Single(pickup => pickup.gameObject.activeSelf);
            var item = activeCollider.GetComponent(itemType);
            Assert.That(item, Is.Not.Null);
            return item;
        }

        private static bool InvokeCollect(PickupDirector director, object item)
        {
            return (bool)RequireMethod(typeof(PickupDirector), "Collect").Invoke(director, new[] { item });
        }

        private static void SetKind(object item, PickupKind kind)
        {
            RequireField(GetPickupItemType(), "<Kind>k__BackingField").SetValue(item, kind);
        }

        private static Type GetPickupItemType()
        {
            var itemType = typeof(PickupDirector).Assembly.GetType("BlockEscape.Tetris.PickupItem");
            Assert.That(itemType, Is.Not.Null);
            return itemType;
        }

        private static FieldInfo RequireField(Type type, string name)
        {
            var field = type.GetField(name, PrivateInstance);
            Assert.That(field, Is.Not.Null, $"Expected {type.Name}.{name} field to exist.");
            return field;
        }

        private static MethodInfo RequireMethod(Type type, string name)
        {
            var method = type.GetMethod(name, PrivateInstance);
            Assert.That(method, Is.Not.Null, $"Expected {type.Name}.{name} method to exist.");
            return method;
        }

        private static PropertyInfo RequireProperty(Type type, string name)
        {
            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"Expected {type.Name}.{name} property to exist.");
            return property;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            _createdObjects.Add(value);
            return value;
        }

        private sealed class PickupHarness
        {
            public PickupHarness(BlockBoard board, GameObject playerObject, PlayerHealth health, PickupDirector director)
            {
                Board = board;
                PlayerObject = playerObject;
                Health = health;
                Director = director;
            }

            public BlockBoard Board { get; }
            public GameObject PlayerObject { get; }
            public PlayerHealth Health { get; }
            public PickupDirector Director { get; }
        }
    }
}
