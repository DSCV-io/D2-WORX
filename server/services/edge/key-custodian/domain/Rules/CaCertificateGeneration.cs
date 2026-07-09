// -----------------------------------------------------------------------
// <copyright file="CaCertificateGeneration.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.Rules;

/// <summary>
/// Pure rule that generates the internal certificate-authority hierarchy: a
/// self-signed ECDSA P-256 root and an intermediate signed by that root.
/// </summary>
/// <remarks>
/// <para>
/// BCL crypto only (<see cref="CertificateRequest"/> + <see cref="ECDsa"/>), no
/// IO / DI / Options. The algorithm is fixed (ECDSA P-256, SHA-256) — an
/// internal PKI with no external relying party, so the curve is a constant, not a
/// caller tunable. The validity windows ARE tunables the caller supplies as
/// <see cref="Duration"/> parameters, the same way KeyCustodian already treats
/// key sizes.
/// </para>
/// <para>
/// Every generation is wrapped so a malformed-input crypto exception becomes a
/// <c>KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST</c> failure rather than a throw —
/// the rule never throws, mirroring the smoke-tester discipline.
/// </para>
/// <para>
/// <b>Basic constraints.</b> The root is a CA with a path-length of 1 (it may
/// sign one intermediate tier); the intermediate is a CA with a path-length of 0
/// (it signs only leaves). Both carry <c>KeyCertSign | CrlSign</c> key usage.
/// </para>
/// </remarks>
public static class CaCertificateGeneration
{
    /// <summary>
    /// The common name of the internal root certificate authority. A fixed label
    /// for an internal PKI with no external relying party.
    /// </summary>
    public const string ROOT_CA_SUBJECT = "D2 Internal Root CA";

    /// <summary>
    /// The common name of the internal issuing-intermediate certificate authority.
    /// </summary>
    public const string INTERMEDIATE_CA_SUBJECT = "D2 Internal Issuing CA";

    private static readonly ECCurve sr_curve = ECCurve.NamedCurves.nistP256;
    private static readonly HashAlgorithmName sr_hash = HashAlgorithmName.SHA256;

    // Front-backdate notBefore by this allowance so a relying peer with a lagging
    // clock does not reject a just-issued certificate (standard mTLS-PKI practice —
    // cert-manager / step-ca / Vault all backdate ~5 min). Front-only: notAfter stays
    // now + validity, so forward validity is never shortened.
    private static readonly Duration sr_clockSkewBackdate = Duration.FromMinutes(5);

