// -----------------------------------------------------------------------
// <copyright file="RealCertAuthority.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpc;

using System.Security.Cryptography.X509Certificates;
using D2.Edge.KeyCustodian.Domain.Rules;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
using WorkloadIdentity = D2.Edge.KeyCustodian.Domain.ValueObjects.WorkloadIdentity;

/// <summary>
/// Certificate-authority fixture for the end-to-end mutual-TLS harness that mints
/// every certificate through KeyCustodian's PRODUCTION certificate-generation rules
/// (<see cref="CaCertificateGeneration"/> + <see cref="WorkloadCertificateIssuance"/>)
/// rather than a hand-rolled copy. Proving the shared mTLS checker accepts (and
/// rejects) genuinely KeyCustodian-issued certificates over a real socket is the
/// whole point of housing the proof here — the unit-test
/// <c>TestCertificateAuthority</c> exercises the validator against a hand-rolled
/// copy, which proves only that the checker accepts copy-certs.
/// </summary>
/// <remarks>
/// <para>
/// Generates a real ECDSA P-256 root + intermediate via
/// <see cref="CaCertificateGeneration"/>, and signs leaves via
/// <see cref="WorkloadCertificateIssuance"/>. The intermediate's private key is
/// retained so leaves can be issued and the single hand-rolled adversarial case (a
/// foreign-trust-domain SAN the production rule physically cannot emit) can be signed
/// by the real intermediate.
/// </para>
/// <para>
/// <b>Schannel ephemeral-key workaround.</b> Every presentable certificate is
/// re-imported through a PKCS#12 round-trip so its private key is not a bare
/// ephemeral key — on Windows, Schannel's TLS handshake can fail with
/// <c>0x8009030E</c> ("No credentials are available in the security package") for an
/// ephemeral-key certificate. The round-trip yields a perishable-key certificate the
/// handshake accepts, without changing the certificate's bytes / identity.
/// </para>
/// </remarks>
internal sealed class RealCertAuthority : IDisposable
{
    // Anchor 5 minutes in the past: the production Window rule derives (now, now +
    // validity) with NO backdate, so anchoring the clock slightly in the past keeps
    // every not-before safely before the handshake instant (no clock-skew flake).
    private readonly Instant r_now =
        NodaTime.SystemClock.Instance.GetCurrentInstant() - Duration.FromMinutes(5);

    private readonly ECDsa r_intermediateKey;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RealCertAuthority"/> class —
    /// generates a self-signed root and a root-signed issuing intermediate via the
    /// production certificate-generation rule. All windows are anchored at real wall
    /// time so the CA + valid leaves are live for the duration of the handshake (the
    /// TLS chain build validates against the real clock).
    /// </summary>
    public RealCertAuthority()
    {
        var clock = new TestClock(r_now);

        var rootResult = CaCertificateGeneration.GenerateRootCa(
            CaCertificateGeneration.ROOT_CA_SUBJECT, Duration.FromDays(3650), clock);

        var root = rootResult.Data!;

        using var rootKey = ECDsa.Create();
        rootKey.ImportPkcs8PrivateKey(root.PrivateKeyPkcs8, out _);

        RootCertificate = X509CertificateLoader.LoadCertificate(root.CertificateDer);

        var intermediateResult = CaCertificateGeneration.GenerateIntermediateCa(
            CaCertificateGeneration.INTERMEDIATE_CA_SUBJECT,
            RootCertificate,
            rootKey,
            Duration.FromDays(365),
            clock);

        var intermediate = intermediateResult.Data!;

        r_intermediateKey = ECDsa.Create();
        r_intermediateKey.ImportPkcs8PrivateKey(intermediate.PrivateKeyPkcs8, out _);

        IntermediateCertificate = X509CertificateLoader.LoadCertificate(intermediate.CertificateDer);

        root.Zero();
        intermediate.Zero();
    }

    /// <summary>Gets the self-signed root certificate (the trust anchor).</summary>
    public X509Certificate2 RootCertificate { get; }

    /// <summary>Gets the issuing intermediate certificate (presented alongside leaves).</summary>
    public X509Certificate2 IntermediateCertificate { get; }

    /// <summary>
    /// Returns the public root as a single-element trust-anchor collection — the shape
    /// <c>D2MutualTlsOptions.TrustAnchorsProvider</c> returns. Public root only, never
    /// a private key.
    /// </summary>
    /// <returns>A collection containing the public root certificate.</returns>
    public X509Certificate2Collection TrustAnchors() =>
        [X509CertificateLoader.LoadCertificate(RootCertificate.RawData)];

