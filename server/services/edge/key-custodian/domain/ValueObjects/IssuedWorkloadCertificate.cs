// -----------------------------------------------------------------------
// <copyright file="IssuedWorkloadCertificate.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.ValueObjects;

/// <summary>
/// A freshly-issued, on-demand workload leaf certificate handed back to the
/// caller. NOT a managed-key aggregate and never persisted — the only
/// persistence on the leaf path is the lightweight issuance audit entry.
/// </summary>
/// <remarks>
/// <b>Private key custody transfers to the caller.</b> Unlike a managed key
/// (whose private material KeyCustodian custodies root-wrapped at rest), a leaf's
/// private key is held by the <em>workload</em>. <see cref="PrivateKeyPkcs8"/>
/// holds the raw PKCS#8 ECDSA private key; once the caller has installed the leaf
/// it MUST zero these bytes via <see cref="Zero"/>. KeyCustodian never stores them.
///
/// <b>Certificate + chain are public.</b> <see cref="CertificateDer"/> (the leaf)
/// and <see cref="IssuerCertificateDer"/> (the signing intermediate) are presented
/// on the wire in the TLS handshake — not secret. The workload pins the root from
/// its own CA-provider trust anchor.
///
/// <b>No <c>ToString</c> leak.</b> A <c>byte[]</c> field would otherwise dump in
/// any interpolation / log; <see cref="ToString"/> emits a redaction sentinel for
/// the private key and byte counts for the public material.
/// </remarks>
public sealed class IssuedWorkloadCertificate
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IssuedWorkloadCertificate"/> class.
    /// </summary>
    /// <param name="workload">The workload identity carried in the leaf's SAN.</param>
    /// <param name="certificateDer">DER-encoded leaf certificate bytes. Must be non-empty.</param>
    /// <param name="privateKeyPkcs8">
    /// Raw PKCS#8 ECDSA leaf private key bytes. Must be non-empty. The caller zeroes
    /// these after install.
    /// </param>
    /// <param name="issuerCertificateDer">
    /// DER-encoded issuing-intermediate certificate so the workload can present the
    /// full chain. Must be non-empty.
    /// </param>
    /// <param name="notBefore">The leaf's not-before instant.</param>
    /// <param name="notAfter">The leaf's not-after instant (drives refresh-ahead).</param>
    /// <exception cref="ArgumentException">
    /// Any of <paramref name="certificateDer"/>, <paramref name="privateKeyPkcs8"/>, or
    /// <paramref name="issuerCertificateDer"/> is empty.
    /// </exception>
    public IssuedWorkloadCertificate(
        WorkloadIdentity workload,
        byte[] certificateDer,
        byte[] privateKeyPkcs8,
        byte[] issuerCertificateDer,
        Instant notBefore,
        Instant notAfter)
    {
        // §5.1a carve-out: reference-type null-guards — no present-but-falsey concept.
        ArgumentNullException.ThrowIfNull(workload);
        ArgumentNullException.ThrowIfNull(certificateDer);
        ArgumentNullException.ThrowIfNull(privateKeyPkcs8);
        ArgumentNullException.ThrowIfNull(issuerCertificateDer);

        if (certificateDer.Length == 0)
            throw new ArgumentException("Leaf certificate must not be empty.", nameof(certificateDer));

        if (privateKeyPkcs8.Length == 0)
        {
            throw new ArgumentException(
                "Leaf private key must not be empty.", nameof(privateKeyPkcs8));
        }

        if (issuerCertificateDer.Length == 0)
        {
            throw new ArgumentException(
                "Issuer certificate must not be empty.", nameof(issuerCertificateDer));
        }

        Workload = workload;
        CertificateDer = certificateDer;
        PrivateKeyPkcs8 = privateKeyPkcs8;
        IssuerCertificateDer = issuerCertificateDer;
        NotBefore = notBefore;
        NotAfter = notAfter;
    }

    /// <summary>Gets the workload identity carried in the leaf's SAN.</summary>
    public WorkloadIdentity Workload { get; }

    /// <summary>Gets the DER-encoded leaf certificate bytes. Public — presented on the wire.</summary>
    public byte[] CertificateDer { get; }

    /// <summary>
    /// Gets the raw PKCS#8 leaf private key bytes. Secret — the caller zeroes after
    /// install; KeyCustodian never persists them. Never log.
    /// </summary>
    public byte[] PrivateKeyPkcs8 { get; }

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

    /// <summary>
    /// Zeroes the <see cref="PrivateKeyPkcs8"/> buffer. The caller calls this after
    /// installing the leaf.
    /// </summary>
    public void Zero() => CryptographicOperations.ZeroMemory(PrivateKeyPkcs8);

    /// <inheritdoc/>
    public override string ToString()
    {
        var certLen = CertificateDer.Length;
        var privateLen = PrivateKeyPkcs8.Length;
        var issuerLen = IssuerCertificateDer.Length;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"IssuedWorkloadCertificate {{ Workload = {Workload.ServiceId}, "
            + $"CertificateDer = [{certLen} bytes], "
            + $"PrivateKeyPkcs8 = [REDACTED, {privateLen} bytes], "
            + $"IssuerCertificateDer = [{issuerLen} bytes], "
            + $"NotBefore = {NotBefore}, NotAfter = {NotAfter} }}");
    }
}
