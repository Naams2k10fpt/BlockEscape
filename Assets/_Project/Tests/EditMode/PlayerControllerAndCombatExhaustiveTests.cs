using System;
using System.Collections.Generic;
using System.Reflection;
using BlockEscape.Core;
using BlockEscape.Player;
using NUnit.Framework;
using UnityEngine;

namespace BlockEscape.Tetris.Tests
{
    /// <summary>
    /// Detailed tests for player configuration, movement state and health rules.
    /// Physics-facing private methods are invoked directly so EditMode tests can
    /// validate deterministic decisions without depending on frame timing.
    /// </summary>
    public sealed class PlayerControllerAndCombatExhaustiveTests
    {
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly List<UnityEngine.Object> _createdObjects = new();

        private static IEnumerable<TestCaseData> NonNegativeConfigCases
        {
            get
            {
                yield return new TestCaseData("moveSpeed", -100f, 0f).SetName("PlayerConfig_MoveSpeedMinus100_ClampsZero");
                yield return new TestCaseData("moveSpeed", -0.01f, 0f).SetName("PlayerConfig_MoveSpeedBelowZero_ClampsZero");
                yield return new TestCaseData("moveSpeed", 0f, 0f).SetName("PlayerConfig_MoveSpeedZero_RemainsZero");
                yield return new TestCaseData("moveSpeed", 7f, 7f).SetName("PlayerConfig_MoveSpeedSeven_RemainsSeven");
                yield return new TestCaseData("jumpVelocity", -100f, 0f).SetName("PlayerConfig_JumpMinus100_ClampsZero");
                yield return new TestCaseData("jumpVelocity", -0.01f, 0f).SetName("PlayerConfig_JumpBelowZero_ClampsZero");
                yield return new TestCaseData("jumpVelocity", 0f, 0f).SetName("PlayerConfig_JumpZero_RemainsZero");
                yield return new TestCaseData("jumpVelocity", 12.5f, 12.5f).SetName("PlayerConfig_JumpDefault_Remains");
                yield return new TestCaseData("gravityScale", -100f, 0f).SetName("PlayerConfig_GravityMinus100_ClampsZero");
                yield return new TestCaseData("gravityScale", -0.01f, 0f).SetName("PlayerConfig_GravityBelowZero_ClampsZero");
                yield return new TestCaseData("gravityScale", 0f, 0f).SetName("PlayerConfig_GravityZero_RemainsZero");
                yield return new TestCaseData("gravityScale", 3f, 3f).SetName("PlayerConfig_GravityDefault_Remains");
                yield return new TestCaseData("coyoteTime", -1f, 0f).SetName("PlayerConfig_CoyoteNegative_ClampsZero");
                yield return new TestCaseData("coyoteTime", 0f, 0f).SetName("PlayerConfig_CoyoteZero_RemainsZero");
                yield return new TestCaseData("coyoteTime", 0.1f, 0.1f).SetName("PlayerConfig_CoyoteDefault_Remains");
                yield return new TestCaseData("jumpBufferTime", -1f, 0f).SetName("PlayerConfig_BufferNegative_ClampsZero");
                yield return new TestCaseData("jumpBufferTime", 0f, 0f).SetName("PlayerConfig_BufferZero_RemainsZero");
                yield return new TestCaseData("jumpBufferTime", 0.12f, 0.12f).SetName("PlayerConfig_BufferDefault_Remains");
                yield return new TestCaseData("maxFallSpeed", -50f, 0f).SetName("PlayerConfig_FallNegative_ClampsZero");
                yield return new TestCaseData("maxFallSpeed", 0f, 0f).SetName("PlayerConfig_FallZero_RemainsZero");
                yield return new TestCaseData("maxFallSpeed", 18f, 18f).SetName("PlayerConfig_FallDefault_Remains");
            }
        }

