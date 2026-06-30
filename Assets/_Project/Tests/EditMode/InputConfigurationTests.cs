using System;
using System.Linq;
using System.Reflection;
using BlockEscape.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlockEscape.Tetris.Tests
{
    public sealed class InputConfigurationTests
    {
        private const string AssetPath = "Assets/InputSystem_Actions.inputactions";

        [Test]
        public void InputAsset_HasRequiredMapsActionsAndKeyboardBindings()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.That(asset, Is.Not.Null, $"Input Action asset is missing at {AssetPath}.");

            var tetris = asset.FindActionMap("Tetris", true);
            var player = asset.FindActionMap("Player", true);
            var system = asset.FindActionMap("System", true);

            AssertAction(tetris, "Move", "<Keyboard>/a", "<Keyboard>/d");
            AssertAction(tetris, "Rotate", "<Keyboard>/w");
            AssertAction(tetris, "SoftDrop", "<Keyboard>/s");

            AssertAction(player, "Move", "<Keyboard>/leftArrow", "<Keyboard>/rightArrow");
            AssertAction(player, "Jump", "<Keyboard>/upArrow");
            AssertAction(player, "Crouch", "<Keyboard>/downArrow");

            AssertAction(system, "Pause", "<Keyboard>/escape");
            AssertAction(system, "ResetRun", "<Keyboard>/r");

            var tetrisPaths = tetris.bindings.Select(binding => binding.path).ToArray();
            Assert.That(
                tetrisPaths.Any(path => path.Contains("Arrow")),
                Is.False,
                "Arrow keys are reserved for the player map.");
        }

        [Test]
        public void InputService_TogglesGameplayMapsWithoutDisablingSystemMap()
        {
            if (InputService.Current != null)
                Assert.Ignore("InputService singleton already exists in the open editor scene.");

            var serviceObject = new GameObject("InputService Test");
            var service = serviceObject.AddComponent<InputService>();

            try
            {
                service.EnsureInitialized();

                Assert.That(service.TetrisMove.enabled, Is.True);
                Assert.That(service.PlayerMove.enabled, Is.True);
                Assert.That(service.Pause.enabled, Is.True);
                Assert.That(service.ResetRun.enabled, Is.True);

                service.SetGameplayEnabled(false);

                Assert.That(service.TetrisMove.enabled, Is.False);
                Assert.That(service.PlayerMove.enabled, Is.False);
                Assert.That(service.Pause.enabled, Is.True);
                Assert.That(service.ResetRun.enabled, Is.True);

                service.SetGameplayEnabled(true);

                Assert.That(service.TetrisMove.enabled, Is.True);
                Assert.That(service.PlayerMove.enabled, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serviceObject);
            }
        }

        [Test]
        public void GameSession_TracksSurvivalRowsAndFinalResult()
        {
            var sessionType = typeof(InputService).Assembly.GetType("BlockEscape.Core.GameSession");
            Assert.That(sessionType, Is.Not.Null);

            var session = Activator.CreateInstance(sessionType);
            sessionType.GetMethod("StartRun", BindingFlags.Instance | BindingFlags.Public).Invoke(session, null);
            sessionType.GetMethod("Tick", BindingFlags.Instance | BindingFlags.Public).Invoke(session, new object[] { 1.1f });
            var rowPoints = (int)sessionType
                .GetMethod("AddRowsCleared", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(session, new object[] { 2 });
            var bonusPoints = (int)sessionType
                .GetMethod("AddBonusScore", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(session, new object[] { 300 });
            var result = sessionType
                .GetMethod("EndRun", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(session, new object[] { "TEST END", 5, 1234 });

            Assert.That(rowPoints, Is.EqualTo(600));
            Assert.That(bonusPoints, Is.EqualTo(300));
            Assert.That((int)sessionType.GetProperty("RowsCleared").GetValue(session), Is.EqualTo(2));
            Assert.That((int)sessionType.GetProperty("Score").GetValue(session), Is.EqualTo(910));
            Assert.That((float)sessionType.GetProperty("SurvivalTime").GetValue(session), Is.EqualTo(1.1f).Within(0.001f));

            var resultType = result.GetType();
            Assert.That((int)resultType.GetProperty("PiecesSpawned").GetValue(result), Is.EqualTo(5));
            Assert.That((int)resultType.GetProperty("RowsCleared").GetValue(result), Is.EqualTo(2));
            Assert.That((int)resultType.GetProperty("Score").GetValue(result), Is.EqualTo(910));
            Assert.That((int)resultType.GetProperty("Phase").GetValue(result), Is.EqualTo(1));
            Assert.That((int)resultType.GetProperty("Seed").GetValue(result), Is.EqualTo(1234));
            Assert.That((string)resultType.GetProperty("Reason").GetValue(result), Is.EqualTo("TEST END"));
        }

        [Test]
        public void GameSession_AdvancesPhaseBySurvivalTime()
        {
            var sessionType = typeof(InputService).Assembly.GetType("BlockEscape.Core.GameSession");
            Assert.That(sessionType, Is.Not.Null);

            var session = Activator.CreateInstance(sessionType);
            sessionType.GetMethod("StartRun", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(float) }, null)
                .Invoke(session, new object[] { 1f });
            sessionType.GetMethod("Tick", BindingFlags.Instance | BindingFlags.Public).Invoke(session, new object[] { 2.2f });

            Assert.That((int)sessionType.GetProperty("Phase").GetValue(session), Is.EqualTo(3));
            Assert.That((float)sessionType.GetProperty("TimeUntilNextPhase").GetValue(session), Is.EqualTo(0.8f).Within(0.001f));
        }

        [Test]
        public void TetrominoSpawner_FallSpeedMultiplierStacksWithPhaseSpeed()
        {
            var spawnerObject = new GameObject("Spawner");
            var config = ScriptableObject.CreateInstance<TetrisBalanceConfig>();
            try
            {
                config.fallSpeedCellsPerSecond = 2f;
                config.fallSpeedIncreasePerPhase = 0.5f;
                config.maxFallSpeedCellsPerSecond = 5f;
                config.Sanitize();

                var spawner = spawnerObject.AddComponent<TetrominoSpawner>();
                typeof(TetrominoSpawner)
                    .GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(spawner, config);

                spawner.ApplyDifficultyPhase(3);
                Assert.That(spawner.CurrentFallSpeed, Is.EqualTo(3f));

                spawner.SetFallSpeedMultiplier(1.6f);
                Assert.That(spawner.FallSpeedMultiplier, Is.EqualTo(1.6f));
                Assert.That(spawner.CurrentFallSpeed, Is.EqualTo(4.8f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
                UnityEngine.Object.DestroyImmediate(spawnerObject);
            }
        }

        [Test]
        public void EventDirector_UsesEarlyIntervalsFromPhaseOne()
        {
            var eventConfigType = typeof(InputService).Assembly.GetType("BlockEscape.Events.DynamicEventConfig");
            var eventDirectorType = typeof(InputService).Assembly.GetType("BlockEscape.Events.EventDirector");
            Assert.That(eventConfigType, Is.Not.Null);
            Assert.That(eventDirectorType, Is.Not.Null);

            var config = ScriptableObject.CreateInstance(eventConfigType);
            try
            {
                eventConfigType.GetField("phase2MinIntervalSeconds").SetValue(config, 4f);
                eventConfigType.GetField("phase2MaxIntervalSeconds").SetValue(config, 6f);
                eventConfigType.GetField("phase3MinIntervalSeconds").SetValue(config, 6f);
                eventConfigType.GetField("phase3MaxIntervalSeconds").SetValue(config, 8f);
                eventConfigType.GetField("phase4MinIntervalSeconds").SetValue(config, 8f);
                eventConfigType.GetField("phase4MaxIntervalSeconds").SetValue(config, 10f);
                eventConfigType.GetMethod("Sanitize").Invoke(config, null);

                var canRunEvents = eventDirectorType.GetMethod("CanRunEvents", BindingFlags.Public | BindingFlags.Static);
                var getInterval = eventDirectorType.GetMethod("GetNextIntervalForPhase", BindingFlags.Public | BindingFlags.Static);
                Assert.That((bool)canRunEvents.Invoke(null, new object[] { 1 }), Is.True);
                Assert.That((bool)canRunEvents.Invoke(null, new object[] { 2 }), Is.True);
                Assert.That((float)getInterval.Invoke(null, new object[] { 1, config, new System.Random(1) }), Is.InRange(4f, 6f));
                Assert.That((float)getInterval.Invoke(null, new object[] { 2, config, new System.Random(1) }), Is.InRange(4f, 6f));
                Assert.That((float)getInterval.Invoke(null, new object[] { 3, config, new System.Random(1) }), Is.InRange(6f, 8f));
                Assert.That((float)getInterval.Invoke(null, new object[] { 4, config, new System.Random(1) }), Is.InRange(8f, 10f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void DroneController_EnablesAtPhaseOneAndDetectsNearbyPlayer()
        {
            var droneType = typeof(InputService).Assembly.GetType("BlockEscape.AI.DroneController");
            var droneConfigType = typeof(InputService).Assembly.GetType("BlockEscape.AI.DroneConfig");
            Assert.That(droneType, Is.Not.Null);
            Assert.That(droneConfigType, Is.Not.Null);

            var boardObject = new GameObject("Board");
            var droneObject = new GameObject("Drone");
            var player = new GameObject("Player");
            var config = ScriptableObject.CreateInstance(droneConfigType);
            TetrisBalanceConfig balance = null;
            try
            {
                balance = ScriptableObject.CreateInstance<TetrisBalanceConfig>();
                balance.boardWidth = 14;
                balance.boardHeight = 20;
                var board = boardObject.AddComponent<BlockBoard>();
                board.Initialize(balance);

                player.transform.position = new Vector3(0f, 4f, 0f);
                droneObject.AddComponent<Rigidbody2D>();
                var collider = droneObject.AddComponent<CircleCollider2D>();
                collider.isTrigger = true;
                droneObject.AddComponent<SpriteRenderer>();
                var drone = droneObject.AddComponent(droneType);

                droneConfigType.GetField("detectRange").SetValue(config, 10f);
                droneConfigType.GetField("detectConfirmSeconds").SetValue(config, 0f);
                droneConfigType.GetField("telegraphSeconds").SetValue(config, 0.8f);

                droneType.GetMethod("Initialize").Invoke(drone, new object[] { config, player.transform, board });
                Assert.That(droneType.GetProperty("State").GetValue(drone).ToString(), Is.EqualTo("Patrol"));

                droneType.GetMethod("SetPhase").Invoke(drone, new object[] { 1 });
                droneType.GetMethod("SetRunning").Invoke(drone, new object[] { true });
                Assert.That(droneType.GetProperty("State").GetValue(drone).ToString(), Is.EqualTo("Patrol"));

                droneType.GetMethod("ManualTick").Invoke(drone, new object[] { 0.1f });
                Assert.That(droneType.GetProperty("State").GetValue(drone).ToString(), Is.EqualTo("Detect"));
                droneType.GetMethod("ManualTick").Invoke(drone, new object[] { 0.1f });
                Assert.That(droneType.GetProperty("State").GetValue(drone).ToString(), Is.EqualTo("Telegraph"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
                if (balance != null) UnityEngine.Object.DestroyImmediate(balance);
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(droneObject);
                UnityEngine.Object.DestroyImmediate(boardObject);
            }
        }

        private static void AssertAction(InputActionMap map, string actionName, params string[] expectedPaths)
        {
            var action = map.FindAction(actionName, true);
            var paths = action.bindings.Select(binding => binding.path).ToArray();
            foreach (var expectedPath in expectedPaths)
                Assert.That(paths, Does.Contain(expectedPath), $"{map.name}/{actionName} is missing {expectedPath}.");
        }
    }
}
