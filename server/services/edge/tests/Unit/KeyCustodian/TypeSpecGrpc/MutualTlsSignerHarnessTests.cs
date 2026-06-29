// -----------------------------------------------------------------------
// <copyright file="MutualTlsSignerHarnessTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpc;

using System.Diagnostics.CodeAnalysis;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using D2.Edge.KeyCustodian.Domain.Rules;
using D2.Edge.Tests.TypeSpecGrpc.Generated;
using D2.Edge.Tests.TypeSpecRoute.Generated.Facade;
using D2.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;
using D2.Services.Protos.SignFixtures.V2Alpha;
using D2.Shared.AspNetCore.Mtls;
using D2.Shared.Auth.Outbound;
using D2.Shared.Auth.Outbound.Grpc;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
using D2.Shared.Result;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using DtoSignFixtureOutput = D2.Edge.Tests.TypeSpecDto.Generated.SignFixtureOutput;

/// <summary>
/// The end-to-end mutual-TLS harness proof. A real Kestrel HTTPS
/// endpoint on <c>127.0.0.1:0</c> (an OS-assigned ephemeral loopback port, so a
/// real TCP socket + a real TLS 1.3 handshake), wired with the SHIPPED
/// <see cref="MutualTlsHostExtensions.AddD2MutualTls"/> (require + validate a
/// client certificate via the default-deny SPIFFE peer validator), hosts the
/// generated <c>Signer.Sign</c> gRPC service. A <see cref="GrpcChannel"/> dials
/// the loopback endpoint presenting a workload leaf, and the adversarial matrix is
/// asserted AT THE HANDSHAKE LEVEL — a rejected peer fails the TLS handshake (an
/// <see cref="RpcException"/>), never reaching the business call.
/// </summary>
/// <remarks>
/// <para>
/// <b>Real KeyCustodian-issued certificates.</b> The valid leaf + the wrong-CA
/// leaf + the expired leaf + the unknown-workload leaf are all minted by
/// KeyCustodian's PRODUCTION certificate-generation rules
/// (<see cref="CaCertificateGeneration"/> + <see cref="WorkloadCertificateIssuance"/>),
/// not a hand-rolled copy — so this proves a genuinely KeyCustodian-issued
/// certificate sails through (or is rejected by) the shared mutual-TLS checker over
/// a real socket. Only the foreign-trust-domain SAN case is hand-rolled, because the
/// production issuance rule only ever emits the <c>d2.internal</c> trust domain and
/// physically cannot emit a foreign one.
/// </para>
/// <para>
/// <b>The client-side server-cert trust is test plumbing, NOT a fail-open.</b> The
/// loopback server presents a SELF-SIGNED certificate (its own trivial chain — see
/// the Windows-test accommodations below), which the machine trust store does not
/// know, so the CLIENT sets
/// <see cref="SslClientAuthenticationOptions.RemoteCertificateValidationCallback"/>
/// to accept it. This is client-side only — it does NOT weaken the SERVER's mutual-TLS
/// client-certificate check, which is precisely what these tests prove: the server
/// runs the full <see cref="SpiffeSanPeerValidator"/> default-deny chain on every
/// connection.
/// </para>
/// <para>
/// <b>Windows-test accommodation: a self-signed server certificate.</b> This harness
/// runs deterministically on a CLEAN box with ZERO cert-store mutation. The Kestrel
/// server certificate is SELF-SIGNED (its own trivial one-element chain) rather than a
/// CA-chained workload leaf, because Kestrel's HTTPS middleware builds an
/// <see cref="SslStreamCertificateContext"/> from the server certificate at startup and
/// on Windows-Schannel that build throws "an unknown chain building error occurred" for
/// any certificate whose chain does not resolve to an OS-trust-store-resident root (the
/// internal CA is deliberately never installed into the OS store). A self-signed cert
/// chains to itself, so the context builds on a clean Windows box. The client skips
/// server-cert validation anyway, so the server identity is irrelevant to the proof —
/// what is under test is the SERVER's mutual-TLS validation of the CLIENT certificate,
/// which is unaffected by the server certificate's issuer. This accommodation does NOT
/// weaken the validation under test.
/// </para>
/// <para>
/// <b>The cert-PRESENTING cases run on the deployment target (Linux/OpenSSL), and skip
/// on Windows.</b> The same Windows-Schannel limitation applies to the CLIENT
/// certificate: .NET builds an <see cref="SslStreamCertificateContext"/> for the
/// presented client leaf during the handshake, and on Windows that build throws for a
/// leaf chaining to the (non-OS-trusted) internal CA — even for a bare leaf, even with
/// the intermediate supplied (proven empirically; documented by Microsoft: "for a
/// client application the only solution is to add the certificates to the certificate
/// store manually"). A clean Windows box therefore cannot present a private-CA client
/// leaf over a real socket without OS-store mutation, which this harness refuses to do.
/// The six cert-presenting cases below are accordingly gated to non-Windows (Linux is
/// the deployment target — there they present the full chain on the wire and exercise
/// the validator over a genuine socket). The no-client-certificate case runs on every
/// platform (it builds no client cert context). The validator's full conjunct matrix
/// (chain-to-root / SPIFFE-SAN trust-domain / allowed-workload / expiry / malformed
/// SAN / CA-as-leaf / garbage / null) is proven cross-platform by the adversarial
/// <see cref="SpiffeSanPeerValidator"/> UNIT matrix, which drives the same validator
/// with in-memory chains and needs no socket.
/// </para>
/// <para>
/// <b>Two client paths (on the deployment target).</b> The valid case is proven over a
/// real socket via direct leaf presentation. The shipped client leaf-presentation stack
/// (<c>AddD2WorkloadCertificateOutbound</c> + <c>AddD2WorkloadCertificate</c> +
/// <c>WorkloadLeafClient</c> + the single-value cache) is then exercised end-to-end: the
/// shipped gRPC client dials the real loopback server, the shipped presentation callback
/// presents the full chain, and the business call round-trips. This exercises
/// <c>WorkloadLeafClient.BuildLiveLeaf</c> (which re-homes the leaf's ephemeral key into
/// a Schannel-usable key container on Windows — an ephemeral-key leaf fails the Windows
/// handshake with <c>0x8009030E</c>) on the Linux/OpenSSL deployment target.
/// </para>
/// <para>
/// <b>Additive, never a skip.</b> This proves the mTLS peer factor gates the
/// transport. Forwarded-token validation is the independent auth-layer factor proven
/// separately; the harness presents no token, so the proof is unambiguously "mTLS
/// alone gates the channel".
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public sealed class MutualTlsSignerHarnessTests
{
    private const string _ALLOWED_WORKLOAD = "edge";
    private const string _SERVER_WORKLOAD = "d2-keycustodian";

    // -----------------------------------------------------------------------
    // Case 0 — valid leaf → the business call SUCCEEDS (both client paths)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ValidLeaf_DirectPresentation_BusinessCallRoundTrips()
    {
        SkipIfClientCertContextUnbuildable();

        const string kid = "key-001";
        const string expected_sig = "sig-base64==";
        var payload = new byte[] { 1, 2, 3 };

        using var ca = new RealCertAuthority();
        using var serverCert = ca.IssueServerCertificate(_SERVER_WORKLOAD);
        var facade = new FakeSignFixtureSignerFacade(
            D2Result<DtoSignFixtureOutput?>.Ok(new DtoSignFixtureOutput(expected_sig)));

        await using var host = await StartServerAsync(ca, serverCert, facade);
        using var clientLeaf = ca.IssueLeaf(_ALLOWED_WORKLOAD);
        using var channel = BuildDirectChannel(host.Endpoint, clientLeaf, ca.IntermediateCertificate);
        var client = new SignFixtureSigner.SignFixtureSignerClient(channel);

        var reply = await client.SignFixtureAsync(new SignFixtureRequest
        {
            Kid = kid,
            Payload = ByteString.CopyFrom(payload),
        });

        // The handshake completed AND the business call round-tripped through the
        // real generated Signer.Sign service over the real mTLS socket.
        reply.Result.Success.Should().BeTrue();
        reply.Result.StatusCode.Should().Be(200);
        reply.Data.Signature.Should().Be(expected_sig);

        // The business call actually reached the hosted service (it was not gated).
        facade.SignCallCount.Should().Be(1);
        facade.LastSignFixtureInput!.Kid.Should().Be(kid);
    }

    [Fact]
    public async Task ShippedClient_FullHandshake_BusinessCallRoundTrips()
    {
        SkipIfClientCertContextUnbuildable();

        const string kid = "key-shipped";
        const string expected_sig = "shipped-sig==";
        var payload = new byte[] { 9, 8, 7 };

        using var ca = new RealCertAuthority();
        using var serverCert = ca.IssueServerCertificate(_SERVER_WORKLOAD);
        var facade = new FakeSignFixtureSignerFacade(
            D2Result<DtoSignFixtureOutput?>.Ok(new DtoSignFixtureOutput(expected_sig)));

        await using var host = await StartServerAsync(ca, serverCert, facade);

        // The SHIPPED client-side leaf-presentation stack — AddD2WorkloadCertificateOutbound
        // (cache + refresh-ahead WorkloadLeafClient + the host-supplied issuer) AND a gRPC
        // client builder with .AddD2WorkloadCertificate() — registered against the REAL
        // loopback endpoint. Built as a plain provider (no IHost) so the refresh hosted
        // service never starts; the leaf + its chain context are produced synchronously on
        // first GetCurrentLeafAsync below, then the channel presents that chain context.
        // (This case runs on the Linux/OpenSSL deployment target — it skips on Windows.)
        await using var clientProvider = BuildShippedClientProvider(ca, host.Endpoint);

        // The shipped refresh-ahead WorkloadLeafClient builds + caches a live leaf + its
        // issuing intermediate (synchronous first call — no wall-clock timing). The cached
        // chain context is exactly what the channel's ClientCertificateContext presents.
        var leafSource = clientProvider.GetRequiredService<IWorkloadLeafSource>();
        var leafResult = await leafSource.GetCurrentLeafAsync();

        leafResult.Success.Should().BeTrue(
            because: "the shipped client builds a live leaf from the in-process issuer's material");

        var leaf = leafResult.Data!;

        // The shipped client produced a leaf carrying the correct SPIFFE identity AND
        // chaining to the CA the server trusts — i.e., a leaf the server's validator
        // accepts. The full handshake below is the end-to-end proof of exactly that.
        leaf.HasPrivateKey.Should().BeTrue();
        SpiffeUriSanOf(leaf).Should().Be("spiffe://d2.internal/workload/edge");
        LeafChainsToTrustAnchor(leaf, ca).Should().BeTrue(
            because: "the shipped client's leaf chains to the same internal CA the server pins");

        // A second call returns the same cached live handle — the shipped cache holds it,
        // which is exactly what AddD2WorkloadCertificate's selection callback presents.
        var second = await leafSource.GetCurrentLeafAsync();
        second.Success.Should().BeTrue();
        ReferenceEquals(second.Data, leaf).Should().BeTrue(
            because: "the shipped single-value cache serves one live handle to all callers");

        // Drive the FULL mTLS handshake through the SHIPPED client: the resolved gRPC
        // client dials the real loopback server, the shipped ClientCertificateContext
        // presents the full leaf -> intermediate chain, the server's default-deny SPIFFE
        // validator accepts it, and the business call round-trips. This is the deployment
        // regression pin on Linux/OpenSSL — the shipped WorkloadLeafClient.BuildSnapshot
        // builds the chain context the channel presents.
        var client = clientProvider.GetRequiredService<SignFixtureSigner.SignFixtureSignerClient>();

        var reply = await client.SignFixtureAsync(new SignFixtureRequest
        {
            Kid = kid,
            Payload = ByteString.CopyFrom(payload),
        });

        reply.Result.Success.Should().BeTrue();
        reply.Result.StatusCode.Should().Be(200);
        reply.Data.Signature.Should().Be(expected_sig);

        // The business call actually reached the hosted service over the shipped client's
        // mTLS channel (it was not gated at the handshake).
        facade.SignCallCount.Should().Be(1);
        facade.LastSignFixtureInput!.Kid.Should().Be(kid);
    }

    // -----------------------------------------------------------------------
    // Case 1 — no client certificate → REJECTED at the handshake
    // (the canary that RequireCertificate actually took)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task NoClientCertificate_RejectedAtHandshake()
    {
        using var ca = new RealCertAuthority();
        using var serverCert = ca.IssueServerCertificate(_SERVER_WORKLOAD);
        var facade = FailIfCalledFacade();

        await using var host = await StartServerAsync(ca, serverCert, facade);

        // No client certificate presented at all.
        using var channel = BuildDirectChannel(
            host.Endpoint, clientLeaf: null, issuingIntermediate: null);
        var client = new SignFixtureSigner.SignFixtureSignerClient(channel);

        var act = async () => await client.SignFixtureAsync(new SignFixtureRequest
        {
            Kid = "k",
            Payload = ByteString.CopyFrom(0x01),
        });

        await act.Should().ThrowAsync<RpcException>(
            because: "ClientCertificateMode.RequireCertificate fails the handshake "
            + "before the validation callback even runs");

        // The connection never carried the business call.
        facade.SignCallCount.Should().Be(0);
    }

    // -----------------------------------------------------------------------
    // Case 2 — wrong-CA leaf → REJECTED (chain-not-trusted conjunct)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task WrongCaLeaf_RejectedAtHandshake()
    {
        SkipIfClientCertContextUnbuildable();

        using var ca = new RealCertAuthority();
        using var foreignCa = new RealCertAuthority();
        using var serverCert = ca.IssueServerCertificate(_SERVER_WORKLOAD);
        var facade = FailIfCalledFacade();

        await using var host = await StartServerAsync(ca, serverCert, facade);

        // A well-formed SPIFFE SAN + an allowed workload, but the leaf chains to a
        // SECOND, foreign CA the server does not trust — minted by the SAME real
        // production rule, just from a different certificate authority.
        using var foreignLeaf = foreignCa.IssueLeaf(_ALLOWED_WORKLOAD);
        using var channel = BuildDirectChannel(
            host.Endpoint, foreignLeaf, foreignCa.IntermediateCertificate);
        var client = new SignFixtureSigner.SignFixtureSignerClient(channel);

        var act = async () => await client.SignFixtureAsync(new SignFixtureRequest
        {
            Kid = "k",
            Payload = ByteString.CopyFrom(0x02),
        });

        await act.Should().ThrowAsync<RpcException>(
            because: "the validator rebuilds the chain against OUR anchors with "
            + "CustomRootTrust; a foreign-CA chain fails the first conjunct");

        facade.SignCallCount.Should().Be(0);
    }

    // -----------------------------------------------------------------------
    // Case 3 — expired leaf → REJECTED (chain build rejects the past window)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExpiredLeaf_RejectedAtHandshake()
    {
        SkipIfClientCertContextUnbuildable();

        using var ca = new RealCertAuthority();
        using var serverCert = ca.IssueServerCertificate(_SERVER_WORKLOAD);
        var facade = FailIfCalledFacade();

        await using var host = await StartServerAsync(ca, serverCert, facade);

        // A real production-issued leaf whose validity window is entirely in the past
        // (the real rule derives the window from the supplied clock — a past clock
        // yields an already-expired leaf; no hand-rolling, no fake clock through TLS).
        using var expiredLeaf = ca.IssueExpiredLeaf(_ALLOWED_WORKLOAD);
        using var channel = BuildDirectChannel(
            host.Endpoint, expiredLeaf, ca.IntermediateCertificate);
        var client = new SignFixtureSigner.SignFixtureSignerClient(channel);

        var act = async () => await client.SignFixtureAsync(new SignFixtureRequest
        {
            Kid = "k",
            Payload = ByteString.CopyFrom(0x03),
        });

        await act.Should().ThrowAsync<RpcException>(
            because: "X509Chain.Build rejects an expired certificate by default");

        facade.SignCallCount.Should().Be(0);
    }

    // -----------------------------------------------------------------------
    // Case 4 — foreign-trust-domain SAN → REJECTED (SPIFFE grammar conjunct)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ForeignTrustDomainSan_RejectedAtHandshake()
    {
        SkipIfClientCertContextUnbuildable();

        using var ca = new RealCertAuthority();
        using var serverCert = ca.IssueServerCertificate(_SERVER_WORKLOAD);
        var facade = FailIfCalledFacade();

        await using var host = await StartServerAsync(ca, serverCert, facade);

        // Chains to our CA, but the SAN names a FOREIGN trust domain. Hand-rolled
        // because the production issuance rule only ever emits d2.internal and
        // physically cannot emit spiffe://prod.internal/... — this isolates the
        // trust-domain conjunct of the SPIFFE grammar.
        using var foreignSanLeaf = ca.IssueLeafWithForeignTrustDomainSan(
            _ALLOWED_WORKLOAD, "spiffe://prod.internal/workload/edge");
        using var channel = BuildDirectChannel(
            host.Endpoint, foreignSanLeaf, ca.IntermediateCertificate);
        var client = new SignFixtureSigner.SignFixtureSignerClient(channel);

        var act = async () => await client.SignFixtureAsync(new SignFixtureRequest
        {
            Kid = "k",
            Payload = ByteString.CopyFrom(0x04),
        });

        await act.Should().ThrowAsync<RpcException>(
            because: "the SPIFFE grammar hard-rejects any non-d2.internal trust domain");

        facade.SignCallCount.Should().Be(0);
    }

    // -----------------------------------------------------------------------
    // Case 5 — unknown workload → REJECTED (allowed-set conjunct)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UnknownWorkload_RejectedAtHandshake()
    {
        SkipIfClientCertContextUnbuildable();

        using var ca = new RealCertAuthority();
        using var serverCert = ca.IssueServerCertificate(_SERVER_WORKLOAD);
        var facade = FailIfCalledFacade();

        await using var host = await StartServerAsync(ca, serverCert, facade);

        // A valid, real production-issued leaf with a well-formed
        // spiffe://d2.internal/workload/ghost SAN, chaining to our CA — but "ghost"
        // is not in AllowedWorkloads = ["edge"].
        using var ghostLeaf = ca.IssueLeaf("ghost");
        using var channel = BuildDirectChannel(
            host.Endpoint, ghostLeaf, ca.IntermediateCertificate);
        var client = new SignFixtureSigner.SignFixtureSignerClient(channel);

        var act = async () => await client.SignFixtureAsync(new SignFixtureRequest
        {
            Kid = "k",
            Payload = ByteString.CopyFrom(0x05),
        });

        await act.Should().ThrowAsync<RpcException>(
            because: "the allowed-workload set is the third default-deny conjunct");

        facade.SignCallCount.Should().Be(0);
    }

    // -----------------------------------------------------------------------
    // Host + channel helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads the single URI subject-alternative-name from a certificate (the shipped
    /// client's leaf carries exactly one — the SPIFFE SVID).
    /// </summary>
    /// <param name="leaf">The leaf certificate.</param>
    /// <returns>The URI SAN string, or empty if none.</returns>
    private static string SpiffeUriSanOf(X509Certificate2 leaf)
    {
        var san = leaf.Extensions
            .OfType<X509SubjectAlternativeNameExtension>()
            .FirstOrDefault();

        if (san is null)
            return string.Empty;

        // Format(false) yields e.g. "URL=spiffe://d2.internal/workload/edge"; strip the label.
        var formatted = san.Format(multiLine: false);
        var marker = formatted.IndexOf("spiffe://", StringComparison.Ordinal);

        return marker < 0 ? string.Empty : formatted[marker..].Trim();
    }

    /// <summary>
    /// Returns whether <paramref name="leaf"/> chains to the certificate authority's
    /// root — proving the shipped client produced a leaf the server's validator (which
    /// rebuilds against the same root with custom trust) would accept.
    /// </summary>
    /// <param name="leaf">The leaf to validate.</param>
    /// <param name="ca">The certificate authority whose root + intermediate anchor the chain.</param>
    /// <returns><c>true</c> when the leaf chains to the CA root.</returns>
    private static bool LeafChainsToTrustAnchor(X509Certificate2 leaf, RealCertAuthority ca)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(ca.TrustAnchors());
        chain.ChainPolicy.ExtraStore.Add(ca.IntermediateCertificate);

        return chain.Build(leaf);
    }

    /// <summary>
    /// A façade that fails the test if its sign path is ever invoked — used by every
    /// reject case to make "the business call never ran" an explicit assertion (the
    /// connection is refused at the TLS layer, before any app logic).
    /// </summary>
    /// <returns>A façade whose canned result is an irrelevant Ok (it must never be reached).</returns>
    private static FakeSignFixtureSignerFacade FailIfCalledFacade() =>
        new(D2Result<DtoSignFixtureOutput?>.Ok(new DtoSignFixtureOutput("unreachable")));

    /// <summary>
    /// Skips a cert-presenting case on Windows. .NET builds an
    /// <see cref="SslStreamCertificateContext"/> for the presented client leaf during
    /// the handshake; on Windows-Schannel that build throws for a leaf chaining to the
    /// (non-OS-trusted) internal CA — even a bare leaf, even with the intermediate
    /// supplied (empirically proven; Microsoft: "for a client application the only
    /// solution is to add the certificates to the certificate store manually"). A clean
    /// Windows box thus cannot present a private-CA client leaf over a real socket
    /// without OS-store mutation, which this harness refuses to do. These cases run on
    /// the Linux/OpenSSL deployment target (full chain on the wire); the validator's
    /// full conjunct matrix is proven cross-platform by the SpiffeSanPeerValidator unit
    /// tests. The no-client-certificate case is exempt — it builds no client context.
    /// </summary>
    private static void SkipIfClientCertContextUnbuildable() =>
        Assert.SkipWhen(
            OperatingSystem.IsWindows(),
            "Windows-Schannel cannot build a client-cert context for a leaf chaining to "
            + "a non-OS-trusted CA without installing the root into the OS store (which "
            + "this harness refuses to do). This real-socket client-presentation case "
            + "runs on the Linux/OpenSSL deployment target; the validator's conjuncts are "
            + "proven cross-platform by the SpiffeSanPeerValidator unit matrix.");

    /// <summary>
    /// Starts a real Kestrel HTTPS host on <c>127.0.0.1:0</c> wired with the shipped
    /// <c>AddD2MutualTls</c> (require + validate a client certificate) and hosting the
    /// generated <c>Signer.Sign</c> service delegating to the supplied façade. The
    /// real-socket host plumbing lives in the shared <see cref="GrpcTestHost"/> test-infra
    /// helper; this harness supplies the mTLS-specific registration (the façade singleton
    /// + <c>AddD2MutualTls</c>) and the service map. The helper invokes the registration
    /// BEFORE its <c>ConfigureKestrel</c>/<c>UseHttps</c>, preserving the
    /// <c>AddD2MutualTls</c>-before-Kestrel ordering the client-certificate require depends
    /// on.
    /// </summary>
    /// <param name="ca">The certificate authority whose public root the host trusts.</param>
    /// <param name="serverCert">The Kestrel server certificate (a leaf with server EKU).</param>
    /// <param name="facade">The in-process façade the generated service delegates to.</param>
    /// <returns>The running host + the loopback endpoint the channel dials.</returns>
    private static Task<GrpcTestHost.RunningServer> StartServerAsync(
        RealCertAuthority ca,
        X509Certificate2 serverCert,
        FakeSignFixtureSignerFacade facade) =>
        GrpcTestHost.StartAsync(
            serverCert,
            services =>
            {
                services.AddSingleton<ISignFixtureSignerFacade>(facade);

                // The SHIPPED server wiring, exercised live. The helper runs this BEFORE
                // ConfigureKestrel so its ConfigureHttpsDefaults action (RequireCertificate
                // + the validation callback) is in place when the per-listener UseHttps
                // applies the HTTPS defaults — the per-listener server cert composes with,
                // and does not reset, the client-certificate require + validate.
                services.AddD2MutualTls(o =>
                {
                    o.Enabled = true;
                    o.AllowedWorkloads = [_ALLOWED_WORKLOAD];
                    o.TrustAnchorsProvider = ca.TrustAnchors;
                });
            },
            app => app.MapGrpcService<SignFixtureSignerService>());

    /// <summary>
    /// Builds a gRPC channel that dials the loopback endpoint over a real socket,
    /// presenting <paramref name="clientLeaf"/> + its issuing intermediate as a full
    /// chain on the TLS handshake (or no client certificate when null). The client
    /// trusts the loopback server certificate via <c>RemoteCertificateValidationCallback</c>
    /// — client-side test plumbing for the self-signed loopback server cert, which does
    /// NOT weaken the server's mutual-TLS client-certificate check (the property under
    /// test).
    /// </summary>
    /// <remarks>
    /// The leaf is presented via a <see cref="SslStreamCertificateContext"/> carrying
    /// the issuing intermediate, so the peer receives the full <c>leaf → intermediate</c>
    /// chain and the server validator's root-anchored rebuild can complete from the
    /// presented chain (offline: no AIA / network fetch). This is exercised on the
    /// Linux/OpenSSL deployment target; on Windows-Schannel the context build throws for
    /// a non-OS-trusted-root leaf, so the cert-presenting cases skip on Windows (see
    /// <see cref="SkipIfClientCertContextUnbuildable"/>).
    /// </remarks>
    /// <param name="endpoint">The loopback HTTPS endpoint.</param>
    /// <param name="clientLeaf">The client leaf to present, or null for the no-cert case.</param>
    /// <param name="issuingIntermediate">The intermediate that signed the leaf (presented alongside it), or null for the no-cert case.</param>
    /// <returns>A configured <see cref="GrpcChannel"/>.</returns>
    private static GrpcChannel BuildDirectChannel(
        Uri endpoint, X509Certificate2? clientLeaf, X509Certificate2? issuingIntermediate) =>
        GrpcTestHost.BuildChannel(
            endpoint,
            sslOptions =>
            {
                if (clientLeaf is not null)
                {
                    // Present the full leaf → intermediate chain so the peer's validator
                    // can rebuild a root-anchored chain (offline: no AIA / network fetch).
                    // On Windows this Create throws for a non-OS-trusted-root leaf, so the
                    // cert-presenting cases skip there; this runs on the Linux deployment
                    // target.
                    sslOptions.ClientCertificateContext = SslStreamCertificateContext.Create(
                        clientLeaf,
                        issuingIntermediate is null ? null : [issuingIntermediate],
                        offline: true);
                }
            });

    /// <summary>
    /// Builds a DI provider wiring the SHIPPED client-side leaf-presentation stack —
    /// <c>AddD2WorkloadCertificateOutbound</c> (the single-value cache + refresh-ahead
    /// <c>WorkloadLeafClient</c> + the host-supplied issuer) plus a gRPC client builder
    /// with <c>.AddD2WorkloadCertificate()</c> (the per-channel leaf-from-cache
    /// presentation opt-in). The in-process issuer mints from the SAME CA the server
    /// trusts. Built as a plain provider (no IHost) so the refresh-ahead hosted service
    /// never starts — the leaf is produced synchronously on first
    /// <c>GetCurrentLeafAsync</c>, sidestepping any wall-clock timing.
    /// </summary>
    /// <remarks>
    /// A trailing <c>ConfigureChannel</c> composes onto the handler the shipped
    /// <c>AddD2WorkloadCertificate</c> created (compose-don't-clobber order) and sets the
    /// client-side <see cref="SslClientAuthenticationOptions.RemoteCertificateValidationCallback"/>
    /// to trust the loopback test-CA SERVER cert (not in the machine store). This is
    /// CLIENT-side test plumbing ONLY — identical to <see cref="BuildDirectChannel"/>; it
    /// does NOT relax the server's mutual-TLS client-certificate validation, which is the
    /// property under test. The shipped selection callback that presents the leaf is left
    /// fully intact.
    /// </remarks>
    /// <param name="ca">The CA the in-process issuer mints leaves from (the one the server trusts).</param>
    /// <param name="endpoint">The real loopback HTTPS endpoint the registered gRPC client dials.</param>
    /// <returns>The configured DI provider.</returns>
    [SuppressMessage(
        "Security",
        "CA5359:Do not disable certificate validation",
        Justification = "Client-side trust of the loopback test-CA SERVER cert only (it "
            + "is not in the machine store). This is the CLIENT validating the SERVER; it "
            + "does NOT relax the SERVER's mutual-TLS client-certificate validation, which "
            + "is the property under test. Test harness, loopback only.")]
    private static ServiceProvider BuildShippedClientProvider(RealCertAuthority ca, Uri endpoint)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // The shipped client-side workload-leaf presentation stack (cache + refresh-
        // ahead leaf client + hosted service). The hosted service is never started.
        services.AddD2WorkloadCertificateOutbound();

        // The host-supplied issuer port — an in-process issuer minting a real leaf for
        // the allowed workload from the SAME CA the server trusts.
        services.AddSingleton<IWorkloadCertificateIssuer>(new SeedingIssuer(ca, _ALLOWED_WORKLOAD));

        // The per-channel presentation opt-in (the SHIPPED selection callback that
        // presents the cached leaf), then a trailing ConfigureChannel that augments the
        // SAME handler with client-side trust of the loopback server cert (test plumbing
        // only — see the remarks). ConfigureChannel callbacks run in registration order
        // and share one GrpcChannelOptions, so the trust callback lands on the handler
        // the shipped extension created.
        services
            .AddGrpcClient<SignFixtureSigner.SignFixtureSignerClient>(o => o.Address = endpoint)
            .AddD2WorkloadCertificate()
            .ConfigureChannel((_, options) =>
            {
                var handler = options.HttpHandler as SocketsHttpHandler
                    ?? new SocketsHttpHandler();

                handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
                options.HttpHandler = handler;
            });

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// An in-process <see cref="IWorkloadCertificateIssuer"/> that mints a real leaf
    /// (via the production issuance rule) from the test CA, returning the neutral
    /// <see cref="WorkloadLeafMaterial"/> DER + PKCS#8 shape the shipped client builds
    /// a live leaf from. The harness's drop-in for the host-supplied issuer adapter.
    /// </summary>
    private sealed class SeedingIssuer(RealCertAuthority ca, string serviceId)
        : IWorkloadCertificateIssuer
    {
        public ValueTask<D2Result<WorkloadLeafMaterial>> IssueAsync(CancellationToken ct = default) =>
            new(D2Result<WorkloadLeafMaterial>.Ok(ca.IssueLeafMaterial(serviceId)));
    }
}
