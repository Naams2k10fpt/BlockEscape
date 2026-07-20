using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BlockEscape.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace BlockEscape.Tetris.Tests
{
    /// <summary>
    /// Contract coverage for input ownership, scoring, session state and save data.
    /// The runtime keeps ScoreService and GameSession internal, so their public
    /// contracts are exercised through reflection exactly as the bootstrap uses them.
    /// </summary>
    public sealed class CoreServicesAndPersistenceExhaustiveTests
    {
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Assembly RuntimeAssembly = typeof(InputService).Assembly;

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private string _temporaryDirectory;

        private static IEnumerable<TestCaseData> DamageInfoCases
        {
            get
            {
                yield return new TestCaseData(0, 0, Vector2.zero, DamageType.Enemy).SetName("DamageInfo_ZeroEnemyDamage");
                yield return new TestCaseData(1, 1, Vector2.zero, DamageType.Enemy).SetName("DamageInfo_OneEnemyDamage");
                yield return new TestCaseData(3, 3, Vector2.left, DamageType.Hazard).SetName("DamageInfo_HazardLeftKnockback");
                yield return new TestCaseData(5, 5, Vector2.right, DamageType.Crush).SetName("DamageInfo_CrushRightKnockback");
                yield return new TestCaseData(-1, 0, Vector2.up, DamageType.Enemy).SetName("DamageInfo_NegativeAmountClampsZero");
                yield return new TestCaseData(99, 99, Vector2.down * 10f, DamageType.Hazard).SetName("DamageInfo_LargeHazardDamage");
                yield return new TestCaseData(int.MaxValue, int.MaxValue, new Vector2(3f, -7f), DamageType.Crush).SetName("DamageInfo_MaximumAmount");
                yield return new TestCaseData(int.MinValue, 0, new Vector2(-2f, 8f), DamageType.Enemy).SetName("DamageInfo_MinimumAmountClampsZero");
            }
        }

        private static IEnumerable<TestCaseData> RowScoreCases
        {
            get
            {
                yield return new TestCaseData(-100, 0).SetName("Score_RowsMinus100_AddsZero");
                yield return new TestCaseData(-1, 0).SetName("Score_RowsMinus1_AddsZero");
                yield return new TestCaseData(0, 0).SetName("Score_Rows0_AddsZero");
                yield return new TestCaseData(1, 250).SetName("Score_Single_Adds250");
                yield return new TestCaseData(2, 600).SetName("Score_Double_Adds600");
                yield return new TestCaseData(3, 1000).SetName("Score_Triple_Adds1000");
                yield return new TestCaseData(4, 1500).SetName("Score_Tetris_Adds1500");
                yield return new TestCaseData(5, 1500).SetName("Score_FiveRows_UsesCap1500");
                yield return new TestCaseData(10, 1500).SetName("Score_TenRows_UsesCap1500");
                yield return new TestCaseData(int.MaxValue, 1500).SetName("Score_MaxRows_UsesCap1500");
            }
        }

        private static IEnumerable<TestCaseData> BonusScoreCases
        {
            get
            {
                yield return new TestCaseData(-1000, 0).SetName("Bonus_Minus1000_IsRejected");
                yield return new TestCaseData(-1, 0).SetName("Bonus_Minus1_IsRejected");
                yield return new TestCaseData(0, 0).SetName("Bonus_Zero_IsRejected");
                yield return new TestCaseData(1, 1).SetName("Bonus_One_IsAccepted");
                yield return new TestCaseData(10, 10).SetName("Bonus_Ten_IsAccepted");
                yield return new TestCaseData(100, 100).SetName("Bonus_PickupScore_IsAccepted");
                yield return new TestCaseData(300, 300).SetName("Bonus_DroneScore_IsAccepted");
                yield return new TestCaseData(1000, 1000).SetName("Bonus_Thousand_IsAccepted");
                yield return new TestCaseData(1000000, 1000000).SetName("Bonus_Million_IsAccepted");
            }
        }

        private static IEnumerable<TestCaseData> SurvivalScoreCases
        {
            get
            {
                yield return new TestCaseData(-10f, 0).SetName("Survival_NegativeTime_AddsZero");
                yield return new TestCaseData(0f, 0).SetName("Survival_ZeroTime_AddsZero");
                yield return new TestCaseData(0.01f, 0).SetName("Survival_OneHundredth_AddsZero");
                yield return new TestCaseData(0.5f, 0).SetName("Survival_HalfSecond_AddsZero");
                yield return new TestCaseData(0.99f, 0).SetName("Survival_BelowSecond_AddsZero");
                yield return new TestCaseData(1f, 10).SetName("Survival_OneSecond_AddsTen");
                yield return new TestCaseData(1.01f, 10).SetName("Survival_OnePointZeroOneAddsTen");
                yield return new TestCaseData(2.9f, 20).SetName("Survival_TwoPointNineAddsTwenty");
                yield return new TestCaseData(10f, 100).SetName("Survival_TenSecondsAddsHundred");
                yield return new TestCaseData(60f, 600).SetName("Survival_MinuteAddsSixHundred");
            }
        }

        private static IEnumerable<TestCaseData> CountdownCases
        {
            get
            {
                yield return new TestCaseData(-3f, "Playing", 0f).SetName("Countdown_NegativeStartsImmediately");
                yield return new TestCaseData(0f, "Playing", 0f).SetName("Countdown_ZeroStartsImmediately");
                yield return new TestCaseData(0.1f, "Countdown", 0.1f).SetName("Countdown_TenthStartsWaiting");
                yield return new TestCaseData(1f, "Countdown", 1f).SetName("Countdown_OneStartsWaiting");
                yield return new TestCaseData(3f, "Countdown", 3f).SetName("Countdown_ThreeStartsWaiting");
                yield return new TestCaseData(10f, "Countdown", 10f).SetName("Countdown_TenStartsWaiting");
            }
        }

        private static IEnumerable<TestCaseData> PhaseCases
        {
            get
            {
                yield return new TestCaseData(1f, 0f, 1, 1f).SetName("Phase_AtStart");
                yield return new TestCaseData(1f, 0.99f, 1, 0.01f).SetName("Phase_JustBeforeSecond");
                yield return new TestCaseData(1f, 1f, 2, 1f).SetName("Phase_AtSecond");
                yield return new TestCaseData(1f, 2.25f, 3, 0.75f).SetName("Phase_ThirdAtTwoPoint25");
                yield return new TestCaseData(5f, 4.99f, 1, 0.01f).SetName("Phase_FiveSecondBoundaryBefore");
                yield return new TestCaseData(5f, 5f, 2, 5f).SetName("Phase_FiveSecondBoundaryAt");
                yield return new TestCaseData(10f, 25f, 3, 5f).SetName("Phase_TenSecondDurationAt25");
                yield return new TestCaseData(45f, 44f, 1, 1f).SetName("Phase_DefaultBefore45");
                yield return new TestCaseData(45f, 45f, 2, 45f).SetName("Phase_DefaultAt45");
                yield return new TestCaseData(45f, 90f, 3, 45f).SetName("Phase_DefaultAt90");
            }
        }

        private static IEnumerable<TestCaseData> VolumeSanitizeCases
        {
            get
            {
                yield return new TestCaseData(-100f, 0f).SetName("Volume_Minus100_ClampsZero");
                yield return new TestCaseData(-1f, 0f).SetName("Volume_Minus1_ClampsZero");
                yield return new TestCaseData(-0.01f, 0f).SetName("Volume_BelowZero_ClampsZero");
                yield return new TestCaseData(0f, 0f).SetName("Volume_Zero_RemainsZero");
                yield return new TestCaseData(0.1f, 0.1f).SetName("Volume_OneTenth_Remains");
                yield return new TestCaseData(0.5f, 0.5f).SetName("Volume_Half_Remains");
                yield return new TestCaseData(0.7f, 0.7f).SetName("Volume_SevenTenths_Remains");
                yield return new TestCaseData(0.99f, 0.99f).SetName("Volume_BelowOne_Remains");
                yield return new TestCaseData(1f, 1f).SetName("Volume_One_RemainsOne");
                yield return new TestCaseData(1.01f, 1f).SetName("Volume_AboveOne_ClampsOne");
                yield return new TestCaseData(2f, 1f).SetName("Volume_Two_ClampsOne");
                yield return new TestCaseData(100f, 1f).SetName("Volume_Hundred_ClampsOne");
            }
        }

        [SetUp]
        public void SetUp()
        {
            if (InputService.Current != null)
                UnityEngine.Object.DestroyImmediate(InputService.Current.gameObject);

            _temporaryDirectory = Path.Combine(
                Application.temporaryCachePath,
                "BlockEscapeComprehensiveCoreTests",
                Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            for (var index = _createdObjects.Count - 1; index >= 0; index--)
            {
                if (_createdObjects[index] != null)
                    UnityEngine.Object.DestroyImmediate(_createdObjects[index]);
            }

            _createdObjects.Clear();
            if (InputService.Current != null)
                UnityEngine.Object.DestroyImmediate(InputService.Current.gameObject);
            if (Directory.Exists(_temporaryDirectory))
                Directory.Delete(_temporaryDirectory, true);
        }

        [Test]
        public void DamageType_ContainsExpectedGameplaySources()
        {
            var names = Enum.GetNames(typeof(DamageType));

            CollectionAssert.AreEqual(new[] { "Impact", "Enemy", "Hazard", "Crush" }, names);
        }

        [TestCaseSource(nameof(DamageInfoCases))]
        public void DamageInfo_PreservesAndSanitizesConstructorValues(
            int amount,
            int expectedAmount,
            Vector2 knockback,
            DamageType type)
        {
            var source = Track(new GameObject("Damage Source"));

            var damage = new DamageInfo(amount, knockback, source, type);

            Assert.That(damage.Amount, Is.EqualTo(expectedAmount));
            Assert.That(damage.Knockback, Is.EqualTo(knockback));
            Assert.That(damage.Source, Is.SameAs(source));
            Assert.That(damage.Type, Is.EqualTo(type));
        }

        [Test]
        public void DamageInfo_AllowsNullSource()
        {
            var damage = new DamageInfo(1, Vector2.up, null, DamageType.Hazard);

            Assert.That(damage.Source, Is.Null);
            Assert.That(damage.Amount, Is.EqualTo(1));
            Assert.That(damage.Knockback, Is.EqualTo(Vector2.up));
            Assert.That(damage.Type, Is.EqualTo(DamageType.Hazard));
        }

        [Test]
        public void InputService_RuntimeDefaultsExposeAllThreeMaps()
        {
            var service = CreateInputService();
            var asset = GetInputAsset(service);

            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.actionMaps.Count, Is.EqualTo(3));
            Assert.That(asset.FindActionMap("Tetris", false), Is.Not.Null);
            Assert.That(asset.FindActionMap("Player", false), Is.Not.Null);
            Assert.That(asset.FindActionMap("System", false), Is.Not.Null);
        }

        [Test]
        public void InputService_RuntimeDefaultsExposeEveryRequiredAction()
        {
            var service = CreateInputService();

            Assert.That(service.TetrisMove, Is.Not.Null);
            Assert.That(service.TetrisRotate, Is.Not.Null);
            Assert.That(service.TetrisSoftDrop, Is.Not.Null);
            Assert.That(service.PlayerMove, Is.Not.Null);
            Assert.That(service.PlayerJump, Is.Not.Null);
            Assert.That(service.PlayerCrouch, Is.Not.Null);
            Assert.That(service.Pause, Is.Not.Null);
            Assert.That(service.ResetRun, Is.Not.Null);
        }

        [Test]
        public void InputService_RuntimeDefaultBindingsMatchDocumentedControls()
        {
            var service = CreateInputService();
            var asset = GetInputAsset(service);

            AssertActionHasPath(asset, "Tetris", "Move", "<Keyboard>/a");
            AssertActionHasPath(asset, "Tetris", "Move", "<Keyboard>/d");
            AssertActionHasPath(asset, "Tetris", "Rotate", "<Keyboard>/w");
            AssertActionHasPath(asset, "Tetris", "SoftDrop", "<Keyboard>/s");
            AssertActionHasPath(asset, "Player", "Move", "<Keyboard>/leftArrow");
            AssertActionHasPath(asset, "Player", "Move", "<Keyboard>/rightArrow");
            AssertActionHasPath(asset, "Player", "Jump", "<Keyboard>/upArrow");
            AssertActionHasPath(asset, "Player", "Crouch", "<Keyboard>/downArrow");
            AssertActionHasPath(asset, "System", "Pause", "<Keyboard>/escape");
            AssertActionHasPath(asset, "System", "ResetRun", "<Keyboard>/r");
        }

        [Test]
        public void InputService_InitializationEnablesGameplayAndSystemMaps()
        {
            var service = CreateInputService();
            var asset = GetInputAsset(service);

            Assert.That(service.GameplayEnabled, Is.True);
            Assert.That(asset.FindActionMap("Tetris", true).enabled, Is.True);
            Assert.That(asset.FindActionMap("Player", true).enabled, Is.True);
            Assert.That(asset.FindActionMap("System", true).enabled, Is.True);
        }

        [Test]
        public void InputService_DisablingGameplayLeavesSystemMapEnabled()
        {
            var service = CreateInputService();
            var asset = GetInputAsset(service);

            service.SetGameplayEnabled(false);

            Assert.That(service.GameplayEnabled, Is.False);
            Assert.That(asset.FindActionMap("Tetris", true).enabled, Is.False);
            Assert.That(asset.FindActionMap("Player", true).enabled, Is.False);
            Assert.That(asset.FindActionMap("System", true).enabled, Is.True);
            Assert.That(service.Pause.enabled, Is.True);
            Assert.That(service.ResetRun.enabled, Is.True);
        }

        [Test]
        public void InputService_GameplayToggleCanBeRepeatedWithoutChangingActions()
        {
            var service = CreateInputService();
            var moveBefore = service.TetrisMove;
            var jumpBefore = service.PlayerJump;

            for (var cycle = 0; cycle < 10; cycle++)
            {
                service.SetGameplayEnabled(false);
                Assert.That(service.GameplayEnabled, Is.False);
                service.SetGameplayEnabled(false);
                Assert.That(service.GameplayEnabled, Is.False);
                service.SetGameplayEnabled(true);
                Assert.That(service.GameplayEnabled, Is.True);
                service.SetGameplayEnabled(true);
                Assert.That(service.GameplayEnabled, Is.True);
            }

            Assert.That(service.TetrisMove, Is.SameAs(moveBefore));
            Assert.That(service.PlayerJump, Is.SameAs(jumpBefore));
        }

        [Test]
        public void InputService_EnsureInitializedIsIdempotent()
        {
            var service = CreateInputService();
            var assetBefore = GetInputAsset(service);
            var actionBefore = service.TetrisMove;

            service.EnsureInitialized();
            service.EnsureInitialized();
            service.EnsureInitialized();

            Assert.That(GetInputAsset(service), Is.SameAs(assetBefore));
            Assert.That(service.TetrisMove, Is.SameAs(actionBefore));
            Assert.That(service.GameplayEnabled, Is.True);
        }

        [Test]
        public void InputService_OnDestroyClearsOnlyTheOwningCurrentInstance()
        {
            var first = CreateInputService("First Input Service");
            var second = CreateInputService("Second Input Service");
            var currentField = typeof(InputService).GetField("<Current>k__BackingField", StaticMembers);
            Assert.That(currentField, Is.Not.Null);
            currentField.SetValue(null, first);

            Invoke(second, "OnDestroy");
            Assert.That(InputService.Current, Is.SameAs(first));

            Invoke(first, "OnDestroy");
            Assert.That(InputService.Current, Is.Null);
        }

        [TestCaseSource(nameof(RowScoreCases))]
        public void ScoreService_RowScoringMatchesBalance(int rows, int expectedPoints)
        {
            var score = CreateInternal("BlockEscape.Core.ScoreService");

            var awarded = (int)Invoke(score, "AddRowsCleared", rows);

            Assert.That(awarded, Is.EqualTo(expectedPoints));
            Assert.That(Get<int>(score, "Score"), Is.EqualTo(expectedPoints));
            Assert.That(Get<int>(score, "RowsCleared"), Is.EqualTo(rows > 0 ? rows : 0));
        }

        [TestCaseSource(nameof(BonusScoreCases))]
        public void ScoreService_BonusScoringAcceptsOnlyPositiveValues(int points, int expected)
        {
            var score = CreateInternal("BlockEscape.Core.ScoreService");

            var awarded = (int)Invoke(score, "AddBonusScore", points);

            Assert.That(awarded, Is.EqualTo(expected));
            Assert.That(Get<int>(score, "Score"), Is.EqualTo(expected));
            Assert.That(Get<int>(score, "RowsCleared"), Is.Zero);
        }

        [TestCaseSource(nameof(SurvivalScoreCases))]
        public void ScoreService_SurvivalTimeAwardsTenPointsPerWholeSecond(float seconds, int expected)
        {
            var score = CreateInternal("BlockEscape.Core.ScoreService");

            Invoke(score, "AddSurvivalTime", seconds);

            Assert.That(Get<int>(score, "Score"), Is.EqualTo(expected));
            Assert.That(Get<int>(score, "RowsCleared"), Is.Zero);
        }

        [Test]
        public void ScoreService_FractionalSurvivalTimeAccumulatesAcrossCalls()
        {
            var score = CreateInternal("BlockEscape.Core.ScoreService");

            Invoke(score, "AddSurvivalTime", 0.25f);
            Invoke(score, "AddSurvivalTime", 0.25f);
            Invoke(score, "AddSurvivalTime", 0.25f);
            Assert.That(Get<int>(score, "Score"), Is.Zero);

            Invoke(score, "AddSurvivalTime", 0.25f);
            Assert.That(Get<int>(score, "Score"), Is.EqualTo(10));

            Invoke(score, "AddSurvivalTime", 2.75f);
            Assert.That(Get<int>(score, "Score"), Is.EqualTo(30));

            Invoke(score, "AddSurvivalTime", 0.25f);
            Assert.That(Get<int>(score, "Score"), Is.EqualTo(40));
        }

        [Test]
        public void ScoreService_ResetClearsAllCountersAndFractions()
        {
            var score = CreateInternal("BlockEscape.Core.ScoreService");
            Invoke(score, "AddSurvivalTime", 1.9f);
            Invoke(score, "AddRowsCleared", 3);
            Invoke(score, "AddBonusScore", 300);

            Invoke(score, "Reset");

            Assert.That(Get<int>(score, "Score"), Is.Zero);
            Assert.That(Get<int>(score, "RowsCleared"), Is.Zero);
            Invoke(score, "AddSurvivalTime", 0.2f);
            Assert.That(Get<int>(score, "Score"), Is.Zero, "Fractional time from the previous run must be cleared.");
        }

        [TestCaseSource(nameof(CountdownCases))]
        public void GameSession_StartCountdownClampsAndSelectsInitialState(float countdown, string expectedState, float expectedRemaining)
        {
            var session = CreateInternal("BlockEscape.Core.GameSession");

            Invoke(session, "StartCountdown", countdown, 45f);

            Assert.That(Get<object>(session, "State").ToString(), Is.EqualTo(expectedState));
            Assert.That(Get<float>(session, "CountdownRemaining"), Is.EqualTo(expectedRemaining).Within(0.0001f));
            Assert.That(Get<float>(session, "SurvivalTime"), Is.Zero);
            Assert.That(Get<int>(session, "Phase"), Is.EqualTo(1));
        }

        [Test]
        public void GameSession_CountdownConsumesOnlyCountdownTime()
        {
            var session = CreateInternal("BlockEscape.Core.GameSession");
            Invoke(session, "StartCountdown", 3f, 45f);

            Invoke(session, "Tick", 1f);
            Assert.That(Get<float>(session, "CountdownRemaining"), Is.EqualTo(2f));
            Assert.That(Get<float>(session, "SurvivalTime"), Is.Zero);
            Assert.That(Get<int>(session, "Score"), Is.Zero);

            Invoke(session, "Tick", 5f);
            Assert.That(Get<object>(session, "State").ToString(), Is.EqualTo("Playing"));
            Assert.That(Get<float>(session, "CountdownRemaining"), Is.Zero);
            Assert.That(Get<float>(session, "SurvivalTime"), Is.Zero, "Overshoot must not count toward gameplay time.");
        }

        [TestCaseSource(nameof(PhaseCases))]
        public void GameSession_PhaseAndNextPhaseTimeFollowSurvivalClock(
            float phaseDuration,
            float elapsed,
            int expectedPhase,
            float expectedRemaining)
        {
            var session = CreateInternal("BlockEscape.Core.GameSession");
            InvokeOverload(session, "StartRun", new[] { typeof(float) }, phaseDuration);

            Invoke(session, "Tick", elapsed);

            Assert.That(Get<float>(session, "SurvivalTime"), Is.EqualTo(Mathf.Max(0f, elapsed)).Within(0.0001f));
            Assert.That(Get<int>(session, "Phase"), Is.EqualTo(expectedPhase));
            Assert.That(Get<float>(session, "TimeUntilNextPhase"), Is.EqualTo(expectedRemaining).Within(0.001f));
        }

        [Test]
        public void GameSession_PhaseDurationIsSanitizedToOneSecond()
        {
            var session = CreateInternal("BlockEscape.Core.GameSession");
            InvokeOverload(session, "StartRun", new[] { typeof(float) }, -100f);

            Invoke(session, "Tick", 2.5f);

            Assert.That(Get<int>(session, "Phase"), Is.EqualTo(3));
            Assert.That(Get<float>(session, "TimeUntilNextPhase"), Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void GameSession_PauseFreezesTimeScoreRowsAndBonus()
        {
            var session = CreateInternal("BlockEscape.Core.GameSession");
            InvokeOverload(session, "StartRun", new[] { typeof(float) }, 5f);
            Invoke(session, "Tick", 1f);
            Assert.That((int)Invoke(session, "AddRowsCleared", 1), Is.EqualTo(250));
            Assert.That((int)Invoke(session, "AddBonusScore", 100), Is.EqualTo(100));

            Invoke(session, "Pause");
            Invoke(session, "Tick", 100f);

            Assert.That(Get<object>(session, "State").ToString(), Is.EqualTo("Paused"));
            Assert.That(Get<float>(session, "SurvivalTime"), Is.EqualTo(1f));
            Assert.That(Get<int>(session, "Score"), Is.EqualTo(360));
            Assert.That((int)Invoke(session, "AddRowsCleared", 4), Is.Zero);
            Assert.That((int)Invoke(session, "AddBonusScore", 300), Is.Zero);
            Assert.That(Get<int>(session, "RowsCleared"), Is.EqualTo(1));
        }

        [Test]
        public void GameSession_ResumeContinuesSameRun()
        {
            var session = CreateInternal("BlockEscape.Core.GameSession");
            InvokeOverload(session, "StartRun", new[] { typeof(float) }, 45f);
            Invoke(session, "Tick", 2f);
            Invoke(session, "Pause");
            Invoke(session, "Tick", 50f);
            Invoke(session, "Resume");
            Invoke(session, "Tick", 3f);

            Assert.That(Get<object>(session, "State").ToString(), Is.EqualTo("Playing"));
            Assert.That(Get<float>(session, "SurvivalTime"), Is.EqualTo(5f));
            Assert.That(Get<int>(session, "Score"), Is.EqualTo(50));
            Assert.That(Get<int>(session, "Phase"), Is.EqualTo(1));
        }

        [Test]
        public void GameSession_InvalidStateTransitionsAreIgnored()
        {
            var session = CreateInternal("BlockEscape.Core.GameSession");
            Invoke(session, "StartCountdown", 3f, 45f);

            Invoke(session, "Pause");
            Assert.That(Get<object>(session, "State").ToString(), Is.EqualTo("Countdown"));

            Invoke(session, "Resume");
            Assert.That(Get<object>(session, "State").ToString(), Is.EqualTo("Countdown"));

            Invoke(session, "Tick", 3f);
            Invoke(session, "Resume");
            Assert.That(Get<object>(session, "State").ToString(), Is.EqualTo("Playing"));

            Invoke(session, "Pause");
            Invoke(session, "Pause");
            Assert.That(Get<object>(session, "State").ToString(), Is.EqualTo("Paused"));
        }

        [Test]
        public void GameSession_EndRunCapturesCompleteImmutableResult()
        {
            var session = CreateInternal("BlockEscape.Core.GameSession");
            InvokeOverload(session, "StartRun", new[] { typeof(float) }, 2f);
            Invoke(session, "Tick", 5.5f);
            Invoke(session, "AddRowsCleared", 2);
            Invoke(session, "AddBonusScore", 300);

            var result = Invoke(session, "EndRun", "PLAYER HP 0", 17, 12345);

            Assert.That(Get<object>(session, "State").ToString(), Is.EqualTo("GameOver"));
            Assert.That(Get<int>(result, "PiecesSpawned"), Is.EqualTo(17));
            Assert.That(Get<int>(result, "RowsCleared"), Is.EqualTo(2));
            Assert.That(Get<int>(result, "Score"), Is.EqualTo(950));
            Assert.That(Get<float>(result, "SurvivalTime"), Is.EqualTo(5.5f));
            Assert.That(Get<int>(result, "Phase"), Is.EqualTo(3));
            Assert.That(Get<int>(result, "Seed"), Is.EqualTo(12345));
            Assert.That(Get<string>(result, "Reason"), Is.EqualTo("PLAYER HP 0"));
        }

        [Test]
        public void GameSession_EndRunIsIdempotent()
        {
            var session = CreateInternal("BlockEscape.Core.GameSession");
            InvokeOverload(session, "StartRun", new[] { typeof(float) }, 45f);
            Invoke(session, "Tick", 2f);

            var first = Invoke(session, "EndRun", "FIRST", 2, 10);
            var second = Invoke(session, "EndRun", "SECOND", 999, 999);

            Assert.That(Get<string>(second, "Reason"), Is.EqualTo("FIRST"));
            Assert.That(Get<int>(second, "PiecesSpawned"), Is.EqualTo(2));
            Assert.That(Get<int>(second, "Seed"), Is.EqualTo(10));
            Assert.That(Get<int>(first, "Score"), Is.EqualTo(Get<int>(second, "Score")));
        }

        [Test]
        public void GameSession_NewRunClearsPreviousResultAndCounters()
        {
            var session = CreateInternal("BlockEscape.Core.GameSession");
            InvokeOverload(session, "StartRun", new[] { typeof(float) }, 45f);
            Invoke(session, "Tick", 10f);
            Invoke(session, "AddRowsCleared", 4);
            Invoke(session, "EndRun", "DONE", 20, 77);

            InvokeOverload(session, "StartRun", new[] { typeof(float) }, 30f);

            Assert.That(Get<object>(session, "State").ToString(), Is.EqualTo("Playing"));
            Assert.That(Get<int>(session, "Score"), Is.Zero);
            Assert.That(Get<int>(session, "RowsCleared"), Is.Zero);
            Assert.That(Get<float>(session, "SurvivalTime"), Is.Zero);
            Assert.That(Get<int>(session, "Phase"), Is.EqualTo(1));
            Assert.That(Get<float>(session, "TimeUntilNextPhase"), Is.EqualTo(30f));
        }

        [TestCaseSource(nameof(VolumeSanitizeCases))]
        public void SaveData_SanitizeClampsEveryVolume(float input, float expected)
        {
            var data = new SaveData
            {
                masterVolume = input,
                musicVolume = input,
                sfxVolume = input
            };

            data.Sanitize();

            Assert.That(data.masterVolume, Is.EqualTo(expected).Within(0.0001f));
            Assert.That(data.musicVolume, Is.EqualTo(expected).Within(0.0001f));
            Assert.That(data.sfxVolume, Is.EqualTo(expected).Within(0.0001f));
        }

        [TestCase(-100, 640)]
        [TestCase(-1, 640)]
        [TestCase(0, 640)]
        [TestCase(1, 640)]
        [TestCase(639, 640)]
        [TestCase(640, 640)]
        [TestCase(641, 641)]
        [TestCase(1280, 1280)]
        [TestCase(1920, 1920)]
        [TestCase(3840, 3840)]
        public void SaveData_SanitizeClampsScreenWidth(int input, int expected)
        {
            var data = new SaveData { screenWidth = input };

            data.Sanitize();

            Assert.That(data.screenWidth, Is.EqualTo(expected));
        }

        [TestCase(-100, 360)]
        [TestCase(-1, 360)]
        [TestCase(0, 360)]
        [TestCase(1, 360)]
        [TestCase(359, 360)]
        [TestCase(360, 360)]
        [TestCase(361, 361)]
        [TestCase(720, 720)]
        [TestCase(1080, 1080)]
        [TestCase(2160, 2160)]
        public void SaveData_SanitizeClampsScreenHeight(int input, int expected)
        {
            var data = new SaveData { screenHeight = input };

            data.Sanitize();

            Assert.That(data.screenHeight, Is.EqualTo(expected));
        }

        [TestCase(-100, 0)]
        [TestCase(-1, 0)]
        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(100, 1)]
        public void SaveData_SanitizeNormalizesVSync(int input, int expected)
        {
            var data = new SaveData { vSyncCount = input };

            data.Sanitize();

            Assert.That(data.vSyncCount, Is.EqualTo(expected));
        }

        [Test]
        public void SaveData_SanitizeRepairsVersionScoresAndTimes()
        {
            var data = new SaveData
            {
                version = -10,
                highScore = -500,
                bestSurvivalTime = -45f
            };

            data.Sanitize();

            Assert.That(data.version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(data.highScore, Is.Zero);
            Assert.That(data.bestSurvivalTime, Is.Zero);
        }

        [Test]
        public void SaveService_MissingFileReturnsDocumentedDefaults()
        {
            var path = NewSavePath("missing.json");

            var data = SaveService.Load(path);

            Assert.That(File.Exists(path), Is.False);
            Assert.That(data.version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(data.highScore, Is.Zero);
            Assert.That(data.bestSurvivalTime, Is.Zero);
            Assert.That(data.masterVolume, Is.EqualTo(1f));
            Assert.That(data.musicVolume, Is.EqualTo(0.7f));
            Assert.That(data.sfxVolume, Is.EqualTo(1f));
            Assert.That(data.fullscreen, Is.True);
            Assert.That(data.screenWidth, Is.EqualTo(1920));
            Assert.That(data.screenHeight, Is.EqualTo(1080));
            Assert.That(data.vSyncCount, Is.EqualTo(1));
        }

        [Test]
        public void SaveService_SaveCreatesDirectoryAndPrettyJson()
        {
            var path = NewSavePath(Path.Combine("nested", "profile.json"));
            var data = SaveService.Load(path);
            data.highScore = 1234;
            data.bestSurvivalTime = 56.75f;

            var saved = SaveService.Save();

            Assert.That(saved, Is.True);
            Assert.That(File.Exists(path), Is.True);
            var json = File.ReadAllText(path);
            Assert.That(json, Does.Contain("\"highScore\": 1234"));
            Assert.That(json, Does.Contain("\"bestSurvivalTime\": 56.75"));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void SaveService_RepeatedSaveAtomicallyReplacesExistingFile()
        {
            var path = NewSavePath("replace.json");
            var data = SaveService.Load(path);
            data.highScore = 100;
            Assert.That(SaveService.Save(), Is.True);

            data.highScore = 200;
            data.bestSurvivalTime = 30f;
            Assert.That(SaveService.Save(), Is.True);
            var loaded = SaveService.Load(path);

            Assert.That(loaded.highScore, Is.EqualTo(200));
            Assert.That(loaded.bestSurvivalTime, Is.EqualTo(30f));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(path), "*.tmp"), Is.Empty);
        }

        [Test]
        public void SaveService_RoundTripPreservesEverySupportedField()
        {
            var path = NewSavePath("roundtrip.json");
            var data = SaveService.Load(path);
            data.highScore = 98765;
            data.bestSurvivalTime = 432.25f;
            data.masterVolume = 0.25f;
            data.musicVolume = 0.5f;
            data.sfxVolume = 0.75f;
            data.fullscreen = false;
            data.screenWidth = 1600;
            data.screenHeight = 900;
            data.vSyncCount = 0;

            Assert.That(SaveService.Save(), Is.True);
            var loaded = SaveService.Load(path);

            Assert.That(loaded.version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(loaded.highScore, Is.EqualTo(98765));
            Assert.That(loaded.bestSurvivalTime, Is.EqualTo(432.25f));
            Assert.That(loaded.masterVolume, Is.EqualTo(0.25f));
            Assert.That(loaded.musicVolume, Is.EqualTo(0.5f));
            Assert.That(loaded.sfxVolume, Is.EqualTo(0.75f));
            Assert.That(loaded.fullscreen, Is.False);
            Assert.That(loaded.screenWidth, Is.EqualTo(1600));
            Assert.That(loaded.screenHeight, Is.EqualTo(900));
            Assert.That(loaded.vSyncCount, Is.Zero);
        }

        [Test]
        public void SaveService_UnsupportedVersionIsBackedUpAndDefaultsAreUsed()
        {
            var path = NewSavePath("future.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "{\"version\":999,\"highScore\":5000}");
            LogAssert.Expect(LogType.Warning, new Regex("Save file could not be loaded.*"));

            var loaded = SaveService.Load(path);

            Assert.That(loaded.version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(loaded.highScore, Is.Zero);
            Assert.That(File.Exists(path), Is.False);
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(path), "future.json.corrupt-*").Length, Is.EqualTo(1));
        }

        [Test]
        public void SaveService_InvalidJsonIsBackedUpAndDefaultsAreUsed()
        {
            var path = NewSavePath("invalid.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "this is not json");
            LogAssert.Expect(LogType.Warning, new Regex("Save file could not be loaded.*"));

            var loaded = SaveService.Load(path);

            Assert.That(loaded.highScore, Is.Zero);
            Assert.That(loaded.bestSurvivalTime, Is.Zero);
            Assert.That(File.Exists(path), Is.False);
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(path), "invalid.json.corrupt-*").Length, Is.EqualTo(1));
        }

        [Test]
        public void SaveService_RecordRunUpdatesOnlyImprovedValues()
        {
            var path = NewSavePath("records.json");
            SaveService.Load(path);

            Assert.That(SaveService.RecordRun(500, 20f), Is.True);
            Assert.That(SaveService.Data.highScore, Is.EqualTo(500));
            Assert.That(SaveService.Data.bestSurvivalTime, Is.EqualTo(20f));

            Assert.That(SaveService.RecordRun(400, 19f), Is.False);
            Assert.That(SaveService.RecordRun(500, 20f), Is.False);
            Assert.That(SaveService.RecordRun(600, 10f), Is.True);
            Assert.That(SaveService.Data.highScore, Is.EqualTo(600));
            Assert.That(SaveService.Data.bestSurvivalTime, Is.EqualTo(20f));

            Assert.That(SaveService.RecordRun(100, 30f), Is.True);
            Assert.That(SaveService.Data.highScore, Is.EqualTo(600));
            Assert.That(SaveService.Data.bestSurvivalTime, Is.EqualTo(30f));
        }

        [TestCase(-100, -100f)]
        [TestCase(-1, -1f)]
        [TestCase(0, 0f)]
        public void SaveService_RecordRunRejectsNonImprovingInvalidValues(int score, float time)
        {
            var path = NewSavePath("invalid-records.json");
            SaveService.Load(path);

            Assert.That(SaveService.RecordRun(score, time), Is.False);
            Assert.That(SaveService.Data.highScore, Is.Zero);
            Assert.That(SaveService.Data.bestSurvivalTime, Is.Zero);
            Assert.That(File.Exists(path), Is.False);
        }

        private InputService CreateInputService(string name = "Comprehensive Input Service")
        {
            var gameObject = Track(new GameObject(name));
            var service = gameObject.AddComponent<InputService>();
            service.EnsureInitialized();
            var asset = GetInputAsset(service);
            if (asset != null && !_createdObjects.Contains(asset))
                _createdObjects.Add(asset);
            return service;
        }

        private static InputActionAsset GetInputAsset(InputService service)
        {
            return (InputActionAsset)RequireField(typeof(InputService), "_actions").GetValue(service);
        }

        private static void AssertActionHasPath(InputActionAsset asset, string mapName, string actionName, string expectedPath)
        {
            var map = asset.FindActionMap(mapName, true);
            var action = map.FindAction(actionName, true);
            var paths = action.bindings.Select(binding => binding.path).ToArray();
            Assert.That(paths, Does.Contain(expectedPath), $"{mapName}/{actionName} must bind {expectedPath}.");
        }

        private static object CreateInternal(string fullName)
        {
            var type = RuntimeAssembly.GetType(fullName);
            Assert.That(type, Is.Not.Null, $"Runtime type {fullName} is missing.");
            return Activator.CreateInstance(type);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            var argumentTypes = arguments.Select(argument => argument?.GetType() ?? typeof(object)).ToArray();
            var candidates = target.GetType().GetMethods(InstanceMembers)
                .Where(method => method.Name == methodName && method.GetParameters().Length == arguments.Length)
                .ToArray();
            var method = candidates.FirstOrDefault(candidate => ParametersAccept(candidate.GetParameters(), argumentTypes));
            Assert.That(method, Is.Not.Null, $"Method {target.GetType().Name}.{methodName} was not found.");
            return method.Invoke(target, arguments);
        }

        private static object InvokeOverload(object target, string methodName, Type[] parameterTypes, params object[] arguments)
        {
            var method = target.GetType().GetMethod(methodName, InstanceMembers, null, parameterTypes, null);
            Assert.That(method, Is.Not.Null, $"Overload {target.GetType().Name}.{methodName} was not found.");
            return method.Invoke(target, arguments);
        }

        private static bool ParametersAccept(ParameterInfo[] parameters, IReadOnlyList<Type> argumentTypes)
        {
            for (var index = 0; index < parameters.Length; index++)
            {
                if (argumentTypes[index] == typeof(object))
                    continue;
                if (!parameters[index].ParameterType.IsAssignableFrom(argumentTypes[index]))
                    return false;
            }

            return true;
        }

        private static T Get<T>(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, InstanceMembers);
            Assert.That(property, Is.Not.Null, $"Property {target.GetType().Name}.{propertyName} was not found.");
            return (T)property.GetValue(target);
        }

        private static FieldInfo RequireField(Type type, string fieldName)
        {
            var field = type.GetField(fieldName, InstanceMembers | StaticMembers);
            Assert.That(field, Is.Not.Null, $"Field {type.Name}.{fieldName} was not found.");
            return field;
        }

        private string NewSavePath(string relativePath)
        {
            return Path.Combine(_temporaryDirectory, relativePath);
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            _createdObjects.Add(value);
            return value;
        }
    }
}
