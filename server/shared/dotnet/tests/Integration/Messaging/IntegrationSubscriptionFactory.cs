// -----------------------------------------------------------------------
// <copyright file="IntegrationSubscriptionFactory.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.Messaging;

using D2.Shared.Messaging;

/// <summary>
/// Hand-built <see cref="MqSubscriptionDescriptor"/> factory for integration
/// tests. Production code uses the codegen'd <c>MqSubscriptions</c>
/// constants + <c>AddD2SubscribersFromAssembly</c>; tests need to construct
/// descriptors per-test (unique queue names) without polluting the
/// production spec, so they use this factory.
/// </summary>
internal static class IntegrationSubscriptionFactory
{
    /// <summary>Builds a descriptor against the test fixtures'
    /// <c>IntegrationAuditEvent</c> message type.</summary>
    /// <param name="queueName">Unique queue name for the test.</param>
    /// <param name="pattern">Queue topology pattern.</param>
    /// <param name="routingKeyBinding">AMQP routing-key binding pattern.</param>
    /// <param name="prefetch">Per-channel basic.qos prefetch count.</param>
    /// <param name="idempotency">Wrap handler with idempotency pre-check.</param>
    public static MqSubscriptionDescriptor ForAuditEvent(
        string queueName,
        QueuePattern pattern = QueuePattern.CompetingConsumer,
        string routingKeyBinding = "#",
        int prefetch = 10,
        bool idempotency = false) => new(
            Constant: "TestSub",
            MessageTypeName: typeof(IntegrationAuditEvent).FullName!,
            QueueName: queueName,
            Pattern: pattern,
            RoutingKeyBinding: routingKeyBinding,
            Prefetch: prefetch,
            Idempotency: idempotency,
            TieredRetry: null);

    /// <summary>Builds a descriptor against
    /// <c>IntegrationPlaintextEvent</c>.</summary>
    /// <param name="queueName">Unique queue name for the test.</param>
    /// <param name="pattern">Queue topology pattern.</param>
    /// <param name="routingKeyBinding">AMQP routing-key binding pattern.</param>
    /// <param name="prefetch">Per-channel basic.qos prefetch count.</param>
    /// <param name="idempotency">Wrap handler with idempotency pre-check.</param>
    public static MqSubscriptionDescriptor ForPlaintextEvent(
        string queueName,
        QueuePattern pattern = QueuePattern.CompetingConsumer,
        string routingKeyBinding = "#",
        int prefetch = 10,
        bool idempotency = false) => new(
            Constant: "TestSub",
            MessageTypeName: typeof(IntegrationPlaintextEvent).FullName!,
            QueueName: queueName,
            Pattern: pattern,
            RoutingKeyBinding: routingKeyBinding,
            Prefetch: prefetch,
            Idempotency: idempotency,
            TieredRetry: null);
}
