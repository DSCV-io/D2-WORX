// -----------------------------------------------------------------------
// <copyright file="LeafIssuanceAudit.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Entities;

/// <summary>
/// Append-only audit record that captures a single workload leaf-certificate
/// issuance.
/// </summary>
/// <remarks>
/// <b>Why a dedicated audit type.</b> A leaf is not a managed key — it has no
/// <c>Kid</c> and its issuance is not a lifecycle transition of any managed key —
/// so it does not fit the <see cref="EncryptionKeyAudit"/> shape (which is keyed
/// by <c>Kid</c> and whose action vocabulary is the five managed-key lifecycle
/// transitions). This record instead captures which workload was issued a leaf,
/// when, signed by which issuing CA, and when the leaf expires — enough to answer
/// the operational question of which workloads currently hold live certificates.
///
/// <b>Append-only.</b> All properties are <c>init</c>-only. No mutation methods
/// exist. The App layer creates one entry per issuance and persists it in the
/// same transaction as the issuance audit write.
///
/// <b>PII discipline.</b> The record carries the workload service id (a non-PII
/// label such as <c>edge</c>), the issuing-CA kid (opaque, loggable), and the
/// timestamps — NEVER key material and NEVER the leaf private key.
/// </remarks>
public sealed record LeafIssuanceAudit
{
    /// <summary>Gets the workload service identifier the leaf was issued for.</summary>
    public required string WorkloadServiceId { get; init; }

    /// <summary>Gets the kid of the issuing intermediate CA that signed the leaf.</summary>
    public required Kid IssuingCaKid { get; init; }

    /// <summary>
    /// Gets the UTC instant at which the leaf was issued.
    /// </summary>
    /// <remarks>
    /// Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp;
    /// no wall-clock context to preserve.
    /// </remarks>
    public required Instant IssuedAt { get; init; }

    /// <summary>
    /// Gets the UTC instant at which the issued leaf expires.
    /// </summary>
    /// <remarks>
    /// Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp;
    /// no wall-clock context to preserve.
    /// </remarks>
    public required Instant LeafNotAfter { get; init; }

    /// <summary>
    /// Creates a <see cref="LeafIssuanceAudit"/> record stamped at the current
    /// clock instant.
    /// </summary>
    /// <param name="workload">The workload the leaf was issued for.</param>
    /// <param name="issuingCaKid">The issuing intermediate CA's kid.</param>
    /// <param name="leafNotAfter">The issued leaf's expiry instant.</param>
    /// <param name="clock">Clock used to stamp <see cref="IssuedAt"/>.</param>
    /// <returns>A new, fully-populated <see cref="LeafIssuanceAudit"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="workload"/>, <paramref name="issuingCaKid"/>, or
    /// <paramref name="clock"/> is <see langword="null"/>.
    /// </exception>
    public static LeafIssuanceAudit Record(
        WorkloadIdentity workload,
        Kid issuingCaKid,
        Instant leafNotAfter,
        IClock clock)
    {
        // §5.1a carve-out: reference-type null-guards (domain VOs + DI interface).
        // No present-but-falsey concept for these types.
        ArgumentNullException.ThrowIfNull(workload);
        ArgumentNullException.ThrowIfNull(issuingCaKid);
        ArgumentNullException.ThrowIfNull(clock);

        return new LeafIssuanceAudit
        {
            WorkloadServiceId = workload.ServiceId,
            IssuingCaKid = issuingCaKid,
            IssuedAt = clock.GetCurrentInstant(),
            LeafNotAfter = leafNotAfter,
        };
    }
}