        private static IEnumerable<TestCaseData> VariableJumpCases
        {
            get
            {
                yield return new TestCaseData(-10f, 0.1f).SetName("VariableJump_Minus10_ClampsPoint1");
                yield return new TestCaseData(-1f, 0.1f).SetName("VariableJump_Minus1_ClampsPoint1");
                yield return new TestCaseData(0f, 0.1f).SetName("VariableJump_Zero_ClampsPoint1");
                yield return new TestCaseData(0.05f, 0.1f).SetName("VariableJump_Point05_ClampsPoint1");
                yield return new TestCaseData(0.1f, 0.1f).SetName("VariableJump_Point1_Remains");
                yield return new TestCaseData(0.25f, 0.25f).SetName("VariableJump_Point25_Remains");
                yield return new TestCaseData(0.5f, 0.5f).SetName("VariableJump_Half_Remains");
                yield return new TestCaseData(0.75f, 0.75f).SetName("VariableJump_Point75_Remains");
                yield return new TestCaseData(1f, 1f).SetName("VariableJump_One_Remains");
                yield return new TestCaseData(1.01f, 1f).SetName("VariableJump_AboveOne_ClampsOne");
                yield return new TestCaseData(10f, 1f).SetName("VariableJump_Ten_ClampsOne");
            }
        }

        private static IEnumerable<TestCaseData> JumpBoostCases
        {
            get
            {
                yield return new TestCaseData(-10f, -10f, 1f, false).SetName("JumpBoost_NegativeValuesClampAndExpire");
                yield return new TestCaseData(0f, 0f, 1f, false).SetName("JumpBoost_ZeroValuesClampAndExpire");
                yield return new TestCaseData(0.5f, 5f, 1f, true).SetName("JumpBoost_SubOneMultiplierClampsOne");
                yield return new TestCaseData(1f, 5f, 1f, true).SetName("JumpBoost_OneMultiplierIsActiveWithoutIncrease");
                yield return new TestCaseData(1.2f, 8f, 1.2f, true).SetName("JumpBoost_StandardPowerup");
                yield return new TestCaseData(1.5f, 3f, 1.5f, true).SetName("JumpBoost_OnePointFive");
                yield return new TestCaseData(2f, 1f, 2f, true).SetName("JumpBoost_DoubleJump");
                yield return new TestCaseData(100f, 0.5f, 100f, true).SetName("JumpBoost_LargeMultiplierIsPreserved");
            }
        }

        private static IEnumerable<TestCaseData> DamageAmountCases
        {
            get
            {
                yield return new TestCaseData(-100, false, 3).SetName("Health_DamageMinus100Rejected");
                yield return new TestCaseData(-1, false, 3).SetName("Health_DamageMinus1Rejected");
                yield return new TestCaseData(0, false, 3).SetName("Health_DamageZeroRejected");
                yield return new TestCaseData(1, true, 2).SetName("Health_DamageOneLeavesTwo");
                yield return new TestCaseData(2, true, 1).SetName("Health_DamageTwoLeavesOne");
                yield return new TestCaseData(3, true, 0).SetName("Health_DamageThreeKills");
                yield return new TestCaseData(4, true, 0).SetName("Health_DamageFourClampsZero");
                yield return new TestCaseData(100, true, 0).SetName("Health_DamageHundredClampsZero");
                yield return new TestCaseData(int.MaxValue, true, 0).SetName("Health_MaxDamageClampsZero");
            }
        }

        private static IEnumerable<TestCaseData> HealAmountCases
        {
            get
            {
                yield return new TestCaseData(-100, false, 1).SetName("Health_HealMinus100Rejected");
                yield return new TestCaseData(-1, false, 1).SetName("Health_HealMinus1Rejected");
                yield return new TestCaseData(0, false, 1).SetName("Health_HealZeroRejected");
                yield return new TestCaseData(1, true, 2).SetName("Health_HealOneAddsOne");
                yield return new TestCaseData(2, true, 3).SetName("Health_HealTwoReachesMax");
                yield return new TestCaseData(3, true, 3).SetName("Health_HealThreeClampsMax");
                yield return new TestCaseData(100, true, 3).SetName("Health_HealHundredClampsMax");
                yield return new TestCaseData(int.MaxValue, true, 3).SetName("Health_MaxHealClampsMax");
            }
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
        }

