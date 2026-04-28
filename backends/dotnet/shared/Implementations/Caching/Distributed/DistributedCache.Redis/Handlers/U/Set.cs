// -----------------------------------------------------------------------
// <copyright file="Set.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.DistributedCache.Redis.Handlers.U;

using System.Text.Json;
using D2.Shared.Handler;
using D2.Shared.I18n;
using D2.Shared.Result;
using D2.Shared.Utilities.Serialization;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using S = D2.Shared.Interfaces.Caching.Distributed.Handlers.U.IUpdate;

/// <summary>
/// Handler for setting a value in the distributed cache.
/// </summary>
///
/// <typeparam name="TValue">
/// The type of the value to cache.
/// </typeparam>
public partial class Set<TValue> : BaseHandler<
        S.ISetHandler<TValue>, S.SetInput<TValue>, S.SetOutput>,
    S.ISetHandler<TValue>
{
    private readonly IConnectionMultiplexer r_redis;

    /// <summary>
    /// Initializes a new instance of the <see cref="Set{TValue}"/> class.
    /// </summary>
    ///
    /// <param name="redis">
    /// The Redis connection multiplexer.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public Set(
        IConnectionMultiplexer redis,
        IHandlerContext context)
        : base(context)
    {
        r_redis = redis;
    }

    /// <inheritdoc/>
    protected override async ValueTask<D2Result<S.SetOutput?>> ExecuteAsync(
        S.SetInput<TValue> input,
        CancellationToken ct = default)
    {
        try
        {
            // Serialize the value.
            var bytes = input.Value is IMessage message
                ? message.ToByteArray()
                : JsonSerializer.SerializeToUtf8Bytes(
                    input.Value,
                    SerializerOptions.SR_IgnoreCycles);

            // Connect to Redis and set the value.
            var db = r_redis.GetDatabase();
            await db.StringSetAsync(
                input.Key,
                bytes,
                input.Expiration is { } ttl ? ttl : Expiration.Default);

            // Return success.
            return D2Result<S.SetOutput?>.Ok(
                new S.SetOutput());
        }
        catch (RedisException ex)
        {
            LogSetFailed(Context.Logger, ex, input.Key, TraceId);

            return D2Result<S.SetOutput?>.ServiceUnavailable(traceId: TraceId);
        }
        catch (JsonException ex)
        {
            LogSetSerializationFailed(Context.Logger, ex, input.Key, TraceId);

            return D2Result<S.SetOutput?>.UnhandledException(
                messages: [TK.Common.Errors.COULD_NOT_BE_SERIALIZED],
                traceId: TraceId);
        }

        // Let the base handler catch any other exceptions.
    }

    /// <summary>
    /// Logs that a Redis exception occurred while setting a cached value.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "RedisException occurred while setting value for key '{Key}'. TraceId: {TraceId}")]
    private static partial void LogSetFailed(ILogger logger, Exception ex, string key, string? traceId);

    /// <summary>
    /// Logs that a JSON serialization exception occurred while setting a cached value.
    /// </summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "JsonException occurred while serializing value for key '{Key}'. TraceId: {TraceId}")]
    private static partial void LogSetSerializationFailed(ILogger logger, Exception ex, string key, string? traceId);
}
