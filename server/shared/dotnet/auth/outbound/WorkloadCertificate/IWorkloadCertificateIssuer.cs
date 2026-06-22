// -----------------------------------------------------------------------
// <copyright file="IWorkloadCertificateIssuer.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.WorkloadCertificate;

using D2.Shared.Result;

/// <summary>
/// The host-supplied port that issues (or re-issues) this workload's leaf
/// certificate. The shared outbound lib defines the PORT; the host supplies the
/// adapter — in development, an in-process adapter that calls KeyCustodian's
/// issuance handler directly; in the end-to-end harness, a local issuance. The
/// shared lib never references a service's domain, so the boundary is the neutral
/// <see cref="WorkloadLeafMaterial"/> (DER + PKCS#8 byte arrays), never a
/// service-domain certificate type.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cross-process issuance is not yet built.</b> The gRPC contract for a separate
/// service to obtain its first leaf from KeyCustodian over the wire (adding the
/// issuance endpoint to the cross-process gRPC contract + bootstrap-identity
/// provisioning) is deferred pending the cross-process issuance contract — see
/// ADR-0023. This port is the in-process / harness seam that proves the full
/// refresh-ahead + presentation path locally.
/// </para>
/// <para>
/// The <see cref="WorkloadLeafClient"/> wraps this port with the refresh-ahead
/// machinery (single-value cache, single-flight dedup, serve-stale-on-transient).
/// Implementations should return a typed failure (not throw) when issuance is
/// transiently unavailable so the client can keep serving a still-valid leaf.
/// </para>
/// </remarks>
public interface IWorkloadCertificateIssuer
{
    /// <summary>
    /// Issues a fresh workload leaf certificate for this workload.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="D2Result{T}"/>.<c>Ok</c> with the freshly-issued
    /// <see cref="WorkloadLeafMaterial"/> on success; a typed failure (e.g.
    /// <c>ServiceUnavailable</c>) when issuance is transiently unavailable.
    /// </returns>
    ValueTask<D2Result<WorkloadLeafMaterial>> IssueAsync(CancellationToken ct = default);
}