        [Test]
        public void PlayerConfig_DefaultsMatchCurrentMovementContract()
        {
            var config = Track(ScriptableObject.CreateInstance<PlayerConfig>());

            Assert.That(config.moveSpeed, Is.EqualTo(7f));
            Assert.That(config.jumpVelocity, Is.EqualTo(12.5f));
            Assert.That(config.gravityScale, Is.EqualTo(3f));
            Assert.That(config.coyoteTime, Is.EqualTo(0.1f));
            Assert.That(config.jumpBufferTime, Is.EqualTo(0.12f));
            Assert.That(config.maxFallSpeed, Is.EqualTo(18f));
            Assert.That(config.variableJumpMultiplier, Is.EqualTo(0.5f));
            Assert.That(config.groundCheckSize, Is.EqualTo(new Vector2(0.58f, 0.08f)));
            Assert.That(config.groundCheckOffsetY, Is.EqualTo(-0.78f));
            Assert.That(config.standingColliderSize, Is.EqualTo(new Vector2(0.72f, 1.45f)));
            Assert.That(config.standingColliderOffset, Is.EqualTo(new Vector2(0f, -0.02f)));
            Assert.That(config.crouchColliderSize, Is.EqualTo(new Vector2(0.72f, 0.82f)));
            Assert.That(config.crouchColliderOffset, Is.EqualTo(new Vector2(0f, -0.335f)));
        }

        [TestCaseSource(nameof(NonNegativeConfigCases))]
        public void PlayerConfig_SanitizeClampsNonNegativeScalar(string fieldName, float input, float expected)
        {
            var config = Track(ScriptableObject.CreateInstance<PlayerConfig>());
            typeof(PlayerConfig).GetField(fieldName).SetValue(config, input);

            config.Sanitize();

            Assert.That((float)typeof(PlayerConfig).GetField(fieldName).GetValue(config), Is.EqualTo(expected));
        }

        [TestCaseSource(nameof(VariableJumpCases))]
        public void PlayerConfig_SanitizeClampsVariableJumpMultiplier(float input, float expected)
        {
            var config = Track(ScriptableObject.CreateInstance<PlayerConfig>());
            config.variableJumpMultiplier = input;

            config.Sanitize();

            Assert.That(config.variableJumpMultiplier, Is.EqualTo(expected));
        }

        [Test]
        public void PlayerConfig_SanitizeClampsGroundCheckDimensionsIndependently()
        {
            var config = Track(ScriptableObject.CreateInstance<PlayerConfig>());
            config.groundCheckSize = new Vector2(-10f, -20f);

            config.Sanitize();

            Assert.That(config.groundCheckSize.x, Is.EqualTo(0.05f));
            Assert.That(config.groundCheckSize.y, Is.EqualTo(0.02f));

            config.groundCheckSize = new Vector2(0.5f, 0.3f);
            config.Sanitize();
            Assert.That(config.groundCheckSize, Is.EqualTo(new Vector2(0.5f, 0.3f)));
        }

        [Test]
        public void PlayerConfig_SanitizeClampsBothColliderSizes()
        {
            var config = Track(ScriptableObject.CreateInstance<PlayerConfig>());
            config.standingColliderSize = new Vector2(-1f, 0f);
            config.crouchColliderSize = new Vector2(0.05f, -100f);
            var standingOffset = config.standingColliderOffset;
            var crouchOffset = config.crouchColliderOffset;

            config.Sanitize();

            Assert.That(config.standingColliderSize, Is.EqualTo(new Vector2(0.1f, 0.1f)));
            Assert.That(config.crouchColliderSize, Is.EqualTo(new Vector2(0.1f, 0.1f)));
            Assert.That(config.standingColliderOffset, Is.EqualTo(standingOffset));
            Assert.That(config.crouchColliderOffset, Is.EqualTo(crouchOffset));
        }

