// -----------------------------------------------------------------------
// <copyright file="ICacheSet.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching;

using DcsvIo.D2.Result;

/// <summary>
/// Distributed-set primitives — add-to-set, cardinality, membership.
/// Maps to Redis SADD / SCARD / SREM / SISMEMBER. Only on
/// <see cref="IDistributedCache"/>; per-process set-cardinality has no
/// realistic use case (it's a single-instance counter that can't
/// aggregate across the cluster), and tiered composition would silently
/// hide the cluster-wide nature of the operation.
/// </summary>
/// <remarks>
/// The motivating use case is rate-limiting "fingerprint too common"
/// detection (per <c>docs/RATE-LIMITING.md</c>): track distinct IPs ever
/// observed for a given fingerprint, compare cardinality against a
/// threshold to decide whether the fingerprint is shared widely enough
/// to be unreliable. Other plausible uses include:
/// <list type="bullet">
/// <item>Distinct-user counts per feature flag</item>
/// <item>Active-session tracking by org</item>
/// <item>Any bounded-cardinality "have I seen X for Y" check that would
/// otherwise need a separate per-pair key</item>
/// </list>
/// </remarks>
public interface ICacheSet
{
    /// <summary>
    /// Adds a member to the set at <paramref name="key"/>. The set is
    /// created on first add. Optional TTL applied on creation; existing
    /// sets keep their TTL.
    /// </summary>
    /// <param name="key">Set key.</param>
    /// <param name="member">Member to add.</param>
    /// <param name="expiration">TTL applied to the set on first add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok(true)</c> if the member was newly added. <c>Ok(false)</c> if
    /// the member already existed in the set. Failure on backing-store error.
    /// </returns>
    ValueTask<D2Result<bool>> SetAddAsync(
        string key, string member, TimeSpan? expiration = null, CancellationToken ct = default);

    /// <summary>
    /// Returns the cardinality (number of distinct members) of the set at
    /// <paramref name="key"/>. Absent set → <c>Ok(0)</c>.
    /// </summary>
    /// <param name="key">Set key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok(count)</c>. Failure on backing-store error.
    /// </returns>
    ValueTask<D2Result<long>> SetCardinalityAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Removes a member from the set at <paramref name="key"/>. Idempotent
    /// — succeeds whether the member existed or not.
    /// </summary>
    /// <param name="key">Set key.</param>
    /// <param name="member">Member to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok(true)</c> if the member was present and removed.
    /// <c>Ok(false)</c> if the member was absent. Failure on backing-store error.
    /// </returns>
    ValueTask<D2Result<bool>> SetRemoveAsync(
        string key, string member, CancellationToken ct = default);

    /// <summary>
    /// Returns whether <paramref name="member"/> is in the set at
    /// <paramref name="key"/>.
    /// </summary>
    /// <param name="key">Set key.</param>
    /// <param name="member">Member to test.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok(true|false)</c>. Failure on backing-store error.
    /// </returns>
    ValueTask<D2Result<bool>> SetContainsAsync(
        string key, string member, CancellationToken ct = default);
}
