// -----------------------------------------------------------------------
// <copyright file="GrpcCertificateAuthorityServiceTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpc;

using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using D2.Edge.KeyCustodian.Clients;
using D2.Edge.Tests.TypeSpecGrpc.Generated;
using D2.Services.Protos.KeyCustodian.V2Alpha;
using D2.Shared.Result;
using Google.Protobuf;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ClientsGetCaCertificateOutput = D2.Edge.KeyCustodian.Clients.GetCaCertificateOutput;
using ClientsGetKeyringOutput = D2.Edge.KeyCustodian.Clients.GetKeyringOutput;
using ClientsIssueLeafOutput = D2.Edge.KeyCustodian.Clients.IssueLeafOutput;
using ClientsSignOutput = D2.Edge.KeyCustodian.Clients.SignOutput;

/// <summary>
/// In-memory gRPC harness tests for the TypeSpec-emitted
/// <c>KeyCustodianCertificateAuthorityService</c> (the <c>IssueWorkloadCertificate</c>
/// wire method) + <c>KeyCustodianCaCertificateService</c> (the <c>GetCaCertificate</c>
/// wire method) — one gRPC service per op. Hosts the generated services via
/// <see cref="TestServer"/> and dials them over an in-process
/// <see cref="GrpcChannel"/> — no sockets. The issuance round-trip drives a REAL
/// caller-side keypair + CSR against a real production-rule-backed signer: the CSR
/// bytes cross the wire intact, the returned leaf certifies the caller's key and
/// chains to the served CA, the validity window survives the ISO-8601 wire form,
/// and the CUSTODY PROOF holds — the wire surface is structurally private-key-free
/// and a structured-log capture of a full issuance never contains the caller's
/// private key (which never left this test).
/// </summary>
public sealed class GrpcCertificateAuthorityServiceTests
{
    // -----------------------------------------------------------------------
    // Issue over the wire — CSR in, leaf + issuer out, envelope fidelity
    // -----------------------------------------------------------------------

    [Fact]
    public async Task IssueWorkloadCertificate_ValidCsr_LeafPairsWithCallerKey_AndChains()
    {
        using var ca = new RealCertAuthority();
        var facade = new CaBackedFacade(ca, "edge");

        using var localKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var csrDer = new CertificateRequest(
            "CN=d2-workload", localKey, HashAlgorithmName.SHA256).CreateSigningRequest();

        using var host = await BuildHost(facade);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianCertificateAuthority
            .KeyCustodianCertificateAuthorityClient(channel);

        var reply = await client.IssueWorkloadCertificateAsync(
            new IssueWorkloadCertificateRequest { CsrDer = ByteString.CopyFrom(csrDer) });

        reply.Result.Success.Should().BeTrue();
        reply.Result.StatusCode.Should().Be(200);

        // The CSR bytes crossed the seam verbatim (§1.32 capture-assert).
        facade.LastCsrDer.Should().Equal(csrDer);

        // The returned leaf certifies the CALLER-side key…
        using var leaf = X509CertificateLoader.LoadCertificate(
            reply.Data.CertificateDer.ToByteArray());
        leaf.PublicKey.ExportSubjectPublicKeyInfo().Should().Equal(
            localKey.ExportSubjectPublicKeyInfo(),
            "the leaf pairs with the caller-generated key across the wire");

        // …and chains to the CA that signed it (issuer rides the response).
        using var intermediate = X509CertificateLoader.LoadCertificate(
            reply.Data.IssuerCertificateDer.ToByteArray());
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(ca.RootCertificate);
        chain.ChainPolicy.ExtraStore.Add(intermediate);
        chain.Build(leaf).Should().BeTrue("the wire-carried leaf chains to the CA root");

        // The validity window survives the ISO-8601 round-trip wire form
        // (utcDateTime rides the proto wire as a string; §25 adversarial temporal —
        // the parsed wire values must equal the leaf's own X.509 window exactly).
        var notBefore = DateTimeOffset.Parse(
            reply.Data.NotBefore,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind);
        var notAfter = DateTimeOffset.Parse(
            reply.Data.NotAfter,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind);
        notBefore.Should().Be(new DateTimeOffset(leaf.NotBefore.ToUniversalTime()));
        notAfter.Should().Be(new DateTimeOffset(leaf.NotAfter.ToUniversalTime()));
        notAfter.Should().BeAfter(notBefore);
    }

