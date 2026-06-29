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
            var result = sessionType
                .GetMethod("EndRun", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(session, new object[] { "TEST END", 5, 1234 });

            Assert.That(rowPoints, Is.EqualTo(600));
            Assert.That((int)sessionType.GetProperty("RowsCleared").GetValue(session), Is.EqualTo(2));
            Assert.That((int)sessionType.GetProperty("Score").GetValue(session), Is.EqualTo(610));
            Assert.That((float)sessionType.GetProperty("SurvivalTime").GetValue(session), Is.EqualTo(1.1f).Within(0.001f));

            var resultType = result.GetType();
            Assert.That((int)resultType.GetProperty("PiecesSpawned").GetValue(result), Is.EqualTo(5));
            Assert.That((int)resultType.GetProperty("RowsCleared").GetValue(result), Is.EqualTo(2));
            Assert.That((int)resultType.GetProperty("Score").GetValue(result), Is.EqualTo(610));
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

        private static void AssertAction(InputActionMap map, string actionName, params string[] expectedPaths)
        {
            var action = map.FindAction(actionName, true);
            var paths = action.bindings.Select(binding => binding.path).ToArray();
            foreach (var expectedPath in expectedPaths)
                Assert.That(paths, Does.Contain(expectedPath), $"{map.name}/{actionName} is missing {expectedPath}.");
        }
    }
}
