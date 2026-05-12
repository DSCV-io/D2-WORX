// -----------------------------------------------------------------------
// <copyright file="GrpcAuthIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Grpc;

using System.Collections.Generic;
using AwesomeAssertions;
using D2.Shared.Auth;
using D2.Shared.Auth.Errors;
using D2.Shared.Auth.Grpc;
using D2.Shared.Auth.Grpc.Endpoints;
using D2.Shared.Auth.Validation;
using D2.Shared.Caching;
using D2.Shared.Caching.Local.Default;
using D2.Shared.Result;
using D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Fixtures;
using D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Protos;
using global::Grpc.Core;
using global::Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;
using GrpcStatusCode = global::Grpc.Core.StatusCode;

/// <summary>
/// End-to-end pipeline-position smoke tests for <c>D2.Shared.Auth.Grpc</c>:
/// hosts a real ASP.NET Core gRPC server via <see cref="TestServer"/>, wires
/// the <c>JwtAuthInterceptor</c> through the host's <c>AddGrpc()</c> /
/// <c>MapGrpcService&lt;T&gt;()</c> pipeline (NOT a hand-driven interceptor
/// invocation), and dials it via an in-process <see cref="GrpcChannel"/>.
/// Sibling to <c>AuthAppBuilderExtensionsTests</c> on the HTTP side. The
/// <c>JwtAuthInterceptorTests</c> file unit-exercises every branch of the
/// interceptor in isolation; this file proves the wiring + scope-metadata
/// pickup works through the production code path.
/// </summary>
/// <remarks>
/// Method-level metadata is supplied via the fluent
/// <c>RequireD2Scope</c> / <c>MarkAsD2HarmlessEndpoint</c> extensions on
/// <see cref="GrpcServiceEndpointConventionBuilder"/> — the most reliable
/// production wiring (the attribute path is covered by separate unit tests
/// over hand-built endpoint metadata; gRPC AspNetCore does not auto-pull
/// arbitrary user attributes onto endpoint metadata, so the fluent path is
/// what end-to-end production hosts use today).
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "TestHost lifetime is bounded by individual tests, not the class.")]
public sealed class GrpcAuthIntegrationTests
{
    private const string _ISSUER = "https://edge.internal";
    private const string _AUDIENCE = "files";
    private const string _SCOPE = "test.scope";

    [Fact]
    public async Task HarmlessEndpointService_NoBearer_Succeeds()
    {
        // TestHealth is wired with .MarkAsD2HarmlessEndpoint(); the pipeline
        // must short-circuit and never inspect the (absent) bearer.
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new TestHealth.TestHealthClient(channel);

        var reply = await client.HealthAsync(new HealthRequest());

        reply.Status.Should().Be("ok");
    }

    [Fact]
    public async Task ScopeProtectedService_NoBearer_RejectedAsUnauthenticated()
    {
        // TestEcho is wired with .RequireD2Scope("test.scope"); missing bearer
        // must surface AUTH_BEARER_MISSING with Status.Unauthenticated.
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new TestEcho.TestEchoClient(channel);

        var act = async () => await client.EchoAsync(new EchoRequest { Payload = "hi" });

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(ex.Which.Trailers, "d2_error_code")
            .Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);

