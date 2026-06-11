// -----------------------------------------------------------------------
// <copyright file="KeyAuditRecord.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Infrastructure.Persistence;

using D2.Edge.KeyCustodian.Domain.Enums;
using NodaTime;

/// <summary>
/// Flat, append-only EF persistence row for a single key-lifecycle transition.
/// </summary>
/// <remarks>
/// <b>Why a flat record.</b> Like <see cref="KeyRecord"/>, the audit row carries
/// only primitive / closed-enum columns so the EF model needs no value
/// converters and the row is uniformly InMemory-testable. The domain audit
/// shape (<c>EncryptionKeyAudit</c>) carries a strong-typed <c>Kid</c>;
/// the mapper flattens it to the <see cref="Kid"/> string column.
///
/// <b>Append-only + PII discipline.</b> One row is inserted per transition; rows
/// are never updated or deleted. The row carries the kid (opaque, loggable),
/// the action, the resulting status, the timestamp, and an OPTIONAL non-PII
/// <see cref="Detail"/> breadcrumb — NEVER key material and NEVER the raw
/// compromise reason.
/// </remarks>
public sealed class KeyAuditRecord
{
    /// <summary>Gets or sets the identity primary key. Database-generated.</summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the key identifier this entry records.
    /// Foreign key to <see cref="KeyRecord.Kid"/>.
    /// </summary>
    public required string Kid { get; set; }

    /// <summary>Gets or sets the lifecycle action that produced this entry.</summary>
    public required KeyAuditAction Action { get; set; }

    /// <summary>Gets or sets the key's status after the transition.</summary>
    public required KeyStatus ResultingStatus { get; set; }

    /// <summary>
    /// Gets or sets the UTC instant at which the transition occurred.
    /// </summary>
    /// <remarks>
    /// Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp;
    /// no wall-clock context to preserve.
    /// </remarks>
    public required Instant OccurredAt { get; set; }

    /// <summary>
    /// Gets or sets an optional non-PII breadcrumb. MUST NOT contain key material,
    /// a raw compromise reason, PII, or personally identifying strings.
    /// </summary>
    public string? Detail { get; set; }
}
