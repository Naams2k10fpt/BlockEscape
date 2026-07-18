using System.Reflection;
using BlockEscape.Player;
using NUnit.Framework;
using UnityEngine;

namespace BlockEscape.Tetris.Tests
{
    public sealed class PickupDirectorTests
    {
        [Test]
        public void PickupDirector_SpawnsFromTopAndExpiresOneSecondAfterLanding()
        {
            var config = ScriptableObject.CreateInstance<TetrisBalanceConfig>();
            var boardObject = new GameObject("Pickup Board Test");
            var playerObject = new GameObject("Pickup Player Test");
            var directorObject = new GameObject("Pickup Director Test");
            try
            {
                boardObject.transform.position = new Vector3(-config.boardWidth * 0.5f, -10f);
                var board = boardObject.AddComponent<BlockBoard>();
                board.Initialize(config);

                playerObject.AddComponent<Rigidbody2D>();
                var health = playerObject.AddComponent<PlayerHealth>();
                var director = directorObject.AddComponent<PickupDirector>();
                director.Initialize(board, health, 1234);

                Assert.That(director.TrySpawnNow(), Is.True);
                Assert.That(director.TrySpawnNow(), Is.True);
                Assert.That(director.TrySpawnNow(), Is.False);
                Assert.That(director.ActiveCount, Is.EqualTo(2));
                Assert.That(Physics2D.GetIgnoreLayerCollision(
                    LayerMask.NameToLayer("Pickup"),
                    LayerMask.NameToLayer("World")), Is.True);
                Assert.That(Physics2D.GetIgnoreLayerCollision(
                    LayerMask.NameToLayer("Pickup"),
                    LayerMask.NameToLayer("Player")), Is.False);

                var pickups = director.GetComponentsInChildren<BoxCollider2D>(false);
                Assert.That(pickups, Has.Length.EqualTo(2));
                foreach (var pickup in pickups)
                {
                    Assert.That(pickup.isTrigger, Is.True);
                    Assert.That(pickup.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Pickup")));
                    Assert.That(pickup.transform.position.x, Is.InRange(board.transform.position.x, board.transform.position.x + board.Width));
                    Assert.That(pickup.transform.position.y,
                        Is.EqualTo(board.WorldForCell(new Vector2Int(0, board.Height - 1)).y).Within(0.001f));
                }

                var itemType = typeof(PickupDirector).Assembly.GetType("BlockEscape.Tetris.PickupItem");
                Assert.That(itemType, Is.Not.Null);
                var item = pickups[0].GetComponent(itemType);
                var landingPosition = (Vector3)itemType.GetField(
                    "_landingPosition",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(item);
                itemType.GetField("_landedAt", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(item, Time.time - 1.01f);
                pickups[0].transform.position = landingPosition;
                itemType.GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(item, null);

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
    }
}
