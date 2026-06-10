// -----------------------------------------------------------------------
// <copyright file="IKeyCustodianDbContext.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Persistence;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// The persistence seam the App layer's command and query handlers depend on.
/// Exposes the two flat record sets plus <see cref="SaveChangesAsync"/>; the
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

    /// <summary>Gets the append-only audit rows.</summary>
    DbSet<KeyAuditRecord> Audit { get; }

    /// <summary>
    /// Persists all tracked changes in a single transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of state entries written to the store.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
