// -----------------------------------------------------------------------
// <copyright file="LoadPublicCaAnchorsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.Host;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DcsvIo.D2.Private.Edge.Api.Mtls;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Trust-anchor loader: public cert only, missing path fail-loud, corrupt bytes fail-loud.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LoadPublicCaAnchorsTests : IDisposable
{
    private readonly string r_tempDir =
        Path.Combine(Path.GetTempPath(), "edge-anchors-" + Guid.NewGuid().ToString("N"));

    public LoadPublicCaAnchorsTests()
    {
        Directory.CreateDirectory(r_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(r_tempDir))
            Directory.Delete(r_tempDir, recursive: true);
    }

    [Fact]
    public void LoadFromPath_ValidPublicCert_LoadsCollection()
    {
        var path = WritePublicCert("ok.cer");

        var collection = LoadPublicCaAnchors.LoadFromPath(path);

        collection.Should().ContainSingle();
        collection[0].HasPrivateKey.Should().BeFalse();
    }

    [Fact]
    public void LoadFromPath_MissingFile_Throws()
    {
        var path = Path.Combine(r_tempDir, "missing.cer");
        var act = () => LoadPublicCaAnchors.LoadFromPath(path);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void LoadFromPath_BlankPath_Throws()
    {
        var act = () => LoadPublicCaAnchors.LoadFromPath("  ");

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void LoadFromPath_CorruptBytes_Throws()
    {
        var path = Path.Combine(r_tempDir, "corrupt.cer");
        File.WriteAllBytes(path, [0x00, 0x01, 0x02, 0xFF, 0xFE, 0xFD]);

        var act = () => LoadPublicCaAnchors.LoadFromPath(path);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Failed to load*");
    }

    [Fact]
    public void FromConfiguration_MissingPath_Throws()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var act = () => LoadPublicCaAnchors.FromConfiguration(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{LoadPublicCaAnchors.TRUST_ANCHOR_PATH_KEY}*");
    }

    [Fact]
    public void FromConfiguration_ValidPath_ProviderReturnsSameCachedCollection()
    {
        var path = WritePublicCert("cfg.cer");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [LoadPublicCaAnchors.TRUST_ANCHOR_PATH_KEY] = path,
            })
            .Build();

        var provider = LoadPublicCaAnchors.FromConfiguration(config);
        var first = provider();
        var second = provider();

        first.Should().ContainSingle();
        second.Should().BeSameAs(first);
    }

    private string WritePublicCert(string fileName)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var request = new CertificateRequest(
            "CN=Public Anchor Test", key, HashAlgorithmName.SHA256);

        using var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(1));

        var path = Path.Combine(r_tempDir, fileName);
        File.WriteAllBytes(path, cert.Export(X509ContentType.Cert));

        return path;
    }
}
