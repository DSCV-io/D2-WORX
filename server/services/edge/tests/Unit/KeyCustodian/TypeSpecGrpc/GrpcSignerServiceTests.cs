// -----------------------------------------------------------------------
// <copyright file="GrpcSignerServiceTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpc;

using System.Threading.Tasks;
using D2.Edge.KeyCustodian.Client.CaCertificate;
using D2.Edge.KeyCustodian.Client.Facade;
using D2.Edge.KeyCustodian.Client.Issuance;
using D2.Edge.KeyCustodian.Client.Jwks;
using D2.Edge.KeyCustodian.Client.Keyring;
using D2.Edge.KeyCustodian.Client.OidcConfiguration;
using D2.Edge.KeyCustodian.Client.Signing;
using D2.Edge.Tests.TypeSpecGrpc.Generated;
using D2.Services.Protos.KeyCustodian.V2Alpha;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ClientsGetCaCertificateOutput = D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateOutput;
using ClientsIssueLeafOutput = D2.Edge.KeyCustodian.Client.Issuance.IssueLeafOutput;
using ClientsSignOutput = D2.Edge.KeyCustodian.Client.Signing.SignOutput;

/// <summary>
/// In-memory gRPC harness tests for the REAL TypeSpec-emitted
/// <c>KeyCustodianSignerService</c> + <c>SignTransportMappers</c> pair (the first
/// non-fixture gRPC wire surface). Hosts the generated service via
/// <see cref="TestServer"/> and dials it over an in-process <see cref="GrpcChannel"/> —
/// no sockets. The service delegates through the generated <see cref="IKeyCustodianApi"/>
/// façade; the response carries the <c>D2ResultProto</c> envelope (field 1) + typed
/// <c>SignOutput</c> data (field 2). gRPC status stays <see cref="StatusCode.OK"/> for
/// every business result — business failures ride the envelope, never thrown.
/// </summary>
public sealed class GrpcSignerServiceTests
{
    [Fact]
    public async Task Sign_Success_ReturnsEnvelopeWithSignatureAndKid()
    {
        const string kid = "kid-001";
        const string expectedSig = "c2lnbmF0dXJl";
        var signingInput = new byte[] { 1, 2, 3 };

        var fakeFacade = new FakeKeyCustodianApi(
            D2Result<ClientsSignOutput?>.Ok(new ClientsSignOutput(expectedSig, kid)));

        using var host = await BuildHost(fakeFacade);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianSigner.KeyCustodianSignerClient(channel);

        var reply = await client.SignAsync(new SignRequest
        {
            KeyDomain = "audit",
            SigningInput = ByteString.CopyFrom(signingInput),
        });

        reply.Result.Success.Should().BeTrue();
        reply.Result.StatusCode.Should().Be(200);
        reply.Data.Signature.Should().Be(expectedSig);
        reply.Data.Kid.Should().Be(kid);
        fakeFacade.SignCallCount.Should().Be(1);
        fakeFacade.LastSignInput!.KeyDomain.Should().Be("audit");
        fakeFacade.LastSignInput.SigningInput.Should().Equal(signingInput);
    }

    [Fact]
    public async Task Sign_BusinessFailure_RidesEnvelopeWithRealCode()
    {
        // The cluster-signing root is reachable only via the minter capability: the general
        // surface returns MinterCapabilityRequired (403). The real code rides the envelope
        // over gRPC; the transport-layer call still succeeds (StatusCode.OK, no throw).
        var fakeFacade = new FakeKeyCustodianApi(
            KeyCustodianFailures<ClientsSignOutput?>.MinterCapabilityRequired());

        using var host = await BuildHost(fakeFacade);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianSigner.KeyCustodianSignerClient(channel);

        var reply = await client.SignAsync(new SignRequest
        {
            KeyDomain = "jwks-signing",
            SigningInput = ByteString.CopyFrom(0xDE, 0xAD),
        });

        reply.Result.Success.Should().BeFalse();
        reply.Result.StatusCode.Should().Be(403);

        // proto3 sub-message fields are null when the sender does not set them.
        reply.Data.Should().BeNull();
    }

