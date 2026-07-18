using System.Reflection;
using BlockEscape.Bootstrap;
using BlockEscape.Core;
using BlockEscape.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BlockEscape.Tetris.Tests
{
    public sealed class ArenaPrefabTests
    {
        private const string ArenaPrefabPath = "Assets/_Project/Prefabs/Arena/Arena.prefab";
        private const string ArenaSandboxScenePath = "Assets/_Project/Scenes/Sandbox/ArenaSandbox.unity";

        [Test]
        public void ArenaPrefab_HasRequiredTilemapsAndSpawnPoints()
        {
            var arena = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath);
            Assert.That(arena, Is.Not.Null, $"Arena prefab is missing at {ArenaPrefabPath}.");

            Assert.That(arena.transform.Find("Grid/Ground Tilemap"), Is.Not.Null);
            Assert.That(arena.transform.Find("Grid/Platform Tilemap"), Is.Not.Null);
            Assert.That(arena.transform.Find("Grid/Obstacle Tilemap"), Is.Not.Null);
            Assert.That(arena.transform.Find("Grid/Decoration Tilemap"), Is.Not.Null);
            Assert.That(arena.transform.Find("Spawn Points/Player Spawn"), Is.Not.Null);
            Assert.That(arena.transform.Find("Spawn Points/Drone Left"), Is.Not.Null);
            Assert.That(arena.transform.Find("Spawn Points/Drone Right"), Is.Not.Null);
            Assert.That(arena.transform.Find("Kill Zone"), Is.Not.Null);
        }

        [Test]
        public void ArenaPrefab_GroundAndPlatformsUseCompositeStaticColliders()
        {
            var arena = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath);
            Assert.That(arena, Is.Not.Null, $"Arena prefab is missing at {ArenaPrefabPath}.");

            AssertStaticCompositeCollider(arena.transform.Find("Grid/Ground Tilemap"));
            AssertStaticCompositeCollider(arena.transform.Find("Grid/Platform Tilemap"));
        }

        [Test]
        public void ArenaPrefab_ObstacleIsTriggerAndDecorationHasNoCollider()
        {
            var arena = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath);
            Assert.That(arena, Is.Not.Null, $"Arena prefab is missing at {ArenaPrefabPath}.");

            var obstacle = arena.transform.Find("Grid/Obstacle Tilemap");
            Assert.That(obstacle, Is.Not.Null);
            Assert.That(obstacle.GetComponent<Tilemap>(), Is.Not.Null);
            Assert.That(obstacle.GetComponent<CompositeCollider2D>(), Is.Not.Null);
            Assert.That(obstacle.GetComponent<CompositeCollider2D>().isTrigger, Is.True);

            var decoration = arena.transform.Find("Grid/Decoration Tilemap");
            Assert.That(decoration, Is.Not.Null);
            Assert.That(decoration.GetComponent<Tilemap>(), Is.Not.Null);
            Assert.That(decoration.GetComponent<Collider2D>(), Is.Null);
        }

        [Test]
        public void ArenaPrefab_PlayerSpawnHasGroundSupport()
        {
            var arenaAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath);
            Assert.That(arenaAsset, Is.Not.Null, $"Arena prefab is missing at {ArenaPrefabPath}.");

            var arena = (GameObject)PrefabUtility.InstantiatePrefab(arenaAsset);
            var player = new GameObject("Player Ground Probe");
            try
            {
                var spawn = arena.transform.Find("Spawn Points/Player Spawn");
                Assert.That(spawn, Is.Not.Null);

                player.layer = LayerMask.NameToLayer("Player");
                player.transform.position = spawn.position + Vector3.up;
                var collider = player.AddComponent<CapsuleCollider2D>();
                collider.size = new Vector2(0.72f, 1.45f);
                collider.direction = CapsuleDirection2D.Vertical;

                PrepareTilemapColliders(arena);

                var worldMask = LayerMask.GetMask("World");
                Assert.That(worldMask, Is.Not.EqualTo(0), "World layer must exist for player ground checks.");

                var filter = new ContactFilter2D();
                filter.SetLayerMask(worldMask);
                filter.useTriggers = false;

                var hits = new RaycastHit2D[8];
                var hitCount = collider.Cast(Vector2.down, filter, hits, 2f);

                Assert.That(hitCount, Is.GreaterThan(0), "Player spawn must have ground support below it.");
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(arena);
            }
        }

        [Test]
        public void ArenaSandbox_ContainsPlayablePlayerAndInputService()
        {
            var scene = EditorSceneManager.OpenScene(ArenaSandboxScenePath, OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True, $"Arena sandbox scene is missing at {ArenaSandboxScenePath}.");

            var inputService = Object.FindFirstObjectByType<InputService>();
            Assert.That(inputService, Is.Not.Null);

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null);
            Assert.That(player.GetComponent<PlayerHealth>(), Is.Not.Null);
            Assert.That(player.GetComponent<Rigidbody2D>(), Is.Not.Null);
            Assert.That(player.GetComponent<CapsuleCollider2D>(), Is.Not.Null);
            Assert.That(player.Config, Is.Not.Null);
        }

        [Test]
        public void PlayerUnstuck_IgnoresShallowLandingContactButDetectsDeepEmbed()
        {
            var player = new GameObject("Player Unstuck Probe");
            var block = new GameObject("World Unstuck Probe");
            try
            {
                player.layer = LayerMask.NameToLayer("Player");
                var playerCollider = player.AddComponent<CapsuleCollider2D>();
                playerCollider.size = new Vector2(0.72f, 1.45f);
                playerCollider.offset = new Vector2(0f, -0.02f);

                block.layer = LayerMask.NameToLayer("World");
                block.AddComponent<BoxCollider2D>().size = Vector2.one;

                var method = typeof(TetrisDemoBootstrap).GetMethod(
                    "HasDeepWorldOverlap",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(method, Is.Not.Null);

                player.transform.position = new Vector3(0f, 1.225f);
                Physics2D.SyncTransforms();
                Assert.That((bool)method.Invoke(null, new object[] { playerCollider }), Is.False);

                player.transform.position = new Vector3(0f, 1.145f);
                Physics2D.SyncTransforms();
                Assert.That((bool)method.Invoke(null, new object[] { playerCollider }), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(block);
                Object.DestroyImmediate(player);
            }
        }

        private static void AssertStaticCompositeCollider(Transform tilemapTransform)
        {
            Assert.That(tilemapTransform, Is.Not.Null);
            Assert.That(tilemapTransform.GetComponent<Tilemap>(), Is.Not.Null);
            Assert.That(tilemapTransform.GetComponent<TilemapCollider2D>(), Is.Not.Null);
            Assert.That(tilemapTransform.GetComponent<CompositeCollider2D>(), Is.Not.Null);
            Assert.That(tilemapTransform.GetComponent<Rigidbody2D>().bodyType, Is.EqualTo(RigidbodyType2D.Static));
            Assert.That(tilemapTransform.GetComponent<CompositeCollider2D>().isTrigger, Is.False);
        }

        private static void PrepareTilemapColliders(GameObject root)
        {
            foreach (var tilemapCollider in root.GetComponentsInChildren<TilemapCollider2D>())
                tilemapCollider.ProcessTilemapChanges();

            foreach (var composite in root.GetComponentsInChildren<CompositeCollider2D>())
                composite.GenerateGeometry();

            Physics2D.SyncTransforms();
        }
    }
}