        [Test]
        public void PlayerController_RequireComponentCreatesPhysicsDependencies()
        {
            var player = Track(new GameObject("Player Required Components"));

            var controller = player.AddComponent<PlayerController>();

            Assert.That(controller, Is.Not.Null);
            Assert.That(player.GetComponent<Rigidbody2D>(), Is.Not.Null);
            Assert.That(player.GetComponent<CapsuleCollider2D>(), Is.Not.Null);
        }

        [Test]
        public void PlayerController_AwakeAppliesConfigPhysicsAndStandingCollider()
        {
            var config = CreateConfig();
            config.gravityScale = 4.5f;
            config.standingColliderSize = new Vector2(0.8f, 1.6f);
            config.standingColliderOffset = new Vector2(0.1f, -0.2f);
            var controller = CreateController(config, out var body, out var collider);

            Assert.That(controller.Config, Is.SameAs(config));
            Assert.That(body.gravityScale, Is.EqualTo(4.5f));
            Assert.That(body.freezeRotation, Is.True);
            Assert.That(collider.size, Is.EqualTo(config.standingColliderSize));
            Assert.That(collider.offset, Is.EqualTo(config.standingColliderOffset));
            Assert.That(collider.sharedMaterial, Is.Not.Null);
            Assert.That(collider.sharedMaterial.friction, Is.Zero);
            Assert.That(collider.sharedMaterial.bounciness, Is.Zero);
        }

        [Test]
        public void PlayerController_FrictionlessMaterialIsReusedAcrossPlayers()
        {
            var first = CreateController(CreateConfig(), out _, out var firstCollider, "First Player");
            var second = CreateController(CreateConfig(), out _, out var secondCollider, "Second Player");

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(firstCollider.sharedMaterial, Is.SameAs(secondCollider.sharedMaterial));
        }

        [TestCaseSource(nameof(JumpBoostCases))]
        public void PlayerController_ApplyJumpBoostSanitizesMultiplierAndDuration(
            float multiplier,
            float duration,
            float expectedMultiplier,
            bool expectedActive)
        {
            var config = CreateConfig();
            config.jumpVelocity = 10f;
            var controller = CreateController(config, out _, out _);

            controller.ApplyJumpBoost(multiplier, duration);

            Assert.That(controller.JumpBoostActive, Is.EqualTo(expectedActive));
            Assert.That(controller.EffectiveJumpVelocity, Is.EqualTo(10f * expectedMultiplier).Within(0.001f));
            if (expectedActive)
                Assert.That(controller.JumpBoostSecondsRemaining, Is.GreaterThan(0f));
            else
                Assert.That(controller.JumpBoostSecondsRemaining, Is.Zero);
        }

        [Test]
        public void PlayerController_SecondJumpBoostReplacesMultiplierAndRefreshesDuration()
        {
            var config = CreateConfig();
            config.jumpVelocity = 10f;
            var controller = CreateController(config, out _, out _);

            controller.ApplyJumpBoost(2f, 1f);
            controller.ApplyJumpBoost(1.2f, 8f);

            Assert.That(controller.EffectiveJumpVelocity, Is.EqualTo(12f));
            Assert.That(controller.JumpBoostSecondsRemaining, Is.GreaterThan(7.9f));
        }

        [Test]
        public void PlayerController_ClearJumpBoostRestoresBaseVelocityAndIsIdempotent()
        {
            var config = CreateConfig();
            config.jumpVelocity = 15f;
            var controller = CreateController(config, out _, out _);
            controller.ApplyJumpBoost(1.5f, 20f);

            controller.ClearJumpBoost();
            controller.ClearJumpBoost();

            Assert.That(controller.JumpBoostActive, Is.False);
            Assert.That(controller.JumpBoostSecondsRemaining, Is.Zero);
            Assert.That(controller.EffectiveJumpVelocity, Is.EqualTo(15f));
        }

