// -----------------------------------------------------------------------
// <copyright file="WorkloadCertificateIssuance.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.Rules;

using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Pure rule that issues a short-lived workload leaf certificate signed by the
/// issuing intermediate certificate authority.
/// </summary>
/// <remarks>
/// <para>
/// BCL crypto only (<see cref="CertificateRequest"/> + <see cref="ECDsa"/> +
/// <see cref="SubjectAlternativeNameBuilder"/>), no IO / DI / Options. The leaf
/// uses the same fixed algorithm as the CA (ECDSA P-256, SHA-256). The validity
/// window is a caller-supplied <see cref="Duration"/> tunable.
/// </para>
/// <para>
/// <b>Leaf shape.</b> The leaf carries its workload identity as a URI SAN
/// (<c>spiffe://d2.internal/workload/&lt;service&gt;</c>), is NOT a CA
/// (basic-constraints <c>certificateAuthority=false</c>), carries
/// <c>DigitalSignature</c> key usage only (ECDSA P-256 uses ephemeral ECDH for
/// TLS 1.3 key-exchange — <c>KeyEncipherment</c> is an RSA concept and does not
/// apply), and both client-auth and server-auth extended key usage (a workload is
/// both a gRPC client and a gRPC server in the mTLS mesh). The private key is
/// returned to the caller (the workload), NOT custodied by KeyCustodian.
/// </para>
/// <para>
/// Every issuance is wrapped so a crypto build/sign exception becomes a
/// <c>KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST</c> failure rather than a throw —
/// the rule never throws, mirroring the smoke-tester discipline.
/// </para>
/// </remarks>
public static class WorkloadCertificateIssuance
{
    // Extended-key-usage OIDs: a workload is both a TLS client and a TLS server.
    private const string _CLIENT_AUTH_OID = "1.3.6.1.5.5.7.3.2";
    private const string _SERVER_AUTH_OID = "1.3.6.1.5.5.7.3.1";

    private static readonly ECCurve sr_curve = ECCurve.NamedCurves.nistP256;
    private static readonly HashAlgorithmName sr_hash = HashAlgorithmName.SHA256;

    /// <summary>
    /// Issues a workload leaf certificate signed by the issuing intermediate.
    /// </summary>
    /// <param name="workload">The validated workload identity carried in the SAN.</param>
    /// <param name="issuerIntermediateCertificate">The intermediate certificate that signs the leaf.</param>
    /// <param name="issuerIntermediateKey">The intermediate's private key used to sign.</param>
    /// <param name="validity">How long the leaf is valid for (strictly positive).</param>
    /// <param name="clock">The current-time source.</param>
    /// <returns>
    /// <c>Ok(<see cref="IssuedWorkloadCertificate"/>)</c> carrying the leaf cert
    /// DER, the raw PKCS#8 leaf private key, the issuer cert DER, and the validity
    /// window on success; a flagged <c>KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST</c>
    /// failure when an argument is invalid or a crypto build/sign operation fails.
    /// </returns>
    public static D2Result<IssuedWorkloadCertificate> IssueLeaf(
        WorkloadIdentity? workload,
        X509Certificate2? issuerIntermediateCertificate,
        ECDsa? issuerIntermediateKey,
        Duration validity,
        IClock? clock)
    {
        if (workload is null
            || issuerIntermediateCertificate is null
            || issuerIntermediateKey is null
            || validity <= Duration.Zero
            || clock is null)
            return KeyCustodianFailures<IssuedWorkloadCertificate>.InvalidCertificateRequest();

        try
        {
            using var ecdsa = ECDsa.Create(sr_curve);

            var request = new CertificateRequest(
                CaCertificateGeneration.BuildDistinguishedName(workload.ServiceId), ecdsa, sr_hash);

            // A leaf must NOT be a CA.
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(
                    certificateAuthority: false,
                    hasPathLengthConstraint: false,
                    pathLengthConstraint: 0,
                    critical: true));

            // ECDSA P-256 leaves use DigitalSignature only. KeyEncipherment is an
            // RSA key-transport concept and does not apply to ECDSA under TLS 1.3.
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature,
                    critical: true));

            // A workload is both a gRPC client AND a gRPC server in the mesh.
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    [new Oid(_CLIENT_AUTH_OID), new Oid(_SERVER_AUTH_OID)],
                    critical: false));

            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddUri(new Uri(workload.Uri));

            // SPIFFE SVID spec §4 requires URI SAN non-critical when CN is present.
            request.CertificateExtensions.Add(sanBuilder.Build(critical: false));

            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

            request.CertificateExtensions.Add(
                X509AuthorityKeyIdentifierExtension.CreateFromCertificate(
                    issuerIntermediateCertificate,
                    includeKeyIdentifier: true,
                    includeIssuerAndSerial: false));

            var (notBefore, notAfter) = CaCertificateGeneration.Window(clock, validity);
            var serialNumber = CaCertificateGeneration.NewSerialNumber();

            // Sign with the intermediate's private key via a signature generator + the
            // intermediate's subject DN — works from a DER-only issuer cert plus its
            // separate key.
            var generator = X509SignatureGenerator.CreateForECDsa(issuerIntermediateKey);

            using var leaf = request.Create(
                issuerIntermediateCertificate.SubjectName,
                generator,
                notBefore,
                notAfter,
                serialNumber);

            return D2Result<IssuedWorkloadCertificate>.Ok(
                new IssuedWorkloadCertificate(
                    workload: workload,
                    certificateDer: leaf.RawData,
                    privateKeyPkcs8: ecdsa.ExportPkcs8PrivateKey(),
                    issuerCertificateDer: issuerIntermediateCertificate.RawData,
                    notBefore: Instant.FromDateTimeOffset(notBefore),
                    notAfter: Instant.FromDateTimeOffset(notAfter)));
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            return KeyCustodianFailures<IssuedWorkloadCertificate>.InvalidCertificateRequest();
        }
    }
}
