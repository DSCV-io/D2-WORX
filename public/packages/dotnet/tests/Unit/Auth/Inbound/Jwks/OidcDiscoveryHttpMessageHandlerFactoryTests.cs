// -----------------------------------------------------------------------
// <copyright file="OidcDiscoveryHttpMessageHandlerFactoryTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Jwks;

using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Jwks;
using Xunit;

/// <summary>
/// Unit tests for private-CA trust on the OIDC discovery HttpClient:
/// custom root trusts intermediate-signed leaves; untrusted roots reject;
/// missing path keeps default system-trust handler; trusted-root handler
/// owns and disposes the loaded public CA certificate.
/// </summary>
/// <remarks>
/// <para>Adversarial matrix (Surface × Category × Test):</para>
/// <list type="table">
/// <listheader><term>Surface</term><term>Category</term><description>Test</description></listheader>
/// <item><term>Create</term><term>null/empty path</term><description>Create_NullPath_*, Create_EmptyPath_*</description></item>
/// <item><term>Create</term><term>missing file</term><description>Create_MissingFile_ThrowsInvalidOperation</description></item>
/// <item><term>Create</term><term>valid root + callback</term><description>Create_ValidRootPath_InstallsCustomValidationCallback</description></item>
/// <item><term>Create</term><term>disposal ownership</term><description>Create_ValidRootPath_DisposeDisposesOwnedTrustedRoot</description></item>
/// <item><term>Validate</term><term>trust accept</term><description>ValidateServerCertificate_LeafChainedToTrustedRoot_Accepts</description></item>
/// <item><term>Validate</term><term>foreign reject</term><description>ValidateServerCertificate_LeafChainedToUntrustedRoot_Rejects</description></item>
/// <item><term>Validate</term><term>null cert / name mismatch</term><description>ValidateServerCertificate_NullCertificate_*, *_NameMismatch_*</description></item>
/// <item><term>LoadPublicRoot</term><term>public material</term><description>LoadPublicRoot_LoadsPemOrDerPublicCert</description></item>
/// </list>
/// </remarks>
public sealed class OidcDiscoveryHttpMessageHandlerFactoryTests : IDisposable
{
    private readonly string r_tempDir;
    private readonly ECDsa r_rootKey;
    private readonly ECDsa r_intermediateKey;
    private readonly ECDsa r_foreignRootKey;
    private readonly X509Certificate2 r_root;
    private readonly X509Certificate2 r_intermediate;
    private readonly X509Certificate2 r_foreignRoot;
    private readonly string r_rootPath;

