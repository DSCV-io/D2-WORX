// -----------------------------------------------------------------------
// <copyright file="KeyRecordQueryExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Persistence;

/// <summary>
/// Server-side composable LINQ filters over <see cref="KeyRecord"/> value
/// columns. Each returns an <see cref="IQueryable{T}"/> so they compose into a
/// single SQL <c>WHERE</c> (verified SQL-side in the Infra integration gate;
/// behavior-verified here against the InMemory provider).
/// </summary>
/// <remarks>
/// Filters predicate on the flat <c>Status</c> / <c>KeyDomain</c> /
/// <c>KeyType</c> value columns — never on the rehydrated aggregate — so EF can
/// translate them to SQL rather than evaluating client-side.
/// </remarks>
public static class KeyRecordQueryExtensions
{
    extension(IQueryable<KeyRecord> source)
    {
        /// <summary>Filters to keys in the <see cref="KeyStatus.Pending"/> state.</summary>
        /// <returns>The filtered query.</returns>
        public IQueryable<KeyRecord> Pending() =>
            source.Where(k => k.Status == KeyStatus.Pending);

        /// <summary>Filters to keys in the <see cref="KeyStatus.Active"/> state.</summary>
        /// <returns>The filtered query.</returns>
        public IQueryable<KeyRecord> Active() =>
            source.Where(k => k.Status == KeyStatus.Active);

        /// <summary>Filters to keys in the <see cref="KeyStatus.Retiring"/> state.</summary>
        /// <returns>The filtered query.</returns>
        public IQueryable<KeyRecord> Retiring() =>
            source.Where(k => k.Status == KeyStatus.Retiring);

        /// <summary>
        /// Filters to live keys — those still serving operations: <c>Pending</c>,
        /// <c>Active</c>, or <c>Retiring</c>. Excludes the terminal <c>Retired</c>
        /// and <c>Compromised</c> states.
        /// </summary>
        /// <returns>The filtered query.</returns>
        public IQueryable<KeyRecord> Live() =>
            source.Where(k =>
                k.Status == KeyStatus.Pending
                || k.Status == KeyStatus.Active
                || k.Status == KeyStatus.Retiring);

        /// <summary>Filters to keys belonging to the supplied domain.</summary>
        /// <param name="domain">The normalized domain value to match.</param>
        /// <returns>The filtered query.</returns>
        public IQueryable<KeyRecord> ForDomain(string domain) =>
            source.Where(k => k.KeyDomain == domain);

        /// <summary>Filters to asymmetric signing keys (<see cref="KeyType.RsaSigning"/>).
        /// </summary>
        /// <returns>The filtered query.</returns>
        public IQueryable<KeyRecord> Signing() =>
            source.Where(k => k.KeyType == KeyType.RsaSigning);

        /// <summary>
        /// Filters to symmetric payload-encryption keys (<see cref="KeyType.AesPayload"/>).
        /// </summary>
        /// <returns>The filtered query.</returns>
        public IQueryable<KeyRecord> Payload() =>
            source.Where(k => k.KeyType == KeyType.AesPayload);

        /// <summary>
        /// Filters to asymmetric ECDH sealing keys (<see cref="KeyType.EcdhSealing"/>).
        /// </summary>
        /// <returns>The filtered query.</returns>
        public IQueryable<KeyRecord> Sealing() =>
            source.Where(k => k.KeyType == KeyType.EcdhSealing);
    }
}
