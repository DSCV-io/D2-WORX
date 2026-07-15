// -----------------------------------------------------------------------
// <copyright file="IntegrationMessageFixturesReseedTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Messaging.RabbitMq.Encryption;
using DcsvIo.D2.Tests.Integration.Messaging;
using Xunit;

/// <summary>
/// Pins that integration fixture types re-seed after a resolver cache wipe.
/// </summary>
/// <remarks>
/// Fail-without-fix: restore Lazy-once <c>EnsureRegistered</c> and this fails
/// after <see cref="MessageWireResolver.ClearCache"/> — the race that turns
/// plaintext (and other fixture) round-trips into poll-budget timeouts under
/// a full parallel unit+integration suite.
/// </remarks>
public sealed class IntegrationMessageFixturesReseedTests
{
    [Fact]
    public void EnsureRegistered_AfterFullClear_ReSeedsPlaintextAndAuditFixtures()
    {
        try
        {
            IntegrationMessageFixtures.EnsureRegistered();

            // Default ClearCache preserves RegisterForTesting pins (so unit
            // tests cannot wipe Integration fixtures mid-delivery). Full wipe
            // is the deliberate path for this pin.
            MessageWireResolver.ClearCache(includeTestPins: true);

            // Without re-seed, Resolve throws (fixture types have no [MqPub]).
            var actBefore = () => MessageWireResolver.Resolve(typeof(IntegrationPlaintextEvent));
            actBefore.Should().Throw<InvalidOperationException>();

            IntegrationMessageFixtures.EnsureRegistered();

            var plain = MessageWireResolver.Resolve(typeof(IntegrationPlaintextEvent));
            plain.IsPlaintext.Should().BeTrue();
            plain.Exchange.Should().Be("d2.test.integration-plaintext");

            var audit = MessageWireResolver.Resolve(typeof(IntegrationAuditEvent));
            audit.IsPlaintext.Should().BeFalse();
            audit.Exchange.Should().Be("d2.test.integration-audit");
            audit.Encryption.Should().Be(IntegrationMessageFixtures.SYMMETRIC_FIXTURE_DOMAIN);

            var broadcast = MessageWireResolver.Resolve(typeof(BroadcastFixtureEvent));
            broadcast.IsPlaintext.Should().BeTrue();
            broadcast.ExchangeType.Should().Be("fanout");
        }
        finally
        {
            // Leave fixtures seeded so a parallel Integration host start mid-test
            // is not left empty if this case failed after a full clear.
            IntegrationMessageFixtures.EnsureRegistered();
        }
    }

    [Fact]
    public void ClearCache_Default_PreservesRegisterForTestingPins()
    {
        try
        {
            IntegrationMessageFixtures.EnsureRegistered();
            MessageWireResolver.ClearCache(); // default: keep pins

            var plain = MessageWireResolver.Resolve(typeof(IntegrationPlaintextEvent));
            plain.Exchange.Should().Be("d2.test.integration-plaintext");
        }
        finally
        {
            IntegrationMessageFixtures.EnsureRegistered();
        }
    }
}