    /// <summary>
    /// Generates a self-signed root certificate authority.
    /// </summary>
    /// <param name="subjectName">
    /// The certificate common name (e.g. <c>D2 Internal Root CA</c>). Non-empty.
    /// </param>
    /// <param name="validity">How long the root is valid for (strictly positive).</param>
    /// <param name="clock">The current-time source.</param>
    /// <returns>
    /// <c>Ok(<see cref="GeneratedCaMaterial"/>)</c> carrying the cert DER + the raw
    /// PKCS#8 private key on success; a flagged
    /// <c>KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST</c> failure when the subject is
    /// empty, the validity is non-positive, or a crypto build operation fails.
    /// </returns>
    public static D2Result<GeneratedCaMaterial> GenerateRootCa(
        string? subjectName, Duration validity, IClock? clock)
    {
        if (subjectName.Falsey() || validity <= Duration.Zero || clock is null)
            return KeyCustodianFailures<GeneratedCaMaterial>.InvalidCertificateRequest();

        try
        {
            using var ecdsa = ECDsa.Create(sr_curve);

            var request = new CertificateRequest(
                BuildDistinguishedName(subjectName!), ecdsa, sr_hash);

            // Root CA: may sign one intermediate tier (pathLength 1).
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(
                    certificateAuthority: true,
                    hasPathLengthConstraint: true,
                    pathLengthConstraint: 1,
                    critical: true));

            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                    critical: true));

            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

            var (notBefore, notAfter) = Window(clock, validity);
            using var certificate = request.CreateSelfSigned(notBefore, notAfter);

            return D2Result<GeneratedCaMaterial>.Ok(
                new GeneratedCaMaterial(
                    privateKeyPkcs8: ecdsa.ExportPkcs8PrivateKey(),
                    certificateDer: certificate.RawData));
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            // Malformed input (bad subject DN, unusable validity window) — a
            // precondition failure surfaced as a flagged result, never a throw.
            return KeyCustodianFailures<GeneratedCaMaterial>.InvalidCertificateRequest();
        }
    }

    /// <summary>
    /// Generates an intermediate certificate authority signed by the supplied root.
    /// </summary>
    /// <param name="subjectName">
    /// The intermediate certificate common name (e.g. <c>D2 Internal Issuing CA</c>). Non-empty.
    /// </param>
    /// <param name="issuerRootCertificate">The root certificate that signs this intermediate.</param>
    /// <param name="issuerRootKey">The root's private key used to sign.</param>
    /// <param name="validity">How long the intermediate is valid for (strictly positive).</param>
    /// <param name="clock">The current-time source.</param>
    /// <returns>
    /// <c>Ok(<see cref="GeneratedCaMaterial"/>)</c> carrying the intermediate cert
    /// DER + its fresh PKCS#8 private key on success; a flagged
    /// <c>KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST</c> failure when an argument is
    /// invalid or a crypto build/sign operation fails (e.g. the supplied root is
    /// not a CA, or its validity window cannot cover the intermediate).
    /// </returns>
    public static D2Result<GeneratedCaMaterial> GenerateIntermediateCa(
        string? subjectName,
        X509Certificate2? issuerRootCertificate,
        ECDsa? issuerRootKey,
        Duration validity,
        IClock? clock)
    {
        if (subjectName.Falsey()
            || issuerRootCertificate is null
            || issuerRootKey is null
            || validity <= Duration.Zero
            || clock is null)
            return KeyCustodianFailures<GeneratedCaMaterial>.InvalidCertificateRequest();

        try
        {
            using var ecdsa = ECDsa.Create(sr_curve);

            var request = new CertificateRequest(
                BuildDistinguishedName(subjectName!), ecdsa, sr_hash);

            // Intermediate CA: signs only leaves (pathLength 0).
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(
                    certificateAuthority: true,
                    hasPathLengthConstraint: true,
                    pathLengthConstraint: 0,
                    critical: true));

            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                    critical: true));

            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

            request.CertificateExtensions.Add(
                X509AuthorityKeyIdentifierExtension.CreateFromCertificate(
                    issuerRootCertificate,
                    includeKeyIdentifier: true,
                    includeIssuerAndSerial: false));

            var (notBefore, notAfter) = Window(clock, validity);
            var serialNumber = NewSerialNumber();

            // Sign with the root's private key via a signature generator + the root's
            // subject DN — works from a DER-only issuer cert plus its separate key,
            // no need to attach the key to the issuer X509Certificate2.
            var generator = X509SignatureGenerator.CreateForECDsa(issuerRootKey);

            using var certificate = request.Create(
                issuerRootCertificate.SubjectName, generator, notBefore, notAfter, serialNumber);

            return D2Result<GeneratedCaMaterial>.Ok(
                new GeneratedCaMaterial(
                    privateKeyPkcs8: ecdsa.ExportPkcs8PrivateKey(),
                    certificateDer: certificate.RawData));
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            return KeyCustodianFailures<GeneratedCaMaterial>.InvalidCertificateRequest();
        }
    }

    /// <summary>
    /// Builds an X.500 distinguished name from a common-name string. Shared by the
    /// CA + leaf-issuance rules so the CN escaping is identical.
    /// </summary>
    /// <param name="commonName">The common name (already non-empty).</param>
    /// <returns>The distinguished name.</returns>
    internal static X500DistinguishedName BuildDistinguishedName(string commonName)
    {
        var builder = new X500DistinguishedNameBuilder();
        builder.AddCommonName(commonName);
        return builder.Build();
    }

    /// <summary>
    /// Computes the <c>(notBefore, notAfter)</c> certificate validity window from
    /// the current instant + a duration. Shared by the CA + leaf-issuance rules.
    /// </summary>
    /// <remarks>
    /// <b>Clock-skew backdating.</b> <c>notBefore</c> is set to
    /// <c>now - <see cref="sr_clockSkewBackdate"/></c> (a small fixed allowance) so a
    /// relying peer with a slightly-lagging clock does not reject a just-issued
    /// certificate. The backdate is FRONT-ONLY: <c>notAfter</c> stays
    /// <c>now + validity</c>, so the caller-requested forward validity is never
    /// shortened — the cushion is added ahead of, not subtracted from, the window.
    /// </remarks>
    /// <param name="clock">The current-time source.</param>
    /// <param name="validity">The validity duration (strictly positive).</param>
    /// <returns>The not-before / not-after instants as <see cref="DateTimeOffset"/>.</returns>
    internal static (DateTimeOffset NotBefore, DateTimeOffset NotAfter) Window(
        IClock clock, Duration validity)
    {
        var now = clock.GetCurrentInstant();
        var notBefore = now - sr_clockSkewBackdate;
        var notAfter = now + validity;
        return (notBefore.ToDateTimeOffset(), notAfter.ToDateTimeOffset());
    }

    /// <summary>
    /// Generates a fresh, unpredictable big-endian serial number for a
    /// CA-signed certificate. Shared by the CA + leaf-issuance rules.
    /// </summary>
    /// <returns>A 16-byte random serial number.</returns>
    internal static byte[] NewSerialNumber() => RandomNumberGenerator.GetBytes(16);
}
