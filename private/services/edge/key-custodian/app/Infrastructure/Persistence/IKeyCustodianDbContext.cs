// -----------------------------------------------------------------------
// <copyright file="IKeyCustodianDbContext.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Infrastructure.Persistence;

/// <summary>
/// The persistence seam the App layer's command and query handlers depend on.
/// Exposes the three flat record sets, <see cref="SaveChangesAsync"/>, and
/// <see cref="ClearChangeTracker"/> (post-failed-save re-read hygiene); the
/// concrete <c>DbContext</c> (with the relational model, value-converters for
/// <c>Instant</c>, the <c>xmin</c> concurrency token, and the migration) lives
/// in the Infra layer.
/// </summary>
/// <remarks>
/// EF-as-DDD: command handlers load a tracked <see cref="KeyRecord"/> via the
/// query extensions, rehydrate the aggregate through the mapper, invoke the
/// domain transition, project the result back onto the same tracked record,
/// append a <see cref="KeyAuditRecord"/>, and call <see cref="SaveChangesAsync"/>
/// once — one ordinary UPDATE plus the audit INSERT in a single transaction.
/// Query handlers read with <c>AsNoTracking()</c>.
/// </remarks>
public interface IKeyCustodianDbContext
{
    /// <summary>Gets the managed-key rows.</summary>
    DbSet<KeyRecord> Keys { get; }

    /// <summary>Gets the append-only managed-key lifecycle audit records.</summary>
    DbSet<KeyAuditRecord> Audit { get; }

    /// <summary>Gets the append-only workload leaf-certificate issuance audit records.</summary>
    DbSet<LeafIssuanceAuditRecord> LeafIssuanceAudit { get; }

    /// <summary>
    /// Persists all tracked changes in a single transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of state entries written to the store.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the EF change tracker of all tracked entities. Required after a failed
    /// <see cref="SaveChangesAsync"/> (e.g. uniqueness / EXCLUDE collision) so subsequent
    /// re-reads are not poisoned by rejected inserts that never committed.
    /// </summary>
    void ClearChangeTracker();
}