        // Status.Detail is intentionally empty (info-leak avoidance).
        ex.Which.Status.Detail.Should().BeEmpty();
    }

    [Fact]
    public async Task ScopeProtectedService_BearerWithRequiredScope_Succeeds()
    {
        // Echo requires "test.scope"; mint a token carrying it via the
        // standard `scope` claim (RFC 6749 §3.3 — space-delimited string).
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new TestEcho.TestEchoClient(channel);
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = _SCOPE,
            });
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        var reply = await client.EchoAsync(
            new EchoRequest { Payload = "hello" }, headers);

        reply.Echoed.Should().Be("hello");
    }

    [Fact]
    public async Task ScopeProtectedService_BearerWithoutRequiredScope_Rejected()
    {
        // Bearer is valid (signature + claims pass) but carries a different
        // scope set — must surface AUTH_SCOPE_INSUFFICIENT and
        // Status.Unauthenticated (NOT PermissionDenied — uniform 401-shape
        // policy mirrors HTTP middleware).
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new TestEcho.TestEchoClient(channel);
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = "some.other.scope",
            });
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        var act = async () => await client.EchoAsync(
            new EchoRequest { Payload = "hi" }, headers);

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(ex.Which.Trailers, "d2_error_code")
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    [Fact]
    public async Task ScopeProtectedService_LowercaseBearerPrefix_Accepted()
    {
        // RFC 6750 §2.1: the `Bearer` scheme is case-insensitive. Verify the
        // gRPC pipeline accepts lowercase `bearer ` prefix end-to-end.
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new TestEcho.TestEchoClient(channel);
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = _SCOPE,
            });
        var headers = new Metadata { { "authorization", "bearer " + token } };

        var reply = await client.EchoAsync(
            new EchoRequest { Payload = "case" }, headers);

        reply.Echoed.Should().Be("case");
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

                        // Swap the network-touching JwksProvider for the in-
                        // memory fake (mirrors the AspNetCore-side host setup).
                        services.RemoveAll<D2.Shared.Auth.Abstractions.Jwks.IJwksProvider>();
                        services.RemoveAll<D2.Shared.Auth.Jwks.HttpJwksProvider>();
                        services.AddSingleton<D2.Shared.Auth.Abstractions.Jwks.IJwksProvider>(
                            new FakeJwksProvider(jwtBuilder.PublicKey));
                        services.RemoveAll<JwtValidator>();
                        services.AddSingleton(sp => new JwtValidator(
                            sp.GetRequiredService<
                                D2.Shared.Auth.Abstractions.Jwks.IJwksProvider>(),
                            sp.GetRequiredService<IOptions<AuthOptions>>(),
                            sp.GetRequiredService<ClaimsToContextMapper>(),
                            Microsoft.Extensions.Logging.Abstractions
                                .NullLogger<JwtValidator>.Instance));

                        // gRPC stack + the auth interceptor wiring under test.
                        services.AddGrpc();
                        services.AddD2AuthGrpc();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGrpcService<TestEchoService>()
                                .RequireD2Scope(_SCOPE);
                            endpoints.MapGrpcService<TestHealthService>()
                                .MarkAsD2HarmlessEndpoint();
                        });
                    });
            });

        var host = await hostBuilder.StartAsync();
        return host;
    }

    private static GrpcChannel CreateChannel(IHost host)
    {
        // GrpcChannel over the TestServer's in-process HttpClient — keeps
        // the test fully in-process (no real socket binding).
        var testServer = host.GetTestServer();
        var httpClient = testServer.CreateClient();
        httpClient.BaseAddress = new Uri("http://localhost");
        return GrpcChannel.ForAddress(
            httpClient.BaseAddress,
            new GrpcChannelOptions { HttpClient = httpClient });
    }

    private static string? ReadTrailer(Metadata trailers, string key)
    {
        foreach (var entry in trailers)
        {
            if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase)
                && !entry.IsBinary)
            {
                return entry.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Test-only gRPC service. Exposes <c>Echo</c>; wired with
    /// <c>.RequireD2Scope("test.scope")</c> at <c>MapGrpcService</c> time.
    /// </summary>
    private sealed class TestEchoService : TestEcho.TestEchoBase
    {
        public override Task<EchoReply> Echo(EchoRequest request, ServerCallContext context)
            => Task.FromResult(new EchoReply { Echoed = request.Payload });
    }

    /// <summary>
    /// Test-only gRPC service. Exposes <c>Health</c>; wired with
    /// <c>.MarkAsD2HarmlessEndpoint()</c> at <c>MapGrpcService</c> time.
    /// </summary>
    private sealed class TestHealthService : TestHealth.TestHealthBase
    {
        public override Task<HealthReply> Health(HealthRequest request, ServerCallContext context)
            => Task.FromResult(new HealthReply { Status = "ok" });
    }

    /// <summary>
    /// Stub <see cref="ITieredCache"/> required by <see cref="JwtValidator"/>'s
    /// transitive dependency tree (session liveness path). The integration tests
    /// don't exercise revocation; the stub no-ops everything.
    /// </summary>
    private sealed class FakeTieredCacheStub : ITieredCache
    {
        public ValueTask<D2Result<bool>> ExistsAsync(
            string key, CancellationToken ct = default)
            => new(D2Result<bool>.Ok(true));

        public ValueTask<D2Result<T?>> GetAsync<T>(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<IReadOnlyDictionary<string, T?>>>
            GetManyAsync<T>(
                IReadOnlyCollection<string> keys, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> SetAsync<T>(
            string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> SetManyAsync<T>(
            IReadOnlyDictionary<string, T> entries,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> RemoveAsync(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> RemoveManyAsync(
            IReadOnlyCollection<string> keys, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<TimeSpan?>> GetTtlAsync(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<long>> IncrementAsync(
            string key,
            long delta = 1,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<bool>> SetNxAsync<T>(
            string key,
            T value,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<bool>> AcquireLockAsync(
            string key, string token, TimeSpan ttl, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> ReleaseLockAsync(
            string key, string token, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> SetAndBroadcastAsync<T>(
            string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> SetManyAndBroadcastAsync<T>(
            IReadOnlyDictionary<string, T> entries,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> RemoveAndBroadcastAsync(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> RemoveManyAndBroadcastAsync(
            IReadOnlyCollection<string> keys, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
