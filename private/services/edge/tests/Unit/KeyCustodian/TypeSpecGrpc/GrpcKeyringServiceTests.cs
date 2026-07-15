// -----------------------------------------------------------------------
// <copyright file="GrpcKeyringServiceTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpc;

using System.Threading.Tasks;
using DcsvIo.D2.Private.Edge.Api.Grpc.KeyCustodian;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.CaCertificate;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Facade;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Issuance;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Jwks;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.OidcConfiguration;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Signing;
using global::D2.Services.Protos.KeyCustodian.V2Alpha;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ClientsGetCaCertificateOutput = DcsvIo.D2.Private.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateOutput;
using ClientsGetKeyringOutput = DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring.GetKeyringOutput;
using ClientsIssueLeafOutput = DcsvIo.D2.Private.Edge.KeyCustodian.Client.Issuance.IssueLeafOutput;
using ClientsKeyringEntry = DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring.KeyringEntry;
using ClientsSignOutput = DcsvIo.D2.Private.Edge.KeyCustodian.Client.Signing.SignOutput;

/// <summary>
/// In-memory gRPC harness tests for the REAL TypeSpec-emitted
/// <c>KeyCustodianKeyringService</c> + <c>GetKeyringTransportMappers</c> pair (a DISTINCT
/// service from the signer). Hosts the generated service via <see cref="TestServer"/> and
/// dials it over an in-process <see cref="GrpcChannel"/> — no sockets. The service delegates
/// through the generated <see cref="IKeyCustodianApi"/> façade; the response carries the
/// <c>D2ResultProto</c> envelope (field 1) + typed <c>GetKeyringOutput</c> data (field 2).
/// gRPC status stays <see cref="StatusCode.OK"/> for every business result — business
/// failures ride the envelope, never thrown.
/// </summary>
public sealed class GrpcKeyringServiceTests
{
    private static readonly byte[] sr_keyBytes =
        Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    private static readonly byte[] sr_aad = "d2/audit"u8.ToArray();

    [Fact]
    public async Task GetKeyring_Success_ReturnsEnvelopeWithActiveKidEntriesAndAad()
    {
        var output = new ClientsGetKeyringOutput(
            "kid-active",
            [new ClientsKeyringEntry("kid-active", sr_keyBytes)],
            sr_aad);
        var fakeFacade = new FakeKeyCustodianApi(D2Result<ClientsGetKeyringOutput?>.Ok(output));

        using var host = await BuildHost(fakeFacade);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianKeyring.KeyCustodianKeyringClient(channel);

        var reply = await client.GetKeyringAsync(new GetKeyringRequest { KeyDomain = "audit" });

        reply.Result.Success.Should().BeTrue();
        reply.Result.StatusCode.Should().Be(200);
        reply.Data.ActiveKid.Should().Be("kid-active");
        reply.Data.Entries.Should().ContainSingle();
        reply.Data.Entries[0].Kid.Should().Be("kid-active");

        // Byte-fidelity across the proto round-trip.
        reply.Data.Entries[0].KeyBytes.ToByteArray().Should().Equal(sr_keyBytes);
        reply.Data.AadContext.ToByteArray().Should().Equal(sr_aad);

        // Input fidelity — the key domain crosses the seam verbatim (§1.32 capture-assert).
        fakeFacade.GetKeyringCallCount.Should().Be(1);
        fakeFacade.LastKeyringInput!.KeyDomain.Should().Be("audit");
    }

    [Fact]
    public async Task GetKeyring_DomainNotAuthorized_RidesEnvelopeWith403()
    {
        var fakeFacade = new FakeKeyCustodianApi(
            KeyCustodianFailures<ClientsGetKeyringOutput?>.KeyringDomainNotAuthorized());

        using var host = await BuildHost(fakeFacade);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianKeyring.KeyCustodianKeyringClient(channel);

        var reply = await client.GetKeyringAsync(
            new GetKeyringRequest { KeyDomain = "jwks-signing" });

        reply.Result.Success.Should().BeFalse();
        reply.Result.StatusCode.Should().Be(403);

        // proto3 sub-message fields are null when the sender does not set them.
        reply.Data.Should().BeNull();
    }

    [Fact]
    public async Task GetKeyring_KeyUnavailable_RidesEnvelopeWith503()
    {
        var fakeFacade = new FakeKeyCustodianApi(
            KeyCustodianFailures<ClientsGetKeyringOutput?>.KeyringKeyUnavailable());

        using var host = await BuildHost(fakeFacade);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianKeyring.KeyCustodianKeyringClient(channel);

        var reply = await client.GetKeyringAsync(new GetKeyringRequest { KeyDomain = "audit" });

        reply.Result.Success.Should().BeFalse();
        reply.Result.StatusCode.Should().Be(503);
        reply.Data.Should().BeNull();
    }