    public OidcDiscoveryHttpMessageHandlerFactoryTests()
    {
        r_tempDir = Path.Combine(
            Path.GetTempPath(),
            "oidc-trust-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(r_tempDir);

        r_rootKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        r_intermediateKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        r_foreignRootKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var now = DateTimeOffset.UtcNow;
        r_root = CreateSelfSignedCa("CN=D2 Test OIDC Root", r_rootKey, now);
        r_intermediate = SignIntermediate(
            "CN=D2 Test OIDC Intermediate",
            r_intermediateKey,
            r_root,
            now);
        r_foreignRoot = CreateSelfSignedCa("CN=D2 Foreign Root", r_foreignRootKey, now);

        r_rootPath = Path.Combine(r_tempDir, "ca-root.crt");
        File.WriteAllBytes(r_rootPath, r_root.Export(X509ContentType.Cert));
    }

    public void Dispose()
    {
        r_root.Dispose();
        r_intermediate.Dispose();
        r_foreignRoot.Dispose();
        r_rootKey.Dispose();
        r_intermediateKey.Dispose();
        r_foreignRootKey.Dispose();

        if (Directory.Exists(r_tempDir))
            Directory.Delete(r_tempDir, recursive: true);
    }

    [Fact]
    public void Create_NullPath_ReturnsDefaultHttpClientHandler()
    {
        using var handler = OidcDiscoveryHttpMessageHandlerFactory.Create(null);

        handler.Should().BeOfType<HttpClientHandler>();
        var httpHandler = (HttpClientHandler)handler;
        httpHandler.ServerCertificateCustomValidationCallback.Should().BeNull(
            "system trust store only — no custom callback when path is empty");
    }

    [Fact]
    public void Create_EmptyPath_ReturnsDefaultHttpClientHandler()
    {
        using var handler = OidcDiscoveryHttpMessageHandlerFactory.Create("   ");

        handler.Should().BeOfType<HttpClientHandler>();
        ((HttpClientHandler)handler).ServerCertificateCustomValidationCallback
            .Should().BeNull();
    }

    [Fact]
    public void Create_MissingFile_ThrowsInvalidOperation()
    {
        var act = () => OidcDiscoveryHttpMessageHandlerFactory.Create(
            Path.Combine(r_tempDir, "does-not-exist.crt"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TrustedRootCertificatePath*");
    }

    [Fact]
    public void Create_ValidRootPath_InstallsCustomValidationCallback()
    {
        using var handler = OidcDiscoveryHttpMessageHandlerFactory.Create(r_rootPath);

        handler.Should()
            .BeOfType<OidcDiscoveryHttpMessageHandlerFactory.TrustedRootHttpClientHandler>();
        ((HttpClientHandler)handler).ServerCertificateCustomValidationCallback
            .Should().NotBeNull();
    }

    [Fact]
    public void Create_ValidRootPath_DisposeDisposesOwnedTrustedRoot()
    {
        var created = OidcDiscoveryHttpMessageHandlerFactory.Create(r_rootPath);
        var handler =
            (OidcDiscoveryHttpMessageHandlerFactory.TrustedRootHttpClientHandler)created;
        var ownedRoot = handler.TrustedRoot;
        ownedRoot.Thumbprint.Should().Be(r_root.Thumbprint);

        handler.Dispose();

        // Second dispose is idempotent (handler + owned root).
        var actDouble = () => handler.Dispose();
        actDouble.Should().NotThrow();

        // Owned root must be disposed with the handler — native handle released.
        var actUse = () => _ = ownedRoot.Thumbprint;
        actUse.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void ValidateServerCertificate_LeafChainedToTrustedRoot_Accepts()
    {
        using var leaf = IssueServerLeaf("d2-edge", r_intermediate);
        using var presented = BuildPresentedChain(leaf);

        var accepted = OidcDiscoveryHttpMessageHandlerFactory.ValidateServerCertificate(
            leaf,
            presented,
            SslPolicyErrors.RemoteCertificateChainErrors,
            r_root);

        accepted.Should().BeTrue(
            "intermediate-signed leaf must validate under CustomRootTrust + trusted root");
    }

    [Fact]
    public void ValidateServerCertificate_LeafChainedToUntrustedRoot_Rejects()
    {
        using var leaf = IssueServerLeaf("d2-edge", r_intermediate);
        using var presented = BuildPresentedChain(leaf);

        var accepted = OidcDiscoveryHttpMessageHandlerFactory.ValidateServerCertificate(
            leaf,
            presented,
            SslPolicyErrors.RemoteCertificateChainErrors,
            r_foreignRoot);

        accepted.Should().BeFalse("foreign root must not validate D2-issued leaves");
    }

    [Fact]
    public void ValidateServerCertificate_NullCertificate_Rejects()
    {
        var accepted = OidcDiscoveryHttpMessageHandlerFactory.ValidateServerCertificate(
            null,
            null,
            SslPolicyErrors.None,
            r_root);

        accepted.Should().BeFalse();
    }

    [Fact]
    public void ValidateServerCertificate_NameMismatch_RejectsEvenWithTrustedRoot()
    {
        using var leaf = IssueServerLeaf("d2-edge", r_intermediate);
        using var presented = BuildPresentedChain(leaf);

        var accepted = OidcDiscoveryHttpMessageHandlerFactory.ValidateServerCertificate(
            leaf,
            presented,
            SslPolicyErrors.RemoteCertificateNameMismatch
                | SslPolicyErrors.RemoteCertificateChainErrors,
            r_root);

        accepted.Should().BeFalse(
            "hostname/SAN mismatch must remain a hard failure under private PKI trust");
    }

    [Fact]
    public void LoadPublicRoot_LoadsPemOrDerPublicCert()
    {
        using var loaded = OidcDiscoveryHttpMessageHandlerFactory.LoadPublicRoot(r_rootPath);

        loaded.Thumbprint.Should().Be(r_root.Thumbprint);
        loaded.HasPrivateKey.Should().BeFalse();
    }

    private static X509Certificate2 CreateSelfSignedCa(
        string subject,
        ECDsa key,
        DateTimeOffset now)
    {
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, true, 1, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        return request.CreateSelfSigned(now.AddMinutes(-5), now.AddYears(10));
    }

    private static X509Certificate2 SignIntermediate(
        string subject,
        ECDsa intermediateKey,
        X509Certificate2 root,
        DateTimeOffset now)
    {
        var request = new CertificateRequest(subject, intermediateKey, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, true, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(
                root, includeKeyIdentifier: true, includeIssuerAndSerial: false));

        var serial = RandomNumberGenerator.GetBytes(16);
        serial[0] &= 0x7F;

        using var publicOnly = request.Create(
            root, now.AddMinutes(-5), now.AddYears(5), serial);

        return publicOnly.CopyWithPrivateKey(intermediateKey);
    }

    private static X509Certificate2 IssueServerLeaf(
        string dnsName,
        X509Certificate2 issuer)
    {
        using var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTimeOffset.UtcNow;
        var request = new CertificateRequest(
            $"CN={dnsName}",
            leafKey,
            HashAlgorithmName.SHA256);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                [new Oid("1.3.6.1.5.5.7.3.1")],
                false));

        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(dnsName);
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(
                issuer, includeKeyIdentifier: true, includeIssuerAndSerial: false));

        var serial = RandomNumberGenerator.GetBytes(16);
        serial[0] &= 0x7F;

        // Sign with the intermediate (must carry private key); export public-only leaf.
        using var withKey = request.Create(
            issuer, now.AddMinutes(-5), now.AddDays(1), serial);

        return X509CertificateLoader.LoadCertificate(withKey.RawData);
    }

    private X509Chain BuildPresentedChain(X509Certificate2 leaf)
    {
        var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(r_root);
        chain.ChainPolicy.ExtraStore.Add(r_intermediate);
        chain.Build(leaf);

        return chain;
    }
}
