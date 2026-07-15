// -----------------------------------------------------------------------
// <copyright file="IAnonymizationTrackable.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.Abstractions;

/// <summary>
/// Provable-anonymization marker. The engine sets the backing field for
/// <see cref="IsAnonymized"/> to <see langword="true"/> via EF Core when it overwrites
/// a row's PII, and excludes already-anonymized rows on subsequent runs for idempotency.
/// </summary>
/// <remarks>
/// <para>
/// This interface is mandatory on any ownership-marked entity (<see cref="IUserOwned"/>
/// or <see cref="IOrgOwned"/>) that carries at least one <see cref="AnonymizableAttribute"/>-
/// decorated property. The startup guard in <c>DcsvIo.D2.DataGovernance.EntityFrameworkCore</c>
/// enforces this at host-build time.
/// </para>
/// <para>
/// The property is <em>read-only on this interface by design</em> — the engine writes via
/// EF Core's <c>ExecuteUpdateAsync</c> or <c>SaveChanges</c>, not through this CLR
/// accessor. Concrete entities provide a mutable backing (e.g. <c>public bool IsAnonymized
/// { get; set; }</c>) that EF Core maps to the persistence column; the read-only contract
/// here prevents callers from setting the flag out-of-band (only the engine owns this
/// transition).
/// </para>
/// </remarks>
public interface IAnonymizationTrackable
{
    /// <summary>
    /// Gets a value indicating whether this entity's PII fields have been overwritten by
    /// the anonymization engine. When <see langword="true"/>, the engine skips this row on
    /// re-run, making the anonymization operation idempotent.
    /// </summary>
    bool IsAnonymized { get; }
}
