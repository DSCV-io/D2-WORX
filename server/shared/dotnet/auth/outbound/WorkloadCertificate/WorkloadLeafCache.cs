// -----------------------------------------------------------------------
// <copyright file="WorkloadLeafCache.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.WorkloadCertificate;

using System.Security.Cryptography.X509Certificates;
using JetBrains.Annotations;

/// <summary>
/// Single-value-per-process cache of the current live workload leaf certificate.
/// Atomic reference swap of a <see cref="WorkloadLeafSnapshot"/> — readers never
/// observe a torn state, and there is no lock on the read path.
/// </summary>
/// <remarks>
/// <para>
/// Not backed by <c>ILocalCache</c>: this is one slot per process, no eviction, no
/// key namespace. A <c>volatile</c>-fenced field reference is the right tool. Mirrors
/// <c>ServiceIdentityCache</c>, with one addition — the cached value is a live
/// <see cref="X509Certificate2"/>, so the cache owns disposal: the superseded
/// snapshot's leaf is disposed on <see cref="Set"/> (a refresh-ahead reissue
/// publishes well before expiry, so connections using the old leaf are already
/// established — the certificate is consulted only at handshake), and the current
/// leaf is disposed when the cache itself is disposed (process shutdown).
/// </para>
/// <para>
/// "Still valid" semantics: <see cref="TryGet"/> returns null both when no leaf has
/// ever been set AND when the cached leaf's <c>NotAfter</c> has passed. The refresh
/// hosted service prevents the latter in steady state by reissuing ahead of expiry.
/// </para>
/// </remarks>
[MustDisposeResource]
internal sealed class WorkloadLeafCache : IDisposable
{
    private WorkloadLeafSnapshot? _current;
    private bool _disposed;

    /// <summary>
    /// Gets the cached snapshot (any state — fresh, near-expiry, or expired)
    /// without freshness filtering. Used by the refresh hosted service to decide
    /// whether reissue is needed.
    /// </summary>
    /// <returns>The current snapshot, or null if none has been set yet.</returns>
    public WorkloadLeafSnapshot? PeekRaw() => Volatile.Read(ref _current);

    /// <summary>
    /// Returns the cached snapshot if its <c>NotAfter</c> is strictly after
    /// <paramref name="now"/>; otherwise null. Used by callers that demand a
    /// presentable leaf (triggers a reissue on null).
    /// </summary>
    /// <param name="now">The current UTC time.</param>
    /// <returns>A non-expired snapshot or null.</returns>
    public WorkloadLeafSnapshot? TryGet(DateTimeOffset now)
    {
        var snapshot = Volatile.Read(ref _current);

        if (snapshot is null) return null;

        return snapshot.NotAfter > now ? snapshot : null;
    }

    /// <summary>
    /// Returns the current live leaf certificate if it is non-expired, else null.
    /// What the channel handler's selection callback reads at connect time.
    /// </summary>
    /// <param name="now">The current UTC time.</param>
    /// <returns>The live, non-expired leaf, or null.</returns>
    public X509Certificate2? GetCurrentLeaf(DateTimeOffset now) => TryGet(now)?.Leaf;

    /// <summary>
    /// Atomically replaces the cached snapshot and disposes the superseded leaf.
    /// Concurrent writers race; the last write wins (which for refresh paths is the
    /// most recently-issued leaf — the right semantics).
    /// </summary>
    /// <param name="snapshot">The new snapshot to publish.</param>
    public void Set(WorkloadLeafSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var prior = Interlocked.Exchange(ref _current, snapshot);

        // Dispose the superseded leaf — a refresh-ahead reissue publishes well
        // before expiry, so any connection using the old leaf is already
        // established (the certificate is consulted only at the TLS handshake).
        if (prior is not null && !ReferenceEquals(prior, snapshot))
            prior.Leaf.Dispose();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        Volatile.Read(ref _current)?.Leaf.Dispose();
    }

    /// <summary>
    /// Clears the cache and disposes the current leaf. Test seam — production code
    /// never invalidates leaf entries (they age out on TTL + refresh-ahead).
    /// </summary>
    internal void ClearForTesting()
    {
        var prior = Interlocked.Exchange(ref _current, null);
        prior?.Leaf.Dispose();
    }
}
