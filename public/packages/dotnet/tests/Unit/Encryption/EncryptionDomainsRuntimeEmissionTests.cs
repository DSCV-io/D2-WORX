// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainsRuntimeEmissionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption;

using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using Xunit;

/// <summary>
/// Runtime-emission pin tests for the public EncryptionDomains closed-set
/// catalog (plaintext + framework fixture sealed domain). Product sealed
/// domains (audit / notifications / courier) live on
/// <c>ProductEncryptionDomains</c> and are not pinned here.
/// </summary>
public sealed class EncryptionDomainsRuntimeEmissionTests
{
    [Theory]
    [InlineData("plaintext")]
    [InlineData("payload-fixture-sealed")]
    public void EveryCatalogValue_HasMatchingConstant(string expectedValue)
    {
        EncryptionDomains.AllDomains.Should().Contain(expectedValue);
    }

    [Fact]
    public void ProductSealedDomains_AreAbsentFromPublicCatalog()
    {
        EncryptionDomains.AllDomains.Should().NotContain(["audit", "notifications", "courier"]);
    }

    [Fact]
    public void FixtureSealedDomain_RegistersResolvableKeyedSealerPathConstant()
    {
        // Public catalog carries one framework fixture sealed domain so sealed
        // ModeFor / compose paths remain unit-testable without product IP.
        EncryptionDomains.FIXTURE_SEALED.Should().Be("payload-fixture-sealed");
        EncryptionDomainModes.ModeFor(EncryptionDomains.FIXTURE_SEALED)
            .Should().Be(EncryptionDomainMode.Sealed);
    }

    [Fact]
    public void PlaintextDomain_FlowsThroughMqMessageDescriptor()
    {
        // The PLAINTEXT sentinel is special — it does NOT register a keyed
        // IPayloadCrypto. Its production emit site is
        // mq-messages.spec entries with "encryption": "plaintext".
        var descriptor = new DcsvIo.D2.Messaging.MqMessageDescriptor(
            Constant: "TestPlaintext",
            MessageTypeName: typeof(object).FullName!,
            Exchange: "d2.test.plaintext",
            ExchangeType: "fanout",
            Encryption: EncryptionDomains.PLAINTEXT,
            EncryptionReason: "Test fixture only.",
            DefaultRoutingKey: string.Empty);

        descriptor.IsPlaintext.Should().BeTrue(
            "the PLAINTEXT catalog value must flow through to the "
            + "wire-resolver's plaintext-detection helper byte-for-byte");
    }

    [Fact]
    public void AllDomains_EnumeratesExactlyThePublicSpecCatalogValues()
    {
        EncryptionDomains.AllDomains
            .Should().BeEquivalentTo(
                ["plaintext", "payload-fixture-sealed"],
                opts => opts.WithStrictOrdering());
    }
}
