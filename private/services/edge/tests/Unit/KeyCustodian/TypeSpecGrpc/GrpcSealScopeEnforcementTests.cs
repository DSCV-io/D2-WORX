// -----------------------------------------------------------------------
// <copyright file="GrpcSealScopeEnforcementTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpc;

using System.Collections.Generic;
using System.Threading.Tasks;
using DcsvIo.D2.Auth;
using DcsvIo.D2.Auth.Abstractions.Jwks;
using DcsvIo.D2.Auth.Abstractions.Sessions;
using DcsvIo.D2.Auth.Errors;
using DcsvIo.D2.Auth.Grpc;
using DcsvIo.D2.Auth.Grpc.Endpoints;
using DcsvIo.D2.Auth.Grpc.Status;
using DcsvIo.D2.Caching;
using DcsvIo.D2.Caching.Local.Default;
using DcsvIo.D2.Private.Edge.Api.Grpc.KeyCustodian;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.CaCertificate;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Facade;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Issuance;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Jwks;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.OidcConfiguration;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Signing;
using DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;
using global::D2.Services.Protos.KeyCustodian.V2Alpha;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ClientsGetCaCertificateOutput = DcsvIo.D2.Private.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateOutput;
using ClientsGetKeyringOutput = DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring.GetKeyringOutput;
using ClientsIssueLeafOutput = DcsvIo.D2.Private.Edge.KeyCustodian.Client.Issuance.IssueLeafOutput;
using ClientsSignOutput = DcsvIo.D2.Private.Edge.KeyCustodian.Client.Signing.SignOutput;
using GrpcStatusCode = Grpc.Core.StatusCode;

/// <summary>
/// gRPC scope-enforcement tests for the two seal-surface services, wired against the
/// SHARED <see cref="DcsvIo.D2.Auth.Grpc"/> mechanism: the generated
/// <c>KeyCustodianSealPublicKeyService</c> is mapped with
/// <c>.RequireAnyScope("internal.kc.seal.encrypt")</c> and the
/// <c>KeyCustodianOwnSealPrivateKeyService</c> with
/// <c>.RequireAnyScope("internal.kc.seal.open")</c> — one gRPC service per op, so each
/// carries its OWN transport scope policy — under the real <c>JwtAuthInterceptor</c>
/// (via <c>AddD2AuthGrpc</c>), backed by the local RS256 <see cref="TestJwtBuilder"/>
/// + <see cref="FakeJwksProvider"/>. Proves per method: no bearer and wrong scope are
/// rejected pre-dispatch; the correct scope reaches the service — and the scopes are NOT
/// interchangeable across the two services, so the broad public-key (encrypt) scope can
/// never open the private-key (open) service that serves root-unwrapped ECDH private
/// material.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "TestHost lifetime is bounded by individual tests.")]
public sealed class GrpcSealScopeEnforcementTests
{
    private const string _ISSUER = "https://edge.internal";
    private const string _AUDIENCE = "d2.internal";
    private const string _SEAL_ENCRYPT_SCOPE = "internal.kc.seal.encrypt";
    private const string _SEAL_OPEN_SCOPE = "internal.kc.seal.open";

    // -----------------------------------------------------------------------
    // GetOrLazyProvisionSealPublicKey — internal.kc.seal.encrypt
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetOrLazyProvisionSealPublicKey_NoBearer_RejectedAsBearerMissing()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianSealPublicKey.KeyCustodianSealPublicKeyClient(channel);

