// -----------------------------------------------------------------------
// <copyright file="KcGrpcHostAuthAdversarialTests.cs" company="DCSV">
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
using DcsvIo.D2.Private.Auth;
using DcsvIo.D2.Private.Edge.Api.Grpc.KeyCustodian;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.CaCertificate;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Facade;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Issuance;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Jwks;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.OidcConfiguration;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Signing;
using DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;
using DcsvIo.D2.Result;
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
using SealOwnPrivIn =

    DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionOwnSealPrivateKeyInput;
using SealOwnPrivOut =
    DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionOwnSealPrivateKeyOutput;
using SealPubIn =
    DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionSealPublicKeyInput;
using SealPubOut =
    DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionSealPublicKeyOutput;

/// <summary>
/// Residual KC gRPC host-path JWT adversarial matrix (wrong audience, expired JWT,
/// missing scope). Harness clones ScopeEnforcement (real
/// <see cref="DcsvIo.D2.Auth.Grpc"/> interceptor + <see cref="TestJwtBuilder"/>), but
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
