// -----------------------------------------------------------------------
// <copyright file="IntegrationMessageFixtures.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.Messaging;

using D2.Shared.Messaging;
using D2.Shared.Messaging.RabbitMq.Encryption;

/// <summary>
/// Pre-seeds <see cref="MessageWireResolver"/> with descriptors for the
/// integration test fixture types. The production registry is codegen'd from
/// <c>contracts/mq-messages/mq-messages.spec.json</c> and only contains real
/// domain messages; integration fixtures live under
/// <c>D2.Shared.Tests.Integration.Messaging</c> and would fail the resolver's
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

    private static readonly Lazy<bool> sr_registration = new(
        Register, isThreadSafe: true);

    /// <summary>Idempotent — first call seeds the resolver cache for
    /// <see cref="IntegrationAuditEvent"/> (encrypted, audit domain),
    /// <see cref="IntegrationPlaintextEvent"/> (plaintext topic), and
    /// <see cref="BroadcastFixtureEvent"/> (plaintext fanout). Subsequent
    /// calls are no-ops.</summary>
    public static void EnsureRegistered() => _ = sr_registration.Value;

    private static bool Register()
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
        return true;
    }
}
