// -----------------------------------------------------------------------
// <copyright file="IWorkloadCertificateIssuer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.WorkloadCertificate;

using D2.Shared.Result;

/// <summary>
/// The host-supplied port that issues (or re-issues) this workload's leaf
/// certificate from a PKCS#10 certificate-signing request. The shared outbound
/// lib defines the PORT; the host supplies the adapter — in development, an
/// in-process adapter that calls KeyCustodian's issuance handler directly; in the
/// end-to-end harness, a local issuance. The shared lib never references a
/// service's domain, so the boundary is the neutral
/// <see cref="WorkloadLeafMaterial"/> (DER byte arrays — all public), never a
/// service-domain certificate type.
/// </summary>
/// <remarks>
/// <para>
/// <b>CSR flow — the private key never crosses this seam.</b> The
/// <see cref="WorkloadLeafClient"/> generates a fresh ECDSA P-256 keypair
/// locally, builds the CSR, and pairs the returned certificate with its local
/// key. The issuer verifies the CSR's proof-of-possession, IGNORES the CSR's
/// subject (the leaf's subject-alternative-name is always the issuer's
/// authenticated view of the caller), and signs the CSR's public key — it never
/// sees, holds, or returns the leaf private key.
/// </para>
/// <para>
/// <b>Live cross-process wiring is host-gated.</b> KeyCustodian's issuance
/// endpoint (the <c>IssueWorkloadCertificate</c> gRPC method) is built and
/// TestServer-proven; the live Edge-host adapter that dials it — plus the
/// first-leaf bootstrap-identity provisioning — lands with the Edge host (see
/// ADR-0023). Until then this port is the in-process / harness seam that proves
/// the full refresh-ahead + presentation path locally.
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
    /// Issues a fresh workload leaf certificate for this workload from its PKCS#10
    /// certificate-signing request.
    /// </summary>
    /// <param name="csrDer">
    /// The DER-encoded PKCS#10 certificate-signing request. PUBLIC material by
    /// construction (public key + request metadata + a self-signature) — it never
    /// carries the private key. The issuer validates proof-of-possession and
    /// ignores the CSR's subject.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="D2Result{T}"/>.<c>Ok</c> with the freshly-issued
    /// <see cref="WorkloadLeafMaterial"/> (leaf + issuing intermediate — all
    /// public) on success; a typed failure (e.g. <c>ServiceUnavailable</c>) when
    /// issuance is transiently unavailable.
    /// </returns>
    ValueTask<D2Result<WorkloadLeafMaterial>> IssueAsync(
        byte[] csrDer, CancellationToken ct = default);
}