        [Test]
        public void PlayerController_OnEnableClearsBufferedTimingState()
        {
            var controller = CreateController(CreateConfig(), out _, out _);
            SetField(controller, "_lastGroundedTime", Time.time);
            SetField(controller, "_lastJumpPressedTime", Time.time);
            SetField(controller, "_lastJumpStartedTime", Time.time);

            Invoke(controller, "OnEnable");

            Assert.That((float)GetField(controller, "_lastGroundedTime"), Is.EqualTo(float.NegativeInfinity));
            Assert.That((float)GetField(controller, "_lastJumpPressedTime"), Is.EqualTo(float.NegativeInfinity));
            Assert.That((float)GetField(controller, "_lastJumpStartedTime"), Is.EqualTo(float.NegativeInfinity));
            Assert.That(controller.HasRecentJumpForBlockBounce, Is.False);
        }

        [Test]
        public void PlayerController_ShouldConsumeJumpRequiresBufferAndCoyoteWindows()
        {
            var config = CreateConfig();
            config.jumpBufferTime = 0.12f;
            config.coyoteTime = 0.1f;
            var controller = CreateController(config, out _, out _);

            SetField(controller, "_lastJumpPressedTime", Time.time - 0.05f);
            SetField(controller, "_lastGroundedTime", Time.time - 0.05f);
            Assert.That((bool)Invoke(controller, "ShouldConsumeJump"), Is.True);

            SetField(controller, "_lastJumpPressedTime", Time.time - 1f);
            Assert.That((bool)Invoke(controller, "ShouldConsumeJump"), Is.False);

            SetField(controller, "_lastJumpPressedTime", Time.time);
            SetField(controller, "_lastGroundedTime", Time.time - 1f);
            Assert.That((bool)Invoke(controller, "ShouldConsumeJump"), Is.False);
        }

        [Test]
        public void PlayerController_FixedUpdateClampsHorizontalAndFallingVelocity()
        {
            var config = CreateConfig();
            config.moveSpeed = 7f;
            config.maxFallSpeed = 18f;
            var controller = CreateController(config, out var body, out _);
            SetField(controller, "_moveInput", 0.5f);
            body.linearVelocity = new Vector2(100f, -100f);

            Invoke(controller, "FixedUpdate");

            Assert.That(body.linearVelocity.x, Is.EqualTo(3.5f).Within(0.001f));
            Assert.That(body.linearVelocity.y, Is.EqualTo(-18f).Within(0.001f));
        }

        [Test]
        public void PlayerController_FixedUpdateConsumesBufferedCoyoteJump()
        {
            var config = CreateConfig();
            config.jumpVelocity = 12f;
            config.jumpBufferTime = 0.2f;
            config.coyoteTime = 0.2f;
            var controller = CreateController(config, out var body, out _);
            SetField(controller, "_lastJumpPressedTime", Time.time);
            SetField(controller, "_lastGroundedTime", Time.time);

            Invoke(controller, "FixedUpdate");

            Assert.That(body.linearVelocity.y, Is.EqualTo(12f).Within(0.001f));
            Assert.That((float)GetField(controller, "_lastJumpPressedTime"), Is.EqualTo(float.NegativeInfinity));
            Assert.That((float)GetField(controller, "_lastGroundedTime"), Is.EqualTo(float.NegativeInfinity));
            Assert.That(controller.IsGrounded, Is.False);
            Assert.That(controller.HasRecentJumpForBlockBounce, Is.True);
        }

        [Test]
        public void PlayerController_JumpReleaseAppliesVariableHeightMultiplier()
        {
            var config = CreateConfig();
            config.variableJumpMultiplier = 0.4f;
            var controller = CreateController(config, out var body, out _);
            body.linearVelocity = new Vector2(0f, 10f);
            SetField(controller, "_jumpReleasedThisFrame", true);

            Invoke(controller, "FixedUpdate");

            Assert.That(body.linearVelocity.y, Is.EqualTo(4f).Within(0.001f));
            Assert.That((bool)GetField(controller, "_jumpReleasedThisFrame"), Is.False);
        }

