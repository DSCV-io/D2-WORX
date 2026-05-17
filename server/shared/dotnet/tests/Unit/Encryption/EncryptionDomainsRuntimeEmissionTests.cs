// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainsRuntimeEmissionTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Encryption;

using AwesomeAssertions;
using D2.Shared.Encryption;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Runtime-emission pin tests for the EncryptionDomains closed-set value
/// catalog. Spec-driving the domain NAMES via the EncryptionDomains
/// SourceGen closes the name-level drift surface; this suite closes the
/// value-level emission surface — each enumerated domain string is shipped
/// through the production registration code path (AddD2EncryptionFor +
/// keyed-services resolution) and verified to land as the keyed-service
/// discriminator byte-for-byte. A future spec entry like "audit_dr" added
/// to contracts/encryption-domains/encryption-domains.spec.json without
/// being wired to any publisher would NOT fail any name-only test, but
/// breaks ops dashboards that filter on the encryption-domain header.
/// </summary>
public sealed class EncryptionDomainsRuntimeEmissionTests
{
    [Theory]
    [InlineData("audit")]
    [InlineData("notifications")]
    [InlineData("courier")]
    [InlineData("plaintext")]
    public void EveryCatalogValue_HasMatchingConstant(string expectedValue)
    {
        // §21.10 catalog completeness: the 4 closed-set values enumerated
        // in contracts/encryption-domains/encryption-domains.spec.json
        // each have a matching EncryptionDomains constant whose value is
        // the literal wire string. AllDomains enumerates every catalog
        // entry — equality with the spec-derived list is the structural
        // guarantee against catalog drift.
        EncryptionDomains.AllDomains.Should().Contain(expectedValue);
    }

    [Theory]
    [InlineData("audit")]
    [InlineData("notifications")]
    [InlineData("courier")]
    public void DomainConstant_RegistersResolvableKeyedCrypto(string domain)
    {
        // Production emit path: composition roots call AddD2EncryptionFor
        // passing one of the EncryptionDomains constants. The discriminator
        // value flows to AddKeyedSingleton<IPayloadCrypto>(serviceKey, ...).
        // Consumers later resolve via [FromKeyedServices(domain)]
        // IPayloadCrypto. This test pins the round-trip: the literal
        // catalog value emitted by the constant is the same value that
        // resolves the keyed service.
        var services = new ServiceCollection();
        services.AddD2EncryptionFor(domain, _ => TestKeyrings.SingleKey($"{domain}-kid", domain));

        using var sp = services.BuildServiceProvider();
        var crypto = sp.GetRequiredKeyedService<IPayloadCrypto>(domain);
        crypto.Should().NotBeNull();
    }

    [Fact]
    public void PlaintextDomain_FlowsThroughMqMessageDescriptor()
    {
        // The PLAINTEXT sentinel is special — it does NOT register a keyed
        // IPayloadCrypto. Its production emit site is
        // contracts/mq-messages/mq-messages.spec.json entries with
        // "encryption": "plaintext", consumed by MessageWireResolver via
        // MqMessageDescriptor.IsPlaintext. This test pins the literal
        // value reaches the descriptor's plaintext-detection helper.
        var descriptor = new D2.Shared.Messaging.MqMessageDescriptor(
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
    public void AllDomains_EnumeratesExactlyTheSpecCatalogValues()
    {
        // §21.10 closed-set guarantee: AllDomains must enumerate every
        // entry in the spec catalog AND no extras. Drift here would
        // either drop a domain from the catalog (silently routing
        // publishes to a missing keyring) OR introduce a phantom domain
        // (publishers wire up against a value no consumer recognizes).
        EncryptionDomains.AllDomains
            .Should().BeEquivalentTo(
                ["audit", "notifications", "courier", "plaintext"],
                opts => opts.WithStrictOrdering());
    }
}
