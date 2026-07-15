// -----------------------------------------------------------------------
// <copyright file="HttpScopeEnforcementIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.Auth.Http;

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AwesomeAssertions;
using D2.Shared.Auth;
using D2.Shared.Auth.Errors;
using D2.Shared.Auth.Http;
using D2.Shared.Auth.Http.Endpoints;
using D2.Shared.Auth.Validation;
using D2.Shared.Caching;
using D2.Shared.Caching.Local.Default;
using D2.Shared.Context.Abstractions;
using D2.Shared.Handler;
using D2.Shared.Handler.Abstractions;
using D2.Shared.ProblemDetails;
using D2.Shared.Result;
using D2.Shared.Tests.Unit.Auth.Inbound.Http.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// End-to-end runtime enforcement tests for HTTP-transport scope checks:
/// Goal A — real <see cref="Microsoft.AspNetCore.TestHost.TestServer"/> host
/// drives a real HTTP request through <c>JwtAuthMiddleware</c>, exercising
/// <c>RequireAnyScope</c> / <c>RequireAllScopes</c> / <c>MarkAsD2HarmlessEndpoint</c>
/// with a real <c>JwtValidator</c> backed by a <see cref="FakeJwksProvider"/>
/// and real RS256-signed JWTs from <see cref="TestJwtBuilder"/>.
///
/// Goal B — focused handler-pipeline integration: constructs a real
/// <c>BaseHandler</c> subclass whose <c>DefaultOptions.ScopeRequirement</c>
/// is set, constructs the real <c>IRequestContext</c> the transport would
/// produce (via <c>MutableRequestContext</c>), and invokes <c>HandleAsync</c>
/// directly — proving the per-handler scope pre-check fires correctly.
///
/// Goal B falls back to the focused handler-pipeline approach (rather than
/// wiring a handler through a live HTTP endpoint) because DI scoping for
/// <c>IRequestContext</c> in the TestServer context requires a per-request
/// scope, and the handler's <c>HandlerContext&lt;T&gt;</c> is Transient with an
/// <c>IRequestContext</c> dependency resolved from that scope — matching the
/// scoped-resolution invariant documented in <c>AuthHttpServiceCollectionExtensions</c>
/// is achievable, but it adds significant test-host ceremony that obscures what
/// Goal B actually exercises: the handler-pipeline scope pre-check itself.
/// The focused approach tests exactly the predicate the feature introduced,
/// with no extraneous wiring indirection.
/// </summary>
/// <remarks>
/// Sibling to <c>GrpcAuthIntegrationTests</c> on the gRPC side, and to
/// <c>JwtAuthMiddlewareTests</c> (unit, isolated middleware invocations). This
/// class is the HTTP integration counterpart: a full TestServer pipeline.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "TestHost lifetime is bounded by individual tests, not the class.")]
public sealed class HttpScopeEnforcementIntegrationTests
{
    private const string _ISSUER = "https://edge.internal";
    private const string _AUDIENCE = "files";
    private const string _SCOPE = "test.scope";
    private const string _SCOPE_A = "scope.a";
    private const string _SCOPE_B = "scope.b";
    private const string _TRANSPORT_SCOPE = "transport.scope";
    private const string _HANDLER_SCOPE = "handler.scope";

    // ── Goal A — HTTP middleware: RequireAnyScope ──────────────────────────

