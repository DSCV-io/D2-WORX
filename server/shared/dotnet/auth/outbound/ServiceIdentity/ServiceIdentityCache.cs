// -----------------------------------------------------------------------
// <copyright file="ServiceIdentityCache.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.ServiceIdentity;

/// <summary>
/// Single-value-per-process cache of the current service-identity token.
/// Atomic reference swap of an <see cref="ServiceIdentitySnapshot"/> — readers
/// never observe a torn state, and there is no lock on the read path.
/// </summary>
/// <remarks>
/// <para>
/// Not backed by <c>ILocalCache</c>: this is one slot per process, no eviction,
/// no key namespace. A <c>volatile</c>-fenced field reference is the right
/// tool here.
/// </para>
/// <para>
/// "Still valid" semantics: <see cref="TryGet"/> returns null both when no
/// token has ever been set AND when the cached token's <c>ExpiresAt</c> has
/// passed. The refresh hosted service prevents the latter case in steady
/// state by refreshing ahead of expiry.
/// </para>
/// </remarks>
internal sealed class ServiceIdentityCache
{
    private ServiceIdentitySnapshot? _current;

    /// <summary>
    /// Gets the cached snapshot (any state — fresh, near-expiry, or expired)
    /// without freshness filtering. Used by the refresh hosted service to
    /// decide whether refresh is needed.
    /// </summary>
    /// <returns>The current snapshot, or null if none has been set yet.</returns>
    public ServiceIdentitySnapshot? PeekRaw() => Volatile.Read(ref _current);

    /// <summary>
    /// Returns the cached snapshot if its <c>ExpiresAt</c> is strictly after
    /// <paramref name="now"/>; otherwise null. Used by callers that demand a
    /// servable token (will trigger a synchronous refresh on null).
    /// </summary>
    /// <param name="now">The current UTC time.</param>
    /// <returns>A non-expired snapshot or null.</returns>
    public ServiceIdentitySnapshot? TryGet(DateTimeOffset now)
    {
        var snapshot = Volatile.Read(ref _current);
        if (snapshot is null) return null;
        return snapshot.ExpiresAt > now ? snapshot : null;
    }

    /// <summary>
    /// Atomically replaces the cached snapshot. Concurrent writers race; the
    /// last write wins (which for refresh paths is the most recently-fetched
    /// token — the right semantics).
    /// </summary>
    /// <param name="snapshot">The new snapshot to publish.</param>
    public void Set(ServiceIdentitySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Volatile.Write(ref _current, snapshot);
    }

    /// <summary>
    /// Clears the cache. Test seam — production code never invalidates
    /// service-identity entries (they age out on TTL).
    /// </summary>
    internal void ClearForTesting() => Volatile.Write(ref _current, null);
}
