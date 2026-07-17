// -----------------------------------------------------------------------
// <copyright file="QueuePattern.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging;

/// <summary>
/// Selects the queue topology declared for a subscriber. The semantic
/// contract per pattern is described on each enum value below.
/// </summary>
public enum QueuePattern
{
    /// <summary>
    /// Durable shared queue. Multiple consumer replicas pull from the same
    /// queue; the broker delivers each message to exactly one. Survives
    /// broker restart. Default for most business event subscribers.
    /// </summary>
    CompetingConsumer = 0,

    /// <summary>
    /// Non-durable, exclusive, auto-delete queue per replica. Every replica
    /// receives every message — fanout. Suitable for cache invalidation /
    /// multi-replica broadcast scenarios. Consumers MUST be idempotent
    /// because at-least-once delivery applies.
    /// </summary>
    FanoutExclusiveAutoDelete = 1,

    /// <summary>
    /// Durable shared queue with no auto-delete. Survives broker restart;
    /// unconsumed messages persist on disk. Used for long-lived event
    /// streams (audit) where data loss is unacceptable.
    /// </summary>
    DurableShared = 2,
}
