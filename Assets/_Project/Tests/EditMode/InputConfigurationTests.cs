using System.Linq;
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
                Object.DestroyImmediate(serviceObject);
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
