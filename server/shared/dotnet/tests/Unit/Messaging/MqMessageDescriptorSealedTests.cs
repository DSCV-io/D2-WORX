// -----------------------------------------------------------------------
// <copyright file="MqMessageDescriptorSealedTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Messaging;

using AwesomeAssertions;
using D2.Shared.Encryption;
using D2.Shared.Messaging;
using Xunit;

/// <summary>
/// Coverage for the descriptor's computed <see cref="MqMessageDescriptor.IsSealed"/> +
/// <see cref="MqMessageDescriptor.ConsumerService"/> properties — read from the spec-derived
/// <see cref="EncryptionDomainModes"/> catalog, never a second generated field.
/// </summary>
public sealed class MqMessageDescriptorSealedTests
{
    [Theory]
    [InlineData(EncryptionDomains.AUDIT, "audit")]
    [InlineData(EncryptionDomains.NOTIFICATIONS, "notifications")]
    [InlineData(EncryptionDomains.COURIER, "courier")]
    public void SealedDomain_IsSealed_WithConsumerService(string domain, string expectedConsumer)
    {
        var descriptor = Descriptor(domain);

        descriptor.IsSealed.Should().BeTrue();
        descriptor.ConsumerService.Should().Be(expectedConsumer);
        descriptor.IsPlaintext.Should().BeFalse();
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
