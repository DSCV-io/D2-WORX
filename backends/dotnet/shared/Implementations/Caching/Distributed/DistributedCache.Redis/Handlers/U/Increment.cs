// -----------------------------------------------------------------------
// <copyright file="Increment.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.DistributedCache.Redis.Handlers.U;

using D2.Shared.Handler;
using D2.Shared.Result;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using S = D2.Shared.Interfaces.Caching.Distributed.Handlers.U.IUpdate;

/// <summary>
/// Handler for atomically incrementing a counter in the distributed cache.
/// </summary>
public partial class Increment : BaseHandler<S.IIncrementHandler, S.IncrementInput, S.IncrementOutput>,
    S.IIncrementHandler
{
    private const string _INCREMENT_WITH_EXPIRE_SCRIPT = """
        local result = redis.call('INCRBY', KEYS[1], ARGV[1])
        if ARGV[2] ~= '0' then
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
        end
        return result
        """;

    private readonly IConnectionMultiplexer r_redis;

    /// <summary>
    /// Initializes a new instance of the <see cref="Increment"/> class.
    /// </summary>
    ///
    /// <param name="redis">
    /// The Redis connection multiplexer.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public Increment(
        IConnectionMultiplexer redis,
        IHandlerContext context)
        : base(context)
    {
        r_redis = redis;
    }

    /// <inheritdoc/>
    protected override async ValueTask<D2Result<S.IncrementOutput?>> ExecuteAsync(
        S.IncrementInput input,
        CancellationToken ct = default)
    {
        try
        {
            var db = r_redis.GetDatabase();

            // Atomic increment with optional expiration via Lua script.
            // This ensures INCRBY and PEXPIRE execute atomically in a single round-trip.
            var expirationMs = input.Expiration.HasValue
                ? (long)input.Expiration.Value.TotalMilliseconds
                : 0L;

            var result = await db.ScriptEvaluateAsync(
                _INCREMENT_WITH_EXPIRE_SCRIPT,
                [(RedisKey)input.Key],
                [input.Amount, expirationMs]);

            var newValue = (long)result;

            return D2Result<S.IncrementOutput?>.Ok(
                new S.IncrementOutput(newValue));
        }
        catch (RedisException ex)
        {
            LogIncrementFailed(Context.Logger, ex, input.Key, TraceId);

            return D2Result<S.IncrementOutput?>.ServiceUnavailable(traceId: TraceId);
        }

        // Let the base handler catch any other exceptions.
    }

    /// <summary>
    /// Logs that a Redis exception occurred while incrementing a key.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "RedisException occurred while incrementing key '{Key}'. TraceId: {TraceId}")]
    private static partial void LogIncrementFailed(ILogger logger, Exception ex, string key, string? traceId);
}
