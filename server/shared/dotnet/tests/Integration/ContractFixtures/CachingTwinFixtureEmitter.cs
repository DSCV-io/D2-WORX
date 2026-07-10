// -----------------------------------------------------------------------
// <copyright file="CachingTwinFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using D2.Shared.Caching;
using D2.Shared.Caching.Distributed.Redis;
using D2.Shared.Caching.Local.Default;
using Xunit;

/// <summary>
/// Emits the dual-runtime caching-twin parity fixture (constants/semantics catalog).
/// One scenario (<c>constants</c>) pins local defaults, local meter + instruments,
/// Redis defaults, Redis meter + instruments, Lua script bodies (normalized),
/// and tiered semantic markers (registration message prefix, EventIds, log levels,
/// binding field names, errorCode binding presence). TS parity asserts field-by-field
/// against the committed fixture under
/// <c>server/shared/typescript/contract-tests/fixtures/caching-twin/</c>.
/// </summary>
/// <remarks>
/// <para>
/// Lua bodies are normalized with <see cref="NormalizeLua"/> (CRLF→LF + trim)
/// so they match the TS <c>.trim()</c> script constants. Instrument tuples are
/// hard-coded to match TS <c>LOCAL_CACHE_INSTRUMENTS</c> /
/// <c>REDIS_CACHE_INSTRUMENTS</c> and .NET <see cref="LocalCacheTelemetry"/> /
/// <see cref="RedisCacheTelemetry"/> Counter registrations (name/unit/description
/// are not reflectable from Counter&lt;T&gt;). When changing either runtime meter,
/// update this emitter + both telemetry files in lockstep.
/// </para>
/// <para>
/// Tiered log message templates intentionally diverge across runtimes — the fixture
/// records EventIds + binding field names + Warning levels only, not template text.
/// </para>
/// </remarks>
public sealed class CachingTwinFixtureEmitter
{
    private const string _CATALOG = "caching-twin";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Constants()
    {
        var local = new LocalCacheOptions();
        var redis = new RedisCacheOptions();

        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["localDefaults"] = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["maxEntries"] = local.MaxEntries,
                ["defaultExpirationMs"] = local.DefaultExpiration.TotalMilliseconds,
                ["keyPrefix"] = local.KeyPrefix,
            },
            ["localMeter"] = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = LocalCacheTelemetry.METER_NAME,
                ["version"] = "1.0.0",
                ["instruments"] = new object[]
                {
                    Instrument("d2.cache.local.hits", "{hit}", "Local cache hits."),
                    Instrument("d2.cache.local.misses", "{miss}", "Local cache misses."),
                    Instrument("d2.cache.local.sets", "{write}", "Local cache writes."),
                    Instrument(
                        "d2.cache.local.removes",
                        "{removal}",
                        "Local cache removals (explicit)."),
                    Instrument(
                        "d2.cache.local.evictions",
                        "{eviction}",
                        "Entries evicted by capacity / expiration."),
                },
            },
            ["redisDefaults"] = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["defaultExpirationMs"] = redis.DefaultExpiration.TotalMilliseconds,
                ["keyPrefix"] = redis.KeyPrefix,
                ["invalidationChannel"] = redis.InvalidationChannel,
                ["commandTimeoutMs"] = redis.CommandTimeout.TotalMilliseconds,
                ["connectTimeoutMs"] = redis.ConnectTimeout.TotalMilliseconds,
                ["connectRetries"] = redis.ConnectRetries,
                ["abortOnConnectFail"] = redis.AbortOnConnectFail,
            },
            ["redisMeter"] = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = RedisCacheTelemetry.METER_NAME,
                ["version"] = "1.0.0",
                ["instruments"] = new object[]
                {
                    Instrument("d2.cache.redis.hits", "{hit}", "Redis cache hits."),
                    Instrument("d2.cache.redis.misses", "{miss}", "Redis cache misses."),
                    Instrument("d2.cache.redis.sets", "{write}", "Redis cache writes."),
                    Instrument("d2.cache.redis.removes", "{removal}", "Redis cache removals."),
                    Instrument(
                        "d2.cache.redis.broadcasts",
                        "{broadcast}",
                        "Invalidation messages published to backplane."),
                    Instrument("d2.cache.redis.errors", "{error}", "Redis-side failures."),
                },
            },
            ["luaScripts"] = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["INCREMENT_WITH_OPTIONAL_TTL"] =
                    NormalizeLua(RedisLuaScripts.INCREMENT_WITH_OPTIONAL_TTL),
                ["RELEASE_LOCK_IF_OWNER"] =
                    NormalizeLua(RedisLuaScripts.RELEASE_LOCK_IF_OWNER),
                ["SET_ADD_WITH_OPTIONAL_TTL"] =
                    NormalizeLua(RedisLuaScripts.SET_ADD_WITH_OPTIONAL_TTL),
            },
            ["tieredSemantics"] = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                // Full .NET vs TS registration messages intentionally differ —
                // parity asserts only this shared prefix (contains).
                ["backplaneNotRegisteredMessagePrefix"] =
                    "ICacheInvalidationBackplane is not registered",
                ["eventIds"] = new object[] { 1, 2 },
                ["logLevels"] = new object[] { "Warning", "Warning" },

                // .NET LoggerMessage binding field names (not template text).
                // EventId 1 = L1InvalidationFailed; EventId 2 = L1WriteFailedAfterL2Success.
                ["dotnetBindingFields"] = new SortedDictionary<string, object?>(
                    StringComparer.Ordinal)
                {
                    ["event1"] = new object[] { "Key", "ErrorCode" },
                    ["event2"] = new object[] { "Operation", "KeyOrCount", "ErrorCode" },
                },
                ["errorCodeBindingPresent"] = true,
            },
        };

        FixturePathHelpers.WriteFixture(_CATALOG, "constants", data);
    }

    /// <summary>
    /// Builds a sorted instrument descriptor matching the TS parity assert shape.
    /// </summary>
    private static SortedDictionary<string, object?> Instrument(
        string name,
        string unit,
        string description)
    {
        return new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = name,
            ["unit"] = unit,
            ["description"] = description,
        };
    }

    /// <summary>
    /// Normalizes a Lua script body for dual-runtime compare: CRLF → LF, then
    /// <see cref="string.Trim()"/>. Matches the TS side's template-literal +
    /// <c>.trim()</c> constants so trailing newlines from C# raw-string literals
    /// do not produce false drift.
    /// </summary>
    /// <param name="script">Raw script constant from <see cref="RedisLuaScripts"/>.</param>
    /// <returns>Trimmed LF-normalized script body.</returns>
    private static string NormalizeLua(string script)
    {
        return script.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
    }
}
