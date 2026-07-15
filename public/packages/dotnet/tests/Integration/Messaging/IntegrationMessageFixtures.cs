// -----------------------------------------------------------------------
// <copyright file="IntegrationMessageFixtures.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq.Encryption;

/// <summary>
/// Pre-seeds <see cref="MessageWireResolver"/> with descriptors for the
/// integration test fixture types. The production registry is codegen'd from
/// <c>contracts/mq-messages/mq-messages.spec.json</c> and only contains real
/// domain messages; integration fixtures live under
/// <c>DcsvIo.D2.Tests.Integration.Messaging</c> and would fail the resolver's
/// FQN check, so we use the test-only registration seam.
/// </summary>
internal static class IntegrationMessageFixtures
{
    /// <summary>
    /// The synthetic SYMMETRIC encryption domain the encrypted integration fixtures ride.
    /// The real audit/notifications/courier domains are now SEALED (per-consumer-service
    /// asymmetric), so the symmetric publish/consume path is exercised on a test-seam
    /// domain — unknown to the generated catalog, therefore Symmetric by the documented
    /// <c>EncryptionDomainModes.ModeFor</c> default. §7.23 fixture marker in the value.
    /// </summary>
    public const string SYMMETRIC_FIXTURE_DOMAIN = "payload-fixture-symmetric";

    /// <summary>
    /// Idempotent re-seed of the resolver cache for
    /// <see cref="IntegrationAuditEvent"/> (encrypted, audit domain),
    /// <see cref="IntegrationPlaintextEvent"/> (plaintext topic), and
    /// <see cref="BroadcastFixtureEvent"/> (plaintext fanout).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Always overwrites — do NOT Lazy-once. Unit tests that call
    /// <see cref="MessageWireResolver.ClearCache"/> (e.g. sealed-startup /
    /// exclusive-redeclare pins) run in parallel with this Integration
    /// collection under a full <c>dotnet test</c>. A one-shot seed leaves
    /// fixture types unresolvable after a mid-suite clear: publish/consume
    /// then fails decode (or never routes) and the poll budget expires as a
    /// false "handler never saw payload" flake — most visible on the
    /// plaintext round-trip because it is the only CompetingConsumer path
    /// that is not also exercised by denser encrypted suites immediately
    /// after re-host.
    /// </para>
    /// <para>
    /// <see cref="MessageWireResolver.RegisterForTesting"/> is a dictionary
    /// set; re-registering the same three types every host start is cheap and
    /// closes the race.
    /// </para>
    /// </remarks>
    public static void EnsureRegistered()
    {
        MessageWireResolver.RegisterForTesting(
            typeof(IntegrationAuditEvent),
            new MqMessageDescriptor(
                Constant: "IntegrationAudit",
                MessageTypeName: typeof(IntegrationAuditEvent).FullName!,
                Exchange: "d2.test.integration-audit",
                ExchangeType: "topic",
                Encryption: SYMMETRIC_FIXTURE_DOMAIN,
                EncryptionReason: null,
                DefaultRoutingKey: string.Empty));
        MessageWireResolver.RegisterForTesting(
            typeof(IntegrationPlaintextEvent),
            new MqMessageDescriptor(
                Constant: "IntegrationPlaintext",
                MessageTypeName: typeof(IntegrationPlaintextEvent).FullName!,
                Exchange: "d2.test.integration-plaintext",
                ExchangeType: "topic",
                Encryption: MqMessageDescriptor.PLAINTEXT,
                EncryptionReason: "Integration fixture exercising the plaintext code path.",
                DefaultRoutingKey: string.Empty));
        MessageWireResolver.RegisterForTesting(
            typeof(BroadcastFixtureEvent),
            new MqMessageDescriptor(
                Constant: "IntegrationBroadcast",
                MessageTypeName: typeof(BroadcastFixtureEvent).FullName!,
                Exchange: "d2.test.integration-broadcast",
                ExchangeType: "fanout",
                Encryption: MqMessageDescriptor.PLAINTEXT,
                EncryptionReason:
                    "Integration fixture exercising the plaintext fanout broadcast path.",
                DefaultRoutingKey: string.Empty));
    }
}
