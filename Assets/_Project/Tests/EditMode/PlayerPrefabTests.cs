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
            Assert.That(config.coyoteTime, Is.EqualTo(0.10f));
            Assert.That(config.jumpBufferTime, Is.EqualTo(0.12f));
            Assert.That(config.maxFallSpeed, Is.EqualTo(18f));
            Assert.That(config.variableJumpMultiplier, Is.EqualTo(0.5f));
        }

        [Test]
        public void PlayerPrefab_HasRequiredPhysicsControllerAndVisualComponents()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.That(player, Is.Not.Null, $"Player prefab is missing at {PlayerPrefabPath}.");

            var body = player.GetComponent<Rigidbody2D>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
            Assert.That(body.freezeRotation, Is.True);

            var collider = player.GetComponent<CapsuleCollider2D>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.direction, Is.EqualTo(CapsuleDirection2D.Vertical));

            var controller = player.GetComponent<PlayerController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Config, Is.Not.Null);

            var visual = player.transform.Find("Visual");
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.GetComponent<SpriteRenderer>(), Is.Not.Null);
            Assert.That(visual.GetComponent<Animator>(), Is.Not.Null);

            Assert.That(player.transform.Find("Ground Check"), Is.Not.Null);
        }
    }
}
