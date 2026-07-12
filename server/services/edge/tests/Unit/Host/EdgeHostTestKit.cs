// -----------------------------------------------------------------------
// <copyright file="EdgeHostTestKit.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.Host;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using D2.Edge.Api.Mtls;
using D2.Edge.Tests.Unit.KeyCustodian.Infra;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Shared fixtures for Edge host unit tests: throwaway public trust-anchor
/// files, root-key dirs, and in-memory configuration covering every required
/// <c>AddD2EdgeHost</c> key. Never touches deny-ruled <c>secrets/</c>.
/// </summary>
internal sealed class EdgeHostTestKit : IDisposable
{
    /// <summary>In-cluster Issuer HTTPS default used by host isolation tests.</summary>
    public const string DEFAULT_ISSUER = "https://d2-edge:8443";

    /// <summary>Non-connecting Redis URI (parsed form only; no live connect).</summary>
    public const string REDIS_URL = "redis://localhost:6379";

    /// <summary>Non-connecting AMQP URI (first-use connect deferred).</summary>
    public const string RABBITMQ_URL = "amqp://guest:guest@localhost:5672/";

    /// <summary>PG URI form that must be parsed to ADO.NET before Npgsql.</summary>
    public const string KC_DATABASE_URL =
        "postgresql://u:p@localhost:5432/d2-keycustodian";

    private readonly string r_rootKeyDir;
    private readonly string r_trustAnchorPath;
    private readonly string r_tempRoot;

    /// <summary>Initializes throwaway root-key dir + public trust-anchor PEM.</summary>
    public EdgeHostTestKit()
    {
        r_tempRoot = Path.Combine(
            Path.GetTempPath(),
            "edge-host-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(r_tempRoot);

        r_rootKeyDir = KcInfraTestKit.CreateRootKeyDir();
        r_trustAnchorPath = Path.Combine(r_tempRoot, "public-ca.cer");

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var request = new CertificateRequest(
            "CN=D2 Edge Test Public CA", key, HashAlgorithmName.SHA256);

        using var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(1));

        File.WriteAllBytes(r_trustAnchorPath, cert.Export(X509ContentType.Cert));
    }

    /// <summary>Gets the throwaway public trust-anchor path.</summary>
    public string TrustAnchorPath => r_trustAnchorPath;

    /// <summary>Gets the throwaway KC root-key directory.</summary>
    public string RootKeyDir => r_rootKeyDir;

    /// <summary>
    /// Sentinel-based walk to the Edge.Api source root (repo root with
    /// <c>server/D2.slnx</c>, or <c>server/</c> with <c>D2.slnx</c>, or the
    /// edge service directory containing both <c>api/</c> and tests).
    /// </summary>
    /// <returns>Absolute path to <c>server/services/edge/api</c>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no sentinel-matched path is found.
    /// </exception>
    public static string ResolveEdgeApiSourceRoot()
    {
        var dir = new DirectoryInfo(
            Path.GetDirectoryName(typeof(EdgeHostTestKit).Assembly.Location)!);

        while (dir is not null)
        {
            var fromRepoRoot = Path.Combine(
                dir.FullName, "server", "services", "edge", "api");

            if (Directory.Exists(fromRepoRoot)
                && File.Exists(Path.Combine(dir.FullName, "server", "D2.slnx")))
            {
                return fromRepoRoot;
            }

            var fromServer = Path.Combine(dir.FullName, "services", "edge", "api");

            if (Directory.Exists(fromServer)
                && File.Exists(Path.Combine(dir.FullName, "D2.slnx")))
            {
                return fromServer;
            }

            var fromEdge = Path.Combine(dir.FullName, "api");

            if (Directory.Exists(fromEdge)
                && File.Exists(
                    Path.Combine(dir.FullName, "tests", "D2.Edge.Tests.csproj")))
            {
                return fromEdge;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Edge.Api source root not found (sentinels: server/D2.slnx, D2.slnx, "
            + "or edge/tests/D2.Edge.Tests.csproj).");
    }

    /// <summary>
    /// Resolves a source file under Edge.Api via
    /// <see cref="ResolveEdgeApiSourceRoot"/>.
    /// </summary>
    /// <param name="relativeSegments">Path segments under <c>edge/api/</c>.</param>
    /// <returns>Absolute path to the source file.</returns>
    public static string ResolveEdgeApiSourceFile(params string[] relativeSegments)
    {
        var parts = new string[relativeSegments.Length + 1];
        parts[0] = ResolveEdgeApiSourceRoot();
        Array.Copy(relativeSegments, 0, parts, 1, relativeSegments.Length);

        return Path.Combine(parts);
    }

    /// <summary>
    /// Absolute path to <c>server/services/edge/tests</c> (sibling of Edge.Api).
    /// </summary>
    /// <returns>The tests project source root.</returns>
    public static string ResolveEdgeTestsSourceRoot()
    {
        var apiRoot = ResolveEdgeApiSourceRoot();
        var edgeRoot = Path.GetDirectoryName(apiRoot)
            ?? throw new InvalidOperationException(
                "Edge service root not found from Edge.Api source root.");

        return Path.Combine(edgeRoot, "tests");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(r_rootKeyDir))
            Directory.Delete(r_rootKeyDir, recursive: true);

        if (Directory.Exists(r_tempRoot))
            Directory.Delete(r_tempRoot, recursive: true);
    }

    /// <summary>
    /// Builds an in-memory configuration with every key <c>AddD2EdgeHost</c> requires.
    /// </summary>
    /// <param name="overrides">Optional key overrides (null value removes the key).</param>
    /// <returns>The configuration root.</returns>
    public IConfiguration BuildConfiguration(
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["KEYCUSTODIAN_APP:IssuerBaseUrl"] = DEFAULT_ISSUER,
            ["KEYCUSTODIAN_APP:Default:Cadence"] = "30.00:00:00",
            ["KEYCUSTODIAN_APP:Default:Grace"] = "7.00:00:00",
            ["KEYCUSTODIAN_APP:Default:SmokeSoak"] = "01:00:00",
            ["KEYCUSTODIAN_INFRA:RootKeyPath"] = r_rootKeyDir,
            ["KEYCUSTODIAN_INFRA:RotationCheckInterval"] = "00:05:00",
            ["KEYCUSTODIAN_INFRA:DbCommandTimeoutSeconds"] = "30",
            ["REDIS_URL"] = REDIS_URL,
            ["RABBITMQ_URL"] = RABBITMQ_URL,
            ["KEYCUSTODIAN_DATABASE_URL"] = KC_DATABASE_URL,
            [LoadPublicCaAnchors.TRUST_ANCHOR_PATH_KEY] = r_trustAnchorPath,
            ["AUDIT_GRPC:Address"] = "https://d2-audit:8443",
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