        [Test]
        public void PlayerController_SetCrouchingSwapsColliderDimensions()
        {
            var config = CreateConfig();
            var controller = CreateController(config, out _, out var collider);

            Invoke(controller, "SetCrouching", true);
            Assert.That(controller.IsCrouching, Is.True);
            Assert.That(collider.size, Is.EqualTo(config.crouchColliderSize));
            Assert.That(collider.offset, Is.EqualTo(config.crouchColliderOffset));

            Invoke(controller, "SetCrouching", false);
            Assert.That(controller.IsCrouching, Is.False);
            Assert.That(collider.size, Is.EqualTo(config.standingColliderSize));
            Assert.That(collider.offset, Is.EqualTo(config.standingColliderOffset));
        }

        [Test]
        public void PlayerController_CanStandReturnsTrueWithoutOverheadWorldCollider()
        {
            var controller = CreateController(CreateConfig(), out _, out _);

            Assert.That((bool)Invoke(controller, "CanStand"), Is.True);
        }

        [Test]
        public void PlayerHealth_RequireComponentCreatesRigidbody()
        {
            var player = Track(new GameObject("Health Required Component"));

            var health = player.AddComponent<PlayerHealth>();
            Invoke(health, "Awake");

            Assert.That(health, Is.Not.Null);
            Assert.That(player.GetComponent<Rigidbody2D>(), Is.Not.Null);
            Assert.That(health.CurrentHp, Is.EqualTo(health.MaxHp));
        }

        [TestCaseSource(nameof(DamageAmountCases))]
        public void PlayerHealth_TakeDamageSanitizesAmountAndClampsHp(int amount, bool expectedAccepted, int expectedHp)
        {
            var health = CreateHealth(out _);

            var accepted = health.TakeDamage(new DamageInfo(amount, Vector2.zero, null, DamageType.Enemy));

            Assert.That(accepted, Is.EqualTo(expectedAccepted));
            Assert.That(health.CurrentHp, Is.EqualTo(expectedHp));
            Assert.That(health.IsDead, Is.EqualTo(expectedHp == 0));
        }

        [Test]
        public void PlayerHealth_AcceptedDamagePublishesHealthChangedPayload()
        {
            var health = CreateHealth(out _);
            var notifications = new List<(int current, int maximum)>();
            health.HealthChanged += (current, maximum) => notifications.Add((current, maximum));

            var accepted = health.TakeDamage(new DamageInfo(2, Vector2.zero, null, DamageType.Hazard));

            Assert.That(accepted, Is.True);
            Assert.That(notifications, Has.Count.EqualTo(1));
            Assert.That(notifications[0], Is.EqualTo((1, 3)));
        }

        [Test]
        public void PlayerHealth_RejectedDamageDoesNotPublishHealthChanged()
        {
            var health = CreateHealth(out _);
            var notifications = 0;
            health.HealthChanged += (_, _) => notifications++;

            Assert.That(health.TakeDamage(new DamageInfo(0, Vector2.zero, null, DamageType.Enemy)), Is.False);
            Assert.That(health.TakeDamage(new DamageInfo(-10, Vector2.zero, null, DamageType.Enemy)), Is.False);

            Assert.That(notifications, Is.Zero);
            Assert.That(health.CurrentHp, Is.EqualTo(health.MaxHp));
        }

        [Test]
        public void PlayerHealth_DamageAppliesExactKnockback()
        {
            var health = CreateHealth(out var body);
            var knockback = new Vector2(-4.5f, 7.25f);

            Assert.That(health.TakeDamage(new DamageInfo(1, knockback, null, DamageType.Enemy)), Is.True);

            Assert.That(body.linearVelocity, Is.EqualTo(knockback));
        }

        [Test]
        public void PlayerHealth_ZeroKnockbackPreservesExistingVelocity()
        {
            var health = CreateHealth(out var body);
            body.linearVelocity = new Vector2(3f, -2f);

            Assert.That(health.TakeDamage(new DamageInfo(1, Vector2.zero, null, DamageType.Hazard)), Is.True);

            Assert.That(body.linearVelocity, Is.EqualTo(new Vector2(3f, -2f)));
        }

