// -----------------------------------------------------------------------
// <copyright file="IssuedWorkloadCertificate.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Domain.ValueObjects;

/// <summary>
/// A freshly-issued, on-demand workload leaf certificate handed back to the
/// caller. NOT a managed-key aggregate and never persisted — the only
/// persistence on the leaf path is the lightweight issuance audit entry.
/// </summary>
/// <remarks>
/// <b>All-public material — no private key exists here.</b> The workload
/// generates its own leaf keypair and submits a PKCS#10 certificate-signing
/// request; KeyCustodian signs the CSR's verified public key and returns only
/// certificates. The leaf private key never enters KeyCustodian, so this type
/// carries no secret member, needs no redaction, and has nothing to zero.
///
/// <b>Certificate + chain are public.</b> <see cref="CertificateDer"/> (the leaf)
/// and <see cref="IssuerCertificateDer"/> (the signing intermediate) are presented
/// on the wire in the TLS handshake — not secret. The workload pins the root
/// trust anchor via the CA-certificate fetch surface, never from issuance.
///
/// <b><c>ToString</c> override.</b> A <c>byte[]</c> field would otherwise dump
/// verbosely in any interpolation / log; <see cref="ToString"/> emits byte counts
/// for the certificate material.
/// </remarks>
public sealed class IssuedWorkloadCertificate
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IssuedWorkloadCertificate"/> class.
    /// </summary>
    /// <param name="workload">The workload identity carried in the leaf's SAN.</param>
    /// <param name="certificateDer">DER-encoded leaf certificate bytes. Must be non-empty.</param>
    /// <param name="issuerCertificateDer">
    /// DER-encoded issuing-intermediate certificate so the workload can present the
    /// full chain. Must be non-empty.
    /// </param>
    /// <param name="notBefore">The leaf's not-before instant.</param>
    /// <param name="notAfter">The leaf's not-after instant (drives refresh-ahead).</param>
    /// <exception cref="ArgumentException">
    /// Either of <paramref name="certificateDer"/> or
    /// <paramref name="issuerCertificateDer"/> is empty.
    /// </exception>
    public IssuedWorkloadCertificate(
        WorkloadIdentity workload,
        byte[] certificateDer,
        byte[] issuerCertificateDer,
        Instant notBefore,
        Instant notAfter)
    {
        // §5.1a: BCL null-guard for the non-collection ref arg; the two byte[] args
        // DO carry a present-but-falsey (empty) concept, so ThrowIfFalsey covers
        // null + empty in one call (BCL-split exceptions), mirroring CsrVerification.
        ArgumentNullException.ThrowIfNull(workload);
        certificateDer.ThrowIfFalsey();
        issuerCertificateDer.ThrowIfFalsey();

        Workload = workload;
        CertificateDer = certificateDer;
        IssuerCertificateDer = issuerCertificateDer;
        NotBefore = notBefore;
        NotAfter = notAfter;
    }

    /// <summary>Gets the workload identity carried in the leaf's SAN.</summary>
    public WorkloadIdentity Workload { get; }

    /// <summary>Gets the DER-encoded leaf certificate bytes. Public — presented on the wire.</summary>
    public byte[] CertificateDer { get; }

    /// <summary>
    /// Gets the DER-encoded issuing-intermediate certificate so the workload can
    /// present the full chain. Public.
    /// </summary>
    public byte[] IssuerCertificateDer { get; }

    /// <summary>
    /// Gets the UTC instant before which the leaf is not valid.
    /// </summary>
    /// <remarks>
    /// Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp;
    /// no wall-clock context to preserve.
    /// </remarks>
    public Instant NotBefore { get; }

    /// <summary>
    /// Gets the UTC instant after which the leaf is no longer valid. Drives the
    /// refresh-ahead reissue condition (<c>NotAfter - now &lt;= leadTime</c>).
    /// </summary>
    /// <remarks>
    /// Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp;
    /// no wall-clock context to preserve.
    /// </remarks>
    public Instant NotAfter { get; }

    /// <inheritdoc/>
    public override string ToString()
    {
        var certLen = CertificateDer.Length;
        var issuerLen = IssuerCertificateDer.Length;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"IssuedWorkloadCertificate {{ Workload = {Workload.ServiceId}, "
            + $"CertificateDer = [{certLen} bytes], "
            + $"IssuerCertificateDer = [{issuerLen} bytes], "
            + $"NotBefore = {NotBefore}, NotAfter = {NotAfter} }}");
    }
}
