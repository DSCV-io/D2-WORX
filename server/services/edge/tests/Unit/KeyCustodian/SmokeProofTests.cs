// -----------------------------------------------------------------------
// <copyright file="SmokeProofTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

using System.Linq;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.Errors;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using D2.Shared.ErrorCodes.Category;
using D2.Shared.Time;
using NodaTime;

/// <summary>
/// Adversarial unit tests for <see cref="SmokeProof"/>.
/// </summary>
public sealed class SmokeProofTests
{
    // -----------------------------------------------------------------------
    // ForPassedSmokeTest — happy path
    // -----------------------------------------------------------------------

    [Fact]
    public void ForPassedSmokeTest_Rsa_StampsClockInstant()
    {
        var instant = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var clock = new TestClock(instant);

        var proof = SmokeProof.ForPassedSmokeTest(KeyType.RsaSigning, clock).Data!;

        proof.VerifiedAt.Should().Be(instant);
        proof.VerifiedType.Should().Be(KeyType.RsaSigning);
    }

    [Fact]
    public void ForPassedSmokeTest_Aes_StampsCorrectType()
    {
        var clock = new TestClock(Instant.FromUtc(2026, 1, 1, 0, 0, 0));
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock).Data!;
        proof.VerifiedType.Should().Be(KeyType.AesPayload);
    }

    [Fact]
    public void ForPassedSmokeTest_AdvancingClock_ChangesStamp()
    {
        var start = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var clock = new TestClock(start);

        var proof1 = SmokeProof.ForPassedSmokeTest(KeyType.Secret, clock).Data!;
        clock.Advance(Duration.FromSeconds(30));
        var proof2 = SmokeProof.ForPassedSmokeTest(KeyType.Secret, clock).Data!;

        proof1.VerifiedAt.Should().NotBe(proof2.VerifiedAt);
        proof2.VerifiedAt.Should().Be(start + Duration.FromSeconds(30));
    }

    // -----------------------------------------------------------------------
    // ForPassedSmokeTest — null clock guard (A3-F1 regression pin)
    // -----------------------------------------------------------------------

    [Fact]
    public void ForPassedSmokeTest_NullClock_FailsPreconditionViolated()
    {
        var result = SmokeProof.ForPassedSmokeTest(KeyType.RsaSigning, null);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_PRECONDITION_VIOLATED);
        result.Category.Should().Be(ErrorCategory.InternalError);

        var message = result.Messages.Single(
            m => m.Key == "keycustodian_internal_PRECONDITION_VIOLATED");

        // PRECONDITION_VIOLATED is an opaque 500 code — internal argument names
        // must not leak onto the wire.
        var hasArgLeak = message.Parameters?.ContainsKey("arg") ?? false;
        hasArgLeak.Should().BeFalse(
            because: "internal C# parameter names must not be serialized onto the wire");
    }

    // -----------------------------------------------------------------------
    // ForPassedSmokeTest — success result carries correct data
    // -----------------------------------------------------------------------

    [Fact]
    public void ForPassedSmokeTest_ValidClock_ReturnsSuccessWithCorrectVerifiedType()
    {
        var instant = Instant.FromUtc(2026, 3, 15, 10, 30, 0);
        var clock = new TestClock(instant);

        var result = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock);

        result.Success.Should().BeTrue();
        result.Data!.VerifiedType.Should().Be(KeyType.AesPayload);
        result.Data!.VerifiedAt.Should().Be(instant);
    }

    // -----------------------------------------------------------------------
    // No public positional constructor
    // Construction is gated through the factory — the type enforces this
    // at compile time; the test documents the contract.
    // -----------------------------------------------------------------------

    [Fact]
    public void SmokeProof_NoPublicConstructor_OnlyFactoryConstruction()
    {
        var constructors = typeof(SmokeProof)
            .GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        // The only construction path is ForPassedSmokeTest — no public ctor.
        constructors.Should().BeEmpty();
    }
}