        [Test]
        public void PlayerHealth_InvulnerabilityRejectsDamageWithoutSideEffects()
        {
            var health = CreateHealth(out var body);
            body.linearVelocity = Vector2.one;
            health.StartInvulnerability(10f);
            var notifications = 0;
            health.HealthChanged += (_, _) => notifications++;

            var accepted = health.TakeDamage(new DamageInfo(1, new Vector2(9f, 9f), null, DamageType.Crush));

            Assert.That(accepted, Is.False);
            Assert.That(health.CurrentHp, Is.EqualTo(health.MaxHp));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.one));
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void PlayerHealth_DeathPublishesDiedExactlyOnce()
        {
            var health = CreateHealth(out _);
            var deaths = 0;
            health.Died += () => deaths++;

            Assert.That(health.TakeDamage(new DamageInfo(3, Vector2.zero, null, DamageType.Crush)), Is.True);
            Assert.That(health.TakeDamage(new DamageInfo(3, Vector2.zero, null, DamageType.Crush)), Is.False);
            Invoke(health, "Die");

            Assert.That(health.IsDead, Is.True);
            Assert.That(health.CurrentHp, Is.Zero);
            Assert.That(deaths, Is.EqualTo(1));
        }

        [Test]
        public void PlayerHealth_DeathStopsInvulnerabilityAndRestoresAlpha()
        {
            var health = CreateHealth(out _, out var renderer);
            health.StartInvulnerability(10f);
            var color = renderer.color;
            color.a = 0.35f;
            renderer.color = color;

            Invoke(health, "Die");

            Assert.That(health.IsDead, Is.True);
            Assert.That(health.IsInvulnerable, Is.False);
            Assert.That(renderer.color.a, Is.EqualTo(1f));
        }

        [TestCaseSource(nameof(HealAmountCases))]
        public void PlayerHealth_TryHealSanitizesAmountAndClampsHp(int amount, bool expectedAccepted, int expectedHp)
        {
            var health = CreateHealth(out _);
            Assert.That(health.TakeDamage(new DamageInfo(2, Vector2.zero, null, DamageType.Enemy)), Is.True);

            var accepted = health.TryHeal(amount);

            Assert.That(accepted, Is.EqualTo(expectedAccepted));
            Assert.That(health.CurrentHp, Is.EqualTo(expectedHp));
            Assert.That(health.IsDead, Is.False);
        }

        [Test]
        public void PlayerHealth_TryHealPublishesOnlyWhenHpChanges()
        {
            var health = CreateHealth(out _);
            var notifications = new List<(int current, int maximum)>();
            health.HealthChanged += (current, maximum) => notifications.Add((current, maximum));

            Assert.That(health.TryHeal(1), Is.False);
            Assert.That(health.TakeDamage(new DamageInfo(1, Vector2.zero, null, DamageType.Enemy)), Is.True);
            notifications.Clear();
            Assert.That(health.TryHeal(1), Is.True);
            Assert.That(health.TryHeal(1), Is.False);

            Assert.That(notifications, Has.Count.EqualTo(1));
            Assert.That(notifications[0], Is.EqualTo((3, 3)));
        }

