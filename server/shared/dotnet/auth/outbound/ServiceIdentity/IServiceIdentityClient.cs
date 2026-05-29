// -----------------------------------------------------------------------
// <copyright file="IServiceIdentityClient.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.ServiceIdentity;

using D2.Shared.Result;

/// <summary>
/// Per-process client that yields a current service-identity JWT for outbound
/// service-to-service calls. The token proves "I am the Files service" (or
/// whichever) to downstream services and is acquired via the OAuth
/// <c>client_credentials</c> grant against Edge's <c>token_endpoint</c>.
/// Carries NO user context — for user-context propagation across services
/// use <see cref="TokenExchange.ITokenExchangeClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// The implementation caches a single token in-memory per process (atomic
/// reference swap; no distributed cache needed) and proactively refreshes
/// shortly before expiry via a background hosted service. Concurrent first
/// callers share the refresh via <c>Singleflight</c>.
/// </para>
/// <para>
/// On Edge unreachable at refresh time the client logs a warning and keeps
/// serving the still-valid existing token; only when the token has actually
/// expired does <see cref="GetCurrentTokenAsync"/> hard-fail with
/// <see cref="D2Result"/>.<c>ServiceUnavailable</c>.
/// </para>
/// </remarks>
public interface IServiceIdentityClient
{
    /// <summary>
    /// Returns a current (non-expired) service-identity JWT, fetching one from
    /// Edge on first call or refreshing if the cached token is expired. Safe
    /// to call concurrently — concurrent first-callers share a single
    /// outbound HTTP request via <c>Singleflight</c>.
    /// </summary>
    /// <param name="ct">
    /// Cancellation token (per-caller; bailing does not affect siblings
    /// sharing the in-flight refresh).
    /// </param>
    /// <returns>
    /// <see cref="D2Result{T}"/>.<c>Ok(token)</c> on success;
    /// <see cref="D2Result{T}"/>.<c>ServiceUnavailable</c> if Edge is unreachable
    /// AND no still-valid cached token exists.
    /// </returns>
    ValueTask<D2Result<string>> GetCurrentTokenAsync(CancellationToken ct = default);
}
