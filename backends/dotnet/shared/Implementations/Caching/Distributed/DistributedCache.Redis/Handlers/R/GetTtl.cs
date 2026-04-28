// -----------------------------------------------------------------------
// <copyright file="GetTtl.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.DistributedCache.Redis.Handlers.R;

using D2.Shared.Handler;
using D2.Shared.Result;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using S = D2.Shared.Interfaces.Caching.Distributed.Handlers.R.IRead;

/// <summary>
/// Handler for getting the time-to-live of a key in the distributed cache.
/// </summary>
public partial class GetTtl : BaseHandler<S.IGetTtlHandler, S.GetTtlInput, S.GetTtlOutput>,
    S.IGetTtlHandler
{
    private readonly IConnectionMultiplexer r_redis;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTtl"/> class.
    /// </summary>
    ///
    /// <param name="redis">
    /// The Redis connection multiplexer.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public GetTtl(
        IConnectionMultiplexer redis,
        IHandlerContext context)
        : base(context)
    {
        r_redis = redis;
    }

    /// <inheritdoc/>
    protected override async ValueTask<D2Result<S.GetTtlOutput?>> ExecuteAsync(
        S.GetTtlInput input,
        CancellationToken ct = default)
    {
        try
        {
            var db = r_redis.GetDatabase();
            var ttl = await db.KeyTimeToLiveAsync(input.Key);

            return D2Result<S.GetTtlOutput?>.Ok(
                new S.GetTtlOutput(ttl));
        }
        catch (RedisException ex)
        {
            LogGetTtlFailed(Context.Logger, ex, input.Key, TraceId);

            return D2Result<S.GetTtlOutput?>.ServiceUnavailable(traceId: TraceId);
        }

        // Let the base handler catch any other exceptions.
    }

    /// <summary>
    /// Logs that a Redis exception occurred while getting the TTL of a key.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "RedisException occurred while getting TTL for key '{Key}'. TraceId: {TraceId}")]
    private static partial void LogGetTtlFailed(ILogger logger, Exception ex, string key, string? traceId);
}
