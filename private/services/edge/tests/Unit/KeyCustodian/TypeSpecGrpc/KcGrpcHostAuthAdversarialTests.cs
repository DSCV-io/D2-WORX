// -----------------------------------------------------------------------
// <copyright file="KcGrpcHostAuthAdversarialTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpc;

using System.Collections.Generic;
using System.Threading.Tasks;
using D2.Edge.Api.Grpc.KeyCustodian;
using D2.Edge.KeyCustodian.Client.CaCertificate;
using D2.Edge.KeyCustodian.Client.Facade;
using D2.Edge.KeyCustodian.Client.Issuance;
using D2.Edge.KeyCustodian.Client.Jwks;
using D2.Edge.KeyCustodian.Client.Keyring;
using D2.Edge.KeyCustodian.Client.OidcConfiguration;
using D2.Edge.KeyCustodian.Client.Signing;
using D2.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;
using D2.Private.Auth;
using D2.Services.Protos.KeyCustodian.V2Alpha;
using D2.Shared.Auth;
using D2.Shared.Auth.Abstractions.Jwks;
using D2.Shared.Auth.Abstractions.Sessions;
using D2.Shared.Auth.Errors;
using D2.Shared.Auth.Grpc;
using D2.Shared.Auth.Grpc.Endpoints;
using D2.Shared.Auth.Grpc.Status;
using D2.Shared.Caching;
using D2.Shared.Caching.Local.Default;
using D2.Shared.Result;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ClientsGetCaCertificateOutput = D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateOutput;
using ClientsGetKeyringOutput = D2.Edge.KeyCustodian.Client.Keyring.GetKeyringOutput;
using ClientsIssueLeafOutput = D2.Edge.KeyCustodian.Client.Issuance.IssueLeafOutput;
using ClientsSignOutput = D2.Edge.KeyCustodian.Client.Signing.SignOutput;
using GrpcStatusCode = Grpc.Core.StatusCode;
using SealOwnPrivIn =

    D2.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionOwnSealPrivateKeyInput;
using SealOwnPrivOut =
    D2.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionOwnSealPrivateKeyOutput;
using SealPubIn =
    D2.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionSealPublicKeyInput;
using SealPubOut =
    D2.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionSealPublicKeyOutput;

/// <summary>
/// Residual KC gRPC host-path JWT adversarial matrix (wrong audience, expired JWT,
/// missing scope). Harness clones ScopeEnforcement (real
/// <see cref="D2.Shared.Auth.Grpc"/> interceptor + <see cref="TestJwtBuilder"/>), but
/// Map binding uses production <see cref="ProductScopes.Internal.Kc.Keyring"/> constants ΓÇö
/// never free-string correct-scope binding. Deny asserts use
/// <see cref="AuthErrorCodes"/> constants; missing/wrong scope is
/// <see cref="GrpcStatusCode.Unauthenticated"/> +
/// <c>AUTH_SCOPE_INSUFFICIENT</c> (401-shape), never 403.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "TestHost lifetime is bounded by individual tests.")]
[Trait("Category", "Unit")]
public sealed class KcGrpcHostAuthAdversarialTests
{
    private const string _ISSUER = "https://edge.internal";
    private const string _AUDIENCE = "d2.internal";

    [Fact]
    public async Task KcGrpc_WrongAudience_Denied()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianKeyring.KeyCustodianKeyringClient(channel);
        var token = jwt.MintToken(
            _ISSUER,
            audience: "wrong-audience",
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = ProductScopes.Internal.Kc.Keyring,
            });
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        var act = async () => await client.GetKeyringAsync(
            new GetKeyringRequest { KeyDomain = "audit" }, headers);

        var ex = await act.Should().ThrowAsync<RpcException>();

        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);

        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_JWT_AUDIENCE_MISMATCH);
    }

    [Fact]
    public async Task KcGrpc_ExpiredJwt_Denied()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianKeyring.KeyCustodianKeyringClient(channel);
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            notBefore: DateTimeOffset.UtcNow.AddHours(-2),
            expires: DateTimeOffset.UtcNow.AddHours(-1),
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = ProductScopes.Internal.Kc.Keyring,
            });
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        var act = async () => await client.GetKeyringAsync(
            new GetKeyringRequest { KeyDomain = "audit" }, headers);

        var ex = await act.Should().ThrowAsync<RpcException>();

        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);

        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_JWT_EXPIRED);
    }

    [Fact]
    public async Task KcGrpc_MissingScope_Returns401ScopeInsufficient()
    {
        // Matrix name KcGrpc_MissingScope_Returns403 is stale Living State alias â€”
        // product status is Unauthenticated + AUTH_SCOPE_INSUFFICIENT (401-shape).
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianKeyring.KeyCustodianKeyringClient(channel);
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = string.Empty,
            });
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        var act = async () => await client.GetKeyringAsync(
            new GetKeyringRequest { KeyDomain = "audit" }, headers);

        var ex = await act.Should().ThrowAsync<RpcException>();

        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);

        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    [Fact]
    public async Task KcGrpc_WrongScope_Returns401ScopeInsufficient()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianKeyring.KeyCustodianKeyringClient(channel);

        // Wrong-scope attack uses catalog constant for the wrong scope value;
        // Map binding still uses ProductScopes.Internal.Kc.Keyring (production).
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = ProductScopes.Internal.Kc.Sign,
            });
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        var act = async () => await client.GetKeyringAsync(
            new GetKeyringRequest { KeyDomain = "audit" }, headers);

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

                        services.AddSingleton<IKeyCustodianApi>(new StubFacade());

                        services.AddGrpc();
                        services.AddD2AuthGrpc();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            // Production Map pattern â€” ProductScopes.Internal.Kc.* constant.
                            endpoints.MapGrpcService<KeyCustodianKeyringService>()
                                .RequireAnyScope(ProductScopes.Internal.Kc.Keyring);
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
        public ValueTask<D2Result<SealPubOut?>> GetOrLazyProvisionSealPublicKeyAsync(
            SealPubIn input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<SealPubOut?>.ServiceUnavailable());

        public ValueTask<D2Result<SealOwnPrivOut?>> GetOrLazyProvisionOwnSealPrivateKeyAsync(
            SealOwnPrivIn input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<SealOwnPrivOut?>.ServiceUnavailable());

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
