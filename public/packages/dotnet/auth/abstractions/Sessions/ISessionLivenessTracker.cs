// -----------------------------------------------------------------------
// <copyright file="ISessionLivenessTracker.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions.Sessions;

using DcsvIo.D2.Result;

/// <summary>
/// Reads-side contract for session liveness — every authenticated request
/// asks "is this <c>d2_session_id</c> claim still alive?" (i.e. has the
/// session been revoked by sign-out, admin action, or detected anomaly).
/// </summary>
/// <remarks>
/// <para>
/// Edge owns the durable session record in <c>d2-auth</c> (<c>session</c>) and the
/// authoritative liveness state. Backend services consume the liveness
/// signal via this contract; the implementation is typically backed by a
/// tiered cache (L1 process-local + L2 Redis) populated and invalidated by
/// Edge through the <c>session-revoked</c> backplane event.
/// </para>
/// <para>
/// <strong>Fail-closed semantics</strong>: when the underlying liveness
/// store is unreachable (Redis blip, backplane partition), implementations
/// return <c>D2Result.ServiceUnavailable</c> — callers MUST translate that
/// to a 401 / Unauthenticated response, never to "alive". Treating an
/// unknown state as alive would let revoked sessions ride through outages.
/// </para>
/// <para>
/// Implementations must NEVER throw — every failure surface comes back as
/// a typed <c>D2Result</c>.
/// </para>
/// </remarks>
public interface ISessionLivenessTracker
{
    /// <summary>
    /// Checks whether the given session is still alive (not revoked).
    /// </summary>
    /// <param name="sessionId">
    /// The session id to check (the JWT's <c>d2_session_id</c> claim).
    /// </param>
    /// <param name="ct">Cancellation token honored by the underlying transport.</param>
    /// <returns>
    /// <list type="bullet">
    ///   <item><see cref="D2Result{TData}"/>.Ok(true) — session is alive.</item>
    ///   <item>
    ///     <see cref="D2Result{TData}"/>.Ok(false) — session is revoked
    ///     (no entry in cache).
    ///   </item>
    ///   <item>
    ///     <see cref="D2Result{TData}"/>.ValidationFailed —
    ///     <paramref name="sessionId"/> is <see cref="Guid.Empty"/>.
    ///   </item>
    ///   <item>
    ///     <see cref="D2Result{TData}"/>.ServiceUnavailable —
    ///     liveness store unreachable; caller MUST fail-closed (401).
    ///   </item>
    /// </list>
    /// </returns>
    ValueTask<D2Result<bool>> IsAliveAsync(Guid sessionId, CancellationToken ct = default);
}
