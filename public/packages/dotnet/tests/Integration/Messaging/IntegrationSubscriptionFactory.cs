// -----------------------------------------------------------------------
// <copyright file="IntegrationSubscriptionFactory.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using DcsvIo.D2.Messaging;

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

    /// <summary>Builds a descriptor against
    /// <c>BroadcastFixtureEvent</c> (plaintext fanout). Defaults to the
    /// <see cref="QueuePattern.FanoutExclusiveAutoDelete"/> pattern with an
    /// empty routing-key binding — the shape a fanout broadcast subscriber
    /// declares (each replica / distinct service gets its own exclusive queue
    /// bound to the shared fanout exchange).</summary>
    /// <param name="queueName">Base queue name for the test (the runtime
    /// appends a per-process suffix for the fanout-exclusive pattern).</param>
    /// <param name="prefetch">Per-channel basic.qos prefetch count.</param>
    public static MqSubscriptionDescriptor ForBroadcastEvent(
        string queueName,
        int prefetch = 10) => new(
            Constant: "TestSub",
            MessageTypeName: typeof(BroadcastFixtureEvent).FullName!,
            QueueName: queueName,
            Pattern: QueuePattern.FanoutExclusiveAutoDelete,
            RoutingKeyBinding: string.Empty,
            Prefetch: prefetch,
            Idempotency: false,
            TieredRetry: null);
}
