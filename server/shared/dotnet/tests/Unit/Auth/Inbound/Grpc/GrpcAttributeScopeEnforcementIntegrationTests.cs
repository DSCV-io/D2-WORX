// -----------------------------------------------------------------------
// <copyright file="GrpcAttributeScopeEnforcementIntegrationTests.cs" company="DCSV">
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
using D2.Shared.Auth.Grpc.Status;
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
/// End-to-end enforcement tests for the ATTRIBUTE declaration path:
/// proves that scope attributes placed on the gRPC service implementation
/// class (<c>[D2RequireAnyScope]</c>, <c>[D2RequireAllScopes]</c>,
/// <c>[D2HarmlessEndpoint]</c>) actually enforce access through the real
/// <see cref="D2.Shared.Auth.Grpc.Interceptors.JwtAuthInterceptor"/> when
/// NO fluent <c>.RequireAnyScope()</c> / <c>.RequireAllScopes()</c> /
/// <c>.MarkAsD2HarmlessEndpoint()</c> is wired at <c>MapGrpcService&lt;T&gt;()</c>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why a separate class from <c>GrpcAuthIntegrationTests</c></strong>:
/// the sibling class uses the FLUENT path (builder extensions set
/// <see cref="D2.Shared.Auth.Grpc.Endpoints.MethodScopeMetadata"/> directly
/// on the endpoint metadata collection).  The interceptor checks that slot
/// FIRST and, when it finds a match, skips the attribute walk entirely.
/// Attribute enforcement is exercised ONLY when the builder extension path
/// is absent — which is the exact setup this class provides.
/// </para>
/// <para>
/// <strong>What this class proves</strong>:
/// <list type="bullet">
///   <item><c>[D2RequireAnyScope]</c> at class level: right scope ⇒ pass;
///     wrong scope ⇒ <c>Unauthenticated</c> + <c>AUTH_SCOPE_INSUFFICIENT</c>;
///     no bearer ⇒ <c>Unauthenticated</c> + <c>AUTH_BEARER_MISSING</c>.</item>
///   <item><c>[D2RequireAllScopes]</c> at class level: both scopes ⇒ pass;
///     one missing ⇒ <c>AUTH_SCOPE_INSUFFICIENT</c>.</item>
///   <item><c>[D2HarmlessEndpoint]</c> at method level: no bearer ⇒ pass
///     (auth pipeline skipped).</item>
///   <item>Method-level override: class-level <c>[D2RequireAnyScope]</c> +
///     method-level <c>[D2HarmlessEndpoint]</c> ⇒ harmless wins
///     (last-declared-wins precedence confirmed at runtime).</item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "TestHost lifetime is bounded by individual tests, not the class.")]
public sealed class GrpcAttributeScopeEnforcementIntegrationTests
{
    private const string _ISSUER = "https://edge.internal";
    private const string _AUDIENCE = "files";
    private const string _SCOPE = "test.scope";
    private const string _SCOPE_A = "scope.a";
    private const string _SCOPE_B = "scope.b";

    // ──────────────────────────────────────────────────────────────────────
    // [D2RequireAnyScope] — class-level attribute path
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AttributeAnyScope_NoBearer_RejectedAsBearerMissing()
    {
        // AnyScope service wired via attribute only — no fluent extension.
        // Missing bearer must surface AUTH_BEARER_MISSING + Unauthenticated.
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new TestEcho.TestEchoClient(channel);

        var act = async () => await client.EchoAsync(new EchoRequest { Payload = "hi" });

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);

