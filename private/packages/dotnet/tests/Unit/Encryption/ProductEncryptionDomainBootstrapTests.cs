// -----------------------------------------------------------------------
// <copyright file="ProductEncryptionDomainBootstrapTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Packages.Tests.Unit.Encryption;

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using DcsvIo.D2.Private.Encryption;
using Xunit;

/// <summary>
/// Pins product sealed-domain bootstrap onto the public
/// <see cref="EncryptionDomainModeCatalog"/> overlay (audit / notifications / courier).
/// Fail-without-fix: public generated catalog alone leaves product domains Symmetric.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ProductEncryptionDomainBootstrapTests
{
    [Theory]
    [InlineData("audit")]
    [InlineData("notifications")]
    [InlineData("courier")]
    public void EnsureRegistered_ProductSealedDomains_ModeForIsSealed(string domain)
    {
        // Generated public catalog still resolves product domains as Symmetric (dual-values split).
        EncryptionDomainModes.ModeFor(domain).Should().Be(EncryptionDomainMode.Symmetric);

        ProductEncryptionDomainBootstrap.EnsureRegistered();

        EncryptionDomainModeCatalog.ModeFor(domain).Should().Be(EncryptionDomainMode.Sealed);
        var found = EncryptionDomainModeCatalog.TryGetConsumerService(
            domain, out var consumer);
        found.Should().BeTrue();
        consumer.Should().Be(domain);
    }

    [Fact]
    public void EnsureRegistered_SecondCall_IsIdempotent()
    {
        ProductEncryptionDomainBootstrap.EnsureRegistered();
        var act = ProductEncryptionDomainBootstrap.EnsureRegistered;

        act.Should().NotThrow();
        EncryptionDomainModeCatalog.ModeFor("audit").Should().Be(EncryptionDomainMode.Sealed);
    }

    [Fact]
    public void EnsureRegistered_ConcurrentCalls_AllSeeSealedAfterJoin()
    {
        var bag = new ConcurrentBag<Exception>();

        Parallel.For(0, 16, _ =>
        {
            try
            {
                ProductEncryptionDomainBootstrap.EnsureRegistered();
            }
            catch (Exception ex)
            {
                bag.Add(ex);
            }
        });

        bag.Should().BeEmpty();
        EncryptionDomainModeCatalog.ModeFor("audit").Should().Be(EncryptionDomainMode.Sealed);
        EncryptionDomainModeCatalog.ModeFor("notifications")
            .Should().Be(EncryptionDomainMode.Sealed);
        EncryptionDomainModeCatalog.ModeFor("courier")
            .Should().Be(EncryptionDomainMode.Sealed);
    }
}
