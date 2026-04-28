// -----------------------------------------------------------------------
// <copyright file="SetNx.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.DistributedCache.Redis.Handlers.C;

using System.Text.Json;
using D2.Shared.Handler;
using D2.Shared.I18n;
using D2.Shared.Result;
using D2.Shared.Utilities.Serialization;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using C = D2.Shared.Interfaces.Caching.Distributed.Handlers.C.ICreate;

/// <summary>
/// Handler for setting a value in the distributed cache only if the key does not already exist (SET NX).
/// </summary>
///
/// <typeparam name="TValue">
/// The type of the value to cache.
/// </typeparam>
public partial class SetNx<TValue> : BaseHandler<
        C.ISetNxHandler<TValue>, C.SetNxInput<TValue>, C.SetNxOutput>,
    C.ISetNxHandler<TValue>
{
    private readonly IConnectionMultiplexer r_redis;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetNx{TValue}"/> class.
    /// </summary>
    ///
    /// <param name="redis">
    /// The Redis connection multiplexer.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public SetNx(
        IConnectionMultiplexer redis,
        IHandlerContext context)
        : base(context)
    {
        r_redis = redis;
    }

    /// <inheritdoc/>
    protected override async ValueTask<D2Result<C.SetNxOutput?>> ExecuteAsync(
        C.SetNxInput<TValue> input,
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

            // Connect to Redis and set the value only if the key does not exist.
            var db = r_redis.GetDatabase();
            var wasSet = await db.StringSetAsync(
                input.Key,
                bytes,
                input.Expiration is { } ttl ? ttl : Expiration.Default,
                When.NotExists);

            // Return success with whether the key was set.
            return D2Result<C.SetNxOutput?>.Ok(
                new C.SetNxOutput(wasSet));
        }
        catch (RedisException ex)
        {
            LogSetNxFailed(Context.Logger, ex, input.Key, TraceId);

            return D2Result<C.SetNxOutput?>.ServiceUnavailable(traceId: TraceId);
        }
        catch (JsonException ex)
        {
            LogSetNxSerializationFailed(Context.Logger, ex, input.Key, TraceId);

            return D2Result<C.SetNxOutput?>.UnhandledException(
                messages: [TK.Common.Errors.COULD_NOT_BE_SERIALIZED],
                traceId: TraceId);
        }

        // Let the base handler catch any other exceptions.
    }

    /// <summary>
    /// Logs that a Redis exception occurred while setting a value with NX semantics.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "RedisException occurred while setting NX value for key '{Key}'. TraceId: {TraceId}")]
    private static partial void LogSetNxFailed(ILogger logger, Exception ex, string key, string? traceId);

    /// <summary>
    /// Logs that a JSON serialization exception occurred while setting a value with NX semantics.
    /// </summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "JsonException occurred while serializing value for key '{Key}'. TraceId: {TraceId}")]
    private static partial void LogSetNxSerializationFailed(ILogger logger, Exception ex, string key, string? traceId);
}