    [Fact]
    public async Task Sign_SigningKeyUnavailable_RidesEnvelopeWith503()
    {
        var fakeFacade = new FakeKeyCustodianApi(
            KeyCustodianFailures<ClientsSignOutput?>.SigningKeyUnavailable());

        using var host = await BuildHost(fakeFacade);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianSigner.KeyCustodianSignerClient(channel);

        var reply = await client.SignAsync(new SignRequest
        {
            KeyDomain = "audit",
            SigningInput = ByteString.CopyFrom(0x01),
        });

        reply.Result.Success.Should().BeFalse();
        reply.Result.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task Sign_DelegatesThroughFacade_RecordsCallCount()
    {
        var fakeFacade = new FakeKeyCustodianApi(
            D2Result<ClientsSignOutput?>.Ok(new ClientsSignOutput("c2ln", "kid")));

        using var host = await BuildHost(fakeFacade);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianSigner.KeyCustodianSignerClient(channel);

        await client.SignAsync(new SignRequest { KeyDomain = "audit", SigningInput = ByteString.CopyFrom(0x01) });
        await client.SignAsync(new SignRequest { KeyDomain = "audit", SigningInput = ByteString.CopyFrom(0x02) });

        fakeFacade.SignCallCount.Should().Be(2);
    }

    [Fact]
    public void SignInput_SigningInputField_CarriesRedactData()
    {
        // The signing input is secret-adjacent material: the generated DTO field carries
        // [RedactData] so it is masked in structured logs (proof of @d2Redact).
        var property = typeof(ClientsSignOutput).Assembly
            .GetType("D2.Edge.KeyCustodian.Client.Signing.SignInput")!
            .GetProperty("SigningInput");

        property.Should().NotBeNull();
        property.GetCustomAttributes(
                typeof(D2.Shared.Utilities.Attributes.RedactDataAttribute), inherit: false)
            .Should().ContainSingle("the signing input is redacted in logs (@d2Redact)");
    }

    private static async Task<IHost> BuildHost(IKeyCustodianApi facade)
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
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGrpcService<KeyCustodianSignerService>();
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
    /// Fake <see cref="IKeyCustodianApi"/> façade for the gRPC harness — returns a fixed
    /// sign result and records the call so delegation + input fidelity can be asserted. The
    /// JWKS / OIDC arms are unused here (the service under test only routes <c>Sign</c>).
    /// </summary>
    private sealed class FakeKeyCustodianApi(D2Result<ClientsSignOutput?> signResult) : IKeyCustodianApi
    {
        public int SignCallCount { get; private set; }

        public SignInput? LastSignInput { get; private set; }

        public ValueTask<D2Result<ClientsSignOutput?>> SignAsync(
            SignInput input, CancellationToken ct = default)
        {
            SignCallCount++;
            LastSignInput = input;
            return ValueTask.FromResult(signResult);
        }

        public ValueTask<D2Result<GetJwksOutput?>> GetJwksAsync(
            GetJwksInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<GetJwksOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<GetOidcConfigurationOutput?>> GetOidcConfigurationAsync(
            GetOidcConfigurationInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<GetOidcConfigurationOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<D2.Edge.KeyCustodian.Client.Keyring.GetKeyringOutput?>> GetKeyringAsync(
            GetKeyringInput input, CancellationToken ct = default)
            => ValueTask.FromResult(
                D2Result<D2.Edge.KeyCustodian.Client.Keyring.GetKeyringOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<ClientsIssueLeafOutput?>> IssueLeafAsync(
            IssueLeafInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<ClientsIssueLeafOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<ClientsGetCaCertificateOutput?>> GetCaCertificateAsync(
            GetCaCertificateInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<ClientsGetCaCertificateOutput?>.ServiceUnavailable());
    }
}