    [Fact]
    public async Task GetKeyring_TypeMismatch_RidesEnvelopeWith400()
    {
        var fakeFacade = new FakeKeyCustodianApi(
            KeyCustodianFailures<ClientsGetKeyringOutput?>.KeyTypeDomainMismatch());

        using var host = await BuildHost(fakeFacade);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianKeyring.KeyCustodianKeyringClient(channel);

        var reply = await client.GetKeyringAsync(new GetKeyringRequest { KeyDomain = "cookie" });

        reply.Result.Success.Should().BeFalse();
        reply.Result.StatusCode.Should().Be(400);
        reply.Data.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // Redaction reflection pins — the [RedactData(SecretInformation)] contract
    // -----------------------------------------------------------------------

    [Fact]
    public void KeyringEntry_KeyBytesField_CarriesRedactData()
    {
        // The raw AES key bytes are secret material: the generated nested DTO field carries
        // [RedactData(SecretInformation)] so it is masked in structured logs (proof of the
        // nested @d2Redact emitter path).
        var property = typeof(ClientsGetKeyringOutput).Assembly
            .GetType("DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring.KeyringEntry")!
            .GetProperty("KeyBytes");

        property.Should().NotBeNull();
        var attributes = property.GetCustomAttributes(
            typeof(DcsvIo.D2.Utilities.Attributes.RedactDataAttribute), inherit: false);
        attributes.Should().ContainSingle("the raw AES key bytes are redacted in logs (@d2Redact)");

        var redact = (DcsvIo.D2.Utilities.Attributes.RedactDataAttribute)attributes[0];
        redact.Reason.Should().Be(
            DcsvIo.D2.Utilities.Enums.RedactReason.SecretInformation,
            "the reason is threaded from @d2Redact — a secret, never PersonalInformation");
    }

    [Fact]
    public void GetKeyringOutput_AadContextField_IsNotRedacted()
    {
        // aadContext is authenticated-not-secret AEAD context (a derivable public convention,
        // "d2/<domain>") — deliberately NOT redacted so operators can debug a cross-service
        // AAD disagreement.
        var property = typeof(ClientsGetKeyringOutput).GetProperty("AadContext");

        property.Should().NotBeNull();
        property.GetCustomAttributes(
                typeof(DcsvIo.D2.Utilities.Attributes.RedactDataAttribute), inherit: false)
            .Should().BeEmpty(
                "the AAD context is authenticated-not-secret and stays visible in logs");
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
                        endpoints.MapGrpcService<KeyCustodianKeyringService>();
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
    /// keyring result and records the call so delegation + input fidelity can be asserted.
    /// The Sign / JWKS / OIDC arms are unused here (the service under test only routes
    /// <c>GetKeyring</c>).
    /// </summary>
    private sealed class FakeKeyCustodianApi(D2Result<ClientsGetKeyringOutput?> keyringResult)
        : IKeyCustodianApi
    {
        public int GetKeyringCallCount { get; private set; }

        public GetKeyringInput? LastKeyringInput { get; private set; }

        // Seal ops — unused by this gRPC harness (no seal service is wired); fully-qualified
        // to stay collision-safe with the proto types imported in this file.
        public ValueTask<D2Result<DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionSealPublicKeyOutput?>>
            GetOrLazyProvisionSealPublicKeyAsync(
                DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionSealPublicKeyInput input,
                CancellationToken ct = default)
            => ValueTask.FromResult(
                D2Result<DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionSealPublicKeyOutput?>
                    .ServiceUnavailable());

        public ValueTask<D2Result<DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionOwnSealPrivateKeyOutput?>>
            GetOrLazyProvisionOwnSealPrivateKeyAsync(
                DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionOwnSealPrivateKeyInput input,
                CancellationToken ct = default)
            => ValueTask.FromResult(
                D2Result<DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionOwnSealPrivateKeyOutput?>
                    .ServiceUnavailable());

        public ValueTask<D2Result<ClientsGetKeyringOutput?>> GetKeyringAsync(
            GetKeyringInput input, CancellationToken ct = default)
        {
            GetKeyringCallCount++;
            LastKeyringInput = input;
            return ValueTask.FromResult(keyringResult);
        }

        public ValueTask<D2Result<ClientsSignOutput?>> SignAsync(
            SignInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<ClientsSignOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<GetJwksOutput?>> GetJwksAsync(
            GetJwksInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<GetJwksOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<GetOidcConfigurationOutput?>> GetOidcConfigurationAsync(
            GetOidcConfigurationInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<GetOidcConfigurationOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<ClientsIssueLeafOutput?>> IssueLeafAsync(
            IssueLeafInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<ClientsIssueLeafOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<ClientsGetCaCertificateOutput?>> GetCaCertificateAsync(
            GetCaCertificateInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<ClientsGetCaCertificateOutput?>.ServiceUnavailable());
    }
}
