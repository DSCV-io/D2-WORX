// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainModesTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption;

using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using Xunit;

/// <summary>
/// Pins public EncryptionDomainModes after dual-values strip. The only sealed
/// domain on the public half is the framework fixture
/// <see cref="EncryptionDomains.FIXTURE_SEALED"/>. Product sealed domains
/// (audit / notifications / courier) resolve as unknown → Symmetric here and
/// are pinned on ProductEncryptionDomainModes in private hosts.
/// </summary>
public sealed class EncryptionDomainModesTests
{
    [Fact]
    public void ModeEnum_HasStableUnderlyingValues()
    {
        ((int)EncryptionDomainMode.Symmetric).Should().Be(0);
        ((int)EncryptionDomainMode.Sealed).Should().Be(1);
    }

    [Fact]
    public void ModeFor_FixtureSealedDomain_ReturnsSealed()
        => EncryptionDomainModes.ModeFor(EncryptionDomains.FIXTURE_SEALED)
            .Should().Be(EncryptionDomainMode.Sealed);

    [Fact]
    public void ModeFor_PlaintextSentinel_ReturnsSymmetric()
        => EncryptionDomainModes.ModeFor(EncryptionDomains.PLAINTEXT)
            .Should().Be(EncryptionDomainMode.Symmetric);

    [Theory]
    [InlineData("audit")]
    [InlineData("notifications")]
    [InlineData("courier")]
    [InlineData("payload-fixture-a")]
    [InlineData("metrics")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AUDIT")]
    public void ModeFor_ProductOrUnknownDomain_ReturnsSymmetric(string domain)
        => EncryptionDomainModes.ModeFor(domain).Should().Be(EncryptionDomainMode.Symmetric);

    [Fact]
    public void ModeFor_NullDomain_ThrowsArgumentNull()
    {
        var act = () => EncryptionDomainModes.ModeFor(null!);
        act.Should().Throw<System.ArgumentNullException>();
    }

    [Fact]
    public void TryGetConsumerService_FixtureSealedDomain_ReturnsTrueAndServiceId()
    {
        var found = EncryptionDomainModes.TryGetConsumerService(
            EncryptionDomains.FIXTURE_SEALED, out var service);

        found.Should().BeTrue();
        service.Should().Be("payload-fixture-sealed");
    }

    [Theory]
    [InlineData("plaintext")]
    [InlineData("audit")]
    [InlineData("notifications")]
    [InlineData("courier")]
    [InlineData("metrics")]
    [InlineData("payload-fixture-a")]
    [InlineData("")]
    public void TryGetConsumerService_NonPublicSealedDomain_ReturnsFalseAndEmpty(string domain)
    {
        var found = EncryptionDomainModes.TryGetConsumerService(domain, out var service);

        found.Should().BeFalse();
        service.Should().BeEmpty();
    }

    [Fact]
    public void ConsumerServiceByDomain_ContainsExactlyThePublicSealedFixture()
    {
        EncryptionDomainModes.ConsumerServiceByDomain.Should().Equal(
            new System.Collections.Generic.Dictionary<string, string>
            {
                ["payload-fixture-sealed"] = "payload-fixture-sealed",
            });
    }

    [Fact]
    public void ConsumerServiceByDomain_KeysAreASubsetOfAllDomains()
        => EncryptionDomainModes.ConsumerServiceByDomain.Keys
            .Should().BeSubsetOf(EncryptionDomains.AllDomains);
}
