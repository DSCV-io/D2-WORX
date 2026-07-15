// -----------------------------------------------------------------------
// <copyright file="NodeLeafClientKeyringHarnessTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Integration.KeyCustodian.NodeLeafClient;

using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using DcsvIo.D2.AspNetCore.Mtls;
using DcsvIo.D2.Auth.Grpc.Mtls;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Handler.Repo.Postgres;
using DcsvIo.D2.Private.Auth;
using DcsvIo.D2.Private.Edge.Api.Grpc.KeyCustodian;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.CertificateAuthority;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.ActivateKey;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetKeyring;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Messaging;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Vault;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring;
using DcsvIo.D2.Private.Edge.KeyCustodian.Domain.ValueObjects;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Persistence.Postgres;
using DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpc.Generated;
using DcsvIo.D2.Private.Edge.Tests.TypeSpecRoute.Generated.Facade;
using DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian;
using DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpc;
using DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using global::D2.Services.Protos.KeyCustodian.V2Alpha;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ClientKeyringOutput = DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring.GetKeyringOutput;
using DtoSignFixtureOutput = DcsvIo.D2.Private.Edge.Tests.TypeSpecDto.Generated.SignFixtureOutput;

/// <summary>
/// The LIVE loopback mutual-TLS proof for the TypeScript <c>getKeyring</c> consumer
/// runtime: the shipped <c>@dcsv-io/d2-private-key-custodian-client</c> <c>GrpcKeyringClient</c> dials a
/// real Kestrel HTTPS KeyCustodian host over a genuine mutual-TLS socket, fetches a payload
/// keyring served by the REAL <see cref="GetKeyringHandler"/> over a Testcontainers
/// PostgreSQL database, and the returned material round-trips through the shipped TS
/// <c>@dcsv-io/d2-encryption</c> <c>PayloadCrypto</c> by decrypting a frame the .NET
/// <c>PayloadCrypto</c> produced under the SAME keyring ΓÇö a cross-runtime,
/// over-the-wire end-to-end pin.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lane reuse ΓÇö the <see cref="NodeLeafClientMutualTlsHarnessTests"/> shape.</b>
/// This reuses the exact loopback-mTLS lane machinery (the shared <see cref="GrpcTestHost"/>
/// real-socket host, the <see cref="RealCertAuthority"/> private CA, the spawned Node probe
/// <c>client-ts/scripts/mtls-probe.fixture.mjs</c>, and the one-time live-handshake
/// feasibility spike). The probe gains a <c>get-keyring</c> mode; nothing about the lane's
/// handshake, host, or skip mechanics changes. Like the other two loopback harnesses
/// (<see cref="MutualTlsSignerHarnessTests"/> + <see cref="NodeLeafClientMutualTlsHarnessTests"/>),
/// this class is self-contained ΓÇö each harness carries its own probe/spike/cert plumbing.
/// </para>
/// <para>
/// <b>Real KeyCustodian surface ΓÇö the distribution-test composition.</b> The mTLS host wires
/// the full real KeyCustodian app (<c>AddD2KeyCustodianApp</c> + <c>AddD2Postgres</c> + the
/// real DbContext over the shared <see cref="KeyCustodianPostgresFixture"/>), so the probe's
/// fetch runs the real <see cref="GetKeyringHandler"/> authority rule
/// (<c>WorkloadCapabilityAuthority.AuthorizeKeyringFetch</c>), the real root-unwrap, and the
/// real overlap partition ΓÇö the same graph the
/// <see cref="DcsvIo.D2.Private.Edge.Tests.Integration.KeyCustodian.KeyCustodianKeyringDistributionIntegrationTests"/>
/// in-process test exercises, here driven over a socket by the shipped TS client.
/// </para>
/// <para>
/// <b>Isolated in-memory TEST grant + the mechanism-proof seam.</b> The
/// caller identity is the VALIDATED mutual-TLS peer (<c>GetD2PeerWorkloadIdentity()</c> ΓÇö the
/// same unforgeable local fact the production
/// <c>RequestOriginCrossProcessInterceptor</c> reads), and the authority grant is an isolated
/// in-memory <see cref="KeyringDomainAuthorityOptions"/> mapping the peer workload to the one
/// test domain. Because the pure-mTLS probe forwards no internal transaction token, the
/// <c>internal.kc.keyring</c> scope ΓÇö the token half a production caller would forward ΓÇö is
/// injected server-side by <see cref="KeyringOverMutualTlsFixtureService"/> as the documented
/// mechanism-proof seam: it establishes <see cref="RequestOrigin.CrossProcessHop"/> +
/// <c>ImmediateCaller</c> from the real peer and the required scope, then invokes the real
/// handler through the real transport mappers. The HARD wall remains the mTLS peer identity;
/// the injected scope is hygiene the wire cannot supply here, never the security boundary.
/// </para>
/// <para>
/// <b>Platform-skip discipline (matches the .NET + Node twins).</b> The cert-presenting flow
/// runs on the deployment target (Linux/OpenSSL) and skips where a private-CA client leaf
/// cannot be presented over a loopback socket (Windows-Schannel) ΓÇö gated by the one-time
/// <see cref="LiveHandshakeSpikeAsync"/> probe, deterministic, never flaky, no new CI lane.
/// The unconditional cross-runtime crypto gate remains the file-based KAT/golden fixtures.
/// </para>
/// <para>
/// <b>Domain choice ΓÇö the fixture-seam domain.</b> The sealed flip removed
/// <c>audit</c>/<c>notifications</c>/<c>courier</c> from the KeyCustodian symmetric payload
/// catalog (they are sealed one-way now), so this test exercises the preserved
/// domain-generic symmetric machinery on a registered fixture payload domain
/// (<see cref="FixturePayloadDomains.PAYLOAD_A"/>) through the
/// <see cref="KeyDomain.RegisterFixturePayloadDomainForTesting"/> seam, exactly as its
/// sibling keyring distribution / lifecycle integration tests do. The field-initializer
/// registration precedes any per-test host boot; <see cref="Dispose"/> unregisters (the
/// registration is ref-counted, per-test-instance).
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Collection(KeyCustodianPostgresCollectionDefinition.NAME)]
public sealed class NodeLeafClientKeyringHarnessTests(KeyCustodianPostgresFixture fixture)
    : IDisposable
{
    private const string _ALLOWED_WORKLOAD = "edge";
    private const string _SERVER_WORKLOAD = "d2-keycustodian";

    // A registered fixture AES-payload domain (audit/notifications/courier left the KC
    // symmetric payload catalog); the field-initializer registration precedes
    // any per-test host boot and Dispose unregisters, exactly as the sibling keyring
    // integration tests do.
    private const string _DOMAIN = FixturePayloadDomains.PAYLOAD_A;
    private const int _PROBE_TIMEOUT_MS = 60_000;

    // Live-handshake feasibility spike (the FIRST-task gate) â€” computed ONCE per test run
    // under the gate, then cached; the cert-presenting case consults it.
    private static readonly SemaphoreSlim sr_spikeGate = new(1, 1);
    private static volatile SpikeOutcome? s_spikeOutcome;

    private readonly IDisposable r_fixtureSeam =
        KeyDomain.RegisterFixturePayloadDomainForTesting(_DOMAIN);

    /// <summary>Unregisters the fixture payload domain (ref-counted, per-test-instance).</summary>
    public void Dispose() => r_fixtureSeam.Dispose();

    [Fact]
    public async Task LiveGetKeyring_OverMutualTls_FetchedKeyringDecryptsDotNetEncryptedFrame()
    {
        SkipIfNodeUnavailable();
        await SkipIfLiveHandshakeInfeasibleAsync();

        await fixture.EnsureMigratedAsync();
        await CleanDomainAsync();

        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0));
        var rootCrypto = BuildRootCrypto();
        var plaintext = "keyring-over-mtls-payload"u8.ToArray();

        // 1. Seed one Active AesPayload key for the domain (System plane) via the real handlers.
        string activeKid;
        await using (var sys =
            BuildProvider(clock, SystemContext(), rootCrypto, keyringAuthority: null))
        {
            (await Handler<IGenerateKeyHandler>(sys).HandleAsync(
                new GenerateKeyInput(_DOMAIN, KeyType.AesPayload), CancellationToken.None))
                .Success.Should().BeTrue("the System plane provisions the domain's first payload key");

            activeKid = await SingleKidAsync(KeyStatus.Pending);
            clock.Advance(Duration.FromHours(2));

            (await Handler<IActivateKeyHandler>(sys).HandleAsync(
                new ActivateKeyInput(activeKid), CancellationToken.None))
                .Success.Should().BeTrue("the pending key activates after its soak window");
        }

        // 2. Fetch the real keyring in-process â†’ encrypt a known plaintext to a v1 frame. This
        //    is the .NET-produced frame the TS side must decrypt with the SAME wire keyring.
        byte[] frame;
        await using (var fetch = BuildProvider(clock, FetchContext(), rootCrypto, BuildGrant()))
        {
            var fetched = await Handler<IGetKeyringHandler>(fetch).HandleAsync(
                new GetKeyringInput(_DOMAIN), CancellationToken.None);

            fetched.Success.Should().BeTrue("the isolated grant authorizes the in-process fetch");
            fetched.Data!.ActiveKid.Should().Be(activeKid);

            using var keyring = ToKeyring(fetched.Data);
            frame = new PayloadCrypto(keyring).Encrypt(plaintext);
        }

        // 3. Issue the client's "edge" leaf from the CA and start the mutual-TLS keyring host
        //    over the SAME PostgreSQL database with the isolated in-memory grant.
        using var ca = new RealCertAuthority();
        using var serverCert = ca.IssueServerCertificate(_SERVER_WORKLOAD);
        using var serverCertPem = new TempPem(ExportCertPem(serverCert));
        using var clientLeaf = ca.IssueLeaf(_ALLOWED_WORKLOAD);
        using var leafPem = new TempPem(ExportCertPem(clientLeaf));
        using var keyPem = new TempPem(ExportPrivateKeyPem(clientLeaf));
        using var chainPem = new TempPem(ExportCertPem(ca.IntermediateCertificate));

        await using var host =
            await StartMutualTlsKeyringHostAsync(ca, serverCert, clock, rootCrypto);

        // 4. The Node probe presents the issued leaf, runs the shipped GrpcKeyringClient over
        //    the real mTLS wire, and decrypts the .NET frame with the fetched keyring.
        var result = await RunProbeAsync(
            "get-keyring",
            serverCertPem.Path,
            HostPort(host.Endpoint),
            leafPem.Path,
            keyPem.Path,
            chainPem.Path,
            _DOMAIN,
            Convert.ToBase64String(frame));

        result.RootElement.GetProperty("keyringFetched").GetBoolean()
            .Should().BeTrue(because: "the shipped TS GrpcKeyringClient fetched the keyring over the real mTLS wire");
        result.RootElement.GetProperty("activeKid").GetString()
            .Should().Be(activeKid, because: "the wire keyring carries the real Active kid the handler served");

        Convert.FromBase64String(result.RootElement.GetProperty("decryptedBase64").GetString()!)
            .Should().Equal(
                plaintext,
                "the fetched keyring decrypts a frame the .NET PayloadCrypto produced (cross-runtime)");

        result.RootElement.GetProperty("selfRoundTripOk").GetBoolean()
            .Should().BeTrue(because: "the fetched keyring also encrypts + decrypts a fresh TS-side round-trip");
    }

    // ----------------------------------------------------------------------
    // Live-handshake feasibility spike (cached per run) â€” the FIRST-task gate.
    // Mirrors NodeLeafClientMutualTlsHarnessTests: a known-good leaf attempts the
    // loopback mutual-TLS handshake against a throwaway business host; a failure
    // records the reason and skips the cert-presenting case.
    // ----------------------------------------------------------------------

    private static async Task SkipIfLiveHandshakeInfeasibleAsync()
    {
        var (feasible, reason) = await LiveHandshakeSpikeAsync();
        Assert.SkipUnless(feasible, reason);
    }

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

            if (!result.RootElement.TryGetProperty("callSucceeded", out var callSucceeded))
            {
                var crash = result.RootElement.TryGetProperty("crash", out var c)
                    ? c.GetString()
                    : "unknown";
                var crashOutcome = new SpikeOutcome(
                    false,
                    $"The Node mutual-TLS probe did not complete the spike handshake "
                    + $"(crash: {crash}); the live getKeyring case skips. The file-based crypto "
                    + "KAT gate is unconditional.");
                s_spikeOutcome = crashOutcome;

                return (crashOutcome.Feasible, crashOutcome.Reason);
            }

            var feasible = callSucceeded.GetBoolean();
            var outcome = feasible
                ? new SpikeOutcome(true, "live handshake feasible")
                : new SpikeOutcome(
                    false,
                    "The Node loopback mutual-TLS handshake could not be established on this "
                    + "host (a known-good leaf did not complete the handshake). The live "
                    + "getKeyring case runs on the deployment lane; the file-based crypto KAT "
                    + "gate proves cross-runtime interop cross-platform.");
            s_spikeOutcome = outcome;

            return (outcome.Feasible, outcome.Reason);
        }
        catch (Exception ex)
        {
            var outcome = new SpikeOutcome(
                false,
                "The live-handshake spike could not run on this host ("
                + ex.GetType().Name
                + "); the live getKeyring case skips. The file-based crypto KAT gate is unconditional.");
            s_spikeOutcome = outcome;

            return (outcome.Feasible, outcome.Reason);
        }
        finally
        {
            sr_spikeGate.Release();
        }
    }

    // ----------------------------------------------------------------------
    // Hosts (static)
    // ----------------------------------------------------------------------

    /// <summary>
    /// Starts a throwaway mutual-TLS business host (the sign-fixture signer, no database) used
    /// only by the live-handshake feasibility spike.
    /// </summary>
    private static Task<GrpcTestHost.RunningServer> StartMutualTlsBusinessHostAsync(
        RealCertAuthority ca, X509Certificate2 serverCert, FakeSignFixtureSignerFacade facade) =>
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

    // ----------------------------------------------------------------------
    // Test data + crypto helpers (static)
    // ----------------------------------------------------------------------

    private static PayloadCryptoKeyring ToKeyring(ClientKeyringOutput output)
    {
        var keys = output.Entries.ToDictionary(e => e.Kid, e => e.KeyBytes, StringComparer.Ordinal);
        return new PayloadCryptoKeyring(output.ActiveKid, keys, output.AadContext);
    }

    private static IPayloadCrypto BuildRootCrypto()
    {
        var key = RandomNumberGenerator.GetBytes(PayloadCryptoKeyring.KEY_SIZE_BYTES);
        var keyring = new PayloadCryptoKeyring(
            "root",
            new Dictionary<string, byte[]> { ["root"] = key },
            "keycustodian-root"u8.ToArray());
        return new PayloadCrypto(keyring);
    }

    private static KeyCustodianOptions BuildOptions() => new()
    {
        RsaKeySizeBits = 2048,
        SecretLengthBytes = 64,
        Default = new RotationPolicyOptions
        {
            // Cadence must be >= Grace + SmokeSoak for a valid policy.
            Cadence = TimeSpan.FromDays(30),
            Grace = TimeSpan.FromDays(7),
            SmokeSoak = TimeSpan.FromHours(1),
        },
    };

    private static KeyringDomainAuthorityOptions BuildGrant()
    {
        var grant = new KeyringDomainAuthorityOptions();
        grant.AllowedKeyringDomainsByWorkload[_ALLOWED_WORKLOAD] = [_DOMAIN];
        return grant;
    }

    private static MutableRequestContext SystemContext() =>
        new() { Origin = RequestOrigin.System };

    private static MutableRequestContext FetchContext() => new()
    {
        Origin = RequestOrigin.CrossProcessHop,
        ImmediateCaller = _ALLOWED_WORKLOAD,
        Scopes = new HashSet<string>(StringComparer.Ordinal) { ProductScopes.Internal.Kc.Keyring },
    };

    private static THandler Handler<THandler>(ServiceProvider provider)
        where THandler : notnull =>
        provider.CreateScope().ServiceProvider.GetRequiredService<THandler>();

    // ----------------------------------------------------------------------
    // Node probe spawn (the shared lane mechanism)
    // ----------------------------------------------------------------------

    private static void SkipIfNodeUnavailable()
    {
        Assert.SkipWhen(
            ResolveNodeExe() is null,
            "node was not found on PATH (set NODE_EXE to override). The Node mutual-TLS "
            + "harness requires a Node runtime; the file-based crypto KAT gate is unconditional.");
        Assert.SkipUnless(
            File.Exists(ProbeDistIndex()),
            "The @dcsv-io/d2-private-key-custodian-client dist is not built (run "
            + "`pnpm --filter @dcsv-io/d2-private-key-custodian-client build`). The file-based crypto KAT gate is unconditional.");
    }

    private static async Task<JsonDocument> RunProbeAsync(string mode, params string[] args)
    {
        var nodeExe = ResolveNodeExe()!;
        var resultPath = Path.Combine(
            Path.GetTempPath(),
            "d2-node-keyring-probe-" + Guid.NewGuid().ToString("N") + ".json");
        var scriptPath = ProbeScript();

        // Working directory MUST be the package root (client-ts/), not scripts/.
        // Under a pnpm filter install on CI, workspace packages resolve via
        // client-ts/node_modules; a scripts/ cwd walks past the package and
        // fails to resolve @dcsv-io/d2-* / @grpc/proto-loader â†’ one live case fails.
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
        psi.Environment["NODE_PATH"] = existingNodePath.Falsey()
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
        process.ErrorDataReceived +=
            (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
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
                $"The Node keyring probe (mode {mode}) did not exit within {_PROBE_TIMEOUT_MS} ms.");
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
            // Already exited between the check and the kill â€” nothing to do.
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
            "private",
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

    // A trailing newline is REQUIRED: the probe concatenates leaf + intermediate PEM to present
    // the chain, and OpenSSL's PEM parser needs a line break between END and the next BEGIN.
    // PemEncoding.Write omits the trailing newline.
    private static string ExportCertPem(X509Certificate2 cert) =>
        new string(PemEncoding.Write("CERTIFICATE", cert.RawData)) + "\n";

    private static string ExportPrivateKeyPem(X509Certificate2 cert)
    {
        using var ecdsa = cert.GetECDsaPrivateKey()
            ?? throw new InvalidOperationException("Leaf has no ECDSA private key.");

        return new string(PemEncoding.Write("PRIVATE KEY", ecdsa.ExportPkcs8PrivateKey())) + "\n";
    }

    private static string HostPort(Uri endpoint) => $"{endpoint.Host}:{endpoint.Port}";

    // ----------------------------------------------------------------------
    // Real KeyCustodian composition + seeding (instance â€” bound to the PG fixture)
    // ----------------------------------------------------------------------

    /// <summary>
    /// Starts a real Kestrel mutual-TLS host serving the REAL <c>getKeyring</c> op over the
    /// shared Testcontainers PostgreSQL database with an isolated in-memory grant, mapping the
    /// context-establishing <see cref="KeyringOverMutualTlsFixtureService"/>.
    /// </summary>
    private Task<GrpcTestHost.RunningServer> StartMutualTlsKeyringHostAsync(
        RealCertAuthority ca,
        X509Certificate2 serverCert,
        TestClock clock,
        IPayloadCrypto rootCrypto) =>
        GrpcTestHost.StartAsync(
            serverCert,
            services =>
            {
                ComposeKeyCustodian(services, clock, rootCrypto);

                // The isolated in-memory TEST grant: the peer workload may fetch this domain.
                var grant = BuildGrant();
                services.AddSingleton(Options.Create(grant));

                // Per-call scoped request context populated from the validated mTLS peer.
                services.AddScoped<MutableRequestContext>();
                services.AddScoped<IRequestContext>(
                    sp => sp.GetRequiredService<MutableRequestContext>());
                services.AddSingleton<KeyringOverMutualTlsFixtureService>();

                services.AddD2MutualTls(o =>
                {
                    o.Enabled = true;
                    o.AllowedWorkloads = [_ALLOWED_WORKLOAD];
                    o.TrustAnchorsProvider = ca.TrustAnchors;
                });
            },
            app => app.MapGrpcService<KeyringOverMutualTlsFixtureService>());

    private void ComposeKeyCustodian(
        IServiceCollection services, TestClock clock, IPayloadCrypto rootCrypto)
    {
        services.AddLogging();
        services.AddD2Handler();

        services.AddSingleton<IClock>(clock);
        services.AddSingleton<IKeyRotationAnnouncer>(new RecordingAnnouncer());
        services.AddSingleton(Options.Create(new SigningDomainAuthorityOptions()));

        services.AddDbContext<KeyCustodianDbContext>(opts =>
            opts.ApplyD2NpgsqlDefaults(
                fixture.ConnectionString,
                commandTimeoutSeconds: 30,
                migrationsAssemblyName: typeof(KeyCustodianDbContext).Assembly.GetName().Name!));
        services.AddScoped<IKeyCustodianDbContext>(
            sp => sp.GetRequiredService<KeyCustodianDbContext>());

        services.AddD2Postgres();

        services.AddKeyedSingleton<IPayloadCrypto>(
            KeyCustodianRootKey.ROOT_SERVICE_KEY, (_, _) => rootCrypto);

        services.AddSingleton(Options.Create(BuildOptions()));

        services.AddD2KeyCustodianApp();

        // The lifecycle-mutation handlers (GenerateKey/Activate/â€¦ used to seed the domain)
        // require the dedicated Â§9.44 root-signing capability, which the general
        // AddD2KeyCustodianApp() deliberately does NOT register (structural isolation) â€”
        // register it here exactly as the sibling keyring integration tests do.
        services.AddD2CaRootSigningCapability();
    }

    private ServiceProvider BuildProvider(
        TestClock clock,
        IRequestContext requestContext,
        IPayloadCrypto rootCrypto,
        KeyringDomainAuthorityOptions? keyringAuthority)
    {
        var services = new ServiceCollection();
        ComposeKeyCustodian(services, clock, rootCrypto);

        services.AddSingleton(requestContext);
        services.AddSingleton(
            Options.Create(keyringAuthority ?? new KeyringDomainAuthorityOptions()));

        return services.BuildServiceProvider();
    }

    private async Task CleanDomainAsync()
    {
        await using var ctx = fixture.NewContext();

        // Audit rows FK-reference key_record with RESTRICT, so the domain's audit children go
        // first; the kid list is materialized so no query lambda captures the disposable ctx.
        var kids = await ctx.Keys
            .Where(k => k.KeyDomain == _DOMAIN)
            .Select(k => k.Kid)
            .ToListAsync();

        await ctx.Audit.Where(a => kids.Contains(a.Kid)).ExecuteDeleteAsync();
        await ctx.Keys.Where(k => k.KeyDomain == _DOMAIN).ExecuteDeleteAsync();
    }

    private async Task<string> SingleKidAsync(KeyStatus status)
    {
        await using var context = fixture.NewContext();
        return await context.Keys.AsNoTracking()
            .Where(k => k.KeyDomain == _DOMAIN && k.Status == status)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => k.Kid)
            .FirstAsync();
    }

    // The cached live-handshake feasibility outcome â€” a single immutable reference published
    // under the spike gate and read lock-free on the fast path, so the multi-field result is
    // never observed torn.
    private sealed record SpikeOutcome(bool Feasible, string Reason);

    /// <summary>A temp PEM file, deleted on dispose.</summary>
    private sealed class TempPem : IDisposable
    {
        public TempPem(string content)
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "d2-node-keyring-" + Guid.NewGuid().ToString("N") + ".pem");
            File.WriteAllText(Path, content);
        }

        public string Path { get; }

        public void Dispose() => TryDelete(Path);
    }

    /// <summary>
    /// The hosted <c>getKeyring</c> gRPC service for the mutual-TLS keyring host. Reads the
    /// VALIDATED mutual-TLS peer identity from the real handshake, establishes a per-call
    /// scoped <see cref="MutableRequestContext"/> (<see cref="RequestOrigin.CrossProcessHop"/>
    /// + the peer as <c>ImmediateCaller</c> + the injected <c>internal.kc.keyring</c> scope
    /// seam), and delegates to the REAL <see cref="GetKeyringHandler"/> through the generated
    /// transport mappers. Origin + caller are set together only when a peer identity is
    /// present, so a peerless call leaves <c>Origin</c> at the fail-closed
    /// <see cref="RequestOrigin.Unestablished"/> and the authority rule denies (Â§9.42).
    /// </summary>
    private sealed class KeyringOverMutualTlsFixtureService(IServiceScopeFactory scopeFactory)
        : KeyCustodianKeyring.KeyCustodianKeyringBase
    {
        public override async Task<GetKeyringResponse> GetKeyring(
            GetKeyringRequest request, ServerCallContext context)
        {
            var peer = context.GetD2PeerWorkloadIdentity();

            using var scope = scopeFactory.CreateScope();
            var requestContext = scope.ServiceProvider.GetRequiredService<MutableRequestContext>();

            if (peer is not null)
            {
                requestContext.Origin = RequestOrigin.CrossProcessHop;
                requestContext.ImmediateCaller = peer;
            }

            requestContext.Scopes =
                new HashSet<string>(StringComparer.Ordinal) { ProductScopes.Internal.Kc.Keyring };

            var handler = scope.ServiceProvider.GetRequiredService<IGetKeyringHandler>();
            var result = await handler
                .HandleAsync(request.ToGetKeyringInput(), context.CancellationToken)
                .ConfigureAwait(false);

            return result.ToProtoResponse();
        }
    }
}