        // Status.Detail is intentionally empty (info-leak avoidance).
        ex.Which.Status.Detail.Should().BeEmpty();
    }

    [Fact]
    public async Task AttributeAnyScope_BearerWithRequiredScope_Succeeds()
    {
        // Attribute-only declaration [D2RequireAnyScope("test.scope")] on class.
        // Token carries the required scope → pipeline must let the call through.
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

        var reply = await client.EchoAsync(new EchoRequest { Payload = "attr-any" }, headers);

        reply.Echoed.Should().Be("attr-any");
    }

    [Fact]
    public async Task AttributeAnyScope_BearerWithWrongScope_RejectedAsScopeInsufficient()
    {
        // Token is valid (signed, not expired) but carries a different scope —
        // the attribute path ResolveFromAttributes must surface AUTH_SCOPE_INSUFFICIENT.
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new TestEcho.TestEchoClient(channel);
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = "entirely.different.scope",
            });
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        var act = async () => await client.EchoAsync(
            new EchoRequest { Payload = "shouldfail" }, headers);

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);

        ex.Which.Status.Detail.Should().BeEmpty();
    }

    [Fact]
    public async Task AttributeAnyScope_BearerWithNoScopes_RejectedAsScopeInsufficient()
    {
        // Token is valid but declares no scopes at all — any-of check must fail
        // with AUTH_SCOPE_INSUFFICIENT.
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new TestEcho.TestEchoClient(channel);
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE);
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        var act = async () => await client.EchoAsync(
            new EchoRequest { Payload = "noscopes" }, headers);

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    // ──────────────────────────────────────────────────────────────────────
    // [D2RequireAllScopes] — class-level attribute path
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AttributeAllScopes_BearerWithBothScopes_Succeeds()
    {
        // Attribute [D2RequireAllScopes("scope.a","scope.b")] on class.
        // Token carries both → all-of check must pass.
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostWithAllScopesAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new TestEcho.TestEchoClient(channel);
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = $"{_SCOPE_A} {_SCOPE_B}",
            });
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        var reply = await client.EchoAsync(
            new EchoRequest { Payload = "all-scopes" }, headers);

        reply.Echoed.Should().Be("all-scopes");
    }

    [Fact]
    public async Task AttributeAllScopes_BearerMissingOneScope_RejectedAsScopeInsufficient()
    {
        // Attribute [D2RequireAllScopes("scope.a","scope.b")] on class.
        // Token carries only scope.a — all-of check must fail.
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostWithAllScopesAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new TestEcho.TestEchoClient(channel);
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = _SCOPE_A,
            });
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        var act = async () => await client.EchoAsync(
            new EchoRequest { Payload = "missing-b" }, headers);

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    [Fact]
    public async Task AttributeAllScopes_NoBearer_RejectedAsBearerMissing()
    {
        // Attribute [D2RequireAllScopes] — no bearer means bearer-missing, not
        // scope-insufficient (bearer extraction runs before scope check).
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostWithAllScopesAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new TestEcho.TestEchoClient(channel);

        var act = async () => await client.EchoAsync(new EchoRequest { Payload = "nob" });

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
    }

    // ──────────────────────────────────────────────────────────────────────
    // [D2HarmlessEndpoint] — method-level attribute path
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AttributeHarmlessEndpoint_NoBearer_Succeeds()
    {
        // Health method decorated with [D2HarmlessEndpoint] via attribute only.
        // No bearer → interceptor must short-circuit and skip all auth work.
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new TestHealth.TestHealthClient(channel);

        var reply = await client.HealthAsync(new HealthRequest());

        reply.Status.Should().Be("ok");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Method-level [D2HarmlessEndpoint] overrides class-level
    // [D2RequireAnyScope] — last-declared-wins precedence at runtime
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AttributeMethodHarmlessOverridesClassAnyScope_NoBearer_Succeeds()
    {
        // AttributeOverrideService has [D2RequireAnyScope("test.scope")] at
        // class level AND [D2HarmlessEndpoint] at method level on Health.
        // ASP.NET routing appends class-level attributes BEFORE method-level
        // ones; last-declared-wins means [D2HarmlessEndpoint] (higher index)
        // overrides the class-level any-scope declaration.  No bearer must
        // succeed (harmless short-circuit wins).
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostWithOverrideServiceAsync(jwt);
        using var channel = CreateChannel(host);
        var client = new TestHealth.TestHealthClient(channel);

        var reply = await client.HealthAsync(new HealthRequest());

        reply.Status.Should().Be("ok");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Host builders — attribute-only wiring, NO fluent extension calls
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Host variant: registers <see cref="AttrAnyScopeEchoService"/> (class-level
    /// <c>[D2RequireAnyScope("test.scope")]</c>) and
    /// <see cref="AttrHarmlessHealthService"/> (method-level
    /// <c>[D2HarmlessEndpoint]</c> on the <c>Health</c> override).
    /// NO fluent scope extension is called at <c>MapGrpcService</c> time —
    /// the ONLY scope metadata comes from attributes.
    /// </summary>
    private static async Task<IHost> BuildHostAsync(TestJwtBuilder jwtBuilder)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices(services => ConfigureServices(services, jwtBuilder))
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            // Attribute-only: [D2RequireAnyScope("test.scope")] on class.
                            // NO .RequireAnyScope() call here — that would attach a
                            // MethodScopeMetadata instance that the interceptor resolves
                            // BEFORE reaching the attribute walk (fluent wins over attr).
                            endpoints.MapGrpcService<AttrAnyScopeEchoService>();

                            // [D2HarmlessEndpoint] on the Health method override.
                            endpoints.MapGrpcService<AttrHarmlessHealthService>();
                        });
                    });
            });

        return await hostBuilder.StartAsync();
    }

    /// <summary>
    /// Host variant: registers <see cref="AttrAllScopesEchoService"/> (class-level
    /// <c>[D2RequireAllScopes("scope.a","scope.b")]</c>).
    /// NO fluent scope extension is called at <c>MapGrpcService</c> time.
    /// </summary>
    private static async Task<IHost> BuildHostWithAllScopesAsync(TestJwtBuilder jwtBuilder)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices(services => ConfigureServices(services, jwtBuilder))
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            // Attribute-only: [D2RequireAllScopes("scope.a","scope.b")] on class.
                            // NO .RequireAllScopes() call here.
                            endpoints.MapGrpcService<AttrAllScopesEchoService>();
                        });
                    });
            });

        return await hostBuilder.StartAsync();
    }

    /// <summary>
    /// Host variant: registers <see cref="AttrOverrideHealthService"/> — a service
    /// with class-level <c>[D2RequireAnyScope("test.scope")]</c> overridden by
    /// method-level <c>[D2HarmlessEndpoint]</c> on the Health method — to prove
    /// last-declared-wins precedence at runtime through the real interceptor.
    /// </summary>
    private static async Task<IHost> BuildHostWithOverrideServiceAsync(TestJwtBuilder jwtBuilder)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices(services => ConfigureServices(services, jwtBuilder))
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            // NO fluent extension — attribute-only declaration.
                            endpoints.MapGrpcService<AttrOverrideHealthService>();
                        });
                    });
            });

        return await hostBuilder.StartAsync();
    }

    private static void ConfigureServices(
        IServiceCollection services,
        TestJwtBuilder jwtBuilder)
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

        // Swap the network-touching JwksProvider for the in-memory fake.
        services.RemoveAll<D2.Shared.Auth.Abstractions.Jwks.IJwksProvider>();
        services.RemoveAll<D2.Shared.Auth.Jwks.HttpJwksProvider>();
        services.AddSingleton<D2.Shared.Auth.Abstractions.Jwks.IJwksProvider>(
            new FakeJwksProvider(jwtBuilder.PublicKey));
        services.RemoveAll<JwtValidator>();
        services.AddSingleton(sp => new JwtValidator(
            sp.GetRequiredService<D2.Shared.Auth.Abstractions.Jwks.IJwksProvider>(),
            sp.GetRequiredService<IOptions<AuthOptions>>(),
            sp.GetRequiredService<ClaimsToContextMapper>(),
            Microsoft.Extensions.Logging.Abstractions
                .NullLogger<JwtValidator>.Instance));

        // gRPC stack + the auth interceptor wiring under test.
        services.AddGrpc();
        services.AddD2AuthGrpc();
    }

    private static GrpcChannel CreateChannel(IHost host)
    {
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
                return entry.Value;
        }

        return null;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test service implementations — ATTRIBUTE declaration path only.
    // NO fluent builder extension is called on these at registration time.
    // Each concrete type carries the attribute(s) under test.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Class-level <c>[D2RequireAnyScope("test.scope")]</c>. NO fluent
    /// <c>.RequireAnyScope()</c> wired at <c>MapGrpcService&lt;T&gt;()</c>.
    /// The attribute is the SOLE source of scope metadata.
    /// </summary>
    [D2RequireAnyScope("test.scope")]
    private sealed class AttrAnyScopeEchoService : TestEcho.TestEchoBase
    {
        public override Task<EchoReply> Echo(EchoRequest request, ServerCallContext context)
            => Task.FromResult(new EchoReply { Echoed = request.Payload });
    }

    /// <summary>
    /// Class-level <c>[D2RequireAllScopes("scope.a","scope.b")]</c>. NO fluent
    /// <c>.RequireAllScopes()</c> wired at <c>MapGrpcService&lt;T&gt;()</c>.
    /// The attribute is the SOLE source of scope metadata.
    /// </summary>
    [D2RequireAllScopes("scope.a", "scope.b")]
    private sealed class AttrAllScopesEchoService : TestEcho.TestEchoBase
    {
        public override Task<EchoReply> Echo(EchoRequest request, ServerCallContext context)
            => Task.FromResult(new EchoReply { Echoed = request.Payload });
    }

    /// <summary>
    /// Method-level <c>[D2HarmlessEndpoint]</c> on the <c>Health</c> override.
    /// No class-level attribute; no fluent wiring. The attribute is the SOLE
    /// source of harmless-endpoint metadata.
    /// </summary>
    private sealed class AttrHarmlessHealthService : TestHealth.TestHealthBase
    {
        [D2HarmlessEndpoint]
        public override Task<HealthReply> Health(HealthRequest request, ServerCallContext context)
            => Task.FromResult(new HealthReply { Status = "ok" });
    }

    /// <summary>
    /// Class-level <c>[D2RequireAnyScope("test.scope")]</c> PLUS method-level
    /// <c>[D2HarmlessEndpoint]</c> on the <c>Health</c> override. Used to prove
    /// that method-level wins over class-level at runtime through
    /// <c>ResolveFromAttributes</c>'s last-declared-wins walk.
    /// </summary>
    [D2RequireAnyScope("test.scope")]
    private sealed class AttrOverrideHealthService : TestHealth.TestHealthBase
    {
        [D2HarmlessEndpoint]
        public override Task<HealthReply> Health(HealthRequest request, ServerCallContext context)
            => Task.FromResult(new HealthReply { Status = "ok" });
    }

    /// <summary>
    /// Stub <see cref="ITieredCache"/> required by <see cref="JwtValidator"/>'s
    /// transitive dependency tree (session liveness path). The integration tests
    /// don't exercise revocation; the stub no-ops the liveness check.
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
