// -----------------------------------------------------------------------
// <copyright file="SmokeProofTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

using AwesomeAssertions;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using D2.Shared.Time;
using NodaTime;
using Xunit;

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

        var proof = SmokeProof.ForPassedSmokeTest(KeyType.RsaSigning, clock);

        proof.VerifiedAt.Should().Be(instant);
        proof.VerifiedType.Should().Be(KeyType.RsaSigning);
    }

    [Fact]
    public void ForPassedSmokeTest_Aes_StampsCorrectType()
    {
        var clock = new TestClock(Instant.FromUtc(2026, 1, 1, 0, 0, 0));
        var proof = SmokeProof.ForPassedSmokeTest(KeyType.AesPayload, clock);
        proof.VerifiedType.Should().Be(KeyType.AesPayload);
    }

    [Fact]
    public void ForPassedSmokeTest_AdvancingClock_ChangesStamp()
    {
        var start = Instant.FromUtc(2026, 1, 1, 0, 0, 0);
        var clock = new TestClock(start);

        var proof1 = SmokeProof.ForPassedSmokeTest(KeyType.Secret, clock);
        clock.Advance(Duration.FromSeconds(30));
        var proof2 = SmokeProof.ForPassedSmokeTest(KeyType.Secret, clock);

        proof1.VerifiedAt.Should().NotBe(proof2.VerifiedAt);
        proof2.VerifiedAt.Should().Be(start + Duration.FromSeconds(30));
    }

    // -----------------------------------------------------------------------
    // ForPassedSmokeTest — null clock guard
    // -----------------------------------------------------------------------

    [Fact]
    public void ForPassedSmokeTest_NullClock_ThrowsArgumentNullException()
    {
        var act = () => SmokeProof.ForPassedSmokeTest(KeyType.RsaSigning, null!);
        act.Should().Throw<ArgumentNullException>();
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
