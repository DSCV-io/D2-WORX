// -----------------------------------------------------------------------
// <copyright file="EncryptionKeyTransitionTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

/// <summary>
/// State-machine legal/illegal/guard test matrix for the <see cref="EncryptionKey"/>
/// hierarchy.
///
/// Temporal note: This domain uses NodaTime <see cref="Instant"/>/<see cref="Duration"/>
/// exclusively — both zone-free. DST gap/overlap and IANA-zone validation cases are
/// therefore N/A by construction. The §25.12 categories that apply are:
/// calendar/boundary edges, clock skew/non-monotonicity, duration edge values,
/// and TestClock boundary behavior.
///
/// Precondition note: every runtime programmer/precondition violation (null
/// argument, successor domain/type mismatch, empty compromise reason) surfaces as
/// a flagged <c>KEYCUSTODIAN_PRECONDITION_VIOLATED</c> internal-error result, NOT a
/// thrown exception. Illegal LIFECYCLE transitions (e.g. Activate/Retire/Compromise
/// on RetiredKey, Compromise on CompromisedKey) remain unrepresentable at compile
/// time — they cannot be expressed as runtime tests.
/// <code>
/// // Does-not-compile examples:
/// // var retired = ...; retired.Activate(...)    — no such method on RetiredKey
/// // var retired = ...; retired.Rotate(...)      — no such method on RetiredKey
/// // var retired = ...; retired.Retire(...)      — no such method on RetiredKey
/// // var retired = ...; retired.Compromise(...)  — no such method on RetiredKey
/// // var comp = ...;    comp.Activate(...)        — no such method on CompromisedKey
/// // var comp = ...;    comp.Compromise(...)      — no such method on CompromisedKey
/// </code>
/// </summary>
public sealed class EncryptionKeyTransitionTests
{
    // -----------------------------------------------------------------------
    // Shared test helpers
    // -----------------------------------------------------------------------

    private static readonly Kid sr_kid = Kid.FromTrusted("test-key-abc");
    private static readonly KeyDomain sr_domain = KeyDomain.FromTrusted("audit");
    private static readonly KeyMaterialEncrypted sr_mat =
        KeyMaterialEncrypted.FromTrusted(new byte[] { 1, 2, 3, 4 });

    private static readonly Duration sr_soak = Duration.FromHours(1);
    private static readonly Duration sr_grace = Duration.FromHours(2);
    private static readonly Duration sr_cadence = sr_soak + sr_grace;

    private static readonly RotationPolicy sr_policy =
        RotationPolicy.Create(sr_cadence, sr_grace, sr_soak).Data!;

    // -----------------------------------------------------------------------
    // PendingKey.Activate — soak boundary
    // -----------------------------------------------------------------------

    [Fact]
    public void Activate_SoakExactlyElapsed_Succeeds()
    {
        // Boundary: now - CreatedAt == SmokeSoak should PASS (>=)
        var created = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var now = created + sr_soak; // exact boundary
        var clock = new TestClock(now);
        var pending = MakePending(created);
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, new TestClock(now)).Data!;

