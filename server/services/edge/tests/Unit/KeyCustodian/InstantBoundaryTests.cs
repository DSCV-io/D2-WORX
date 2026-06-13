// -----------------------------------------------------------------------
// <copyright file="InstantBoundaryTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

/// <summary>
/// §25.12 adversarial tests for Instant extreme-boundary arithmetic in the
/// <see cref="EncryptionKey"/> soak and grace transition guards.
///
/// NodaTime <see cref="Instant"/> does NOT wrap — arithmetic near
/// <c>Instant.MinValue</c>/<c>Instant.MaxValue</c> throws
/// <see cref="OverflowException"/> rather than silently wrapping. These tests
/// prove the guards in <see cref="PendingKey.Activate"/> (soak math) and
/// <see cref="RetiringKey.Retire"/> (grace math) fire correctly before any
/// arithmetic that could overflow, and that realistic extreme-boundary values
/// produce predictable results rather than exceptions.
/// </summary>
public sealed class InstantBoundaryTests
{
    private static readonly KeyMaterialEncrypted sr_mat =
        KeyMaterialEncrypted.FromTrusted(new byte[] { 1, 2, 3, 4 });

    private static readonly Kid sr_kid = Kid.FromTrusted("boundary-test");
    private static readonly KeyDomain sr_domain = KeyDomain.FromTrusted("audit");

    // The policy uses 1-hour soak + 2-hour grace. These are small relative to
    // Instant range, so boundary tests are framed around keys created very
    // close to Instant.MinValue and now values very close to Instant.MaxValue.
    private static readonly Duration sr_soak = Duration.FromHours(1);
    private static readonly Duration sr_grace = Duration.FromHours(2);
    private static readonly RotationPolicy sr_policy =
        RotationPolicy.Create(sr_soak + sr_grace, sr_grace, sr_soak).Data!;

    // -----------------------------------------------------------------------
    // Soak math near Instant.MinValue (key created very early)
    // -----------------------------------------------------------------------

    [Fact]
    public void Activate_CreatedAtMinValuePlusSoak_ExactBoundary_Succeeds()
    {
        // Created at the earliest instant that still allows exactly 1-hour soak
        // before another representable Instant (MinValue + soak + epsilon).
        // now = created + soak — exact boundary — must PASS.
        var created = Instant.MinValue + sr_soak;
        var now = created + sr_soak; // exactly elapsed
        var clock = new TestClock(now);
        var pending = MakePending(created);
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock).Data!;

        var result = pending.Activate(proof, sr_policy, clock);

