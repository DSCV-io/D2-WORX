// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainModesTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Encryption;

using AwesomeAssertions;
using D2.Shared.Encryption;
using Xunit;

/// <summary>
/// Pins the spec-derived per-domain encryption-mode + consumer-service
/// lookups emitted by the EncryptionDomains SourceGen. The three sealed
/// domains (audit / notifications / courier) route to their own consumer
/// service; every other (or unknown / test-seam) domain resolves to
/// <see cref="EncryptionDomainMode.Symmetric"/> — the documented default
/// that keeps a synthetic domain from being mistaken for sealed.
/// </summary>
public sealed class EncryptionDomainModesTests
{
    [Fact]
    public void ModeEnum_HasStableUnderlyingValues()
    {
        ((int)EncryptionDomainMode.Symmetric).Should().Be(0);
        ((int)EncryptionDomainMode.Sealed).Should().Be(1);
    }

    [Theory]
    [InlineData("audit")]
    [InlineData("notifications")]
    [InlineData("courier")]
    public void ModeFor_SealedDomain_ReturnsSealed(string domain)
        => EncryptionDomainModes.ModeFor(domain).Should().Be(EncryptionDomainMode.Sealed);

    [Fact]
    public void ModeFor_PlaintextSentinel_ReturnsSymmetric()
        => EncryptionDomainModes.ModeFor(EncryptionDomains.PLAINTEXT)
            .Should().Be(EncryptionDomainMode.Symmetric);

    [Theory]
    [InlineData("payload-fixture-a")]
    [InlineData("metrics")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AUDIT")]
    public void ModeFor_UnknownOrNonCatalogDomain_ReturnsSymmetric(string domain)
        => EncryptionDomainModes.ModeFor(domain).Should().Be(EncryptionDomainMode.Symmetric);

    [Fact]
    public void ModeFor_NullDomain_ThrowsArgumentNull()
    {
        // The domain is a required non-null argument; a null value is a
        // caller contract violation and fails loud rather than masking as
        // a symmetric default.
        var act = () => EncryptionDomainModes.ModeFor(null!);
        act.Should().Throw<System.ArgumentNullException>();
    }

    [Theory]
    [InlineData("audit", "audit")]
    [InlineData("notifications", "notifications")]
    [InlineData("courier", "courier")]
    public void TryGetConsumerService_SealedDomain_ReturnsTrueAndServiceId(
        string domain, string expectedService)
    {
        var found = EncryptionDomainModes.TryGetConsumerService(domain, out var service);

        found.Should().BeTrue();
        service.Should().Be(expectedService);
    }

    [Theory]
    [InlineData("plaintext")]
    [InlineData("metrics")]
    [InlineData("payload-fixture-a")]
    [InlineData("")]
    public void TryGetConsumerService_NonSealedDomain_ReturnsFalseAndEmpty(string domain)
    {
        var found = EncryptionDomainModes.TryGetConsumerService(domain, out var service);

        found.Should().BeFalse();
        service.Should().BeEmpty();
    }

    [Fact]
    public void ConsumerServiceByDomain_ContainsExactlyTheSealedDomains()
    {
        EncryptionDomainModes.ConsumerServiceByDomain.Should().Equal(
            new System.Collections.Generic.Dictionary<string, string>
            {
                ["audit"] = "audit",
                ["notifications"] = "notifications",
                ["courier"] = "courier",
            });
    }

    [Fact]
    public void ConsumerServiceByDomain_KeysAreASubsetOfAllDomains()
        => EncryptionDomainModes.ConsumerServiceByDomain.Keys
            .Should().BeSubsetOf(EncryptionDomains.AllDomains);
}
