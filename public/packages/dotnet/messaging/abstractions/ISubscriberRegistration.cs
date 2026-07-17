// -----------------------------------------------------------------------
// <copyright file="ISubscriberRegistration.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging;

/// <summary>
/// Metadata for one registered subscriber. Built by
/// <c>services.AddD2SubscribersFromAssembly(...)</c> (the scanner finds
/// classes carrying <see cref="MqSubAttribute"/>) or by the explicit
/// programmatic helper <c>services.AddD2Subscriber&lt;TSub, TIn&gt;(...)</c>.
/// Held by the DI-singleton <see cref="SubscriberRegistry"/>; the transport's
/// consumer host reads the registry at startup to declare topology and open
/// consumer channels.
/// </summary>
/// <remarks>
/// Carries the resolved <see cref="MqSubscriptionDescriptor"/> + the
/// resolved per-replica queue name (already suffixed for fanout-exclusive
/// patterns). The registration is transport-agnostic — exactly the same data
/// shape works for a Kafka / NATS impl.
/// </remarks>
public interface ISubscriberRegistration
{
    /// <summary>Gets the handler type implementing
    /// <c>BaseHandler&lt;TSub, TIn, Unit&gt;</c>.</summary>
    Type HandlerType { get; }

    /// <summary>Gets the message type (the <c>TIn</c> the handler
    /// consumes).</summary>
    Type MessageType { get; }

    /// <summary>Gets the resolved subscription descriptor from the spec.</summary>
    MqSubscriptionDescriptor Descriptor { get; }

    /// <summary>Gets the EFFECTIVE queue name — same as
    /// <see cref="MqSubscriptionDescriptor.QueueName"/> for shared patterns;
    /// auto-suffixed with a per-process token for
    /// <see cref="QueuePattern.FanoutExclusiveAutoDelete"/> so multi-replica
    /// services don't conflict on the broker's exclusive-queue lock.</summary>
    string ResolvedQueueName { get; }
}
