// -----------------------------------------------------------------------
// <copyright file="ITokenExchangeClient.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.TokenExchange;

using D2.Shared.Result;

/// <summary>
/// Per-process client that exchanges an inbound user JWT for a downstream-
/// audience JWT via the OAuth Token Exchange grant (RFC 8693). The downstream
/// service receives the user's identity directly — no separate user-context
/// envelope needed for sync gRPC / HTTP calls.
/// </summary>
/// <remarks>
/// <para>
/// Cached per <c>(sessionId, audience, scope-set)</c> tuple in the shared
/// <c>ILocalCache</c> singleton. <c>sessionId</c> comes from the inbound
/// JWT's <c>d2_session_id</c> claim and is the invalidation key used by
/// the cache-invalidation backplane on session-revoke events.
/// </para>
/// <para>
/// Concurrent first-callers for the same key share a single outbound
/// HTTP request via <c>Singleflight</c>. Edge unreachable on cache miss
/// returns <see cref="D2Result"/>.<c>ServiceUnavailable</c> — there is no
/// fallback (graceful-degradation here would create silent
/// stale-token behavior that's harder to debug than a fast fail).
/// </para>
/// </remarks>
public interface ITokenExchangeClient
{
    /// <summary>
    /// Exchanges <paramref name="subjectToken"/> for a JWT with
    /// <c>aud=<paramref name="targetAudience"/></c>, optionally narrowing the
    /// requested scopes to a subset.
    /// </summary>
    /// <param name="subjectToken">
    /// The inbound user JWT (already validated by inbound auth middleware
    /// upstream of this call). Must carry a <c>d2_session_id</c> claim.
    /// </param>
    /// <param name="targetAudience">
    /// The downstream service audience URL — typically one of the
    /// <c>D2.Shared.Auth.Abstractions.Audiences.*</c> constants.
    /// </param>
    /// <param name="narrowedScopes">
    /// Optional subset of scopes to request on the new token. Null = request
    /// all scopes the subject token carries.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="D2Result{T}"/>.<c>Ok(token)</c> on success;
    /// <see cref="D2Result{T}"/>.<c>ValidationFailed</c> on invalid input
    /// (subject token without a parseable session id);
    /// <see cref="D2Result{T}"/>.<c>ServiceUnavailable</c> if Edge is
    /// unreachable on cache miss.
    /// </returns>
    ValueTask<D2Result<string>> ExchangeAsync(
        string subjectToken,
        string targetAudience,
        IReadOnlySet<string>? narrowedScopes = null,
        CancellationToken ct = default);
}