    [Fact]
    public async Task RequireAnyScope_BearerWithRequiredScope_Returns200()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        var client = host.GetTestServer().CreateClient();
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = _SCOPE });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("https://localhost/any");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RequireAnyScope_BearerWithDifferentScope_Returns401ScopeInsufficient()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        var client = host.GetTestServer().CreateClient();
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = "some.other.scope" });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("https://localhost/any");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var code = await ReadErrorCodeAsync(response);
        code.Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    [Fact]
    public async Task RequireAnyScope_BearerWithNoScopes_Returns401ScopeInsufficient()
    {
        // Token has no scope claim at all (empty set after parsing).
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        var client = host.GetTestServer().CreateClient();

        // MintToken without extraClaims ⇒ no "scope" claim in the JWT.
        var token = jwt.MintToken(_ISSUER, _AUDIENCE);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("https://localhost/any");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var code = await ReadErrorCodeAsync(response);
        code.Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    [Fact]
    public async Task RequireAnyScope_NoBearer_Returns401BearerMissing()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        var client = host.GetTestServer().CreateClient();

        // No Authorization header at all.
        var response = await client.GetAsync("https://localhost/any");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var code = await ReadErrorCodeAsync(response);
        code.Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
    }

    // ── Goal A — HTTP middleware: RequireAllScopes ─────────────────────────

    [Fact]
    public async Task RequireAllScopes_BearerWithBothScopes_Returns200()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        var client = host.GetTestServer().CreateClient();
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = $"{_SCOPE_A} {_SCOPE_B}",
            });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("https://localhost/all");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RequireAllScopes_BearerMissingOneScope_Returns401ScopeInsufficient()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        var client = host.GetTestServer().CreateClient();

        // Only scope.a — scope.b missing.
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = _SCOPE_A });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("https://localhost/all");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var code = await ReadErrorCodeAsync(response);
        code.Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    [Fact]
    public async Task RequireAllScopes_BearerWithNoScopes_Returns401ScopeInsufficient()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        var client = host.GetTestServer().CreateClient();
        var token = jwt.MintToken(_ISSUER, _AUDIENCE);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("https://localhost/all");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var code = await ReadErrorCodeAsync(response);
        code.Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    [Fact]
    public async Task RequireAllScopes_NoBearer_Returns401BearerMissing()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        var client = host.GetTestServer().CreateClient();

        var response = await client.GetAsync("https://localhost/all");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var code = await ReadErrorCodeAsync(response);
        code.Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
    }

    // ── Goal A — HTTP middleware: MarkAsD2HarmlessEndpoint ─────────────────

    [Fact]
    public async Task HarmlessEndpoint_NoBearer_Returns200()
    {
        // MarkAsD2HarmlessEndpoint() ⇒ middleware short-circuits before bearer
        // extraction; no Authorization header is required.
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        var client = host.GetTestServer().CreateClient();

        var response = await client.GetAsync("https://localhost/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Goal B — full-chain: handler ScopeRequirement defense-in-depth ─────

    [Fact]
    public async Task HandlerScopeRequirement_CallerLacksHandlerScope_ReturnsForbidden()
    {
        // Transport passes (caller has transport.scope), but the handler requires
        // handler.scope which the caller does NOT hold ⇒ handler returns Forbidden.
        var request = BuildRequestContext(scopes: [_TRANSPORT_SCOPE]);
        var handler = BuildHandler(
            request,
            scopeRequirement: new ScopeRequirement(
                HandlerScopeMatch.All,
                FrozenSet.ToFrozenSet([_HANDLER_SCOPE])));

        var result = await handler.HandleAsync(new EchoInput("ping"));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HandlerScopeRequirement_CallerHasBothTransportAndHandlerScopes_Succeeds()
    {
        // Caller has transport.scope (transport layer would admit) AND handler.scope
        // (handler scope pre-check passes) ⇒ handler executes and returns Ok.
        var request = BuildRequestContext(scopes: [_TRANSPORT_SCOPE, _HANDLER_SCOPE]);
        var handler = BuildHandler(
            request,
            scopeRequirement: new ScopeRequirement(
                HandlerScopeMatch.All,
                FrozenSet.ToFrozenSet([_HANDLER_SCOPE])));

        var result = await handler.HandleAsync(new EchoInput("ping"));

        result.Success.Should().BeTrue();
        result.Data.Should().Be("ping");
    }

    [Fact]
    public async Task HandlerScopeRequirement_AnyOf_CallerHasOneOfRequired_Succeeds()
    {
        // HandlerScopeMatch.Any: caller holds "handler.scope" which overlaps
        // the required set [handler.scope, other.scope] ⇒ passes.
        var request = BuildRequestContext(scopes: [_TRANSPORT_SCOPE, _HANDLER_SCOPE]);
        var handler = BuildHandler(
            request,
            scopeRequirement: new ScopeRequirement(
                HandlerScopeMatch.Any,
                FrozenSet.ToFrozenSet([_HANDLER_SCOPE, "other.scope"])));

        var result = await handler.HandleAsync(new EchoInput("pong"));

        result.Success.Should().BeTrue();
        result.Data.Should().Be("pong");
    }

    [Fact]
    public async Task HandlerScopeRequirement_AnyOf_CallerHasNoneOfRequired_ReturnsForbidden()
    {
        // HandlerScopeMatch.Any: caller holds transport.scope only — no overlap
        // with [handler.scope, other.scope] ⇒ handler returns Forbidden.
        var request = BuildRequestContext(scopes: [_TRANSPORT_SCOPE]);
        var handler = BuildHandler(
            request,
            scopeRequirement: new ScopeRequirement(
                HandlerScopeMatch.Any,
                FrozenSet.ToFrozenSet([_HANDLER_SCOPE, "other.scope"])));

        var result = await handler.HandleAsync(new EchoInput("x"));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HandlerScopeRequirement_Null_AnyAuthenticatedCallerSucceeds()
    {
        // Null ScopeRequirement ⇒ pipeline guard is disabled; any authenticated
        // caller (that passed transport) may invoke the handler.
        var request = BuildRequestContext(scopes: [_TRANSPORT_SCOPE]);
        var handler = BuildHandler(request, scopeRequirement: null);

        var result = await handler.HandleAsync(new EchoInput("hi"));

        result.Success.Should().BeTrue();
        result.Data.Should().Be("hi");
    }

    // ── Host builder ───────────────────────────────────────────────────────

    private static async Task<IHost> BuildHostAsync(TestJwtBuilder jwtBuilder)
    {
        // Mirror the GrpcAuthIntegrationTests host-builder pattern: wire the
        // real JwtAuthMiddleware (via UseD2Auth()) with a real JwtValidator
        // backed by FakeJwksProvider + FakeSessionLivenessTracker, then map
        // minimal endpoints annotated with the fluent scope extensions.
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

                        // Swap the network-touching JWKS provider for the in-memory
                        // fake — same pattern as GrpcAuthIntegrationTests.
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
                            NullLogger<JwtValidator>.Instance));

                        // Swap the session liveness tracker for the in-memory fake
                        // (always alive — tests focus on scope enforcement, not
                        // session revocation).
                        services.RemoveAll<D2.Shared.Auth.Abstractions.Sessions.ISessionLivenessTracker>();
                        services.AddSingleton<D2.Shared.Auth.Abstractions.Sessions.ISessionLivenessTracker>(
                            new FakeSessionLivenessTracker());
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();

                        // JwtAuthMiddleware — registered AFTER UseRouting (so
                        // endpoint metadata is available) and BEFORE UseEndpoints.
                        app.UseD2Auth();

                        app.UseEndpoints(endpoints =>
                        {
                            // /any — RequireAnyScope("test.scope")
                            endpoints
                                .MapGet("/any", () => Results.Ok("ok"))
                                .RequireAnyScope(_SCOPE);

                            // /all — RequireAllScopes("scope.a", "scope.b")
                            endpoints
                                .MapGet("/all", () => Results.Ok("ok"))
                                .RequireAllScopes(_SCOPE_A, _SCOPE_B);

                            // /health — MarkAsD2HarmlessEndpoint() (no auth required)
                            endpoints
                                .MapGet("/health", () => Results.Ok("ok"))
                                .MarkAsD2HarmlessEndpoint();
                        });
                    });
            });

        return await hostBuilder.StartAsync();
    }

    // ── Goal B helpers ─────────────────────────────────────────────────────

    private static IRequestContext BuildRequestContext(IEnumerable<string> scopes)
    {
        // Construct the same MutableRequestContext the transport middleware would
        // produce after successful JWT validation, populating only the fields
        // relevant to the handler scope pre-check. The handler's pipeline guard
        // reads IRequestContext.Scopes — everything else can be default.
        return new MutableRequestContext
        {
            IsAuthenticated = true,
            Scopes = scopes.ToFrozenSet(StringComparer.Ordinal),
            TraceId = "test-trace-id",
        };
    }

    private static EchoHandler BuildHandler(
        IRequestContext request,
        ScopeRequirement? scopeRequirement)
    {
        // Build the minimum DI graph needed to instantiate a real BaseHandler:
        // an IServiceProvider with HandlerContext<EchoHandler> registered
        // Transient via AddD2Handler(). IRequestContext is provided directly
        // (not resolved from scope) because this is the focused-handler path
        // that bypasses the HTTP transport.
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddD2Handler();

        // Register the request context as a singleton so HandlerContext<T>
        // can resolve it via DI (matches what the scoped transport resolver
        // would do; here the "scope" is the test method itself).
        services.AddSingleton(request);

        var sp = services.BuildServiceProvider();
        var context = sp.GetRequiredService<HandlerContext<EchoHandler>>();
        return new EchoHandler(context, scopeRequirement);
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty(
                D2ProblemDetailsKeys.EXTENSION_ERROR_CODE, out var el))
            return el.GetString();
        return null;
    }

    // ── Test-only handler ──────────────────────────────────────────────────

    /// <summary>
    /// Minimal input record for <see cref="EchoHandler"/>.
    /// </summary>
    private sealed record EchoInput(string Payload);

    /// <summary>
    /// Test-only <see cref="BaseHandler{TSelf,TInput,TOutput}"/> that echoes its
    /// input payload as output. Exposes the <c>scopeRequirement</c> ctor argument via
    /// <see cref="DefaultOptions"/> so individual tests can inject any requirement.
    /// </summary>
    private sealed class EchoHandler
        : BaseHandler<EchoHandler, EchoInput, string>
    {
        private readonly ScopeRequirement? r_requirement;

        public EchoHandler(
            HandlerContext<EchoHandler> context,
            ScopeRequirement? scopeRequirement)
            : base(context)
        {
            r_requirement = scopeRequirement;
        }

        protected override HandlerOptions DefaultOptions =>
            new() { ScopeRequirement = r_requirement };

        protected override ValueTask<D2Result<string?>> ExecuteAsync(
            EchoInput input,
            CancellationToken ct)
            => new(D2Result<string?>.Ok(input.Payload));
    }

    // ── Stubs ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Stub <see cref="ITieredCache"/> — satisfies the transitive DI
    /// dependency of <see cref="JwtValidator"/> (session liveness path) without
    /// actually touching any cache. Integration tests don't exercise revocation;
    /// the stub no-ops everything.
    /// </summary>
    private sealed class FakeTieredCacheStub : ITieredCache
    {
        public ValueTask<D2Result<bool>> ExistsAsync(
            string key, CancellationToken ct = default)
            => new(D2Result<bool>.Ok(true));

        public ValueTask<D2Result<T?>> GetAsync<T>(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<IReadOnlyDictionary<string, T?>>> GetManyAsync<T>(
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
