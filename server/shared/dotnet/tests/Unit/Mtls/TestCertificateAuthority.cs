// -----------------------------------------------------------------------
// <copyright file="TestCertificateAuthority.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Mtls;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Self-contained ECDSA-P256 certificate-authority fixture for the mTLS unit
/// tests. Mints a root + issuing intermediate and signs SPIFFE-SAN workload leaves
/// (and adversarial variants) using BCL <see cref="CertificateRequest"/> only — no
/// dependency on any service domain. Mirrors the shipped CA-generation /
/// leaf-issuance rules' shape (P-256, SHA-256, URI SAN, DigitalSignature leaf key
/// usage, intermediate-signed) so the tests exercise the real validator against
/// real certificates.
/// </summary>
internal sealed class TestCertificateAuthority : IDisposable
{
    private const string _CLIENT_AUTH_OID = "1.3.6.1.5.5.7.3.2";
    private const string _SERVER_AUTH_OID = "1.3.6.1.5.5.7.3.1";

    private static readonly ECCurve sr_curve = ECCurve.NamedCurves.nistP256;
    private static readonly HashAlgorithmName sr_hash = HashAlgorithmName.SHA256;

    private readonly ECDsa r_rootKey;
    private readonly ECDsa r_intermediateKey;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestCertificateAuthority"/>
    /// class — generates a self-signed root and a root-signed issuing intermediate.
    /// </summary>
    /// <param name="trustDomain">The SPIFFE trust domain leaves are minted under (default <c>d2.internal</c>).</param>
    public TestCertificateAuthority(string trustDomain = "d2.internal")
    {
        TrustDomain = trustDomain;

        r_rootKey = ECDsa.Create(sr_curve);
        var rootRequest = new CertificateRequest("CN=Test Root CA", r_rootKey, sr_hash);

        rootRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, true, 1, true));
        rootRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        rootRequest.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(rootRequest.PublicKey, false));

        var now = DateTimeOffset.UtcNow;
        RootCertificate = rootRequest.CreateSelfSigned(now.AddMinutes(-5), now.AddYears(10));

        r_intermediateKey = ECDsa.Create(sr_curve);
        IntermediateCertificate = SignIntermediate(
            "CN=Test Issuing CA", r_intermediateKey, RootCertificate, r_rootKey, now);
    }

    /// <summary>Gets the SPIFFE trust domain leaves are minted under.</summary>
    public string TrustDomain { get; }

    /// <summary>Gets the self-signed root certificate (the trust anchor).</summary>
    public X509Certificate2 RootCertificate { get; }

    /// <summary>Gets the issuing intermediate certificate.</summary>
    public X509Certificate2 IntermediateCertificate { get; }

    /// <summary>
    /// Gets the public root as a single-element trust-anchor collection — the shape
    /// <c>D2MutualTlsOptions.TrustAnchorsProvider</c> returns.
    /// </summary>
    /// <returns>A collection containing the public root certificate.</returns>
    public X509Certificate2Collection TrustAnchors() =>
        [X509CertificateLoader.LoadCertificate(RootCertificate.RawData)];

    /// <summary>
    /// Builds the <see cref="X509Chain"/> a peer would present alongside its leaf —
    /// the issuing intermediate seeded into the extra store, the shape Kestrel's
    /// <c>ClientCertificateValidation</c> callback receives. The validator pulls the
    /// intermediate from this chain so its root-anchored rebuild can complete.
    /// </summary>
    /// <param name="leaf">The presented leaf.</param>
    /// <returns>A chain whose elements include the issuing intermediate.</returns>
    public X509Chain PresentedChain(X509Certificate2 leaf)
    {
        var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(RootCertificate);
        chain.ChainPolicy.ExtraStore.Add(IntermediateCertificate);
        chain.Build(leaf);

        return chain;
    }

    /// <summary>
    /// Issues a valid workload leaf carrying a SPIFFE URI SAN
    /// (<c>spiffe://{TrustDomain}/workload/{serviceId}</c>), signed by the intermediate.
    /// </summary>
    /// <param name="serviceId">The workload service id placed in the SAN.</param>
    /// <returns>The issued leaf (with its private key attached).</returns>
    public X509Certificate2 IssueLeaf(string serviceId)
    {
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddUri(new Uri($"spiffe://{TrustDomain}/workload/{serviceId}"));

        return IssueLeafCore(serviceId, sanBuilder, isCa: false);
    }

    /// <summary>
    /// Issues a valid workload leaf as RAW material (DER + PKCS#8 + issuer DER) —
    /// the DER/PKCS#8 shape an <c>IWorkloadCertificateIssuer</c> returns. The
    /// cache-relevant not-after is supplied by the caller (the fake issuer derives
    /// it from its injected clock for deterministic refresh-ahead tests).
    /// </summary>
    /// <param name="serviceId">The workload service id placed in the SAN.</param>
    /// <param name="validity">How long the X509 leaf's own validity window is (default 24h).</param>
    /// <returns>The DER leaf, raw PKCS#8 private key, and issuer DER.</returns>
    public (byte[] CertDer, byte[] Pkcs8, byte[] IssuerDer) IssueLeafMaterial(
        string serviceId, TimeSpan? validity = null)
    {
        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter = DateTimeOffset.UtcNow.Add(validity ?? TimeSpan.FromHours(24));

        using var leafKey = ECDsa.Create(sr_curve);
        var pkcs8 = leafKey.ExportPkcs8PrivateKey();

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddUri(new Uri($"spiffe://{TrustDomain}/workload/{serviceId}"));

        using var leaf = BuildLeafFromKey(
            serviceId, leafKey, sanBuilder, isCa: false, (notBefore, notAfter));

        return (leaf.RawData, pkcs8, IntermediateCertificate.RawData);
    }

    /// <summary>
    /// Issues a leaf with NO subject-alternative-name at all (a plain CN-only cert).
    /// </summary>
    /// <param name="serviceId">The CN service id.</param>
    /// <returns>The issued CN-only leaf.</returns>
    public X509Certificate2 IssueLeafWithoutSan(string serviceId) =>
        IssueLeafCore(serviceId, sanBuilder: null, isCa: false);

    /// <summary>
    /// Issues a leaf whose only SAN is a DNS name (no URI SAN).
    /// </summary>
    /// <param name="serviceId">The CN service id.</param>
    /// <param name="dnsName">The DNS SAN.</param>
    /// <returns>The issued DNS-only leaf.</returns>
    public X509Certificate2 IssueLeafWithDnsSan(string serviceId, string dnsName)
    {
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(dnsName);

        return IssueLeafCore(serviceId, sanBuilder, isCa: false);
    }

    /// <summary>
    /// Issues a leaf carrying a raw URI SAN verbatim (for adversarial SAN values —
    /// foreign trust domain, wrong scheme, etc.).
    /// </summary>
    /// <param name="serviceId">The CN service id.</param>
    /// <param name="rawUri">The raw URI SAN to embed.</param>
    /// <returns>The issued leaf.</returns>
    public X509Certificate2 IssueLeafWithRawUriSan(string serviceId, string rawUri)
    {
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddUri(new Uri(rawUri));

        return IssueLeafCore(serviceId, sanBuilder, isCa: false);
    }

    /// <summary>
    /// Issues a leaf carrying TWO URI SANs (an unexpected shape the validator rejects).
    /// </summary>
    /// <param name="serviceIdA">The first workload service id.</param>
    /// <param name="serviceIdB">The second workload service id.</param>
    /// <returns>The issued multi-URI-SAN leaf.</returns>
    public X509Certificate2 IssueLeafWithTwoUriSans(string serviceIdA, string serviceIdB)
    {
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddUri(new Uri($"spiffe://{TrustDomain}/workload/{serviceIdA}"));
        sanBuilder.AddUri(new Uri($"spiffe://{TrustDomain}/workload/{serviceIdB}"));

        return IssueLeafCore(serviceIdA, sanBuilder, isCa: false);
    }

    /// <summary>
    /// Issues an EXPIRED leaf (back-dated validity window) carrying a valid SPIFFE SAN.
    /// </summary>
    /// <param name="serviceId">The workload service id.</param>
    /// <returns>The expired leaf.</returns>
    public X509Certificate2 IssueExpiredLeaf(string serviceId)
    {
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddUri(new Uri($"spiffe://{TrustDomain}/workload/{serviceId}"));

        var expiredWindow = (NotBefore: DateTimeOffset.UtcNow.AddDays(-10),
            NotAfter: DateTimeOffset.UtcNow.AddDays(-1));

        return IssueLeafCore(serviceId, sanBuilder, isCa: false, window: expiredWindow);
    }

    /// <summary>
    /// Issues a CA certificate (BasicConstraints CA=true) carrying a SPIFFE SAN —
    /// presented as a "leaf" for the defense-in-depth not-a-leaf assertion.
    /// </summary>
    /// <param name="serviceId">The workload service id.</param>
    /// <returns>The CA-flagged certificate.</returns>
    public X509Certificate2 IssueCaAsLeaf(string serviceId)
    {
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddUri(new Uri($"spiffe://{TrustDomain}/workload/{serviceId}"));

        return IssueLeafCore(serviceId, sanBuilder, isCa: true);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        RootCertificate.Dispose();
        IntermediateCertificate.Dispose();
        r_rootKey.Dispose();
        r_intermediateKey.Dispose();
    }

    private static X509Certificate2 SignIntermediate(
        string subject,
        ECDsa intermediateKey,
        X509Certificate2 rootCert,
        ECDsa rootKey,
        DateTimeOffset now)
    {
        var request = new CertificateRequest(subject, intermediateKey, sr_hash);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, true, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(rootCert, true, false));

        var generator = X509SignatureGenerator.CreateForECDsa(rootKey);

        return request.Create(
            rootCert.SubjectName,
            generator,
            now.AddMinutes(-5),
            now.AddYears(1),
            RandomNumberGenerator.GetBytes(16));
    }

    private X509Certificate2 IssueLeafCore(
        string serviceId,
        SubjectAlternativeNameBuilder? sanBuilder,
        bool isCa,
        (DateTimeOffset NotBefore, DateTimeOffset NotAfter)? window = null)
    {
        using var leafKey = ECDsa.Create(sr_curve);

        var effectiveWindow = window
            ?? (DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(24));

        using var leaf = BuildLeafFromKey(serviceId, leafKey, sanBuilder, isCa, effectiveWindow);

        // Reattach the private key so callers can present a working client cert.
        return leaf.CopyWithPrivateKey(leafKey);
    }

    private X509Certificate2 BuildLeafFromKey(
        string serviceId,
        ECDsa leafKey,
        SubjectAlternativeNameBuilder? sanBuilder,
        bool isCa,
        (DateTimeOffset NotBefore, DateTimeOffset NotAfter) window)
    {
        var request = new CertificateRequest($"CN={serviceId}", leafKey, sr_hash);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(isCa, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                isCa
                    ? X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign
                    : X509KeyUsageFlags.DigitalSignature,
                true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                [new Oid(_CLIENT_AUTH_OID), new Oid(_SERVER_AUTH_OID)], false));

        if (sanBuilder is not null)
            request.CertificateExtensions.Add(sanBuilder.Build(critical: false));

        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(
                IntermediateCertificate, true, false));

        var generator = X509SignatureGenerator.CreateForECDsa(r_intermediateKey);

        return request.Create(
            IntermediateCertificate.SubjectName,
            generator,
            window.NotBefore,
            window.NotAfter,
            RandomNumberGenerator.GetBytes(16));
    }
}