        var result = pending.Activate(proof, sr_policy, clock);

        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be(KeyStatus.Active);
        result.Data!.ActivatedAt.Should().Be(now);
    }

    [Fact]
    public void Activate_SoakOneNsBeforeElapsed_Fails()
    {
        // One nanosecond before the boundary should FAIL
        var created = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var now = created + sr_soak - Duration.FromNanoseconds(1);
        var clock = new TestClock(now);
        var pending = MakePending(created);
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock).Data!;

        var result = pending.Activate(proof, sr_policy, clock);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SOAK_NOT_ELAPSED);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
        result.Messages.Should().Contain(m => m.Key == "keycustodian_validation_SOAK_NOT_ELAPSED");
    }

    [Fact]
    public void Activate_SoakOneNsAfterElapsed_Succeeds()
    {
        var created = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var now = created + sr_soak + Duration.FromNanoseconds(1);
        var clock = new TestClock(now);
        var pending = MakePending(created);
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock).Data!;

        var result = pending.Activate(proof, sr_policy, clock);

        result.Success.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // PendingKey.Activate — proof type mismatch
    // -----------------------------------------------------------------------

    [Fact]
    public void Activate_ProofTypeMismatch_Fails()
    {
        var created = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var now = created + sr_soak; // soak elapsed
        var clock = new TestClock(now);
        var pending = MakePending(created); // AesPayload key

        // Proof issued for RsaSigning — mismatch
        var rsaProof = SmokeProof.ForPassedSmokeTest(KeyType.RsaSigning, clock).Data!;
        var result = pending.Activate(rsaProof, sr_policy, clock);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SMOKE_PROOF_TYPE_MISMATCH);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
        result.Messages.Should().Contain(
            m => m.Key == "keycustodian_validation_SMOKE_PROOF_TYPE_MISMATCH");
    }

    // -----------------------------------------------------------------------
    // PendingKey.Activate — clock skew / non-monotonic
    // -----------------------------------------------------------------------

    [Fact]
    public void Activate_NowBeforeCreatedAt_FailsSoakNotElapsed()
    {
        // Non-monotonic clock: now < createdAt → elapsed is negative → < soak
        var created = Instant.FromUtc(2026, 6, 1, 12, 0, 0);
        var now = created - Duration.FromSeconds(1); // before creation
        var clock = new TestClock(now);
        var pending = MakePending(created);
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock).Data!;

        var result = pending.Activate(proof, sr_policy, clock);

        // Negative elapsed < soak → SOAK_NOT_ELAPSED (no underflow/throw)
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SOAK_NOT_ELAPSED);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
        result.Messages.Should().Contain(m => m.Key == "keycustodian_validation_SOAK_NOT_ELAPSED");
    }

    [Fact]
    public void Activate_AtCreatedAt_FailsSoakNotElapsed()
    {
        // Zero elapsed < positive soak → fails
        var created = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var clock = new TestClock(created); // now == createdAt
        var pending = MakePending(created);
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock).Data!;

        var result = pending.Activate(proof, sr_policy, clock);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SOAK_NOT_ELAPSED);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // -----------------------------------------------------------------------
    // PendingKey.Activate — null guards (flagged precondition results)
    // -----------------------------------------------------------------------

    [Fact]
    public void Activate_NullProof_FailsPreconditionViolated()
    {
        var clock = new TestClock(Instant.FromUtc(2026, 1, 1, 12, 0, 0));
        var pending = MakePending(Instant.FromUtc(2026, 1, 1, 0, 0, 0));

        var result = pending.Activate(null, sr_policy, clock);

        AssertPreconditionViolated(result);
    }

    [Fact]
    public void Activate_NullPolicy_FailsPreconditionViolated()
    {
        var clock = new TestClock(Instant.FromUtc(2026, 1, 1, 12, 0, 0));
        var pending = MakePending(Instant.FromUtc(2026, 1, 1, 0, 0, 0));
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock).Data!;

        var result = pending.Activate(proof, null, clock);

        AssertPreconditionViolated(result);
    }

    [Fact]
    public void Activate_NullClock_FailsPreconditionViolated()
    {
        var pending = MakePending(Instant.FromUtc(2026, 1, 1, 0, 0, 0));
        var proof = SmokeProof.ForPassedSmokeTest(
            KeyType.AesPayload, new TestClock(Instant.FromUtc(2026, 1, 1, 12, 0, 0))).Data!;

        var result = pending.Activate(proof, sr_policy, null);

        AssertPreconditionViolated(result);
    }

    // -----------------------------------------------------------------------
    // PendingKey.Activate — carries core fields
    // -----------------------------------------------------------------------

    [Fact]
    public void Activate_Success_CarriesCoreFields()
    {
        var created = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var now = created + sr_soak;
        var clock = new TestClock(now);
        var pending = MakePending(created);
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock).Data!;

        var active = pending.Activate(proof, sr_policy, clock).Data!;

        active.Kid.Should().Be(sr_kid);
        active.KeyDomain.Should().Be(sr_domain);
        active.KeyType.Should().Be(KeyType.AesPayload);
        active.CreatedAt.Should().Be(created);
        active.ActivatedAt.Should().Be(now);
        active.Status.Should().Be(KeyStatus.Active);
    }

    // -----------------------------------------------------------------------
    // ActiveKey.Rotate — happy path
    // -----------------------------------------------------------------------

    [Fact]
    public void Rotate_ValidSuccessor_ReturnsRetiringAndSuccessor()
    {
        var created = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var activatedAt = created + sr_soak;
        var rotateAt = activatedAt + Duration.FromDays(30);
        var clock = new TestClock(rotateAt);

        var active = MakeActive(created, activatedAt);

        var successorPending = MakePending(rotateAt);
        var result = active.Rotate(successorPending, clock);

        result.Success.Should().BeTrue();
        var (retiring, successor) = result.Data;
        retiring.Status.Should().Be(KeyStatus.Retiring);
        retiring.RetiringAt.Should().Be(rotateAt);
        retiring.ActivatedAt.Should().Be(activatedAt);
        successor.Should().BeSameAs(successorPending);
    }

    [Fact]
    public void Rotate_RetiringAtEqualsActivatedAt_IsAllowed()
    {
        // Rotate immediately after activate — RetiringAt == ActivatedAt is valid
        var instant = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var clock = new TestClock(instant + sr_soak);
        var active = MakeActive(instant, instant + sr_soak);

        var successor = MakePending(instant + sr_soak);
        var result = active.Rotate(successor, clock);

        result.Success.Should().BeTrue();
        result.Data.Retiring.RetiringAt.Should().Be(active.ActivatedAt);
    }

    // -----------------------------------------------------------------------
    // ActiveKey.Rotate — domain/type mismatch (flagged precondition result)
    // -----------------------------------------------------------------------

    [Fact]
    public void Rotate_MismatchedDomain_FailsPreconditionViolated()
    {
        var instant = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var clock = new TestClock(instant + sr_soak);
        var active = MakeActive(instant, instant + sr_soak);

        // Successor in a different domain
        var wrongDomain = KeyDomain.FromTrusted("courier");
        var successor = PendingKey.Create(
            sr_kid, wrongDomain, KeyType.AesPayload, sr_mat, null, instant + sr_soak).Data!;

        var result = active.Rotate(successor, clock);

        AssertPreconditionViolated(result);
    }

    [Fact]
    public void Rotate_MismatchedType_FailsPreconditionViolated()
    {
        var instant = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var clock = new TestClock(instant + sr_soak);
        var active = MakeActive(instant, instant + sr_soak);

        // Successor is Secret type, but key is AesPayload
        var successor = PendingKey.Create(
            sr_kid, sr_domain, KeyType.Secret, sr_mat, null, instant + sr_soak).Data!;

        var result = active.Rotate(successor, clock);

        AssertPreconditionViolated(result);
    }

    [Fact]
    public void Rotate_NullSuccessor_FailsPreconditionViolated()
    {
        var instant = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var clock = new TestClock(instant + sr_soak);
        var active = MakeActive(instant, instant + sr_soak);

        var result = active.Rotate(null, clock);

        AssertPreconditionViolated(result);
    }

    [Fact]
    public void Rotate_NullClock_FailsPreconditionViolated()
    {
        var instant = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var active = MakeActive(instant, instant + sr_soak);
        var successor = MakePending(instant + sr_soak);

        var result = active.Rotate(successor, null);

        AssertPreconditionViolated(result);
    }

    // -----------------------------------------------------------------------
    // RetiringKey.Retire — grace boundary
    // -----------------------------------------------------------------------

    [Fact]
    public void Retire_GraceExactlyElapsed_Succeeds()
    {
        var retiring = MakeRetiringKey(out var retiringAt);
        var now = retiringAt + sr_grace; // exactly at boundary
        var clock = new TestClock(now);

        var result = retiring.Retire(sr_policy, clock);

        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be(KeyStatus.Retired);
        result.Data!.RetiredAt.Should().Be(now);
    }

    [Fact]
    public void Retire_GraceOneNsBeforeElapsed_Fails()
    {
        var retiring = MakeRetiringKey(out var retiringAt);
        var now = retiringAt + sr_grace - Duration.FromNanoseconds(1);
        var clock = new TestClock(now);

        var result = retiring.Retire(sr_policy, clock);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_GRACE_NOT_ELAPSED);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
        result.Messages.Should().Contain(m => m.Key == "keycustodian_validation_GRACE_NOT_ELAPSED");
    }

    [Fact]
    public void Retire_GraceOneNsAfterElapsed_Succeeds()
    {
        var retiring = MakeRetiringKey(out var retiringAt);
        var now = retiringAt + sr_grace + Duration.FromNanoseconds(1);
        var clock = new TestClock(now);

        var result = retiring.Retire(sr_policy, clock);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Retire_NowBeforeRetiringAt_Fails()
    {
        // Non-monotonic clock: now < retiringAt → negative elapsed → GRACE_NOT_ELAPSED
        var retiring = MakeRetiringKey(out var retiringAt);
        var clock = new TestClock(retiringAt - Duration.FromSeconds(1));

        var result = retiring.Retire(sr_policy, clock);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_GRACE_NOT_ELAPSED);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // -----------------------------------------------------------------------
    // RetiringKey.Retire — null guards (flagged precondition results)
    // -----------------------------------------------------------------------

    [Fact]
    public void Retire_NullPolicy_FailsPreconditionViolated()
    {
        var retiring = MakeRetiringKey(out var retiringAt);
        var clock = new TestClock(retiringAt + sr_grace);

        var result = retiring.Retire(null, clock);

        AssertPreconditionViolated(result);
    }

    [Fact]
    public void Retire_NullClock_FailsPreconditionViolated()
    {
        var retiring = MakeRetiringKey(out _);

        var result = retiring.Retire(sr_policy, null);

        AssertPreconditionViolated(result);
    }

    // -----------------------------------------------------------------------
    // RetiringKey.Retire — carries all fields
    // -----------------------------------------------------------------------

    [Fact]
    public void Retire_Success_CarriesAllTimestamps()
    {
        var retiring = MakeRetiringKey(out var retiringAt);
        var now = retiringAt + sr_grace;
        var clock = new TestClock(now);

        var retired = retiring.Retire(sr_policy, clock).Data!;

        retired.Status.Should().Be(KeyStatus.Retired);
        retired.RetiringAt.Should().Be(retiringAt);
        retired.RetiredAt.Should().Be(now);
    }

    // -----------------------------------------------------------------------
    // Compromise — from each live state
    // -----------------------------------------------------------------------

    [Fact]
    public void Compromise_FromPending_ReturnsCompromisedKey()
    {
        var pending = MakePending(Instant.FromUtc(2026, 1, 1, 0, 0, 0));
        var now = Instant.FromUtc(2026, 1, 2, 0, 0, 0);
        var clock = new TestClock(now);

        var result = pending.Compromise("security incident", clock);

        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be(KeyStatus.Compromised);
        result.Data!.CompromisedAt.Should().Be(now);
        result.Data!.Reason.Should().Be("security incident");
    }

    [Fact]
    public void Compromise_FromActive_ReturnsCompromisedKey()
    {
        var instant = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var active = MakeActive(instant, instant + sr_soak);

        var now = instant + sr_soak + Duration.FromDays(1);
        var result = active.Compromise("key exposed in breach", new TestClock(now));

        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be(KeyStatus.Compromised);
        result.Data!.CompromisedAt.Should().Be(now);
    }

    [Fact]
    public void Compromise_ActiveNullClock_FailsPreconditionViolated()
    {
        var instant = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var active = MakeActive(instant, instant + sr_soak);

        var result = active.Compromise("key exposed in breach", null);

        AssertPreconditionViolated(result);
    }

    [Fact]
    public void Compromise_FromRetiring_ReturnsCompromisedKey()
    {
        var retiring = MakeRetiringKey(out _);
        var now = Instant.FromUtc(2026, 2, 1, 0, 0, 0);

        var result = retiring.Compromise("hardware fault", new TestClock(now));

        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be(KeyStatus.Compromised);
    }

    [Fact]
    public void Compromise_RetiringNullClock_FailsPreconditionViolated()
    {
        var retiring = MakeRetiringKey(out _);

        var result = retiring.Compromise("hardware fault", null);

        AssertPreconditionViolated(result);
    }

    [Fact]
    public void Compromise_EmptyReason_FailsPreconditionViolated()
    {
        var pending = MakePending(Instant.FromUtc(2026, 1, 1, 0, 0, 0));
        var clock = new TestClock(Instant.FromUtc(2026, 1, 2, 0, 0, 0));

        var result = pending.Compromise(string.Empty, clock);

        AssertPreconditionViolated(result);
    }

    [Fact]
    public void Compromise_WhitespaceReason_FailsPreconditionViolated()
    {
        var pending = MakePending(Instant.FromUtc(2026, 1, 1, 0, 0, 0));
        var clock = new TestClock(Instant.FromUtc(2026, 1, 2, 0, 0, 0));

        var result = pending.Compromise("   ", clock);

        AssertPreconditionViolated(result);
    }

    [Fact]
    public void Compromise_NullClock_FailsPreconditionViolated()
    {
        var pending = MakePending(Instant.FromUtc(2026, 1, 1, 0, 0, 0));

        var result = pending.Compromise("security incident", null);

        AssertPreconditionViolated(result);
    }

    [Fact]
    public void Compromise_ReasonOverMax_TruncatesToReasonMax()
    {
        var pending = MakePending(Instant.FromUtc(2026, 1, 1, 0, 0, 0));
        var clock = new TestClock(Instant.FromUtc(2026, 1, 2, 0, 0, 0));
        var longReason = new string('x', CompromisedKey.REASON_MAX + 100);

        var result = pending.Compromise(longReason, clock);

        result.Success.Should().BeTrue();
        result.Data!.Reason.Length.Should().Be(CompromisedKey.REASON_MAX);
    }

    // -----------------------------------------------------------------------
    // Status — compile-time constant per sealed type
    // -----------------------------------------------------------------------

    [Fact]
    public void Status_PerType_IsFixedConstant()
    {
        var created = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var pending = MakePending(created);
        pending.Status.Should().Be(KeyStatus.Pending);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static void AssertPreconditionViolated<T>(
        D2.Shared.Result.D2Result<T> result)
    {
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_PRECONDITION_VIOLATED);
        result.Category.Should().Be(ErrorCategory.InternalError);
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.InternalServerError);

        // PRECONDITION_VIOLATED is a 500/internal_error opaque code — the internal
        // argument name must NOT leak onto the wire. Assert the message key is
        // present but carries no "arg" parameter.
        var message = result.Messages.Single(
            m => m.Key == "keycustodian_internal_PRECONDITION_VIOLATED");
        var hasArgLeak = message.Parameters?.ContainsKey("arg") ?? false;
        hasArgLeak.Should().BeFalse(
            because: "internal C# parameter names must not be serialized onto the wire");
    }

    private static PendingKey MakePending(Instant createdAt) =>
        PendingKey.Create(sr_kid, sr_domain, KeyType.AesPayload, sr_mat, null, createdAt).Data!;

    private static ActiveKey MakeActive(Instant createdAt, Instant activatedAt)
    {
        var clock = new TestClock(activatedAt);
        return MakePending(createdAt)
            .Activate(
                SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock).Data!, sr_policy, clock)
            .Data!;
    }

    private static RetiringKey MakeRetiringKey(out Instant retiringAt)
    {
        var created = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var activatedAt = created + sr_soak;
        retiringAt = activatedAt + Duration.FromDays(30);

        var active = MakeActive(created, activatedAt);
        return active.Rotate(MakePending(retiringAt), new TestClock(retiringAt)).Data.Retiring;
    }
}
