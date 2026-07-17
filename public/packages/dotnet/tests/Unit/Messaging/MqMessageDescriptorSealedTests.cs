// -----------------------------------------------------------------------
// <copyright file="MqMessageDescriptorSealedTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using DcsvIo.D2.Messaging;
using Xunit;

/// <summary>
/// Coverage for the descriptor's computed <see cref="MqMessageDescriptor.IsSealed"/> +
/// <see cref="MqMessageDescriptor.ConsumerService"/> properties — resolved via
/// <see cref="EncryptionDomainModeCatalog"/> (overlay first, then generated
/// <see cref="EncryptionDomainModes"/> baseline) after dual-values strip.
/// </summary>
public sealed class MqMessageDescriptorSealedTests
{
    [Fact]
    public void FixtureSealedDomain_IsSealed_WithConsumerService()
    {
        var descriptor = Descriptor(EncryptionDomains.FIXTURE_SEALED);

        descriptor.IsSealed.Should().BeTrue();
        descriptor.ConsumerService.Should().Be("payload-fixture-sealed");
        descriptor.IsPlaintext.Should().BeFalse();
    }

    [Theory]
    [InlineData("audit")]
    [InlineData("notifications")]
    [InlineData("courier")]
    public void ProductSealedDomainStrings_AreNotSealedOnPublicCatalog(string domain)
    {
        // Product sealed domains live on ProductEncryptionDomainModes only.
        // On the public catalog they resolve as unknown → Symmetric.
        var descriptor = Descriptor(domain);

        descriptor.IsSealed.Should().BeFalse();
        descriptor.ConsumerService.Should().BeNull();
    }

    [Fact]
    public void PlaintextDomain_IsNotSealed_NoConsumerService()
    {
        var descriptor = Descriptor(MqMessageDescriptor.PLAINTEXT);

        descriptor.IsSealed.Should().BeFalse();
        descriptor.ConsumerService.Should().BeNull();
        descriptor.IsPlaintext.Should().BeTrue();
    }

    [Fact]
    public void UnknownDomain_DefaultsToSymmetric_NotSealed()
    {
        // A synthetic test-seam domain is by construction not sealed (sealed-ness can only
        // originate in the spec catalog) — the documented ModeFor unknown → Symmetric default.
        var descriptor = Descriptor("some-fixture-domain");

        descriptor.IsSealed.Should().BeFalse();
        descriptor.ConsumerService.Should().BeNull();
    }

    private static MqMessageDescriptor Descriptor(string domain) => new(
        Constant: "TestConstant",
        MessageTypeName: "D2.Test.SampleMessage",
        Exchange: "d2.test.events",
        ExchangeType: "fanout",
        Encryption: domain,
        EncryptionReason: domain == MqMessageDescriptor.PLAINTEXT ? "test" : null,
        DefaultRoutingKey: string.Empty);
}
