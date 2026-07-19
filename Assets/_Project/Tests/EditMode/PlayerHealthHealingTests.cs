using System.Collections.Generic;
using System.Reflection;
using BlockEscape.Core;
using BlockEscape.Player;
using NUnit.Framework;
using UnityEngine;

namespace BlockEscape.Tetris.Tests
{
    public sealed class PlayerHealthHealingTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<GameObject> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (var i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void TryHeal_LargeAmountClampsToMaxAndPublishesFinalValues()
        {
            var health = CreateHealth();
            Assert.That(
                health.TakeDamage(new DamageInfo(2, Vector2.zero, null, DamageType.Enemy)),
                Is.True);

            var notifications = new List<(int current, int maximum)>();
            health.HealthChanged += (current, maximum) => notifications.Add((current, maximum));

            Assert.That(health.TryHeal(99), Is.True);
            Assert.That(health.CurrentHp, Is.EqualTo(health.MaxHp));
            Assert.That(notifications, Has.Count.EqualTo(1));
            Assert.That(notifications[0].current, Is.EqualTo(health.MaxHp));
            Assert.That(notifications[0].maximum, Is.EqualTo(health.MaxHp));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(-100)]
        public void TryHeal_NonPositiveAmountIsRejectedWithoutNotification(int amount)
        {
            var health = CreateHealth();
            var notifications = 0;
            health.HealthChanged += (_, _) => notifications++;

            Assert.That(health.TryHeal(amount), Is.False);
            Assert.That(health.CurrentHp, Is.EqualTo(health.MaxHp));
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void TryHeal_AtFullHpIsRejectedWithoutNotification()
        {
            var health = CreateHealth();
            var notifications = 0;
            health.HealthChanged += (_, _) => notifications++;

            Assert.That(health.TryHeal(1), Is.False);
            Assert.That(health.CurrentHp, Is.EqualTo(health.MaxHp));
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void TryHeal_WhileDeadCannotReviveOrPublishHealthChanged()
        {
            var health = CreateHealth();
            Assert.That(
                health.TakeDamage(new DamageInfo(health.MaxHp, Vector2.zero, null, DamageType.Hazard)),
                Is.True);
            var notifications = 0;
            health.HealthChanged += (_, _) => notifications++;

            Assert.That(health.TryHeal(1), Is.False);
            Assert.That(health.CurrentHp, Is.Zero);
            Assert.That(health.IsDead, Is.True);
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void ResetHealth_AfterDeathRestoresHpAndPublishesOneNotification()
        {
            var health = CreateHealth();
            Assert.That(
                health.TakeDamage(new DamageInfo(health.MaxHp, Vector2.zero, null, DamageType.Crush)),
                Is.True);
            var notifications = new List<(int current, int maximum)>();
            health.HealthChanged += (current, maximum) => notifications.Add((current, maximum));

            health.ResetHealth();

            Assert.That(health.IsDead, Is.False);
            Assert.That(health.IsInvulnerable, Is.False);
            Assert.That(health.CurrentHp, Is.EqualTo(health.MaxHp));
            Assert.That(notifications, Has.Count.EqualTo(1));
            Assert.That(notifications[0], Is.EqualTo((health.MaxHp, health.MaxHp)));
            Assert.That(
                health.TakeDamage(new DamageInfo(1, Vector2.zero, null, DamageType.Enemy)),
                Is.True,
                "Reset health must make the player damageable again.");
        }

        [Test]
        public void ResetHealth_SanitizesInvalidMaxHpBeforePublishing()
        {
            var health = CreateHealth();
            var maxHpField = typeof(PlayerHealth).GetField("_maxHp", PrivateInstance);
            Assert.That(maxHpField, Is.Not.Null);
            maxHpField.SetValue(health, 0);
            var reportedCurrent = -1;
            var reportedMaximum = -1;
            health.HealthChanged += (current, maximum) =>
            {
                reportedCurrent = current;
                reportedMaximum = maximum;
            };

            health.ResetHealth();

            Assert.That(health.MaxHp, Is.EqualTo(1));
            Assert.That(health.CurrentHp, Is.EqualTo(1));
            Assert.That(reportedCurrent, Is.EqualTo(1));
            Assert.That(reportedMaximum, Is.EqualTo(1));
        }

        [Test]
        public void Disable_CancelsInvulnerabilityAndRestoresSpriteAlpha()
        {
            var health = CreateHealth(out var spriteRenderer);
            health.StartInvulnerability(10f);
            var color = spriteRenderer.color;
            color.a = 0.35f;
            spriteRenderer.color = color;

            var onDisable = typeof(PlayerHealth).GetMethod("OnDisable", PrivateInstance);
            Assert.That(onDisable, Is.Not.Null);
            onDisable.Invoke(health, null);

            Assert.That(health.IsInvulnerable, Is.False);
            Assert.That(spriteRenderer.color.a, Is.EqualTo(1f));
        }

        private PlayerHealth CreateHealth()
        {
            return CreateHealth(out _);
        }

        private PlayerHealth CreateHealth(out SpriteRenderer spriteRenderer)
        {
            var player = new GameObject("Player Health Healing Test");
            _createdObjects.Add(player);
            player.AddComponent<Rigidbody2D>();
            spriteRenderer = player.AddComponent<SpriteRenderer>();
            var health = player.AddComponent<PlayerHealth>();

            var awake = typeof(PlayerHealth).GetMethod("Awake", PrivateInstance);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(health, null);
            return health;
        }
    }
}
