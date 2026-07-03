// -----------------------------------------------------------------------
// <copyright file="GrpcCertificateAuthorityScopeEnforcementTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpc;

using System.Collections.Generic;
using System.Threading.Tasks;
using D2.Edge.KeyCustodian.Clients;
using D2.Edge.Tests.TypeSpecGrpc.Generated;
using D2.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;
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
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ClientsGetCaCertificateOutput = D2.Edge.KeyCustodian.Clients.GetCaCertificateOutput;
using ClientsGetKeyringOutput = D2.Edge.KeyCustodian.Clients.GetKeyringOutput;
using ClientsIssueLeafOutput = D2.Edge.KeyCustodian.Clients.IssueLeafOutput;
using ClientsSignOutput = D2.Edge.KeyCustodian.Clients.SignOutput;
using GrpcStatusCode = Grpc.Core.StatusCode;

/// <summary>
/// gRPC scope-enforcement tests for the two certificate-authority-surface services,
/// wired against the SHARED <see cref="D2.Shared.Auth.Grpc"/> mechanism: the
/// generated <c>KeyCustodianCertificateAuthorityService</c> is mapped with
/// <c>.RequireAnyScope("internal.kc.issue")</c> and the
/// <c>KeyCustodianCaCertificateService</c> with
/// <c>.RequireAnyScope("internal.kc.cacert")</c> — one gRPC service per op, so each
/// carries its OWN transport scope policy — under the real <c>JwtAuthInterceptor</c>
/// (via <c>AddD2AuthGrpc</c>), backed by the local RS256 <see cref="TestJwtBuilder"/>
/// + <see cref="FakeJwksProvider"/>. Proves per method: no bearer and wrong scope
/// are rejected pre-dispatch; the correct scope reaches the service — and the
/// scopes are NOT interchangeable across the two services.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "TestHost lifetime is bounded by individual tests.")]
public sealed class GrpcCertificateAuthorityScopeEnforcementTests
{
    private const string _ISSUER = "https://edge.internal";
    private const string _AUDIENCE = "d2.internal";
    private const string _ISSUE_SCOPE = "internal.kc.issue";
    private const string _CACERT_SCOPE = "internal.kc.cacert";

    // -----------------------------------------------------------------------
    // IssueWorkloadCertificate — internal.kc.issue
    // -----------------------------------------------------------------------

    [Fact]
    public async Task IssueWorkloadCertificate_NoBearer_RejectedAsBearerMissing()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianCertificateAuthority
            .KeyCustodianCertificateAuthorityClient(channel);

        var act = async () => await client.IssueWorkloadCertificateAsync(
            new IssueWorkloadCertificateRequest { CsrDer = ByteString.CopyFrom([0x30]) });

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
    }

    [Fact]
    public async Task IssueWorkloadCertificate_BearerWithIssueScope_ReachesService()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianCertificateAuthority
            .KeyCustodianCertificateAuthorityClient(channel);
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = _ISSUE_SCOPE });
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        // Auth passes → the interceptor lets the call through to the service, which
        // delegates to the stub façade (returns 503 on the envelope). A NON-thrown
        // reply with the envelope populated proves the scope gate admitted the call.
        var reply = await client.IssueWorkloadCertificateAsync(
            new IssueWorkloadCertificateRequest { CsrDer = ByteString.CopyFrom([0x30]) },
            headers);

        reply.Result.StatusCode.Should().Be(
            503, "the scope gate admitted the call to the service");
    }

    [Fact]
    public async Task IssueWorkloadCertificate_BearerWithCacertScope_RejectedAsScopeInsufficient()
    {
        // The CA-chain scope does NOT open the issuance service — the two services
        // carry distinct transport policies.
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianCertificateAuthority
            .KeyCustodianCertificateAuthorityClient(channel);
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = _CACERT_SCOPE });
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        var act = async () => await client.IssueWorkloadCertificateAsync(
            new IssueWorkloadCertificateRequest { CsrDer = ByteString.CopyFrom([0x30]) },
            headers);

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    // -----------------------------------------------------------------------
    // GetCaCertificate — internal.kc.cacert
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetCaCertificate_NoBearer_RejectedAsBearerMissing()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianCaCertificate.KeyCustodianCaCertificateClient(channel);

        var act = async () => await client.GetCaCertificateAsync(
            new GetCaCertificateRequest());

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
    }

    [Fact]
    public async Task GetCaCertificate_BearerWithCacertScope_ReachesService()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianCaCertificate.KeyCustodianCaCertificateClient(channel);
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = _CACERT_SCOPE });
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        var reply = await client.GetCaCertificateAsync(
            new GetCaCertificateRequest(), headers);

        reply.Result.StatusCode.Should().Be(
            503, "the scope gate admitted the call to the service");
    }

    [Fact]
    public async Task GetCaCertificate_BearerWithIssueScope_RejectedAsScopeInsufficient()
    {
        // The issuance scope does NOT open the CA-chain service.
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianCaCertificate.KeyCustodianCaCertificateClient(channel);
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = _ISSUE_SCOPE });
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        var act = async () => await client.GetCaCertificateAsync(
            new GetCaCertificateRequest(), headers);

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
                                .MapGrpcService<KeyCustodianCertificateAuthorityService>()
                                .RequireAnyScope(_ISSUE_SCOPE);
                            endpoints
                                .MapGrpcService<KeyCustodianCaCertificateService>()
                                .RequireAnyScope(_CACERT_SCOPE);
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
        public ValueTask<D2Result<ClientsIssueLeafOutput?>> IssueLeafAsync(
            IssueLeafInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<ClientsIssueLeafOutput?>.ServiceUnavailable());

        public ValueTask<D2Result<ClientsGetCaCertificateOutput?>> GetCaCertificateAsync(
            GetCaCertificateInput input, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<ClientsGetCaCertificateOutput?>.ServiceUnavailable());

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
    }
}
