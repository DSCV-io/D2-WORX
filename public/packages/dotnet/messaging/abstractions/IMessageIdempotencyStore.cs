// -----------------------------------------------------------------------
// <copyright file="IMessageIdempotencyStore.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging;

using DcsvIo.D2.Result;

/// <summary>
/// Optional dedup helper for subscribers. When
/// <see cref="MqSubscriptionDescriptor.Idempotency"/> is true, the consumer
/// calls <see cref="HasSeenAsync"/> before invoking the handler — a hit
/// short-circuits to ack without re-doing the work. After a successful
/// handler invocation, the consumer calls <see cref="MarkSeenAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// Default impl in <c>DcsvIo.D2.Messaging.RabbitMq</c> is backed by
/// <c>IDistributedCache</c> with a 24-hour TTL — enough to cover the
/// retry / redelivery window without growing unbounded.
/// </para>
/// <para>
/// Handlers that do transactional dedup (e.g. DB row insert with a UNIQUE
/// constraint on <c>message_id</c>) typically leave this disabled — they
/// already get exactly-once via the database. This store exists for the
/// "idempotent side-effect" case (calling an external API, sending an
/// email) where database transactions don't help.
/// </para>
/// </remarks>
public interface IMessageIdempotencyStore
{
    /// <summary>
    /// Returns <c>Ok(true)</c> if the message-id has been seen recently,
    /// <c>Ok(false)</c> otherwise. <c>ServiceUnavailable</c> on backing
    /// store outage — the consumer treats this as "fail open" (proceed to
    /// the handler; better to risk a duplicate than reject every message
    /// during a Redis blip).
    /// </summary>
    /// <param name="messageId">The message identifier (typically UUIDv7).</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<D2Result<bool>> HasSeenAsync(
        string messageId, CancellationToken ct = default);

    /// <summary>
    /// Records the message-id as processed. Best-effort —
    /// <c>ServiceUnavailable</c> on backing store outage means the next
    /// redelivery may re-invoke the handler (acceptable; handlers must be
    /// safe under at-least-once anyway).
    /// </summary>
    /// <param name="messageId">The message identifier (typically UUIDv7).</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<D2Result> MarkSeenAsync(
        string messageId, CancellationToken ct = default);
}