    [Fact]
    public async Task IssueWorkloadCertificate_BusinessDeny_RidesEnvelopeWith403()
    {
        var facade = new FailingFacade(
            KeyCustodianFailures<ClientsIssueLeafOutput?>.IssuanceNotAuthorized());

        using var host = await BuildHost(facade);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianCertificateAuthority
            .KeyCustodianCertificateAuthorityClient(channel);

        var reply = await client.IssueWorkloadCertificateAsync(
            new IssueWorkloadCertificateRequest { CsrDer = ByteString.CopyFrom([0x30]) });

        reply.Result.Success.Should().BeFalse();
        reply.Result.StatusCode.Should().Be(403);
        reply.Data.Should().BeNull();
    }

    [Fact]
    public async Task IssueWorkloadCertificate_InvalidCsrReject_RidesEnvelopeWith400()
    {
        var facade = new FailingFacade(
            KeyCustodianFailures<ClientsIssueLeafOutput?>.InvalidCsr());

        using var host = await BuildHost(facade);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianCertificateAuthority
            .KeyCustodianCertificateAuthorityClient(channel);

        var reply = await client.IssueWorkloadCertificateAsync(
            new IssueWorkloadCertificateRequest { CsrDer = ByteString.CopyFrom([0xFF]) });

        reply.Result.Success.Should().BeFalse();
        reply.Result.StatusCode.Should().Be(400);
        reply.Data.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // GetCaCertificate over the wire
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetCaCertificate_ReturnsChain_BothTiersParse()
    {
        using var ca = new RealCertAuthority();
        var facade = new CaBackedFacade(ca, "edge");

        using var host = await BuildHost(facade);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianCaCertificate.KeyCustodianCaCertificateClient(channel);

        var reply = await client.GetCaCertificateAsync(new GetCaCertificateRequest());

        reply.Result.Success.Should().BeTrue();
        reply.Result.StatusCode.Should().Be(200);

        using var root = X509CertificateLoader.LoadCertificate(
            reply.Data.RootCertificateDer.ToByteArray());
        using var intermediate = X509CertificateLoader.LoadCertificate(
            reply.Data.IntermediateCertificateDer.ToByteArray());

        root.RawData.Should().Equal(ca.RootCertificate.RawData);
        intermediate.RawData.Should().Equal(ca.IntermediateCertificate.RawData);
    }

    [Fact]
    public async Task GetCaCertificate_Unavailable_RidesEnvelopeWith503()
    {
        var facade = new FailingFacade(
            KeyCustodianFailures<ClientsIssueLeafOutput?>.IssuanceNotAuthorized(),
            KeyCustodianFailures<ClientsGetCaCertificateOutput?>.NoActiveIssuingCa());

        using var host = await BuildHost(facade);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianCaCertificate.KeyCustodianCaCertificateClient(channel);

        var reply = await client.GetCaCertificateAsync(new GetCaCertificateRequest());

        reply.Result.Success.Should().BeFalse();
        reply.Result.StatusCode.Should().Be(503);
        reply.Data.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // Proto round-trip for the new messages
    // -----------------------------------------------------------------------

    [Fact]
    public void IssueWorkloadCertificateMessages_RoundTripBytes()
    {
        var request = new IssueWorkloadCertificateRequest
        {
            CsrDer = ByteString.CopyFrom([0x30, 0x82, 0x01]),
        };
        IssueWorkloadCertificateRequest.Parser.ParseFrom(request.ToByteArray())
            .CsrDer.ToByteArray().Should().Equal(request.CsrDer.ToByteArray());

        var output = new D2.Services.Protos.KeyCustodian.V2Alpha.IssueLeafOutput
        {
            CertificateDer = ByteString.CopyFrom([0x01]),
            IssuerCertificateDer = ByteString.CopyFrom([0x02]),
            NotBefore = "2026-01-01T00:00:00.0000000+00:00",
            NotAfter = "2026-01-02T00:00:00.0000000+00:00",
        };
        var parsed = D2.Services.Protos.KeyCustodian.V2Alpha.IssueLeafOutput.Parser
            .ParseFrom(output.ToByteArray());
        parsed.Should().Be(output);
    }

    [Fact]
    public void GetCaCertificateMessages_RoundTripBytes()
    {
        var output = new D2.Services.Protos.KeyCustodian.V2Alpha.GetCaCertificateOutput
        {
            RootCertificateDer = ByteString.CopyFrom([0x01]),
            IntermediateCertificateDer = ByteString.CopyFrom([0x02]),
        };
        var parsed = D2.Services.Protos.KeyCustodian.V2Alpha.GetCaCertificateOutput.Parser
            .ParseFrom(output.ToByteArray());
        parsed.Should().Be(output);

        new GetCaCertificateRequest().CalculateSize().Should().Be(
            0, "the parameterless request serializes to zero bytes");
    }

    // -----------------------------------------------------------------------
    // Custody proof — the wire surface is structurally private-key-free
    // -----------------------------------------------------------------------

    [Fact]
    public void IssuanceWireSurface_HasNoPrivateKeyMember_Structural()
    {
        // DTOs (the C# wire shapes).
        foreach (var dto in new[]
                 {
                     typeof(IssueLeafInput),
                     typeof(ClientsIssueLeafOutput),
                     typeof(GetCaCertificateInput),
                     typeof(ClientsGetCaCertificateOutput),
                 })
        {
            dto.GetProperties().Should().NotContain(
                p => p.Name.Contains("PrivateKey") || p.Name.Contains("Pkcs8"),
                $"{dto.Name} is all-public wire material");
        }

        // Proto messages (the wire-format field names).
        foreach (var descriptor in new[]
                 {
                     IssueWorkloadCertificateRequest.Descriptor,
                     IssueWorkloadCertificateResponse.Descriptor,
                     D2.Services.Protos.KeyCustodian.V2Alpha.IssueLeafOutput.Descriptor,
                     GetCaCertificateRequest.Descriptor,
                     GetCaCertificateResponse.Descriptor,
                     D2.Services.Protos.KeyCustodian.V2Alpha.GetCaCertificateOutput.Descriptor,
                 })
        {
            descriptor.Fields.InDeclarationOrder()
                .Should().NotContain(
                    f => f.Name.Contains("private_key") || f.Name.Contains("pkcs8"),
                    $"proto message {descriptor.Name} carries no private-key field");
        }
    }

    [Fact]
    public async Task FullIssuance_StructuredLogCapture_NeverContainsTheCallerPrivateKey()
    {
        // The caller's private key never leaves this test — a full wire issuance
        // must not surface it in ANY log record on the serving host.
        using var ca = new RealCertAuthority();
        var facade = new CaBackedFacade(ca, "edge");
        var logSink = new CapturingLoggerProvider();

        using var localKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var csrDer = new CertificateRequest(
            "CN=d2-workload", localKey, HashAlgorithmName.SHA256).CreateSigningRequest();
        var privateKeyHex = Convert.ToHexString(localKey.ExportPkcs8PrivateKey());

        using var host = await BuildHost(facade, logSink);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianCertificateAuthority
            .KeyCustodianCertificateAuthorityClient(channel);

        var reply = await client.IssueWorkloadCertificateAsync(
            new IssueWorkloadCertificateRequest { CsrDer = ByteString.CopyFrom(csrDer) });

        reply.Result.Success.Should().BeTrue();
        logSink.Messages.Should().NotContain(
            m => m.Contains(privateKeyHex, StringComparison.OrdinalIgnoreCase),
            "the leaf private key never existed on the serving side");
    }

    // -----------------------------------------------------------------------
    // Host plumbing
    // -----------------------------------------------------------------------

    private static async Task<IHost> BuildHost(
        IKeyCustodianApi facade, CapturingLoggerProvider? logSink = null)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddSingleton(facade);
                    services.AddRouting();
                    services.AddGrpc();

                    if (logSink is not null)
                        services.AddLogging(b => b.AddProvider(logSink));
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGrpcService<KeyCustodianCertificateAuthorityService>();
                        endpoints.MapGrpcService<KeyCustodianCaCertificateService>();
                    });
                });
            })
            .Build();

        await host.StartAsync();
        return host;
    }

    private static GrpcChannel CreateChannel(IHost host)
    {
        var httpClient = host.GetTestClient();
        return GrpcChannel.ForAddress(
            httpClient.BaseAddress!,
            new GrpcChannelOptions { HttpClient = httpClient });
    }

    /// <summary>
    /// Façade backed by the REAL production certificate rules via
    /// <see cref="RealCertAuthority"/>: <c>IssueLeafAsync</c> verifies + signs the
    /// received CSR (subject ignored — the SAN comes from the configured peer view)
    /// and captures the received bytes for the seam assertion;
    /// <c>GetCaCertificateAsync</c> serves the fixture's real chain. The other arms
    /// are unused here.
    /// </summary>
    private sealed class CaBackedFacade(RealCertAuthority ca, string serviceId)
        : IKeyCustodianApi
    {
        public byte[]? LastCsrDer { get; private set; }

        public ValueTask<D2Result<ClientsIssueLeafOutput?>> IssueLeafAsync(
            IssueLeafInput input, CancellationToken ct = default)
        {
            LastCsrDer = input.CsrDer;

            var material = ca.IssueLeafMaterial(input.CsrDer, serviceId);

            using var leaf = X509CertificateLoader.LoadCertificate(material.CertificateDer);

            return ValueTask.FromResult(D2Result<ClientsIssueLeafOutput?>.Ok(
                new ClientsIssueLeafOutput(
                    material.CertificateDer,
                    material.IssuerCertificateDer,
                    new DateTimeOffset(leaf.NotBefore.ToUniversalTime()),
                    new DateTimeOffset(leaf.NotAfter.ToUniversalTime()))));
        }

        public ValueTask<D2Result<ClientsGetCaCertificateOutput?>> GetCaCertificateAsync(
            GetCaCertificateInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<ClientsGetCaCertificateOutput?>.Ok(
                new ClientsGetCaCertificateOutput(
                    ca.RootCertificate.RawData,
                    ca.IntermediateCertificate.RawData)));

        public ValueTask<D2Result<GetJwksOutput?>> GetJwksAsync(
            GetJwksInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<GetJwksOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<GetOidcConfigurationOutput?>> GetOidcConfigurationAsync(
            GetOidcConfigurationInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<GetOidcConfigurationOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<ClientsSignOutput?>> SignAsync(
            SignInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<ClientsSignOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<ClientsGetKeyringOutput?>> GetKeyringAsync(
            GetKeyringInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<ClientsGetKeyringOutput?>.ServiceUnavailable());
    }

    /// <summary>
    /// Façade whose issuance / CA-certificate arms return fixed failures — drives the
    /// envelope-fidelity pins (business failures ride the envelope, never thrown).
    /// </summary>
    private sealed class FailingFacade(
        D2Result<ClientsIssueLeafOutput?> issueResult,
        D2Result<ClientsGetCaCertificateOutput?>? caCertResult = null)
        : IKeyCustodianApi
    {
        public ValueTask<D2Result<ClientsIssueLeafOutput?>> IssueLeafAsync(
            IssueLeafInput input, CancellationToken ct = default)
            => ValueTask.FromResult(issueResult);

        public ValueTask<D2Result<ClientsGetCaCertificateOutput?>> GetCaCertificateAsync(
            GetCaCertificateInput input, CancellationToken ct = default)
            => ValueTask.FromResult(
                caCertResult
                ?? D2Result<ClientsGetCaCertificateOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<GetJwksOutput?>> GetJwksAsync(
            GetJwksInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<GetJwksOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<GetOidcConfigurationOutput?>> GetOidcConfigurationAsync(
            GetOidcConfigurationInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<GetOidcConfigurationOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<ClientsSignOutput?>> SignAsync(
            SignInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<ClientsSignOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<ClientsGetKeyringOutput?>> GetKeyringAsync(
            GetKeyringInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<ClientsGetKeyringOutput?>.ServiceUnavailable());
    }

    /// <summary>Captures every rendered log message emitted by the serving host.</summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => messages.Enqueue(formatter(state, exception));
        }
    }
}
