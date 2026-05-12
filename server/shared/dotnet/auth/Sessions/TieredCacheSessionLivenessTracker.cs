// -----------------------------------------------------------------------
// <copyright file="TieredCacheSessionLivenessTracker.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Sessions;

using System.Diagnostics;
using D2.Shared.Auth.Abstractions.Sessions;
using D2.Shared.Auth.Errors;
using D2.Shared.Auth.Telemetry;
using D2.Shared.Caching;
using D2.Shared.Result;
using D2.Shared.Utilities.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Default <see cref="ISessionLivenessTracker"/> impl — sentinel-only
/// presence check via <c>ITieredCache.ExistsAsync</c>. See
/// <see cref="ISessionLivenessTracker"/> for the canonical contract
/// (sentinel model + fail-closed semantics).
/// </summary>
/// <remarks>
/// L1 invalidation happens automatically via
/// <see cref="D2.Shared.Caching.Tiered.DefaultTieredCache"/>'s built-in
/// backplane subscription — when Edge revokes a session and publishes
/// the invalidation, every instance's tiered cache drops the L1 entry.
/// The separate <see cref="SessionRevokedBackplaneSubscriber"/> exists
/// only for telemetry observation, not for cache management.
/// </remarks>
internal sealed class TieredCacheSessionLivenessTracker : ISessionLivenessTracker
{
    private readonly ITieredCache r_cache;
    private readonly AuthOptions r_options;
    private readonly ILogger<TieredCacheSessionLivenessTracker> r_logger;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="TieredCacheSessionLivenessTracker"/> class.
    /// </summary>
    /// <param name="cache">The tiered cache (L1 + L2 + backplane-driven invalidation).</param>
    /// <param name="options">The auth options snapshot.</param>
    /// <param name="logger">Logger for cache-outage diagnostics.</param>
    public TieredCacheSessionLivenessTracker(
        ITieredCache cache,
        IOptions<AuthOptions> options,
        ILogger<TieredCacheSessionLivenessTracker> logger)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        r_cache = cache;
        r_options = options.Value;
        r_logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<D2Result<bool>> IsAliveAsync(
        Guid sessionId, CancellationToken ct = default)
    {
        if (sessionId.Falsey())
        {
            AuthTelemetry.SessionLivenessChecks.Add(
                1, new KeyValuePair<string, object?>("outcome", "invalid_input"));
            return D2Result<bool>.ValidationFailed();
        }

        var sw = Stopwatch.StartNew();
        var key = $"{r_options.Sessions.CacheKeyPrefix}{sessionId:N}";
        var existsResult = await r_cache.ExistsAsync(key, ct).ConfigureAwait(false);
        AuthTelemetry.SessionLivenessLookupDurationMs.Record(sw.Elapsed.TotalMilliseconds);

        if (!existsResult.Success)
        {
            // Cache backing-store failure → fail-closed. Caller MUST translate
            // to 401 / Unauthenticated; never to "alive".
            r_logger.SessionLivenessLookupFailed(
                existsResult.ErrorCode ?? "<no-error-code>",
                existsResult.StatusCode.ToString());
            AuthTelemetry.SessionLivenessChecks.Add(
                1, new KeyValuePair<string, object?>("outcome", "unavailable"));
            return AuthFailures.SessionLivenessUnavailable<bool>();
        }

        var alive = existsResult.Data;
        AuthTelemetry.SessionLivenessChecks.Add(
            1,
            new KeyValuePair<string, object?>("outcome", alive ? "alive" : "revoked"));
        return D2Result<bool>.Ok(alive);
    }
}
