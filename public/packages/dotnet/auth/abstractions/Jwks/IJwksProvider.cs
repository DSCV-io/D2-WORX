// -----------------------------------------------------------------------
// <copyright file="IJwksProvider.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions.Jwks;

using DcsvIo.D2.Result;

/// <summary>
/// Reads-side contract for JWKS verify keys — every consumer-side service
/// (DcsvIo.D2.Auth runtime, Edge's own validators) calls this to look up
/// signing keys by <c>kid</c> when validating inbound JWTs.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are expected to cache the snapshot proactively and
/// refresh on (a) TTL expiry, (b) external trigger such as a backplane
/// <c>key-rotated</c> event, and (c) reactive validation failure when an
/// unknown <c>kid</c> appears (cooldown-protected, typically via
/// Singleflight).
/// </para>
/// <para>
/// All operations return <c>D2Result&lt;T&gt;</c>: success with the snapshot
/// on a hit / fresh fetch; <c>ServiceUnavailable</c> when neither cache nor
/// upstream is reachable. Implementations must NEVER throw — every failure
/// surface comes back as a typed result.
/// </para>
/// </remarks>
public interface IJwksProvider
{
    /// <summary>
    /// Returns the current verify-key snapshot. Cache-first: implementations
    /// must avoid an outbound HTTP call when a fresh snapshot is held.
    /// </summary>
    /// <param name="ct">Cancellation token honored by the underlying transport.</param>
    /// <returns>
    /// <list type="bullet">
    ///   <item><see cref="D2Result{TData}"/>.Ok(snapshot) — current snapshot.</item>
    ///   <item>
    ///     <see cref="D2Result{TData}"/>.ServiceUnavailable —
    ///     neither cache nor upstream available.
    ///   </item>
    /// </list>
    /// </returns>
    ValueTask<D2Result<JwksKeySetSnapshot>> GetKeysAsync(CancellationToken ct = default);

    /// <summary>
    /// Forces a refresh from the upstream JWKS endpoint, bypassing any cached
    /// snapshot. Used by the rotation-event subscriber and by the reactive
    /// refresh-on-unknown-kid path during JWT validation.
    /// </summary>
    /// <param name="ct">Cancellation token honored by the underlying transport.</param>
    /// <returns>
    /// <list type="bullet">
    ///   <item>
    ///     <see cref="D2Result"/>.Ok — refresh completed
    ///     (the new snapshot is now cached).
    ///   </item>
    ///   <item><see cref="D2Result"/>.ServiceUnavailable — upstream unreachable.</item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// Implementations MUST gate concurrent calls (Singleflight pattern): N
    /// callers triggering refresh at once result in 1 outbound HTTP call, with
    /// all callers awaiting the same in-flight task. They MUST also enforce
    /// a minimum interval (e.g. 30s) between refresh attempts to prevent
    /// reactive-refresh stampedes during sustained validation failures.
    /// Idempotent.
    /// </remarks>
    ValueTask<D2Result> RefreshAsync(CancellationToken ct = default);
}
