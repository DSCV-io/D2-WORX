// -----------------------------------------------------------------------
// <copyright file="AuditHostTestKit.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Audit.Tests.Unit.Host;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using D2.Audit.Api.Mtls;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Shared fixtures for Audit host unit tests: throwaway public trust-anchor
/// files and in-memory configuration covering every required
/// <c>AddD2AuditHost</c> key. Never touches deny-ruled <c>secrets/</c>.
/// </summary>
internal sealed class AuditHostTestKit : IDisposable
{
    /// <summary>In-cluster Issuer HTTPS default used by host isolation tests.</summary>
    public const string DEFAULT_ISSUER = "https://d2-edge:8443";

    /// <summary>Non-connecting Redis URI (parsed form only; no live connect).</summary>
    public const string REDIS_URL = "redis://localhost:6379";

    private readonly string r_trustAnchorPath;
    private readonly string r_tempRoot;

    /// <summary>Initializes throwaway public trust-anchor PEM.</summary>
    public AuditHostTestKit()
    {
        r_tempRoot = Path.Combine(
            Path.GetTempPath(),
            "audit-host-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(r_tempRoot);

        r_trustAnchorPath = Path.Combine(r_tempRoot, "public-ca.cer");

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var request = new CertificateRequest(
            "CN=D2 Audit Test Public CA", key, HashAlgorithmName.SHA256);

        using var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(1));

        File.WriteAllBytes(r_trustAnchorPath, cert.Export(X509ContentType.Cert));
    }

    /// <summary>Gets the throwaway public trust-anchor path.</summary>
    public string TrustAnchorPath => r_trustAnchorPath;

    /// <summary>
    /// Sentinel-based walk to the Audit.Api source root.
    /// </summary>
    /// <returns>Absolute path to <c>private/services/audit/api</c>.</returns>
    public static string ResolveAuditApiSourceRoot()
    {
        var dir = new DirectoryInfo(
            Path.GetDirectoryName(typeof(AuditHostTestKit).Assembly.Location)!);

        while (dir is not null)
        {
            var fromRepoRoot = Path.Combine(
                dir.FullName, "server", "services", "audit", "api");

            if (Directory.Exists(fromRepoRoot)
                && File.Exists(Path.Combine(dir.FullName, "server", "D2.slnx")))
            {
                return fromRepoRoot;
            }

            var fromServer = Path.Combine(dir.FullName, "services", "audit", "api");

            if (Directory.Exists(fromServer)
                && File.Exists(Path.Combine(dir.FullName, "D2.slnx")))
            {
                return fromServer;
            }

            var fromAudit = Path.Combine(dir.FullName, "api");

            if (Directory.Exists(fromAudit)
                && File.Exists(
                    Path.Combine(dir.FullName, "tests", "D2.Audit.Tests.csproj")))
            {
                return fromAudit;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Audit.Api source root not found (sentinels: D2.slnx, D2.slnx, "
            + "or audit/tests/D2.Audit.Tests.csproj).");
    }

    /// <summary>
    /// Resolves a source file under Audit.Api via
    /// <see cref="ResolveAuditApiSourceRoot"/>.
    /// </summary>
    /// <param name="relativeSegments">Path segments under <c>audit/api/</c>.</param>
    /// <returns>Absolute path to the source file.</returns>
    public static string ResolveAuditApiSourceFile(params string[] relativeSegments)
    {
        var parts = new string[relativeSegments.Length + 1];
        parts[0] = ResolveAuditApiSourceRoot();
        Array.Copy(relativeSegments, 0, parts, 1, relativeSegments.Length);

        return Path.Combine(parts);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(r_tempRoot))
            Directory.Delete(r_tempRoot, recursive: true);
    }

    /// <summary>
    /// Builds an in-memory configuration with every key <c>AddD2AuditHost</c> requires.
    /// </summary>
    /// <param name="overrides">Optional key overrides (null value removes the key).</param>
    /// <returns>The configuration root.</returns>
    public IConfiguration BuildConfiguration(
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["KEYCUSTODIAN_APP:IssuerBaseUrl"] = DEFAULT_ISSUER,
            ["REDIS_URL"] = REDIS_URL,
            [LoadPublicCaAnchors.TRUST_ANCHOR_PATH_KEY] = r_trustAnchorPath,
        };

        if (overrides is not null)
        {
            foreach (var (key, value) in overrides)
            {
                if (value is null)
                    data.Remove(key);
                else
                    data[key] = value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
    }
}