    /// <summary>
    /// Issues a valid, private-key-bearing workload leaf for <paramref name="serviceId"/>
    /// via the production issuance rule — a real <c>spiffe://d2.internal/workload/&lt;id&gt;</c>
    /// SAN, client + server EKU, intermediate-signed, valid window.
    /// </summary>
    /// <param name="serviceId">The workload service id placed in the SAN.</param>
    /// <returns>The live leaf (with its private key, Schannel-compatible).</returns>
    public X509Certificate2 IssueLeaf(string serviceId) =>
        IssueLeafForWindow(serviceId, r_now);

    /// <summary>
    /// Issues a SELF-SIGNED certificate usable as the Kestrel HTTPS server certificate
    /// for the loopback harness.
    /// </summary>
    /// <remarks>
    /// Windows-test accommodation (does NOT weaken the validation under test). The
    /// server certificate is deliberately self-signed — its own trivial one-element
    /// chain — rather than a CA-chained workload leaf. Kestrel's HTTPS middleware
    /// builds an <c>SslStreamCertificateContext</c> from the server certificate at
    /// startup; on Windows-Schannel that build throws "an unknown chain building
    /// error occurred" for a leaf whose internal-CA root is not installed in the OS
    /// trust store (which this harness deliberately never installs), so the host
    /// cannot start. A self-signed server certificate chains to itself, so the
    /// context builds on a clean Windows box without any cert-store mutation. The
    /// client trusts this loopback server certificate via
    /// <c>RemoteCertificateValidationCallback => true</c>, so the server's identity
    /// is irrelevant to the proof — what is under test is the SERVER's mutual-TLS
    /// validation of the CLIENT certificate, which is entirely unaffected by the
    /// server certificate's issuer.
    /// </remarks>
    /// <param name="serviceId">The server certificate common-name service id.</param>
    /// <returns>The live self-signed server certificate (with its private key, Schannel-compatible).</returns>
    public X509Certificate2 IssueServerCertificate(string serviceId)
    {
        using var serverKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var request = new CertificateRequest(
            $"CN={serviceId}", serverKey, HashAlgorithmName.SHA256);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: true));

        // Server-auth EKU — this certificate is presented by the loopback HTTPS server.
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], critical: false));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
        request.CertificateExtensions.Add(sanBuilder.Build(critical: false));

        // Anchor the validity window at the fixture clock (now - 5m) so the server cert
        // is live for the handshake with the same no-clock-skew margin as the leaves.
        var notBefore = r_now.ToDateTimeOffset();

        using var selfSigned = request.CreateSelfSigned(
            notBefore, notBefore + TimeSpan.FromHours(24));

        return MakeSchannelCompatible(selfSigned);
    }

    /// <summary>
    /// Issues an EXPIRED leaf for <paramref name="serviceId"/> via the production rule
    /// by deriving the validity window from a past clock — the rule computes
    /// <c>(now, now + validity)</c>, so a clock 10 days in the past with a 1-day
    /// validity yields an already-expired, real production-issued leaf (no hand-rolling,
    /// no fake clock threaded through TLS).
    /// </summary>
    /// <param name="serviceId">The workload service id.</param>
    /// <returns>The expired leaf (with its private key, Schannel-compatible).</returns>
    public X509Certificate2 IssueExpiredLeaf(string serviceId) =>
        IssueLeafForWindow(serviceId, r_now - Duration.FromDays(10), Duration.FromDays(1));

    /// <summary>
    /// Issues a valid leaf for <paramref name="serviceId"/> as raw
    /// <see cref="WorkloadLeafMaterial"/> (DER + PKCS#8 + issuer DER + not-after) — the
    /// neutral shape the shipped <c>IWorkloadCertificateIssuer</c> hands back, so the
    /// shipped client builds a live leaf from it. The window is derived from real wall
    /// time so the leaf is non-expired during the test run.
    /// </summary>
    /// <param name="serviceId">The workload service id.</param>
    /// <returns>The raw leaf material.</returns>
    public WorkloadLeafMaterial IssueLeafMaterial(string serviceId)
    {
        // Window anchored at r_now (now-5m) so the leaf is live for the test and the
        // shipped cache (which filters on NotAfter > now) treats it as presentable.
        var clock = new TestClock(r_now);

        var issued = WorkloadCertificateIssuance.IssueLeaf(
            WorkloadIdentity.FromTrusted(serviceId),
            IntermediateCertificate,
            r_intermediateKey,
            Duration.FromHours(24),
            clock).Data!;

        return new WorkloadLeafMaterial(
            CertificateDer: issued.CertificateDer,
            PrivateKeyPkcs8: issued.PrivateKeyPkcs8,
            IssuerCertificateDer: issued.IssuerCertificateDer,
            NotAfter: issued.NotAfter);
    }

    /// <summary>
    /// Issues a leaf whose only URI SAN names a FOREIGN trust domain (e.g.
    /// <c>spiffe://prod.internal/workload/edge</c>), signed by the real intermediate so
    /// it chains to this CA. HAND-ROLLED because the production issuance rule only ever
    /// emits the <c>d2.internal</c> trust domain and physically cannot produce a foreign
    /// one — this isolates the SPIFFE trust-domain conjunct of the peer check.
    /// </summary>
    /// <param name="serviceId">The leaf common-name service id.</param>
    /// <param name="foreignSanUri">The raw foreign-trust-domain SPIFFE URI to embed in the SAN.</param>
    /// <returns>The live leaf carrying the foreign SAN (with its private key, Schannel-compatible).</returns>
    public X509Certificate2 IssueLeafWithForeignTrustDomainSan(string serviceId, string foreignSanUri)
    {
        using var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var request = new CertificateRequest(
            $"CN={serviceId}", leafKey, HashAlgorithmName.SHA256);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));

        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                [new Oid("1.3.6.1.5.5.7.3.2"), new Oid("1.3.6.1.5.5.7.3.1")],
                critical: false));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddUri(new Uri(foreignSanUri));
        request.CertificateExtensions.Add(sanBuilder.Build(critical: false));

        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));
        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(
                IntermediateCertificate, includeKeyIdentifier: true, includeIssuerAndSerial: false));

        var generator = X509SignatureGenerator.CreateForECDsa(r_intermediateKey);
        var nowUtc = DateTimeOffset.UtcNow;

        using var signed = request.Create(
            IntermediateCertificate.SubjectName,
            generator,
            nowUtc.AddMinutes(-5),
            nowUtc.AddHours(24),
            RandomNumberGenerator.GetBytes(16));

        using var withKey = signed.CopyWithPrivateKey(leafKey);

        return MakeSchannelCompatible(withKey);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        RootCertificate.Dispose();
        IntermediateCertificate.Dispose();
        r_intermediateKey.Dispose();
    }

    /// <summary>
    /// Re-imports a private-key-bearing certificate through a PKCS#12 round-trip so its
    /// key is a perishable (non-ephemeral) key — the Windows Schannel TLS handshake can
    /// reject a bare ephemeral key with <c>0x8009030E</c>. The certificate bytes /
    /// identity are unchanged.
    /// </summary>
    /// <param name="certificate">The ephemeral-key certificate.</param>
    /// <returns>A Schannel-compatible copy with a perishable key.</returns>
    private static X509Certificate2 MakeSchannelCompatible(X509Certificate2 certificate)
    {
        var pfx = certificate.Export(X509ContentType.Pkcs12);

        try
        {
            // Reload WITHOUT EphemeralKeySet so the key lands in a (perishable) key
            // container Schannel can use — an ephemeral key is exactly what triggers
            // the 0x8009030E handshake failure. The container is released on Dispose.
            return X509CertificateLoader.LoadPkcs12(
                pfx,
                password: null,
                keyStorageFlags: X509KeyStorageFlags.Exportable);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pfx);
        }
    }

    /// <summary>
    /// Issues a leaf for the given window-start instant + validity via the production
    /// rule, assembles a live private-key-bearing certificate from the returned DER +
    /// PKCS#8, and makes it Schannel-compatible.
    /// </summary>
    /// <param name="serviceId">The workload service id.</param>
    /// <param name="windowStart">The clock instant the rule derives the not-before from.</param>
    /// <param name="validity">The validity duration (default 24h).</param>
    /// <returns>The live leaf.</returns>
    private X509Certificate2 IssueLeafForWindow(
        string serviceId, Instant windowStart, Duration? validity = null)
    {
        var clock = new TestClock(windowStart);

        var issued = WorkloadCertificateIssuance.IssueLeaf(
            WorkloadIdentity.FromTrusted(serviceId),
            IntermediateCertificate,
            r_intermediateKey,
            validity ?? Duration.FromHours(24),
            clock).Data!;

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(issued.PrivateKeyPkcs8, out _);
        issued.Zero();

        using var certOnly = X509CertificateLoader.LoadCertificate(issued.CertificateDer);
        using var withKey = certOnly.CopyWithPrivateKey(ecdsa);

        return MakeSchannelCompatible(withKey);
    }
}
