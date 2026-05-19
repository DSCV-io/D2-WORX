// -----------------------------------------------------------------------
// <copyright file="TokenExchangeCache.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.TokenExchange;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using D2.Shared.Auth.Outbound.Telemetry;
using D2.Shared.Caching;
using D2.Shared.Result;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Token-exchange cache facade over the shared <see cref="ILocalCache"/>
/// singleton, plus a per-process reverse-index that maps inbound
/// <c>sessionId</c> → cache keys so a session-revoked backplane event can
/// purge every exchange token bound to that session.
/// </summary>
/// <remarks>
/// <para>
/// Key shape: <c>{KeyPrefix}{sessionId}:{audience}:{scopeSetHash}</c>.
/// scopeSetHash is 16 lowercase hex chars derived from the first 8 bytes of
/// SHA-256 over the sorted comma-joined scope names (or the literal
/// <c>_default</c> when no narrowing is requested) — keeps keys bounded and
/// injection-safe even if a scope string ever contains the key delimiter.
/// </para>
/// <para>
/// Audience strings beyond 2048 chars are rejected (cache miss returned).
/// In production, audiences are codegen'd <c>Audiences.*</c> constants —
/// well below the cap; the cap exists as defense-in-depth against an
/// attacker-controlled or accidentally-oversized audience string blowing up
/// cache-key memory.
/// </para>
/// <para>
/// The reverse-index is in-process only — a process restart loses it, but
/// the entries it points at also live in the in-process
/// <see cref="ILocalCache"/> singleton, so they vanish in the same restart.
/// No persistence needed.
/// </para>
/// <para>
/// Known limitation — slow leak: reverse-index entries for sessions whose
/// cache entries TTL-expire (rather than being explicitly revoked) linger
/// until process restart. Per sessionId the leak is small (~64 bytes for
/// the GUID + the empty inner dictionary), so a busy host with high session
/// churn accumulates on the order of MB / day before restart resets it.
/// Acceptable for current scope; revisit with a periodic prune if multi-day
/// process uptime + high churn becomes a real workload.
/// </para>
/// <para>
/// Backplane subscription is OPTIONAL — if <see cref="ICacheInvalidationBackplane"/>
/// is not registered in DI, this cache logs a startup warning and falls back
/// to TTL-only invalidation. In a single-instance deployment that's fine; in
/// a multi-instance deployment without the backplane, session-revoke events
/// won't propagate to other instances' token-exchange caches and they'll
/// serve stale exchange tokens until TTL expiry.
/// </para>
/// </remarks>
[MustDisposeResource(false)]
internal sealed class TokenExchangeCache : IAsyncDisposable
{
    /// <summary>
    /// Maximum length of the <c>audience</c> argument permitted in
    /// <see cref="BuildKey"/>. Audiences in production come from codegen'd
    /// <c>Audiences.*</c> URL constants (well under 256 chars); 2048 is the
    /// browser/server URL-length convention and a generous defense-in-depth
    /// cap against attacker-controlled or accidentally-oversized audiences
    /// blowing up cache-key memory.
    /// </summary>
    public const int MAX_AUDIENCE_LENGTH = 2048;

    private const string _NO_SCOPE_NARROWING_SENTINEL = "_default";

    /// <summary>
    /// Maximum length of the malformed-key string included in the warning log
    /// when a backplane delivers an unparseable session-revoked event. Caps
    /// log-injection blast radius if an attacker with backplane access
    /// crafts arbitrarily-long invalidation keys.
    /// </summary>
    private const int _MAX_LOGGED_KEY_LENGTH = 256;

    private readonly ILocalCache r_localCache;
    private readonly AuthOutboundOptions r_options;
    private readonly ILogger<TokenExchangeCache> r_logger;
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> r_reverseIndex = new();
    private readonly IAsyncDisposable? r_backplaneSubscription;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="TokenExchangeCache"/> class.</summary>
    /// <param name="localCache">The shared per-process cache.</param>
    /// <param name="options">Outbound auth options (key prefix, fallback TTL).</param>
    /// <param name="logger">The logger.</param>
    /// <param name="backplane">
    /// Optional cache-invalidation backplane. When supplied, the cache
    /// subscribes for <c>session-revoked:{guid}</c> events and purges
    /// matching reverse-index entries.
    /// </param>
    [MustDisposeResource(false)]
    public TokenExchangeCache(
        ILocalCache localCache,
        IOptions<AuthOutboundOptions> options,
        ILogger<TokenExchangeCache> logger,
        ICacheInvalidationBackplane? backplane = null)
    {
        ArgumentNullException.ThrowIfNull(localCache);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        r_localCache = localCache;
        r_options = options.Value;
        r_logger = logger;

        if (backplane is null)
        {
            r_logger.TokenExchangeBackplaneAbsent();
            return;
        }

        r_backplaneSubscription = backplane.Subscribe(OnBackplaneInvalidationAsync);
    }

