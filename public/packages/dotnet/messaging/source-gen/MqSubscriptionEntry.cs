// -----------------------------------------------------------------------
// <copyright file="MqSubscriptionEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.SourceGen;

/// <summary>
/// One entry parsed from <c>mq-subscriptions.spec.json</c>. Pairs a
/// subscription identity with its consumer-side topology config (queue,
/// pattern, binding, prefetch, idempotency, optional tiered retry).
/// </summary>
/// <param name="Constant">PascalCase identifier emitted as a const string under
/// <c>MqSubscriptions</c>.</param>
/// <param name="MessageType">Fully-qualified .NET type name of the message
/// this subscription consumes. MUST match a messageType in
/// <c>mq-messages.spec.json</c>.</param>
/// <param name="QueueName">Queue name (lowercase, dot- or hyphen-separated).
/// For <c>FanoutExclusiveAutoDelete</c> patterns, treated as a prefix; a
/// per-replica suffix is auto-appended at startup.</param>
/// <param name="Pattern">Queue topology — <c>CompetingConsumer</c>,
/// <c>FanoutExclusiveAutoDelete</c>, or <c>DurableShared</c>.</param>
/// <param name="RoutingKeyBinding">Binding pattern (with AMQP wildcards
/// <c>*</c> and <c>#</c>). Empty for fanout. Null = empty.</param>
/// <param name="Prefetch">Per-channel basic.qos prefetch count. Null =
/// default 10.</param>
/// <param name="Idempotency">When true, consumer wraps handler with
/// <c>IMessageIdempotencyStore</c> pre-check. Null = false.</param>
/// <param name="TieredRetry">Optional broker-level retry tier topology.
/// Null = retries off, handler failures go straight to DLQ.</param>
internal sealed record MqSubscriptionEntry(
    string Constant,
    string MessageType,
    string QueueName,
    string Pattern,
    string? RoutingKeyBinding,
    int? Prefetch,
    bool? Idempotency,
    TieredRetryConfig? TieredRetry);
