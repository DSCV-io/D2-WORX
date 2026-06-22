// -----------------------------------------------------------------------
// <copyright file="LeafIssuanceAuditRecord.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Infrastructure.Persistence;

/// <summary>
/// Flat, append-only EF persistence row for a single workload leaf-certificate
/// issuance.
/// </summary>
/// <remarks>
/// <b>Why a dedicated record.</b> A leaf is not a managed key, so the
/// <see cref="KeyAuditRecord"/> shape (keyed by a managed-key kid + a lifecycle
/// action vocabulary) does not fit. This record captures the leaf-issuance facts
/// instead: which workload was issued a leaf, when, signed by which issuing CA,
/// and when the leaf expires.
///
/// <b>Why a flat record.</b> Like <see cref="KeyRecord"/> / <see cref="KeyAuditRecord"/>,
/// this row carries only primitive / BCL columns so the EF model needs no value
/// converters and the record is uniformly InMemory-testable. The domain shape
/// (<c>LeafIssuanceAudit</c>) carries a strong-typed <c>Kid</c>; the mapper
/// flattens it to the <see cref="IssuingCaKid"/> string column.
///
/// <b>Append-only + PII discipline.</b> One row is inserted per issuance; rows are
/// never updated or deleted. The row carries the workload service id (a non-PII
/// label), the issuing-CA kid (opaque, loggable), and the timestamps — NEVER key
/// material and NEVER the leaf private key.
/// </remarks>
public sealed class LeafIssuanceAuditRecord
{
    /// <summary>Gets or sets the identity primary key. Database-generated.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the workload service identifier the leaf was issued for.</summary>
    public required string WorkloadServiceId { get; set; }

    /// <summary>
    /// Gets or sets the kid of the issuing intermediate CA that signed the leaf.
    /// Foreign key to <see cref="KeyRecord.Kid"/>.
    /// </summary>
    public required string IssuingCaKid { get; set; }

    /// <summary>
    /// Gets or sets the UTC instant at which the leaf was issued.
    /// </summary>
    /// <remarks>
    /// Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp;
    /// no wall-clock context to preserve.
    /// </remarks>
    public required Instant IssuedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC instant at which the issued leaf expires.
    /// </summary>
    /// <remarks>
    /// Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp;
    /// no wall-clock context to preserve.
    /// </remarks>
    public required Instant LeafNotAfter { get; set; }
}
