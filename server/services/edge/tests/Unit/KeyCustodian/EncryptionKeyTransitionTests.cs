// -----------------------------------------------------------------------
// <copyright file="EncryptionKeyTransitionTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

using AwesomeAssertions;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.Errors;
using D2.Edge.KeyCustodian.Domain.Keys;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using D2.Shared.ErrorCodes.Category;
using D2.Shared.Time;
using NodaTime;
using Xunit;

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
/// Illegal transitions (e.g. Activate/Retire/Compromise on RetiredKey,
/// Compromise on CompromisedKey) are proven unrepresentable at compile time.
/// They cannot be expressed as runtime tests — the compiler rejects them.
/// A comment block below documents each rejected call site:
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

    private static readonly Kid s_kid = Kid.FromTrusted("test-key-abc");
    private static readonly KeyDomain s_domain = KeyDomain.FromTrusted("audit");
    private static readonly KeyMaterialEncrypted s_mat = KeyMaterialEncrypted.FromTrusted(new byte[] { 1, 2, 3, 4 });
    private static readonly Duration s_soak = Duration.FromHours(1);
    private static readonly Duration s_grace = Duration.FromHours(2);
    private static readonly Duration s_cadence = s_soak + s_grace;

    // sr_ prefix: static readonly field, initialized after the duration fields.
    private static readonly RotationPolicy sr_policy =
        RotationPolicy.Create(s_cadence, s_grace, s_soak).Data!;

    // -----------------------------------------------------------------------
    // PendingKey.Activate — soak boundary
    // -----------------------------------------------------------------------

    [Fact]
    public void Activate_SoakExactlyElapsed_Succeeds()
    {
        // Boundary: now - CreatedAt == SmokeSoak should PASS (>=)
        var created = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var now = created + s_soak; // exact boundary
        var clock = new TestClock(now);
        var pending = MakePending(created);
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, new TestClock(now));

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
        var now = created + s_soak - Duration.FromNanoseconds(1);
        var clock = new TestClock(now);
        var pending = MakePending(created);
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock);

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
        var now = created + s_soak + Duration.FromNanoseconds(1);
        var clock = new TestClock(now);
        var pending = MakePending(created);
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock);

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
        var now = created + s_soak; // soak elapsed
        var clock = new TestClock(now);
        var pending = MakePending(created); // AesPayload key

        // Proof issued for RsaSigning — mismatch
        var rsaProof = SmokeProof.ForPassedSmokeTest(KeyType.RsaSigning, clock);
        var result = pending.Activate(rsaProof, sr_policy, clock);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SMOKE_PROOF_TYPE_MISMATCH);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
        result.Messages.Should().Contain(m => m.Key == "keycustodian_validation_SMOKE_PROOF_TYPE_MISMATCH");
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
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock);

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
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock);

        var result = pending.Activate(proof, sr_policy, clock);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SOAK_NOT_ELAPSED);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // -----------------------------------------------------------------------
    // PendingKey.Activate — null guards
    // -----------------------------------------------------------------------

    [Fact]
    public void Activate_NullProof_ThrowsArgumentNullException()
    {
        var clock = new TestClock(Instant.FromUtc(2026, 1, 1, 12, 0, 0));
        var pending = MakePending(Instant.FromUtc(2026, 1, 1, 0, 0, 0));
        var act = () => pending.Activate(null!, sr_policy, clock);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Activate_NullPolicy_ThrowsArgumentNullException()
    {
        var clock = new TestClock(Instant.FromUtc(2026, 1, 1, 12, 0, 0));
        var pending = MakePending(Instant.FromUtc(2026, 1, 1, 0, 0, 0));
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock);
        var act = () => pending.Activate(proof, null!, clock);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Activate_NullClock_ThrowsArgumentNullException()
    {
        var pending = MakePending(Instant.FromUtc(2026, 1, 1, 0, 0, 0));
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, new TestClock(Instant.FromUtc(2026, 1, 1, 12, 0, 0)));
        var act = () => pending.Activate(proof, sr_policy, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // -----------------------------------------------------------------------
    // PendingKey.Activate — carries core fields
    // -----------------------------------------------------------------------

    [Fact]
    public void Activate_Success_CarriesCoreFields()
    {
        var created = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var now = created + s_soak;
        var clock = new TestClock(now);
        var pending = MakePending(created);
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock);

        var active = pending.Activate(proof, sr_policy, clock).Data!;

        active.Kid.Should().Be(s_kid);
        active.KeyDomain.Should().Be(s_domain);
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
        var activatedAt = created + s_soak;
        var rotateAt = activatedAt + Duration.FromDays(30);
        var clock = new TestClock(rotateAt);

        var active = MakePending(created).Activate(
            SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, new TestClock(activatedAt)),
            sr_policy,
            new TestClock(activatedAt)).Data!;

        var successorPending = MakePending(rotateAt);
        var (retiring, successor) = active.Rotate(successorPending, clock);

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
        var clock = new TestClock(instant + s_soak);
        var active = MakePending(instant)
            .Activate(SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock), sr_policy, clock).Data!;

        var successor = MakePending(instant + s_soak);
        var (retiring, _) = active.Rotate(successor, clock);

        retiring.RetiringAt.Should().Be(active.ActivatedAt);
    }

    // -----------------------------------------------------------------------
    // ActiveKey.Rotate — domain/type mismatch guard
    // -----------------------------------------------------------------------

    [Fact]
    public void Rotate_MismatchedDomain_ThrowsArgumentException()
    {
        var instant = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var clock = new TestClock(instant + s_soak);
        var active = MakePending(instant)
            .Activate(SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock), sr_policy, clock).Data!;

        // Successor in a different domain
        var wrongDomain = KeyDomain.FromTrusted("courier");
        var successor = PendingKey.Create(s_kid, wrongDomain, KeyType.AesPayload, s_mat, null, instant + s_soak);

        var act = () => active.Rotate(successor, clock);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rotate_MismatchedType_ThrowsArgumentException()
    {
        var instant = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var clock = new TestClock(instant + s_soak);
        var active = MakePending(instant)
            .Activate(SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock), sr_policy, clock).Data!;

        // Successor is Secret type, but key is AesPayload
        var successor = PendingKey.Create(s_kid, s_domain, KeyType.Secret, s_mat, null, instant + s_soak);

        var act = () => active.Rotate(successor, clock);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rotate_NullSuccessor_ThrowsArgumentNullException()
    {
        var instant = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var clock = new TestClock(instant + s_soak);
        var active = MakePending(instant)
            .Activate(SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock), sr_policy, clock).Data!;

        var act = () => active.Rotate(null!, clock);
        act.Should().Throw<ArgumentNullException>();
    }

    // -----------------------------------------------------------------------
    // RetiringKey.Retire — grace boundary
    // -----------------------------------------------------------------------

    [Fact]
    public void Retire_GraceExactlyElapsed_Succeeds()
    {
        var retiring = MakeRetiringKey(out var retiringAt);
        var now = retiringAt + s_grace; // exactly at boundary
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
        var now = retiringAt + s_grace - Duration.FromNanoseconds(1);
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
        var now = retiringAt + s_grace + Duration.FromNanoseconds(1);
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
    // RetiringKey.Retire — carries all fields
    // -----------------------------------------------------------------------

    [Fact]
    public void Retire_Success_CarriesAllTimestamps()
    {
        var retiring = MakeRetiringKey(out var retiringAt);
        var now = retiringAt + s_grace;
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

        var compromised = pending.Compromise("security incident", clock);

        compromised.Status.Should().Be(KeyStatus.Compromised);
        compromised.CompromisedAt.Should().Be(now);
        compromised.Reason.Should().Be("security incident");
    }

    [Fact]
    public void Compromise_FromActive_ReturnsCompromisedKey()
    {
        var instant = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var activeClock = new TestClock(instant + s_soak);
        var active = MakePending(instant)
            .Activate(SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, activeClock), sr_policy, activeClock).Data!;

        var now = instant + s_soak + Duration.FromDays(1);
        var compromised = active.Compromise("key exposed in breach", new TestClock(now));

        compromised.Status.Should().Be(KeyStatus.Compromised);
        compromised.CompromisedAt.Should().Be(now);
    }

    [Fact]
    public void Compromise_FromRetiring_ReturnsCompromisedKey()
    {
        var retiring = MakeRetiringKey(out _);
        var now = Instant.FromUtc(2026, 2, 1, 0, 0, 0);

        var compromised = retiring.Compromise("hardware fault", new TestClock(now));

        compromised.Status.Should().Be(KeyStatus.Compromised);
    }

    [Fact]
    public void Compromise_EmptyReason_ThrowsArgumentException()
    {
        var pending = MakePending(Instant.FromUtc(2026, 1, 1, 0, 0, 0));
        var clock = new TestClock(Instant.FromUtc(2026, 1, 2, 0, 0, 0));

        var act = () => pending.Compromise(string.Empty, clock);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Compromise_ReasonOverMax_TruncatesToReasonMax()
    {
        var pending = MakePending(Instant.FromUtc(2026, 1, 1, 0, 0, 0));
        var clock = new TestClock(Instant.FromUtc(2026, 1, 2, 0, 0, 0));
        var longReason = new string('x', CompromisedKey.REASON_MAX + 100);

        var compromised = pending.Compromise(longReason, clock);

        compromised.Reason.Length.Should().Be(CompromisedKey.REASON_MAX);
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

    private static PendingKey MakePending(Instant createdAt) =>
        PendingKey.Create(s_kid, s_domain, KeyType.AesPayload, s_mat, null, createdAt);

    private static RetiringKey MakeRetiringKey(out Instant retiringAt)
    {
        var created = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var activatedAt = created + s_soak;
        retiringAt = activatedAt + Duration.FromDays(30);

        var activeClock = new TestClock(activatedAt);
        var active = MakePending(created)
            .Activate(SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, activeClock), sr_policy, activeClock).Data!;

        var (retiring, _) = active.Rotate(MakePending(retiringAt), new TestClock(retiringAt));
        return retiring;
    }
}
