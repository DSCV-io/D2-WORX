// -----------------------------------------------------------------------
// <copyright file="RedisDistributedCache.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching.Distributed.Redis;

using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

/// <summary>
/// StackExchange.Redis-backed implementation of <see cref="IDistributedCache"/>.
/// Implements all four building blocks (<see cref="ICacheBasic"/>,
/// <see cref="ICacheAtomic"/>, <see cref="ICacheBroadcast"/>,
/// <see cref="ICacheSet"/>) directly — no per-op handler classes.
/// </summary>
/// <remarks>
/// <para>
/// Atomicity for compound ops (Increment with TTL, ReleaseLock with
/// owner check, SetAdd with TTL on first add) uses Lua scripts in
/// <see cref="RedisLuaScripts"/> so each op is one round-trip.
/// </para>
/// <para>
/// Error handling: <see cref="RedisException"/> →
/// <c>ServiceUnavailable</c> (graceful degradation; caller decides
/// fail-open vs fail-closed). Increment on a non-numeric key triggers
/// Redis WRONGTYPE → mapped to <c>Conflict</c>.
/// </para>
/// </remarks>
public sealed class RedisDistributedCache : IDistributedCache
{
    private readonly IConnectionMultiplexer r_redis;
    private readonly RedisCacheOptions r_options;
    private readonly ICacheSerializer r_serializer;
    private readonly ILogger<RedisDistributedCache> r_logger;
    private readonly ICacheInvalidationBackplane? r_backplane;

