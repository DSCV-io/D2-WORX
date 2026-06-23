// -----------------------------------------------------------------------
// <copyright file="KeyRotatedEventTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Events;

using System.Reflection;
using AwesomeAssertions;
using D2.Shared.Auth.Events;
using D2.Shared.Messaging;
using D2.Shared.Messaging.RabbitMq.Encryption;
using Xunit;

/// <summary>
/// Pins the publish contract for <see cref="KeyRotatedEvent"/>. The publisher's
/// <see cref="MessageWireResolver"/> reads <see cref="MqPubAttribute"/> off the
/// runtime CLR type via reflection and fails default-deny if it is absent — so the
/// attribute MUST sit on the exact single sealed record that is instantiated and
/// published, and the type's FQN MUST equal the <c>messageType</c> declared in
/// <c>contracts/mq-messages/mq-messages.spec.json</c>. These tests exercise that
/// exact production resolution path against the real codegen registry.
/// </summary>
public sealed class KeyRotatedEventTests
{
    private const string _EXPECTED_FQN = "D2.Shared.Auth.Events.KeyRotatedEvent";

    [Fact]
    public void KeyRotatedEvent_CarriesMqPubAttribute_OnThePublishedType()
    {
        // The attribute must be on the concrete type the publisher inspects —
        // inherit:false matches the resolver's own lookup. A cross-assembly
        // partial would NOT merge here, so this guards the regression directly.
        var attr = typeof(KeyRotatedEvent)
            .GetCustomAttribute<MqPubAttribute>(inherit: false);

        attr.Should().NotBeNull();
        attr.Constant.Should().Be(MqMessages.AuthKeyRotated);
    }

    [Fact]
    public void KeyRotatedEvent_FullName_MatchesSpecMessageType()
    {
        // The spec's messageType string is what the resolver cross-checks against
        // the descriptor; drift here is exactly what breaks publish.
        typeof(KeyRotatedEvent).FullName.Should().Be(_EXPECTED_FQN);
    }

    [Fact]
    public void KeyRotatedEvent_ResolvesAgainstProductionRegistry_ToPlaintextFanout()
    {
        // Full production path: [MqPub] -> MqMessagesRegistry -> descriptor, with
        // the FQN-vs-spec cross-check the resolver enforces.
        //
        // NOTE: ClearCache() was removed here. KeyRotatedEvent is a production type
        // in MqMessagesRegistry, so its cache entry is always correct (seeded by the
        // production Resolve path). The only alternative seeder — RegisterForTesting()
        // used by IntegrationMessageFixtures — never registers KeyRotatedEvent. A
        // global ClearCache() call from a unit test evicts entries that
        // parallel integration tests rely on via RegisterForTesting(), causing a
        // race where the integration test's fixture types (which have no [MqPub]
        // attribute and are NOT in MqMessagesRegistry) throw on re-resolution.
        var descriptor = MessageWireResolver.Resolve(typeof(KeyRotatedEvent));

        descriptor.Constant.Should().Be(MqMessages.AuthKeyRotated);
        descriptor.MessageTypeName.Should().Be(_EXPECTED_FQN);
        descriptor.Exchange.Should().Be("d2.security.key-rotated");
        descriptor.ExchangeType.Should().Be("fanout");
        descriptor.IsPlaintext.Should().BeTrue();
    }

    [Fact]
    public void KeyRotatedEvent_RecordEquality_IsValueBased()
    {
        var a = new KeyRotatedEvent
        {
            Domain = "jwks-signing",
            Kid = "kid-1",
            NewStatus = "Active",
            Urgent = false,
        };
        var b = new KeyRotatedEvent
        {
            Domain = "jwks-signing",
            Kid = "kid-1",
            NewStatus = "Active",
            Urgent = false,
        };

        b.Should().Be(a);
        (b == a).Should().BeTrue();
    }

    [Fact]
    public void KeyRotatedEvent_UrgentDiffers_BreaksEquality()
    {
        var routine = new KeyRotatedEvent
        {
            Domain = "jwks-signing",
            Kid = "kid-1",
            NewStatus = "Active",
            Urgent = false,
        };
        var urgent = routine with { Urgent = true };

        urgent.Should().NotBe(routine);
    }
}