        [Test]
        public void PlayerHealth_TryHealCannotReviveDeadPlayer()
        {
            var health = CreateHealth(out _);
            health.TakeDamage(new DamageInfo(3, Vector2.zero, null, DamageType.Crush));
            var notifications = 0;
            health.HealthChanged += (_, _) => notifications++;

            Assert.That(health.TryHeal(100), Is.False);

            Assert.That(health.CurrentHp, Is.Zero);
            Assert.That(health.IsDead, Is.True);
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void PlayerHealth_ResetRestoresLifeHpInvulnerabilityAndAlpha()
        {
            var health = CreateHealth(out _, out var renderer);
            health.TakeDamage(new DamageInfo(3, Vector2.zero, null, DamageType.Crush));
            var color = renderer.color;
            color.a = 0.2f;
            renderer.color = color;

            health.ResetHealth();

            Assert.That(health.CurrentHp, Is.EqualTo(health.MaxHp));
            Assert.That(health.IsDead, Is.False);
            Assert.That(health.IsInvulnerable, Is.False);
            Assert.That(renderer.color.a, Is.EqualTo(1f));
        }

        [Test]
        public void PlayerHealth_ResetSanitizesMaxHpAndPublishesPayload()
        {
            var health = CreateHealth(out _);
            SetField(health, "_maxHp", -10);
            var payload = (-1, -1);
            health.HealthChanged += (current, maximum) => payload = (current, maximum);

            health.ResetHealth();

            Assert.That(health.MaxHp, Is.EqualTo(1));
            Assert.That(health.CurrentHp, Is.EqualTo(1));
            Assert.That(payload, Is.EqualTo((1, 1)));
        }

        [Test]
        public void PlayerHealth_StartInvulnerabilityIsIgnoredWhenDead()
        {
            var health = CreateHealth(out _);
            health.TakeDamage(new DamageInfo(3, Vector2.zero, null, DamageType.Crush));

            health.StartInvulnerability(100f);

            Assert.That(health.IsInvulnerable, Is.False);
            Assert.That(GetField(health, "_iFrameRoutine"), Is.Null);
        }

        [Test]
        public void PlayerHealth_OnDisableCancelsIFramesAndRestoresAlpha()
        {
            var health = CreateHealth(out _, out var renderer);
            health.StartInvulnerability(10f);
            var color = renderer.color;
            color.a = 0.35f;
            renderer.color = color;

            Invoke(health, "OnDisable");

            Assert.That(health.IsInvulnerable, Is.False);
            Assert.That(GetField(health, "_iFrameRoutine"), Is.Null);
            Assert.That(renderer.color.a, Is.EqualTo(1f));
        }

        private PlayerConfig CreateConfig()
        {
            return Track(ScriptableObject.CreateInstance<PlayerConfig>());
        }

        private PlayerController CreateController(
            PlayerConfig config,
            out Rigidbody2D body,
            out CapsuleCollider2D collider,
            string name = "Comprehensive Player Controller")
        {
            var player = Track(new GameObject(name));
            body = player.AddComponent<Rigidbody2D>();
            collider = player.AddComponent<CapsuleCollider2D>();
            player.SetActive(false);
            var controller = player.AddComponent<PlayerController>();
            SetField(controller, "_config", config);
            player.SetActive(true);
            Invoke(controller, "Awake");
            return controller;
        }

        private PlayerHealth CreateHealth(out Rigidbody2D body)
        {
            return CreateHealth(out body, out _);
        }

        private PlayerHealth CreateHealth(out Rigidbody2D body, out SpriteRenderer renderer)
        {
            var player = Track(new GameObject("Comprehensive Player Health"));
            body = player.AddComponent<Rigidbody2D>();
            renderer = player.AddComponent<SpriteRenderer>();
            var health = player.AddComponent<PlayerHealth>();
            Invoke(health, "Awake");
            return health;
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            var methods = target.GetType().GetMethods(InstanceMembers);
            foreach (var method in methods)
            {
                if (method.Name != methodName || method.GetParameters().Length != arguments.Length)
                    continue;
                return method.Invoke(target, arguments);
            }

            Assert.Fail($"Method {target.GetType().Name}.{methodName} was not found.");
            return null;
        }

        private static object GetField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstanceMembers);
            Assert.That(field, Is.Not.Null, $"Field {target.GetType().Name}.{fieldName} was not found.");
            return field.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstanceMembers);
            Assert.That(field, Is.Not.Null, $"Field {target.GetType().Name}.{fieldName} was not found.");
            field.SetValue(target, value);
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            _createdObjects.Add(value);
            return value;
        }
    }
}
