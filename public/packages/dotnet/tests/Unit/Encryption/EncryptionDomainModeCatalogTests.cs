// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainModeCatalogTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption;

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using Xunit;

/// <summary>
/// Unit pins for the public composition overlay
/// <see cref="EncryptionDomainModeCatalog"/> — product sealed domains register
/// here so messaging can resolve <c>IsSealed</c> without private package refs.
/// Uses unique per-test domain ids so static registrations never pollute the
/// public catalog baseline used by other Shared.Tests cases.
/// </summary>
public sealed class EncryptionDomainModeCatalogTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterSealedDomain_NullOrWhitespaceDomain_Throws(string? domain)
    {
        var act = () => EncryptionDomainModeCatalog.RegisterSealedDomain(domain!, "consumer-a");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterSealedDomain_NullOrWhitespaceConsumer_Throws(string? consumer)
    {
        var domain = UniqueDomain("cat-guard");
        var act = () => EncryptionDomainModeCatalog.RegisterSealedDomain(domain, consumer!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterSealedDomain_ThenModeForAndTryGetConsumer_ReturnSealedAndServiceId()
    {
        // Fail-without-fix: ModeFor on an unregistered unique domain is Symmetric.
        var domain = UniqueDomain("cat-ok");
        const string consumer = "consumer-ok";

        EncryptionDomainModeCatalog.ModeFor(domain).Should().Be(EncryptionDomainMode.Symmetric);
        EncryptionDomainModeCatalog.TryGetConsumerService(domain, out _).Should().BeFalse();

        EncryptionDomainModeCatalog.RegisterSealedDomain(domain, consumer);

        EncryptionDomainModeCatalog.ModeFor(domain).Should().Be(EncryptionDomainMode.Sealed);
        var found = EncryptionDomainModeCatalog.TryGetConsumerService(
            domain, out var resolved);
        found.Should().BeTrue();
        resolved.Should().Be(consumer);
    }

    [Fact]
    public void RegisterSealedDomain_IdenticalReRegister_IsIdempotent()
    {
        var domain = UniqueDomain("cat-idem");
        const string consumer = "consumer-idem";

        EncryptionDomainModeCatalog.RegisterSealedDomain(domain, consumer);
        var act = () => EncryptionDomainModeCatalog.RegisterSealedDomain(domain, consumer);

        act.Should().NotThrow();
        EncryptionDomainModeCatalog.ModeFor(domain).Should().Be(EncryptionDomainMode.Sealed);
    }

    [Fact]
    public void RegisterSealedDomain_ConflictingConsumerReRegister_Throws()
    {
        var domain = UniqueDomain("cat-conflict");

        EncryptionDomainModeCatalog.RegisterSealedDomain(domain, "consumer-a");
        var act = () => EncryptionDomainModeCatalog.RegisterSealedDomain(domain, "consumer-b");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot re-map*");
    }

    [Fact]
    public void ModeFor_FallsBackToGeneratedPublicCatalog_ForFixtureSealed()
        => EncryptionDomainModeCatalog.ModeFor(EncryptionDomains.FIXTURE_SEALED)
            .Should().Be(EncryptionDomainMode.Sealed);

    [Fact]
    public void ModeFor_UnknownDomain_ReturnsSymmetric()
        => EncryptionDomainModeCatalog.ModeFor("no-such-domain-xyz")
            .Should().Be(EncryptionDomainMode.Symmetric);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ModeFor_NullOrWhitespace_ReturnsSymmetric(string? domain)
        => EncryptionDomainModeCatalog.ModeFor(domain!).Should().Be(EncryptionDomainMode.Symmetric);

    [Fact]
    public void TryGetConsumerService_FallsBackToGeneratedPublicCatalog_ForFixtureSealed()
    {
        var found = EncryptionDomainModeCatalog.TryGetConsumerService(
            EncryptionDomains.FIXTURE_SEALED, out var consumer);

        found.Should().BeTrue();
        consumer.Should().Be("payload-fixture-sealed");
    }

    [Fact]
    public void Overlay_TakesPrecedence_OverGeneratedBaseline_ForRegisteredDomain()
    {
        // Register a domain that is NOT in the public generated sealed set; overlay alone wins.
        var domain = UniqueDomain("cat-overlay");
        EncryptionDomainModeCatalog.RegisterSealedDomain(domain, "overlay-consumer");

        EncryptionDomainModeCatalog.ModeFor(domain).Should().Be(EncryptionDomainMode.Sealed);
        EncryptionDomainModes.ModeFor(domain).Should().Be(
            EncryptionDomainMode.Symmetric,
            because: "generated catalog must stay free of the overlay registration");
    }

    [Fact]
    public void RegisterSealedDomain_ConcurrentIdenticalRegister_IsSafe()
    {
        var domain = UniqueDomain("cat-conc");
        const string consumer = "consumer-conc";
        var bag = new ConcurrentBag<Exception>();

        Parallel.For(0, 32, _ =>
        {
            try
            {
                EncryptionDomainModeCatalog.RegisterSealedDomain(domain, consumer);
            }
            catch (Exception ex)
            {
                bag.Add(ex);
            }
        });

        bag.Should().BeEmpty();
        EncryptionDomainModeCatalog.ModeFor(domain).Should().Be(EncryptionDomainMode.Sealed);
        var found = EncryptionDomainModeCatalog.TryGetConsumerService(
            domain, out var resolved);
        found.Should().BeTrue();
        resolved.Should().Be(consumer);
    }

    private static string UniqueDomain(string prefix) =>
        prefix + "-" + Guid.NewGuid().ToString("N");
}