    /// <summary>
    /// Builds the cache key for a given (sessionId, audience, scope-set)
    /// tuple. Stable across calls for the same input — same key shape that
    /// the writer used. Returns <c>null</c> when <paramref name="audience"/>
    /// exceeds <see cref="MAX_AUDIENCE_LENGTH"/>; callers MUST treat null as
    /// "key cannot be built" and skip the cache path entirely.
    /// </summary>
    /// <param name="sessionId">The session id from the inbound JWT.</param>
    /// <param name="audience">The downstream audience URL.</param>
    /// <param name="narrowedScopes">Optional narrowed scope set; null = no narrowing.</param>
    /// <returns>The fully-qualified cache key, or null if the audience is too long.</returns>
    public string? BuildKey(Guid sessionId, string audience, IReadOnlySet<string>? narrowedScopes)
    {
        ArgumentNullException.ThrowIfNull(audience);
        if (audience.Length > MAX_AUDIENCE_LENGTH)
            return null;

        var scopeFragment = narrowedScopes is null
            ? _NO_SCOPE_NARROWING_SENTINEL
            : HashScopeSet(narrowedScopes);

        return $"{r_options.TokenExchangeCacheKeyPrefix}{sessionId:D}:{audience}:{scopeFragment}";
    }

    /// <summary>Returns the cached token for <paramref name="key"/>, or null on miss.</summary>
    /// <param name="key">The cache key (built via <see cref="BuildKey"/>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cached token or null.</returns>
    public async ValueTask<string?> TryGetAsync(string key, CancellationToken ct = default)
    {
        var result = await r_localCache.GetAsync<string>(key, ct);
        return result.Success ? result.Data : null;
    }

    /// <summary>
    /// Writes <paramref name="token"/> under <paramref name="key"/> with
    /// <paramref name="ttl"/>, and adds the key to the reverse-index for
    /// <paramref name="sessionId"/> so a backplane invalidation event for
    /// that session can drop the entry.
    /// </summary>
    /// <param name="sessionId">The session id from the inbound JWT.</param>
    /// <param name="key">The cache key (built via <see cref="BuildKey"/>).</param>
    /// <param name="token">The exchanged JWT to cache.</param>
    /// <param name="ttl">TTL for the entry — derived from the OAuth response's <c>expires_in</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>Ok</c> on success; the underlying <see cref="ILocalCache"/> failure on cache error.</returns>
    public async ValueTask<D2Result> SetAsync(
        Guid sessionId,
        string key,
        string token,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        var setResult = await r_localCache.SetAsync(key, token, ttl, ct);
        if (!setResult.Success)
            return setResult;

        var sessionKeys = r_reverseIndex.GetOrAdd(
            sessionId,
            static _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
        sessionKeys.TryAdd(key, 0);
        return setResult;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (r_backplaneSubscription is not null)
            await r_backplaneSubscription.DisposeAsync();
    }

    /// <summary>
    /// Test seam — exposes the reverse-index so backplane-invalidation tests
    /// can assert that entries are added on Set and removed on event.
    /// </summary>
    /// <param name="sessionId">The session id to inspect.</param>
    /// <returns>The cache keys currently mapped to <paramref name="sessionId"/>.</returns>
    internal IReadOnlyCollection<string> GetKeysForSessionForTesting(Guid sessionId) =>
        r_reverseIndex.TryGetValue(sessionId, out var set)
            ? [.. set.Keys]
            : [];

    private static string HashScopeSet(IReadOnlySet<string> scopes)
    {
        // Sorted comma-join → SHA-256 → first 16 lowercase hex chars. Stable
        // across calls; bounded length; injection-safe.
        var sorted = scopes.OrderBy(s => s, StringComparer.Ordinal);
        var joined = string.Join(",", sorted);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexStringLower(hash.AsSpan(0, 8));
    }

    private async ValueTask OnBackplaneInvalidationAsync(string key, CancellationToken ct)
    {
        // Convention: session-revoked events publish "session-revoked:{guid}".
        // The cache backplane carries arbitrary keys, so we must filter to the
        // ones we care about and ignore the rest.
        const string sessionRevokedPrefix = "session-revoked:";
        if (!key.StartsWith(sessionRevokedPrefix, StringComparison.Ordinal))
            return;

        var guidSpan = key.AsSpan(sessionRevokedPrefix.Length);
        if (!Guid.TryParse(guidSpan, out var sessionId))
        {
            // Truncate the offending key before logging — bounds log-injection
            // blast radius if the backplane delivered a maliciously oversized
            // key.
            var loggable = key.Length > _MAX_LOGGED_KEY_LENGTH
                ? key[.._MAX_LOGGED_KEY_LENGTH] + "..."
                : key;
            r_logger.TokenExchangeBackplaneMalformedSessionRevoke(loggable);
            return;
        }

        // Tiny race window: a SetAsync racing with this TryRemove for the same
        // sessionId may see the inner dict missing and create a fresh one,
        // leaving its newly-written cache entry orphaned in r_reverseIndex.
        // The orphan still expires on TTL — bounded staleness — and
        // session-revoked is one-shot per session in practice. Documented as a
        // known limitation; no defensive lock since the contention is rare and
        // the impact is bounded.
        if (!r_reverseIndex.TryRemove(sessionId, out var keys))
            return;

        var keyList = keys.Keys.ToList();
        await r_localCache.RemoveManyAsync(keyList, ct);
        r_logger.TokenExchangeSessionRevokedPurged(sessionId, keyList.Count);

        OutboundTelemetry.SR_TokenExchangeRevokedPurges.Add(keyList.Count);
    }
}
