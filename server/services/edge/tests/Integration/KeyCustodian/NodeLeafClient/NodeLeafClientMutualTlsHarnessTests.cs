// -----------------------------------------------------------------------
// <copyright file="NodeLeafClientMutualTlsHarnessTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Integration.KeyCustodian.NodeLeafClient;

using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using D2.Edge.Api.Grpc.KeyCustodian;
using D2.Edge.KeyCustodian.Client.Facade;
using D2.Edge.Tests.TypeSpecGrpc.Generated;
using D2.Edge.Tests.TypeSpecRoute.Generated.Facade;
using D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpc;
using D2.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;
using D2.Services.Protos.SignFixtures.V2Alpha;
using D2.Shared.AspNetCore.Mtls;
using D2.Shared.Auth.Grpc.Mtls;
using D2.Shared.Result;
using D2.Shared.Result.Grpc;
using D2.Shared.Utilities.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using DtoSignFixtureOutput = D2.Edge.Tests.TypeSpecDto.Generated.SignFixtureOutput;

/// <summary>
/// The LIVE loopback mutual-TLS handshake harness for the Node workload-leaf
/// client — the twin of <see cref="MutualTlsSignerHarnessTests"/>, with the Node
/// production client (built <c>@d2/key-custodian-client</c> dist) in the client
/// role. It stands up TWO real Kestrel HTTPS hosts: a server-TLS KeyCustodian
/// ISSUANCE host (the emitted TS gRPC client rides the REAL wire to obtain its
/// leaf) and a mutual-TLS-REQUIRED business host wired with the shipped
/// <see cref="MutualTlsHostExtensions.AddD2MutualTls"/> default-deny SPIFFE peer
/// validator. A spawned Node probe (<c>client-ts/scripts/mtls-probe.fixture.mjs</c>)
/// runs the FULL production path — keypair → CSR → wire issuance → mismatch
/// defense → CA-chain fetch → mutual-TLS presentation — and the adversarial matrix
/// is asserted at the handshake level.
/// </summary>
/// <remarks>
/// <para>
/// <b>Platform-skip discipline (matches the .NET twin).</b> Like
/// <see cref="MutualTlsSignerHarnessTests"/>, the cert-presenting cases run on the
/// deployment target and skip where the loopback mutual-TLS handshake cannot be
/// established on the host. The gate is a one-time <see cref="LiveHandshakeSpikeAsync"/>
/// probe cached per test run: if a known-good leaf cannot complete the handshake,
/// the whole cert-presenting matrix skips with the spike's reason (the primary
/// file-based CSR gate in <see cref="NodeLeafClientCsrFixtureTests"/> is
/// unconditional and covers verification cross-platform). The no-client-cert
/// reject case runs everywhere (it presents no client context).
/// </para>
/// <para>
/// <b>Node availability.</b> The probe requires <c>node</c> on PATH (overridable
/// via the <c>NODE_EXE</c> env var) AND the built client dist. When either is
/// absent the cases skip with an explicit reason — no silent pass.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public sealed class NodeLeafClientMutualTlsHarnessTests
{
    private const string _ALLOWED_WORKLOAD = "edge";
    private const string _SERVER_WORKLOAD = "d2-keycustodian";
    private const int _PROBE_TIMEOUT_MS = 60_000;

    // Live-handshake feasibility spike (the FIRST-task gate) — computed ONCE per test
    // run under the gate, then cached; the cert-presenting matrix consults it.
    private static readonly SemaphoreSlim sr_spikeGate = new(1, 1);
    private static volatile SpikeOutcome? s_spikeOutcome;

    // ----------------------------------------------------------------------
    // The full production client flow over the real wire → business call OK
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ClientFlow_IssuesOverWire_ThenBusinessCallRoundTripsOverMutualTls()
    {
        SkipIfNodeUnavailable();
        await SkipIfLiveHandshakeInfeasibleAsync();

        using var ca = new RealCertAuthority();
        using var serverCert = ca.IssueServerCertificate(_SERVER_WORKLOAD);
        using var serverCertPem = new TempPem(ExportCertPem(serverCert));

        await using var issuanceHost = await StartIssuanceHostAsync(ca, serverCert);
        var facade = new FakeSignFixtureSignerFacade(
            D2Result<DtoSignFixtureOutput?>.Ok(new DtoSignFixtureOutput("live-sig==")));
        await using var mtlsHost = await StartMutualTlsBusinessHostAsync(ca, serverCert, facade);

        var result = await RunProbeAsync(
            "client-flow",
            serverCertPem.Path,
            HostPort(mtlsHost.Endpoint),
            HostPort(issuanceHost.Endpoint));

        result.RootElement.GetProperty("issuanceSucceeded").GetBoolean()
            .Should().BeTrue(because: "the Node client obtained a leaf over the real gRPC issuance wire");
        result.RootElement.GetProperty("trustBundleAssembled").GetBoolean()
            .Should().BeTrue(because: "the client fetched the CA chain and assembled a trust bundle");
        result.RootElement.GetProperty("callSucceeded").GetBoolean()
            .Should().BeTrue(because: "the issued leaf completed the mutual-TLS handshake and reached the business service");
        result.RootElement.GetProperty("signature").GetString()
            .Should().Be("live-sig==");

        // The fetched CA chain equals the real CA's material (the D4 trust fetch is honest).
        result.RootElement.GetProperty("caRootDerSha256").GetString()
            .Should().Be(Sha256Hex(ca.RootCertificate.RawData));
        result.RootElement.GetProperty("caIntermediateDerSha256").GetString()
            .Should().Be(Sha256Hex(ca.IntermediateCertificate.RawData));

        // The business call actually reached the hosted service over the mutual-TLS channel.
        facade.SignCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ClientFlow_HostedService_SurfacesTheIssuedLeafSpiffeIdentity()
    {
        SkipIfNodeUnavailable();
        await SkipIfLiveHandshakeInfeasibleAsync();

        using var ca = new RealCertAuthority();
        using var serverCert = ca.IssueServerCertificate(_SERVER_WORKLOAD);
        using var serverCertPem = new TempPem(ExportCertPem(serverCert));

        await using var issuanceHost = await StartIssuanceHostAsync(ca, serverCert);
        var captor = new PeerIdCapturingSignFixtureService();
        await using var mtlsHost = await StartPeerCapturingHostAsync(ca, serverCert, captor);

        var result = await RunProbeAsync(
            "client-flow",
            serverCertPem.Path,
            HostPort(mtlsHost.Endpoint),
            HostPort(issuanceHost.Endpoint));

        result.RootElement.GetProperty("callSucceeded").GetBoolean().Should().BeTrue();

        // The hosted service read the validated peer SAN from the real handshake — the
        // Node-issued leaf carries the structural self-issue identity, not a wire claim.
        result.RootElement.GetProperty("signature").GetString().Should().Be(_ALLOWED_WORKLOAD);
        captor.LastPeerId.Should().Be(_ALLOWED_WORKLOAD);
    }

    // ----------------------------------------------------------------------
    // Reject matrix at the handshake level
    // ----------------------------------------------------------------------

    [Fact]
    public async Task NoClientCertificate_RejectedAtHandshake()
    {
        // The no-cert case builds no client context — it runs on every platform.
        SkipIfNodeUnavailable();

        using var ca = new RealCertAuthority();
        using var serverCert = ca.IssueServerCertificate(_SERVER_WORKLOAD);
        using var serverCertPem = new TempPem(ExportCertPem(serverCert));

        var facade = FailIfCalledFacade();
        await using var mtlsHost = await StartMutualTlsBusinessHostAsync(ca, serverCert, facade);

        var result = await RunProbeAsync(
            "no-cert", serverCertPem.Path, HostPort(mtlsHost.Endpoint));

        result.RootElement.GetProperty("callSucceeded").GetBoolean()
            .Should().BeFalse(because: "RequireCertificate fails the handshake before any business logic");
        facade.SignCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ForeignCaLeaf_RejectedAtHandshake()
    {
        SkipIfNodeUnavailable();
        await SkipIfLiveHandshakeInfeasibleAsync();

        using var ca = new RealCertAuthority();
        using var foreignCa = new RealCertAuthority();
        using var serverCert = ca.IssueServerCertificate(_SERVER_WORKLOAD);
        using var serverCertPem = new TempPem(ExportCertPem(serverCert));

        // A well-formed, allowed-workload leaf — but chaining to a foreign CA the
        // business host does not trust. Presented via the present-pem probe mode.
        using var foreignLeaf = foreignCa.IssueLeaf(_ALLOWED_WORKLOAD);
        using var leafPem = new TempPem(ExportCertPem(foreignLeaf));
        using var keyPem = new TempPem(ExportPrivateKeyPem(foreignLeaf));
        using var chainPem = new TempPem(ExportCertPem(foreignCa.IntermediateCertificate));

        var facade = FailIfCalledFacade();
        await using var mtlsHost = await StartMutualTlsBusinessHostAsync(ca, serverCert, facade);

        var result = await RunProbeAsync(
            "present-pem",
            serverCertPem.Path,
            HostPort(mtlsHost.Endpoint),
            leafPem.Path,
            keyPem.Path,
            chainPem.Path);

        result.RootElement.GetProperty("callSucceeded").GetBoolean()
            .Should().BeFalse(because: "the server validator rebuilds against ITS anchors; a foreign-CA chain fails");
        facade.SignCallCount.Should().Be(0);
    }

    [Fact]
    public async Task UnknownWorkloadLeaf_RejectedAtHandshake()
    {
        SkipIfNodeUnavailable();
        await SkipIfLiveHandshakeInfeasibleAsync();

        using var ca = new RealCertAuthority();
        using var serverCert = ca.IssueServerCertificate(_SERVER_WORKLOAD);
        using var serverCertPem = new TempPem(ExportCertPem(serverCert));

        // A valid, real production-issued leaf chaining to OUR CA — but "ghost" is
        // not in AllowedWorkloads = ["edge"].
        using var ghostLeaf = ca.IssueLeaf("ghost");
        using var leafPem = new TempPem(ExportCertPem(ghostLeaf));
        using var keyPem = new TempPem(ExportPrivateKeyPem(ghostLeaf));
        using var chainPem = new TempPem(ExportCertPem(ca.IntermediateCertificate));

        var facade = FailIfCalledFacade();
        await using var mtlsHost = await StartMutualTlsBusinessHostAsync(ca, serverCert, facade);

        var result = await RunProbeAsync(
            "present-pem",
            serverCertPem.Path,
            HostPort(mtlsHost.Endpoint),
            leafPem.Path,
            keyPem.Path,
            chainPem.Path);

        result.RootElement.GetProperty("callSucceeded").GetBoolean()
            .Should().BeFalse(because: "the allowed-workload set is a default-deny conjunct");
        facade.SignCallCount.Should().Be(0);
    }

    // ----------------------------------------------------------------------
    // Live-handshake feasibility spike (cached per run) — the FIRST-task gate
    // ----------------------------------------------------------------------

    private static async Task SkipIfLiveHandshakeInfeasibleAsync()
    {
        var (feasible, reason) = await LiveHandshakeSpikeAsync();
        Assert.SkipUnless(feasible, reason);
    }

    /// <summary>
    /// Runs ONCE per test run: a known-good Node-issued leaf attempts the mutual-TLS
    /// handshake against a throwaway business host. A success means the platform can
    /// present a private-CA client leaf over a real loopback socket (so the
    /// cert-presenting matrix runs); a failure records the reason and skips it.
    /// </summary>
    private static async Task<(bool Feasible, string Reason)> LiveHandshakeSpikeAsync()
    {
        if (s_spikeOutcome is { } cached)
            return (cached.Feasible, cached.Reason);

        await sr_spikeGate.WaitAsync();

        try
        {
            if (s_spikeOutcome is { } inner)
                return (inner.Feasible, inner.Reason);

            using var ca = new RealCertAuthority();
            using var serverCert = ca.IssueServerCertificate(_SERVER_WORKLOAD);
            using var serverCertPem = new TempPem(ExportCertPem(serverCert));
            using var leaf = ca.IssueLeaf(_ALLOWED_WORKLOAD);
            using var leafPem = new TempPem(ExportCertPem(leaf));
            using var keyPem = new TempPem(ExportPrivateKeyPem(leaf));
            using var chainPem = new TempPem(ExportCertPem(ca.IntermediateCertificate));

            var facade = new FakeSignFixtureSignerFacade(
                D2Result<DtoSignFixtureOutput?>.Ok(new DtoSignFixtureOutput("spike==")));
            await using var host = await StartMutualTlsBusinessHostAsync(ca, serverCert, facade);

            var result = await RunProbeAsync(
                "present-pem",
                serverCertPem.Path,
                HostPort(host.Endpoint),
                leafPem.Path,
                keyPem.Path,
                chainPem.Path);

            // A probe crash (missing callSucceeded) is treated as infeasible, surfacing
            // the crash shape so a genuinely-broken probe is not mistaken for a
            // platform limitation.
            if (!result.RootElement.TryGetProperty("callSucceeded", out var callSucceeded))
            {
                var crash = result.RootElement.TryGetProperty("crash", out var c)
                    ? c.GetString()
                    : "unknown";
                var crashOutcome = new SpikeOutcome(
                    false,
                    $"The Node mutual-TLS probe did not complete the spike handshake "
                    + $"(crash: {crash}); cert-presenting cases skip. The file-based CSR "
                    + "gate (NodeLeafClientCsrFixtureTests) is unconditional.");
                s_spikeOutcome = crashOutcome;

                return (crashOutcome.Feasible, crashOutcome.Reason);
            }

            var feasible = callSucceeded.GetBoolean();
            var outcome = feasible
                ? new SpikeOutcome(true, "live handshake feasible")
                : new SpikeOutcome(
                    false,
                    "The Node loopback mutual-TLS handshake could not be established on "
                    + "this host (a known-good leaf did not complete the handshake). The "
                    + "cert-presenting cases run on the deployment lane; the file-based CSR "
                    + "gate (NodeLeafClientCsrFixtureTests) proves verification cross-platform.");
            s_spikeOutcome = outcome;

            return (outcome.Feasible, outcome.Reason);
        }
        catch (Exception ex)
        {
            var outcome = new SpikeOutcome(
                false,
                "The live-handshake spike could not run on this host ("
                + ex.GetType().Name
                + "); cert-presenting cases skip. The file-based CSR gate is unconditional.");
            s_spikeOutcome = outcome;

            return (outcome.Feasible, outcome.Reason);
        }
        finally
        {
            sr_spikeGate.Release();
        }
    }

    // ----------------------------------------------------------------------
    // Hosts
    // ----------------------------------------------------------------------

    private static Task<GrpcTestHost.RunningServer> StartIssuanceHostAsync(
        RealCertAuthority ca, X509Certificate2 serverCert) =>
        GrpcTestHost.StartAsync(
            serverCert,
            services => services.AddSingleton<IKeyCustodianApi>(
                new FakeCaBackedIssuanceFacade(ca, _ALLOWED_WORKLOAD)),
            app =>
            {
                app.MapGrpcService<KeyCustodianCertificateAuthorityService>();
                app.MapGrpcService<KeyCustodianCaCertificateService>();
            });

    private static Task<GrpcTestHost.RunningServer> StartMutualTlsBusinessHostAsync(
        RealCertAuthority ca,
        X509Certificate2 serverCert,
        FakeSignFixtureSignerFacade facade) =>
        GrpcTestHost.StartAsync(
            serverCert,
            services =>
            {
                services.AddSingleton<ISignFixtureSignerFacade>(facade);
                services.AddD2MutualTls(o =>
                {
                    o.Enabled = true;
                    o.AllowedWorkloads = [_ALLOWED_WORKLOAD];
                    o.TrustAnchorsProvider = ca.TrustAnchors;
                });
            },
            app => app.MapGrpcService<SignFixtureSignerService>());

    private static Task<GrpcTestHost.RunningServer> StartPeerCapturingHostAsync(
        RealCertAuthority ca,
        X509Certificate2 serverCert,
        PeerIdCapturingSignFixtureService captor) =>
        GrpcTestHost.StartAsync(
            serverCert,
            services =>
            {
                services.AddSingleton(captor);
                services.AddD2MutualTls(o =>
                {
                    o.Enabled = true;
                    o.AllowedWorkloads = [_ALLOWED_WORKLOAD];
                    o.TrustAnchorsProvider = ca.TrustAnchors;
                });
            },
            app => app.MapGrpcService<PeerIdCapturingSignFixtureService>());

    // ----------------------------------------------------------------------
    // Node probe spawn
    // ----------------------------------------------------------------------

    private static void SkipIfNodeUnavailable()
    {
        Assert.SkipWhen(
            ResolveNodeExe() is null,
            "node was not found on PATH (set NODE_EXE to override). The Node mutual-TLS "
            + "harness requires a Node runtime; the file-based CSR gate is unconditional.");
        Assert.SkipUnless(
            File.Exists(ProbeDistIndex()),
            "The @d2/key-custodian-client dist is not built (run "
            + "`pnpm --filter @d2/key-custodian-client build`). The file-based CSR gate is unconditional.");
    }

    private static async Task<JsonDocument> RunProbeAsync(string mode, params string[] args)
    {
        var nodeExe = ResolveNodeExe()!;
        var resultPath = Path.Combine(
            Path.GetTempPath(),
            "d2-node-leaf-probe-" + Guid.NewGuid().ToString("N") + ".json");
        var scriptPath = ProbeScript();

        // Working directory MUST be the package root (client-ts/), not scripts/.
        // Under a pnpm filter install on CI, workspace packages resolve via
        // client-ts/node_modules; a scripts/ cwd walks past the package and
        // fails to resolve @d2/* / @grpc/proto-loader → one live case fails.
        var packageRoot = ClientTsDir();
        var psi = new ProcessStartInfo
        {
            FileName = nodeExe,
            WorkingDirectory = packageRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // Ensure package-local node_modules win on filter-scoped CI installs.
        var nm = Path.Combine(packageRoot, "node_modules");
        var pathSep = OperatingSystem.IsWindows() ? ";" : ":";
        var existingNodePath = Environment.GetEnvironmentVariable("NODE_PATH") ?? string.Empty;
        psi.Environment["NODE_PATH"] = string.IsNullOrEmpty(existingNodePath)
            ? nm
            : nm + pathSep + existingNodePath;

        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add(mode);
        psi.ArgumentList.Add(resultPath);

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = new Process();
        process.StartInfo = psi;
        var stderr = new System.Text.StringBuilder();
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
        process.OutputDataReceived += (_, _) => { };

        process.Start();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        try
        {
            using var cts = new CancellationTokenSource(_PROBE_TIMEOUT_MS);
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            KillTree(process);
            throw new TimeoutException(
                $"The Node mutual-TLS probe (mode {mode}) did not exit within {_PROBE_TIMEOUT_MS} ms.");
        }

        File.Exists(resultPath).Should().BeTrue(
            because: "the probe writes a JSON result; stderr was: " + stderr);

        try
        {
            return JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
        }
        finally
        {
            TryDelete(resultPath);
        }
    }

    private static void KillTree(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited between the check and the kill — nothing to do.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Best-effort cleanup of a temp file; a leftover is harmless.
        }
    }

    private static string? ResolveNodeExe()
    {
        var overridden = Environment.GetEnvironmentVariable("NODE_EXE");

        if (overridden.Truthy() && File.Exists(overridden))
            return overridden;

        var exeName = OperatingSystem.IsWindows() ? "node.exe" : "node";
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            if (dir.Falsey())
                continue;

            var candidate = Path.Combine(dir.Trim(), exeName);

            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string ClientTsDir() =>
        Path.Combine(
            TestPaths.RepoRoot(),
            "server",
            "services",
            "edge",
            "key-custodian",
            "client-ts");

    private static string ProbeScript() =>
        Path.Combine(ClientTsDir(), "scripts", "mtls-probe.fixture.mjs");

    private static string ProbeDistIndex() =>
        Path.Combine(ClientTsDir(), "dist", "index.js");

    // ----------------------------------------------------------------------
    // Cert + PEM helpers
    // ----------------------------------------------------------------------

    // A trailing newline is REQUIRED: the probe concatenates leaf + intermediate PEM
    // to present the chain, and OpenSSL's PEM parser needs a line break between the
    // END and the next BEGIN. PemEncoding.Write omits the trailing newline.
    private static string ExportCertPem(X509Certificate2 cert) =>
        new string(PemEncoding.Write("CERTIFICATE", cert.RawData)) + "\n";

    private static string ExportPrivateKeyPem(X509Certificate2 cert)
    {
        using var ecdsa = cert.GetECDsaPrivateKey()
            ?? throw new InvalidOperationException("Leaf has no ECDSA private key.");

        return new string(PemEncoding.Write("PRIVATE KEY", ecdsa.ExportPkcs8PrivateKey())) + "\n";
    }

    private static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string HostPort(Uri endpoint) => $"{endpoint.Host}:{endpoint.Port}";

    private static FakeSignFixtureSignerFacade FailIfCalledFacade() =>
        new(D2Result<DtoSignFixtureOutput?>.Ok(new DtoSignFixtureOutput("unreachable")));

    // The cached live-handshake feasibility outcome — a single immutable reference
    // published under the spike gate and read lock-free on the fast path, so the
    // multi-field result is never observed torn.
    private sealed record SpikeOutcome(bool Feasible, string Reason);

    /// <summary>A temp PEM file, deleted on dispose.</summary>
    private sealed class TempPem : IDisposable
    {
        public TempPem(string content)
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "d2-node-leaf-" + Guid.NewGuid().ToString("N") + ".pem");
            File.WriteAllText(Path, content);
        }

        public string Path { get; }

        public void Dispose() => TryDelete(Path);
    }

    /// <summary>
    /// Issuance façade backed by <see cref="RealCertAuthority"/>: verifies + signs
    /// the received CSR through the PRODUCTION rules (subject ignored — the SAN is
    /// the fixed authenticated peer view) and serves the real CA chain. The other
    /// arms are unused on the issuance host.
    /// </summary>
    private sealed class FakeCaBackedIssuanceFacade(RealCertAuthority ca, string serviceId)
        : IKeyCustodianApi
    {
        public ValueTask<D2Result<D2.Edge.KeyCustodian.Client.Issuance.IssueLeafOutput?>> IssueLeafAsync(
            D2.Edge.KeyCustodian.Client.Issuance.IssueLeafInput input,
            CancellationToken ct = default)
        {
            var material = ca.IssueLeafMaterial(input.CsrDer, serviceId);

            using var leaf = X509CertificateLoader.LoadCertificate(material.CertificateDer);

            return ValueTask.FromResult(
                D2Result<D2.Edge.KeyCustodian.Client.Issuance.IssueLeafOutput?>.Ok(
                    new D2.Edge.KeyCustodian.Client.Issuance.IssueLeafOutput(
                        material.CertificateDer,
                        material.IssuerCertificateDer,
                        new DateTimeOffset(leaf.NotBefore.ToUniversalTime()),
                        new DateTimeOffset(leaf.NotAfter.ToUniversalTime()))));
        }

        public ValueTask<D2Result<D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateOutput?>> GetCaCertificateAsync(
            D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateInput input,
            CancellationToken ct = default)
            => ValueTask.FromResult(
                D2Result<D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateOutput?>.Ok(
                    new D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateOutput(
                        ca.RootCertificate.RawData,
                        ca.IntermediateCertificate.RawData)));

        public ValueTask<D2Result<D2.Edge.KeyCustodian.Client.Jwks.GetJwksOutput?>> GetJwksAsync(
            D2.Edge.KeyCustodian.Client.Jwks.GetJwksInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<D2.Edge.KeyCustodian.Client.Jwks.GetJwksOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<D2.Edge.KeyCustodian.Client.OidcConfiguration.GetOidcConfigurationOutput?>> GetOidcConfigurationAsync(
            D2.Edge.KeyCustodian.Client.OidcConfiguration.GetOidcConfigurationInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<D2.Edge.KeyCustodian.Client.OidcConfiguration.GetOidcConfigurationOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<D2.Edge.KeyCustodian.Client.Signing.SignOutput?>> SignAsync(
            D2.Edge.KeyCustodian.Client.Signing.SignInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<D2.Edge.KeyCustodian.Client.Signing.SignOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<D2.Edge.KeyCustodian.Client.Keyring.GetKeyringOutput?>> GetKeyringAsync(
            D2.Edge.KeyCustodian.Client.Keyring.GetKeyringInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<D2.Edge.KeyCustodian.Client.Keyring.GetKeyringOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<D2.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionSealPublicKeyOutput?>> GetOrLazyProvisionSealPublicKeyAsync(
            D2.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionSealPublicKeyInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<D2.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionSealPublicKeyOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<D2.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionOwnSealPrivateKeyOutput?>> GetOrLazyProvisionOwnSealPrivateKeyAsync(
            D2.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionOwnSealPrivateKeyInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<D2.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionOwnSealPrivateKeyOutput?>.ServiceUnavailable());
    }

    /// <summary>
    /// A test-only gRPC service reading the validated peer workload identity via
    /// <c>context.GetD2PeerWorkloadIdentity()</c> and echoing it in the response so
    /// the probe can assert the surfaced identity over the real handshake.
    /// </summary>
    private sealed class PeerIdCapturingSignFixtureService
        : SignFixtureSigner.SignFixtureSignerBase
    {
        private string? _lastPeerId;

        public string? LastPeerId => Volatile.Read(ref _lastPeerId);

        public override Task<SignFixtureResponse> SignFixture(
            SignFixtureRequest request,
            Grpc.Core.ServerCallContext context)
        {
            var peerId = context.GetD2PeerWorkloadIdentity();
            Volatile.Write(ref _lastPeerId, peerId);

            var ok = D2Result<DtoSignFixtureOutput?>.Ok(new DtoSignFixtureOutput(peerId ?? string.Empty));

            return Task.FromResult(new SignFixtureResponse
            {
                Result = ok.ToProto(),
                Data = new SignFixtureOutput
                {
                    Signature = peerId ?? string.Empty,
                },
            });
        }
    }
}