        var act = async () => await client.GetOrLazyProvisionSealPublicKeyAsync(
            new GetOrLazyProvisionSealPublicKeyRequest { ServiceId = "audit" });

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
    }

    [Fact]
    public async Task GetOrLazyProvisionSealPublicKey_BearerWithSealEncryptScope_ReachesService()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianSealPublicKey.KeyCustodianSealPublicKeyClient(channel);
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = _SEAL_ENCRYPT_SCOPE });
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        // Auth passes → the interceptor lets the call through to the service, which
        // delegates to the stub façade (returns 503 on the envelope). A NON-thrown
        // reply with the envelope populated proves the scope gate admitted the call.
        var reply = await client.GetOrLazyProvisionSealPublicKeyAsync(
            new GetOrLazyProvisionSealPublicKeyRequest { ServiceId = "audit" }, headers);

        reply.Result.StatusCode.Should().Be(
            503, "the scope gate admitted the call to the service");
    }

    [Fact]
    public async Task GetOrLazyProvisionSealPublicKey_BearerWithSealOpenScope_RejectedAsScopeInsufficient()
    {
        // The private-key (open) scope does NOT open the public-key (encrypt) service —
        // the two services carry distinct transport policies.
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianSealPublicKey.KeyCustodianSealPublicKeyClient(channel);
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = _SEAL_OPEN_SCOPE });
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        var act = async () => await client.GetOrLazyProvisionSealPublicKeyAsync(
            new GetOrLazyProvisionSealPublicKeyRequest { ServiceId = "audit" }, headers);

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    // -----------------------------------------------------------------------
    // GetOrLazyProvisionOwnSealPrivateKey — internal.kc.seal.open (serves private ECDH material)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetOrLazyProvisionOwnSealPrivateKey_NoBearer_RejectedAsBearerMissing()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianOwnSealPrivateKey
            .KeyCustodianOwnSealPrivateKeyClient(channel);

        var act = async () => await client.GetOrLazyProvisionOwnSealPrivateKeyAsync(
            new GetOrLazyProvisionOwnSealPrivateKeyRequest());

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
    }

    [Fact]
    public async Task GetOrLazyProvisionOwnSealPrivateKey_BearerWithSealOpenScope_ReachesService()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianOwnSealPrivateKey
            .KeyCustodianOwnSealPrivateKeyClient(channel);
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = _SEAL_OPEN_SCOPE });
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        var reply = await client.GetOrLazyProvisionOwnSealPrivateKeyAsync(
            new GetOrLazyProvisionOwnSealPrivateKeyRequest(), headers);

        reply.Result.StatusCode.Should().Be(
            503, "the scope gate admitted the call to the service");
    }

    [Fact]
    public async Task GetOrLazyProvisionOwnSealPrivateKey_BearerWithSealEncryptScope_RejectedAsScopeInsufficient()
    {
        // The broad public-key (encrypt) scope must NEVER open the private-key (open)
        // service — the highest-value endpoint, serving root-unwrapped ECDH private keys.
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianOwnSealPrivateKey
            .KeyCustodianOwnSealPrivateKeyClient(channel);
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = _SEAL_ENCRYPT_SCOPE });
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        var act = async () => await client.GetOrLazyProvisionOwnSealPrivateKeyAsync(
            new GetOrLazyProvisionOwnSealPrivateKeyRequest(), headers);

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    private static async Task<IHost> BuildHostAsync(TestJwtBuilder jwtBuilder)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddLogging();
                        services.AddRouting();
                        services.AddD2LocalCache();
                        services.AddSingleton<ITieredCache, FakeTieredCacheStub>();
                        services.AddD2Auth(opts =>
                        {
                            opts.Issuer = new Uri(_ISSUER);
                            opts.Audience = _AUDIENCE;
                        });

                        services.RemoveAll<IJwksProvider>();
                        services.AddSingleton<IJwksProvider>(
                            new FakeJwksProvider(jwtBuilder.PublicKey));

                        services.RemoveAll<ISessionLivenessTracker>();
                        services.AddSingleton<ISessionLivenessTracker>(
                            new FakeSessionLivenessTracker());

                        // The services' façade dependency — returns 503 so a completed
                        // call proves auth passed (the scope gate is what's under test).
                        services.AddSingleton<IKeyCustodianApi>(new StubFacade());

                        services.AddGrpc();
                        services.AddD2AuthGrpc();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            // One gRPC service per op — each carries its own scope.
                            endpoints
                                .MapGrpcService<KeyCustodianSealPublicKeyService>()
                                .RequireAnyScope(_SEAL_ENCRYPT_SCOPE);
                            endpoints
                                .MapGrpcService<KeyCustodianOwnSealPrivateKeyService>()
                                .RequireAnyScope(_SEAL_OPEN_SCOPE);
                        });
                    });
            });

        return await hostBuilder.StartAsync();
    }

    private static GrpcChannel CreateChannel(IHost host)
    {
        var httpClient = host.GetTestClient();
        return GrpcChannel.ForAddress(
            httpClient.BaseAddress!,
            new GrpcChannelOptions { HttpClient = httpClient });
    }

    private static string? ReadTrailer(Metadata trailers, string key)
    {
        foreach (var entry in trailers)
        {
            if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase)
                && !entry.IsBinary)
                return entry.Value;
        }

        return null;
    }

    private sealed class StubFacade : IKeyCustodianApi
    {
        // Seal ops — fully-qualified to stay collision-safe with the proto types
        // imported in this file (the same bare names live in both namespaces).
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
            => ValueTask.FromResult(D2Result<ClientsGetKeyringOutput?>.ServiceUnavailable());

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
