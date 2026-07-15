// -----------------------------------------------------------------------
// <copyright file="MqSubscriptionDescriptor.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging;

/// <summary>
/// Fully-resolved subscription contract for one handler. Codegen-emitted
/// from <c>contracts/mq-subscriptions/mq-subscriptions.spec.json</c> by
/// <c>DcsvIo.D2.Messaging.SourceGen</c>; one per <c>MqSubscriptions.X</c> constant.
/// </summary>
/// <param name="Constant">The string constant identifying this descriptor
/// (matches the value of the corresponding <c>MqSubscriptions.X</c> field).</param>
/// <param name="MessageTypeName">Fully-qualified .NET type name of the
/// message this subscription consumes. The handler's <c>TIn</c> generic
/// parameter MUST resolve to this type.</param>
/// <param name="QueueName">Queue name (or prefix for
/// <see cref="QueuePattern.FanoutExclusiveAutoDelete"/>; consumer host
/// auto-suffixes a per-replica token at startup).</param>
/// <param name="Pattern">Queue topology pattern.</param>
/// <param name="RoutingKeyBinding">AMQP routing-key binding pattern (with
/// optional <c>*</c> / <c>#</c> wildcards). Empty for fanout.</param>
/// <param name="Prefetch">Per-channel basic.qos prefetch count.</param>
/// <param name="Idempotency">When true, consumer wraps handler with
/// <see cref="IMessageIdempotencyStore"/> pre-check.</param>
/// <param name="TieredRetry">Optional broker-level retry tier topology.
/// Null = retries off; handler failures go straight to DLQ.</param>
public sealed record MqSubscriptionDescriptor(
    string Constant,
    string MessageTypeName,
    string QueueName,
    QueuePattern Pattern,
    string RoutingKeyBinding,
    int Prefetch,
    bool Idempotency,
    TieredRetryDescriptor? TieredRetry);