    /// <summary>Initializes a new <see cref="RedisDistributedCache"/>.</summary>
    /// <param name="redis">Redis connection multiplexer (singleton).</param>
    /// <param name="options">Cache options.</param>
    /// <param name="serializer">Value serializer (default JSON).</param>
    /// <param name="logger">Logger.</param>
    /// <param name="backplane">
    /// Optional invalidation backplane; required for <c>*AndBroadcast*</c>
    /// methods.
    /// </param>
    public RedisDistributedCache(
        IConnectionMultiplexer redis,
        IOptions<RedisCacheOptions> options,
        ICacheSerializer serializer,
        ILogger<RedisDistributedCache> logger,
        ICacheInvalidationBackplane? backplane = null)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(logger);
        r_redis = redis;
        r_options = options.Value;
        r_serializer = serializer;
        r_logger = logger;
        r_backplane = backplane;
    }

    private IDatabase Db => r_redis.GetDatabase();

    // ----- ICacheBasic -----

    /// <inheritdoc />
    public async ValueTask<D2Result<T?>> GetAsync<T>(string key, CancellationToken ct = default)
    {
        if (key.Falsey())
            return InputFailures.Required<T?>(nameof(key));

        try
        {
            var raw = await Db.StringGetAsync(Prefixed(key));
            if (raw.IsNull)
            {
                RedisCacheTelemetry.SR_Misses.Add(1);
                return D2Result<T?>.NotFound();
            }

            RedisCacheTelemetry.SR_Hits.Add(1);
            var deserialized = r_serializer.Deserialize<T>((byte[])raw!);

            if (deserialized.IsOk)
                return D2Result<T?>.Ok(deserialized.Data);

            return D2Result<T?>.BubbleFail(deserialized);
        }
        catch (RedisException ex)
        {
            return ServiceUnavailable<T?>(ex, "GetAsync", key);
        }
    }

    /// <inheritdoc />
    public async ValueTask<D2Result<IReadOnlyDictionary<string, T?>>> GetManyAsync<T>(
        IReadOnlyCollection<string> keys, CancellationToken ct = default)
    {
        if (keys.Falsey())
            return InputFailures.Required<IReadOnlyDictionary<string, T?>>(nameof(keys));

        try
        {
            var keyArray = keys.ToArray();
            var prefixed = keyArray.Select(k => (RedisKey)Prefixed(k)).ToArray();
            var values = await Db.StringGetAsync(prefixed);

            var hits = new Dictionary<string, T?>(keys.Count, StringComparer.Ordinal);
            var hitCount = 0;
            for (var i = 0; i < keyArray.Length; i++)
            {
                if (values[i].IsNull) continue;

                var deserialized = r_serializer.Deserialize<T>((byte[])values[i]!);

                if (deserialized.IsOk)
                {
                    hits[keyArray[i]] = deserialized.Data;
                    hitCount++;
                }
            }

            RedisCacheTelemetry.SR_Hits.Add(hitCount);
            RedisCacheTelemetry.SR_Misses.Add(keys.Count - hitCount);

            if (hitCount == 0)
                return D2Result<IReadOnlyDictionary<string, T?>>.NotFound();

            if (hitCount == keys.Count)
                return D2Result<IReadOnlyDictionary<string, T?>>.Ok(hits);

            return D2Result<IReadOnlyDictionary<string, T?>>.SomeFound(hits);
        }
        catch (RedisException ex)
        {
            RedisCacheTelemetry.SR_Errors.Add(1);
            RedisCacheLog.RedisOpFailed(
                r_logger, "GetManyAsync", ex.GetType().Name, $"{keys.Count} keys");
            return D2Result<IReadOnlyDictionary<string, T?>>.ServiceUnavailable();
        }
    }

    /// <inheritdoc />
    public async ValueTask<D2Result<bool>> ExistsAsync(string key, CancellationToken ct = default)
    {
        if (key.Falsey())
            return InputFailures.Required<bool>(nameof(key));

        try
        {
            var exists = await Db.KeyExistsAsync(Prefixed(key));
            return D2Result<bool>.Ok(exists);
        }
        catch (RedisException ex)
        {
            return ServiceUnavailable<bool>(ex, "ExistsAsync", key);
        }
    }

    /// <inheritdoc />
    public async ValueTask<D2Result<TimeSpan?>> GetTtlAsync(
        string key, CancellationToken ct = default)
    {
        if (key.Falsey())
            return InputFailures.Required<TimeSpan?>(nameof(key));

        var prefixed = Prefixed(key);
        try
        {
            // Use EXISTS first to distinguish "absent key" (NotFound) from
            // "present key with no TTL" (Ok(null)).
            if (!await Db.KeyExistsAsync(prefixed))
                return D2Result<TimeSpan?>.NotFound();

            var ttl = await Db.KeyTimeToLiveAsync(prefixed);
            return D2Result<TimeSpan?>.Ok(ttl);
        }
        catch (RedisException ex)
        {
            return ServiceUnavailable<TimeSpan?>(ex, "GetTtlAsync", key);
        }
    }

    /// <inheritdoc />
    public async ValueTask<D2Result> SetAsync<T>(
        string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        if (key.Falsey())
            return InputFailures.Required(nameof(key));

        try
        {
            var serialized = r_serializer.Serialize(value);
            if (!serialized.IsOk)
            {
                // No non-generic D2Result.BubbleFail overload exists today —
                // construct manually to surface the serializer failure with
                // its original messages / errorCode / statusCode intact.
                return new D2Result(
                    false,
                    serialized.Messages,
                    statusCode: serialized.StatusCode,
                    errorCode: serialized.ErrorCode);
            }

            await Db.StringSetAsync(
                Prefixed(key),
                serialized.Data!,
                expiration ?? r_options.DefaultExpiration);

            RedisCacheTelemetry.SR_Sets.Add(1);

            return D2Result.Ok();
        }
        catch (RedisException ex)
        {
            return ServiceUnavailable(ex, "SetAsync", key);
        }
    }

    /// <inheritdoc />
    public async ValueTask<D2Result> SetManyAsync<T>(
        IReadOnlyDictionary<string, T> entries,
        TimeSpan? expiration = null,
        CancellationToken ct = default)
    {
        if (entries.Falsey())
            return InputFailures.Required(nameof(entries));

        try
        {
            var ttl = expiration ?? r_options.DefaultExpiration;
            var batch = Db.CreateBatch();
            var tasks = new List<Task>(entries.Count);
            foreach (var (key, value) in entries)
            {
                var serialized = r_serializer.Serialize(value);
                if (!serialized.IsOk)
                {
                    return new D2Result(
                        false,
                        serialized.Messages,
                        statusCode: serialized.StatusCode,
                        errorCode: serialized.ErrorCode);
                }

                tasks.Add(batch.StringSetAsync(Prefixed(key), serialized.Data!, ttl));
            }

            batch.Execute();
            await Task.WhenAll(tasks);
            RedisCacheTelemetry.SR_Sets.Add(entries.Count);
            return D2Result.Ok();
        }
        catch (RedisException ex)
        {
            return ServiceUnavailable(ex, "SetManyAsync", $"{entries.Count} entries");
        }
    }

    /// <inheritdoc />
    public async ValueTask<D2Result> RemoveAsync(string key, CancellationToken ct = default)
    {
        if (key.Falsey()) return InputFailures.Required(nameof(key));

        try
        {
            await Db.KeyDeleteAsync(Prefixed(key));
            RedisCacheTelemetry.SR_Removes.Add(1);
            return D2Result.Ok();
        }
        catch (RedisException ex)
        {
            return ServiceUnavailable(ex, "RemoveAsync", key);
        }
    }

    /// <inheritdoc />
    public async ValueTask<D2Result> RemoveManyAsync(
        IReadOnlyCollection<string> keys, CancellationToken ct = default)
    {
        if (keys.Falsey()) return InputFailures.Required(nameof(keys));

        try
        {
            var prefixed = keys.Select(k => (RedisKey)Prefixed(k)).ToArray();
            await Db.KeyDeleteAsync(prefixed);
            RedisCacheTelemetry.SR_Removes.Add(keys.Count);
            return D2Result.Ok();
        }
        catch (RedisException ex)
        {
            return ServiceUnavailable(ex, "RemoveManyAsync", $"{keys.Count} keys");
        }
    }

    // ----- ICacheAtomic -----

    /// <inheritdoc />
    public async ValueTask<D2Result<bool>> SetNxAsync<T>(
        string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        if (key.Falsey()) return InputFailures.Required<bool>(nameof(key));

        try
        {
            var serialized = r_serializer.Serialize(value);
            if (!serialized.IsOk)
                return D2Result<bool>.BubbleFail(serialized);

            var written = await Db.StringSetAsync(
                Prefixed(key),
                serialized.Data!,
                expiration ?? r_options.DefaultExpiration,
                When.NotExists);

            if (written)
                RedisCacheTelemetry.SR_Sets.Add(1);

            return D2Result<bool>.Ok(written);
        }
        catch (RedisException ex)
        {
            return ServiceUnavailable<bool>(ex, "SetNxAsync", key);
        }
    }

    /// <inheritdoc />
    public async ValueTask<D2Result<long>> IncrementAsync(
        string key, long amount = 1, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        if (key.Falsey()) return InputFailures.Required<long>(nameof(key));

        try
        {
            var ttlMs = expiration is not null
                ? (long)((TimeSpan)expiration).TotalMilliseconds
                : 0L;

            var result = await Db.ScriptEvaluateAsync(
                RedisLuaScripts.INCREMENT_WITH_OPTIONAL_TTL,
                [(RedisKey)Prefixed(key)],
                [amount, ttlMs]);

            return D2Result<long>.Ok((long)result);
        }
        catch (RedisServerException ex) when (
            ex.Message.Contains("safe_integer_overflow", StringComparison.Ordinal))
        {
            // Lua reversed INCRBY atomically; dual-runtime bound matches JS
            // Number.MAX_SAFE_INTEGER so .NET and TS refuse the same range.
            return InputFailures.Invalid<long>(nameof(amount));
        }
        catch (RedisServerException ex) when (
            ex.Message.Contains("WRONGTYPE", StringComparison.Ordinal)
            || ex.Message.Contains("not an integer", StringComparison.Ordinal))
        {
            // Type collision: either the key holds a non-string data structure
            // (Redis WRONGTYPE) or it holds a string that isn't a valid integer
            // ("value is not an integer or out of range"). Both map to Conflict
            // per the abstraction's contract — caller's stored type doesn't
            // match what Increment requires.
            return D2Result<long>.Conflict(traceId: null);
        }
        catch (RedisException ex)
        {
            return ServiceUnavailable<long>(ex, "IncrementAsync", key);
        }
    }

    /// <inheritdoc />
    public async ValueTask<D2Result<bool>> AcquireLockAsync(
        string key, string lockId, TimeSpan expiration, CancellationToken ct = default)
    {
        if (key.Falsey()) return InputFailures.Required<bool>(nameof(key));
        if (lockId.Falsey()) return InputFailures.Required<bool>(nameof(lockId));

        try
        {
            var acquired = await Db.StringSetAsync(
                Prefixed(key),
                lockId,
                expiration,
                When.NotExists);
            return D2Result<bool>.Ok(acquired);
        }
        catch (RedisException ex)
        {
            return ServiceUnavailable<bool>(ex, "AcquireLockAsync", key);
        }
    }

    /// <inheritdoc />
    public async ValueTask<D2Result> ReleaseLockAsync(
        string key, string lockId, CancellationToken ct = default)
    {
        if (key.Falsey()) return InputFailures.Required(nameof(key));
        if (lockId.Falsey()) return InputFailures.Required(nameof(lockId));

        try
        {
            await Db.ScriptEvaluateAsync(
                RedisLuaScripts.RELEASE_LOCK_IF_OWNER,
                [(RedisKey)Prefixed(key)],
                [lockId]);
            return D2Result.Ok();
        }
        catch (RedisException ex)
        {
            return ServiceUnavailable(ex, "ReleaseLockAsync", key);
        }
    }

    // ----- ICacheBroadcast -----

    /// <inheritdoc />
    public async ValueTask<D2Result> SetAndBroadcastAsync<T>(
        string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        var setResult = await SetAsync(key, value, expiration, ct);
        if (!setResult.IsOk) return setResult;
        return await PublishAsync(key, ct);
    }

    /// <inheritdoc />
    public async ValueTask<D2Result> SetManyAndBroadcastAsync<T>(
        IReadOnlyDictionary<string, T> entries,
        TimeSpan? expiration = null,
        CancellationToken ct = default)
    {
        var setResult = await SetManyAsync(entries, expiration, ct);
        if (!setResult.IsOk) return setResult;
        return await PublishManyAsync(entries.Keys.ToArray(), ct);
    }

    /// <inheritdoc />
    public async ValueTask<D2Result> RemoveAndBroadcastAsync(
        string key, CancellationToken ct = default)
    {
        var removeResult = await RemoveAsync(key, ct);
        if (!removeResult.IsOk) return removeResult;
        return await PublishAsync(key, ct);
    }

    /// <inheritdoc />
    public async ValueTask<D2Result> RemoveManyAndBroadcastAsync(
        IReadOnlyCollection<string> keys, CancellationToken ct = default)
    {
        var removeResult = await RemoveManyAsync(keys, ct);
        if (!removeResult.IsOk) return removeResult;
        return await PublishManyAsync(keys, ct);
    }

    // ----- ICacheSet -----

    /// <inheritdoc />
    public async ValueTask<D2Result<bool>> SetAddAsync(
        string key, string member, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        if (key.Falsey()) return InputFailures.Required<bool>(nameof(key));
        if (member.Falsey()) return InputFailures.Required<bool>(nameof(member));

        try
        {
            var ttlMs = expiration is not null
                ? (long)((TimeSpan)expiration).TotalMilliseconds
                : 0L;

            var result = await Db.ScriptEvaluateAsync(
                RedisLuaScripts.SET_ADD_WITH_OPTIONAL_TTL,
                [(RedisKey)Prefixed(key)],
                [member, ttlMs]);

            return D2Result<bool>.Ok((long)result == 1);
        }
        catch (RedisException ex)
        {
            return ServiceUnavailable<bool>(ex, "SetAddAsync", key);
        }
    }

    /// <inheritdoc />
    public async ValueTask<D2Result<long>> SetCardinalityAsync(
        string key, CancellationToken ct = default)
    {
        if (key.Falsey()) return InputFailures.Required<long>(nameof(key));

        try
        {
            var count = await Db.SetLengthAsync(Prefixed(key));
            return D2Result<long>.Ok(count);
        }
        catch (RedisException ex)
        {
            return ServiceUnavailable<long>(ex, "SetCardinalityAsync", key);
        }
    }

    /// <inheritdoc />
    public async ValueTask<D2Result<bool>> SetRemoveAsync(
        string key, string member, CancellationToken ct = default)
    {
        if (key.Falsey()) return InputFailures.Required<bool>(nameof(key));
        if (member.Falsey()) return InputFailures.Required<bool>(nameof(member));

        try
        {
            var removed = await Db.SetRemoveAsync(Prefixed(key), member);
            return D2Result<bool>.Ok(removed);
        }
        catch (RedisException ex)
        {
            return ServiceUnavailable<bool>(ex, "SetRemoveAsync", key);
        }
    }

    /// <inheritdoc />
    public async ValueTask<D2Result<bool>> SetContainsAsync(
        string key, string member, CancellationToken ct = default)
    {
        if (key.Falsey()) return InputFailures.Required<bool>(nameof(key));
        if (member.Falsey()) return InputFailures.Required<bool>(nameof(member));

        try
        {
            var contains = await Db.SetContainsAsync(Prefixed(key), member);
            return D2Result<bool>.Ok(contains);
        }
        catch (RedisException ex)
        {
            return ServiceUnavailable<bool>(ex, "SetContainsAsync", key);
        }
    }

    private async ValueTask<D2Result> PublishAsync(string key, CancellationToken ct)
    {
        if (r_backplane is null)
        {
            throw new InvalidOperationException(
                "ICacheInvalidationBackplane is not registered. Use SetAsync / "
                + "RemoveAsync (no broadcast), or register the backplane via "
                + "AddD2RedisCacheInvalidationBackplane.");
        }

        RedisCacheTelemetry.SR_Broadcasts.Add(1);
        return await r_backplane.PublishInvalidationAsync(Prefixed(key), ct);
    }

    private async ValueTask<D2Result> PublishManyAsync(
        IReadOnlyCollection<string> keys, CancellationToken ct)
    {
        if (r_backplane is null)
        {
            throw new InvalidOperationException(
                "ICacheInvalidationBackplane is not registered. Use SetManyAsync "
                + "/ RemoveManyAsync (no broadcast), or register the backplane "
                + "via AddD2RedisCacheInvalidationBackplane.");
        }

        RedisCacheTelemetry.SR_Broadcasts.Add(keys.Count);
        var prefixed = keys.Select(Prefixed).ToArray();
        return await r_backplane.PublishInvalidationManyAsync(prefixed, ct);
    }

    private string Prefixed(string key)
        => r_options.KeyPrefix.Falsey() ? key : r_options.KeyPrefix + key;

    private D2Result ServiceUnavailable(RedisException ex, string op, string keyOrCount)
    {
        RedisCacheTelemetry.SR_Errors.Add(1);
        RedisCacheLog.RedisOpFailed(r_logger, op, ex.GetType().Name, keyOrCount);
        return D2Result.ServiceUnavailable();
    }

    private D2Result<T?> ServiceUnavailable<T>(RedisException ex, string op, string keyOrCount)
    {
        RedisCacheTelemetry.SR_Errors.Add(1);
        RedisCacheLog.RedisOpFailed(r_logger, op, ex.GetType().Name, keyOrCount);
        return D2Result<T?>.ServiceUnavailable();
    }
}
