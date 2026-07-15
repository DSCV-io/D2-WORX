// -----------------------------------------------------------------------
// <copyright file="CacheIdempotencyStore.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Idempotency;

using DcsvIo.D2.Caching;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Default <see cref="IMessageIdempotencyStore"/> implementation backed by
/// the cluster-wide <see cref="IDistributedCache"/>. Marks a processed
/// message id with <see cref="ICacheAtomic.SetNxAsync"/> + a 24-hour TTL —
/// enough to cover the redelivery / retry window without growing unbounded.
/// </summary>
internal sealed class CacheIdempotencyStore : IMessageIdempotencyStore
{
    private const string _KEY_PREFIX = "msg-idem:";
    private static readonly TimeSpan sr_ttl = TimeSpan.FromHours(24);

    private readonly IDistributedCache r_cache;

    /// <summary>Initializes the store.</summary>
    /// <param name="cache">Distributed cache used to record seen message ids.</param>
    public CacheIdempotencyStore(IDistributedCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        r_cache = cache;
    }

    /// <inheritdoc />
    public ValueTask<D2Result<bool>> HasSeenAsync(
        string messageId, CancellationToken ct = default)
    {
        if (messageId.Falsey())
        {
            return new ValueTask<D2Result<bool>>(
                MessagingFailures.Required<bool>(nameof(messageId)));
        }

        return r_cache.ExistsAsync(BuildKey(messageId), ct);
    }

    /// <inheritdoc />
    public async ValueTask<D2Result> MarkSeenAsync(
        string messageId, CancellationToken ct = default)
    {
        if (messageId.Falsey())
            return MessagingFailures.Required(nameof(messageId));

        var setResult = await r_cache.SetNxAsync(
            BuildKey(messageId), value: 1, expiration: sr_ttl, ct);
        return setResult.Failed
            ? D2Result.ServiceUnavailable(messages: setResult.Messages)
            : D2Result.Ok(traceId: setResult.TraceId);
    }

    private static string BuildKey(string messageId) => _KEY_PREFIX + messageId;
}