        result.Success.Should().BeTrue(
            because: "soak is exactly elapsed at MinValue + 2*soak");
    }

    // long test name — cannot shorten without losing meaning
    [Fact]
    public void Activate_CreatedAtMinValuePlusSoakMinus1Ns_NowAtMinValuePlusSoak_FailsSoakNotElapsed()
    {
        // now - created = 1 ns, which is less than 1 hour → SOAK_NOT_ELAPSED (no overflow).
        var created = Instant.MinValue + sr_soak;
        var now = Instant.MinValue + sr_soak + Duration.FromNanoseconds(1);
        var clock = new TestClock(now);
        var pending = MakePending(created);
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock).Data!;

        var result = pending.Activate(proof, sr_policy, clock);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SOAK_NOT_ELAPSED);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // -----------------------------------------------------------------------
    // Soak math near Instant.MaxValue (now very close to MaxValue)
    // -----------------------------------------------------------------------

    [Fact]
    public void Activate_NowNearMaxValue_SoakElapsed_Succeeds()
    {
        // Key created 2 hours before MaxValue — soak of 1 hour elapsed.
        // Arithmetic stays within Instant range.
        var created = Instant.MaxValue - Duration.FromHours(2);
        var now = created + sr_soak; // 1 hour after creation — soak exactly elapsed
        var clock = new TestClock(now);
        var pending = MakePending(created);
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock).Data!;

        var result = pending.Activate(proof, sr_policy, clock);

        result.Success.Should().BeTrue(
            because: "soak is exactly elapsed near MaxValue with no overflow");
    }

    [Fact]
    public void Activate_NowNearMaxValue_SoakNotElapsed_FailsCleanly()
    {
        // Created 30 minutes before now (which is near MaxValue) — only half the soak has
        // elapsed.
        var now = Instant.MaxValue - Duration.FromHours(1);
        var created = now - Duration.FromMinutes(30);
        var clock = new TestClock(now);
        var pending = MakePending(created);
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock).Data!;

        var result = pending.Activate(proof, sr_policy, clock);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SOAK_NOT_ELAPSED);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // -----------------------------------------------------------------------
    // Grace math near Instant.MinValue (retiring at very early instant)
    // -----------------------------------------------------------------------

    [Fact]
    public void Retire_RetiringAtMinValuePlusGrace_NowAtExactBoundary_Succeeds()
    {
        // RetiringAt = MinValue + grace; now = retiringAt + grace — exactly elapsed.
        var created = Instant.MinValue + sr_soak;
        var activatedAt = created + sr_soak;

        // use grace as offset so retiringAt + grace fits within Instant range
        var retiringAt = Instant.MinValue + sr_grace;
        var retiring = MakeRetiring(created, activatedAt, retiringAt);

        var now = retiringAt + sr_grace; // exactly elapsed
        var result = retiring.Retire(sr_policy, new TestClock(now));

        result.Success.Should().BeTrue(
            because: "grace is exactly elapsed near MinValue");
    }

    [Fact]
    public void Retire_RetiringAtMinValuePlusGrace_NowOneNsBefore_FailsGraceNotElapsed()
    {
        var created = Instant.MinValue + sr_soak;
        var activatedAt = created + sr_soak;
        var retiringAt = Instant.MinValue + sr_grace;
        var retiring = MakeRetiring(created, activatedAt, retiringAt);

        var now = retiringAt + sr_grace - Duration.FromNanoseconds(1);
        var result = retiring.Retire(sr_policy, new TestClock(now));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_GRACE_NOT_ELAPSED);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // -----------------------------------------------------------------------
    // Grace math near Instant.MaxValue (now very close to MaxValue)
    // -----------------------------------------------------------------------

    [Fact]
    public void Retire_NowNearMaxValue_GraceElapsed_Succeeds()
    {
        // retiringAt 3 hours before MaxValue; now at retiringAt + grace (2h).
        var created = Instant.MaxValue - Duration.FromHours(5);
        var activatedAt = created + sr_soak;
        var retiringAt = Instant.MaxValue - Duration.FromHours(3);
        var retiring = MakeRetiring(created, activatedAt, retiringAt);

        var now = retiringAt + sr_grace; // 2 hours later — exactly at grace
        var result = retiring.Retire(sr_policy, new TestClock(now));

        result.Success.Should().BeTrue(
            because: "grace is exactly elapsed near MaxValue with no overflow");
    }

    [Fact]
    public void Retire_NowNearMaxValue_GraceNotElapsed_FailsCleanly()
    {
        // retiringAt 3 hours before MaxValue; now only 1 hour later — grace not elapsed.
        var created = Instant.MaxValue - Duration.FromHours(5);
        var activatedAt = created + sr_soak;
        var retiringAt = Instant.MaxValue - Duration.FromHours(3);
        var retiring = MakeRetiring(created, activatedAt, retiringAt);

        var now = retiringAt + Duration.FromHours(1); // only 1h of the 2h grace has elapsed
        var result = retiring.Retire(sr_policy, new TestClock(now));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_GRACE_NOT_ELAPSED);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // -----------------------------------------------------------------------
    // Non-monotonic clock near boundaries — negative elapsed stays safe
    // -----------------------------------------------------------------------

    [Fact]
    public void Activate_NearMaxValue_NowBeforeCreated_FailsSoakNotElapsedNoOverflow()
    {
        // now < created — negative elapsed; guard fires before any problematic math.
        var created = Instant.MaxValue - Duration.FromHours(1);
        var now = created - Duration.FromSeconds(1);
        var clock = new TestClock(now);
        var pending = MakePending(created);
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock).Data!;

        var result = pending.Activate(proof, sr_policy, clock);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SOAK_NOT_ELAPSED);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void Retire_NearMaxValue_NowBeforeRetiring_FailsGraceNotElapsedNoOverflow()
    {
        var created = Instant.MaxValue - Duration.FromHours(6);
        var activatedAt = created + sr_soak;
        var retiringAt = Instant.MaxValue - Duration.FromHours(4);
        var retiring = MakeRetiring(created, activatedAt, retiringAt);

        var now = retiringAt - Duration.FromSeconds(1); // before retiringAt
        var result = retiring.Retire(sr_policy, new TestClock(now));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_GRACE_NOT_ELAPSED);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static PendingKey MakePending(Instant createdAt) =>
        PendingKey.Create(sr_kid, sr_domain, KeyType.AesPayload, sr_mat, null, createdAt).Data!;

    private static ActiveKey MakeActive(Instant createdAt, Instant activatedAt)
    {
        var clock = new TestClock(activatedAt);
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock).Data!;
        return MakePending(createdAt).Activate(proof, sr_policy, clock).Data!;
    }

    private static RetiringKey MakeRetiring(
        Instant createdAt, Instant activatedAt, Instant retiringAt)
    {
        var active = MakeActive(createdAt, activatedAt);
        var successor = MakePending(retiringAt);
        return active.Rotate(successor, new TestClock(retiringAt)).Data.Retiring;
    }
}
