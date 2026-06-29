using BlockEscape.Core;
using BlockEscape.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BlockEscape.Tetris.Tests
{
    public sealed class PlayerPrefabTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player.prefab";
        private const string PlayerConfigPath = "Assets/_Project/Resources/PlayerConfig.asset";

        [Test]
        public void PlayerConfig_UsesAgreedDefaultMovementValues()
        {
            var config = AssetDatabase.LoadAssetAtPath<PlayerConfig>(PlayerConfigPath);
            Assert.That(config, Is.Not.Null, $"Player config is missing at {PlayerConfigPath}.");

            Assert.That(config.moveSpeed, Is.EqualTo(7f));
            Assert.That(config.jumpVelocity, Is.EqualTo(11f));
            Assert.That(config.gravityScale, Is.EqualTo(4f));
            Assert.That(config.coyoteTime, Is.EqualTo(0.10f));
            Assert.That(config.jumpBufferTime, Is.EqualTo(0.12f));
            Assert.That(config.maxFallSpeed, Is.EqualTo(18f));
            Assert.That(config.variableJumpMultiplier, Is.EqualTo(0.5f));
            Assert.That(config.standingColliderSize, Is.EqualTo(new Vector2(0.72f, 1.45f)));
            Assert.That(config.standingColliderOffset, Is.EqualTo(new Vector2(0f, -0.02f)));
            Assert.That(config.crouchColliderSize, Is.EqualTo(new Vector2(0.72f, 0.82f)));
            Assert.That(config.crouchColliderOffset, Is.EqualTo(new Vector2(0f, -0.335f)));
        }

        [Test]
        public void PlayerPrefab_HasRequiredPhysicsControllerAndVisualComponents()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.That(player, Is.Not.Null, $"Player prefab is missing at {PlayerPrefabPath}.");

            var body = player.GetComponent<Rigidbody2D>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
            Assert.That(body.gravityScale, Is.EqualTo(player.GetComponent<PlayerController>().Config.gravityScale));
            Assert.That(body.freezeRotation, Is.True);

            var collider = player.GetComponent<CapsuleCollider2D>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.direction, Is.EqualTo(CapsuleDirection2D.Vertical));

            var controller = player.GetComponent<PlayerController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Config, Is.Not.Null);

            var health = player.GetComponent<PlayerHealth>();
            Assert.That(health, Is.Not.Null);

            var visual = player.transform.Find("Visual");
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.GetComponent<SpriteRenderer>(), Is.Not.Null);
            Assert.That(visual.GetComponent<Animator>(), Is.Not.Null);

            Assert.That(player.transform.Find("Ground Check"), Is.Not.Null);
        }

        [Test]
        public void PlayerController_AssignsFrictionlessColliderMaterialAtRuntime()
        {
            var player = new GameObject("Frictionless Player Test");
            try
            {
                player.AddComponent<Rigidbody2D>();
                var collider = player.AddComponent<CapsuleCollider2D>();
                player.AddComponent<PlayerController>();

                Assert.That(collider.sharedMaterial, Is.Not.Null);
                Assert.That(collider.sharedMaterial.friction, Is.EqualTo(0f));
                Assert.That(collider.sharedMaterial.bounciness, Is.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void PlayerHealth_AppliesDamageKnockbackAndInvulnerability()
        {
            var player = new GameObject("Health Test");
            try
            {
                var body = player.AddComponent<Rigidbody2D>();
                var health = player.AddComponent<PlayerHealth>();

                var accepted = health.TakeDamage(new DamageInfo(1, new Vector2(2f, 3f), null, DamageType.Enemy));

                Assert.That(accepted, Is.True);
                Assert.That(health.CurrentHp, Is.EqualTo(2));
                Assert.That(health.IsInvulnerable, Is.True);
                Assert.That(body.linearVelocity, Is.EqualTo(new Vector2(2f, 3f)));

                var blocked = health.TakeDamage(new DamageInfo(1, Vector2.zero, null, DamageType.Enemy));
                Assert.That(blocked, Is.False);
                Assert.That(health.CurrentHp, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void PlayerHealth_DiesOnceAndRejectsFurtherDamage()
        {
            var player = new GameObject("Health Death Test");
            try
            {
                player.AddComponent<Rigidbody2D>();
                var health = player.AddComponent<PlayerHealth>();
                var deathCount = 0;
                health.Died += () => deathCount++;

                var accepted = health.TakeDamage(new DamageInfo(3, Vector2.zero, null, DamageType.Hazard));
                var afterDeath = health.TakeDamage(new DamageInfo(1, Vector2.zero, null, DamageType.Hazard));

                Assert.That(accepted, Is.True);
                Assert.That(afterDeath, Is.False);
                Assert.That(health.CurrentHp, Is.EqualTo(0));
                Assert.That(health.IsDead, Is.True);
                Assert.That(deathCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void PlayerHealth_ResetHealthRestoresHpAndAllowsDamageAgain()
        {
            var player = new GameObject("Health Reset Test");
            try
            {
                player.AddComponent<Rigidbody2D>();
                var health = player.AddComponent<PlayerHealth>();

                health.TakeDamage(new DamageInfo(3, Vector2.zero, null, DamageType.Hazard));
                health.ResetHealth();
                var accepted = health.TakeDamage(new DamageInfo(1, Vector2.zero, null, DamageType.Enemy));

                Assert.That(health.CurrentHp, Is.EqualTo(2));
                Assert.That(health.IsDead, Is.False);
                Assert.That(accepted, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void PlayerHealth_StartInvulnerabilityBlocksDamage()
        {
            var player = new GameObject("Health Invulnerability Test");
            try
            {
                player.AddComponent<Rigidbody2D>();
                var health = player.AddComponent<PlayerHealth>();

                health.StartInvulnerability(3f);
                var accepted = health.TakeDamage(new DamageInfo(1, Vector2.zero, null, DamageType.Crush));

                Assert.That(health.IsInvulnerable, Is.True);
                Assert.That(accepted, Is.False);
                Assert.That(health.CurrentHp, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }
    }
}
