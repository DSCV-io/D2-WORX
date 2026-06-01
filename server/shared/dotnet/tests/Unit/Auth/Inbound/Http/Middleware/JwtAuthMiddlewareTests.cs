// -----------------------------------------------------------------------
// <copyright file="JwtAuthMiddlewareTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Http.Middleware;

using System.IO;
using System.Text.Json;
using AwesomeAssertions;
using D2.Shared.Auth;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Abstractions.Http;
using D2.Shared.Auth.Errors;
using D2.Shared.Auth.Http.Endpoints;
using D2.Shared.Auth.Http.Middleware;
using D2.Shared.Auth.Validation;
using D2.Shared.Context.Abstractions;
using D2.Shared.Tests.Unit.Auth.Inbound.Http.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Xunit;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "AccessToDisposedClosure",
    Justification = "Lambdas execute within the test method's using-scope; "
        + "the captured builders outlive the lambda's invocation.")]
public sealed class JwtAuthMiddlewareTests
{
    private const string _ISSUER = "https://edge.internal";
    private const string _AUDIENCE = "files";
    private const string _BEARER_PREFIX = "Bearer ";

    [Fact]
    public async Task InvokeAsync_HarmlessEndpoint_SkipsValidatorAndCallsNext()
    {
        using var builder = new TestJwtBuilder();
        var liveness = new FakeSessionLivenessTracker();
        var (mw, nextCalled) = MakeMiddleware(builder, liveness);
        var ctx = MakeContext(
            authorization: null, metadata: EndpointScopeMetadata.HarmlessEndpoint);

        await mw.InvokeAsync(ctx);

        nextCalled().Should().BeTrue();
        liveness.InvocationCount.Should().Be(0);
        ctx.Items.Should().NotContainKey(D2HttpContextItems.REQUEST_CONTEXT);
    }

    [Fact]
    public async Task InvokeAsync_NoAuthorizationHeader_EmitsBearerMissing401()
    {
        using var builder = new TestJwtBuilder();
        var (mw, nextCalled) = MakeMiddleware(builder);
        var ctx = MakeContext(authorization: null);

        await mw.InvokeAsync(ctx);

        nextCalled().Should().BeFalse();
        ctx.Response.StatusCode.Should().Be(401);
        var problem = await ReadProblemAsync(ctx);
        problem.GetProperty("d2_error_code").GetString()
            .Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
    }

    [Fact]
    public async Task InvokeAsync_BasicAuthScheme_EmitsBearerMissing401()
    {
        using var builder = new TestJwtBuilder();
        var (mw, _) = MakeMiddleware(builder);
        var ctx = MakeContext(authorization: "Basic dXNlcjpwYXNz");

        await mw.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(401);
        var problem = await ReadProblemAsync(ctx);
        problem.GetProperty("d2_error_code").GetString()
            .Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
    }

    [Fact]
    public async Task InvokeAsync_LowercaseBearerPrefix_AcceptedCaseInsensitively()
    {
        // RFC 6750 §2.1: prefix is case-insensitive.
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE);
        var (mw, nextCalled) = MakeMiddleware(builder);
        var ctx = MakeContext(authorization: $"bearer {token}");

        await mw.InvokeAsync(ctx);

        nextCalled().Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_EmptyTokenAfterBearerPrefix_EmitsBearerMissing401()
    {
        // "Bearer " with no token after — treated as missing (semantically
        // nothing to validate), distinct from "malformed" (validator's job).
        using var builder = new TestJwtBuilder();
        var (mw, _) = MakeMiddleware(builder);
        var ctx = MakeContext(authorization: "Bearer ");

        await mw.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(401);
        var problem = await ReadProblemAsync(ctx);
        problem.GetProperty("d2_error_code").GetString()
            .Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
    }

    [Fact]
    public async Task InvokeAsync_MultipleAuthorizationHeaders_TakesFirst()
    {
        using var builder = new TestJwtBuilder();
        var validToken = builder.MintToken(_ISSUER, _AUDIENCE);
        var (mw, nextCalled) = MakeMiddleware(builder);
        var ctx = MakeContext(authorization: null);

        // Set BOTH headers — the first one (valid) should be used, the second
        // (garbage) ignored.
        ctx.Request.Headers.Authorization = new StringValues(
            new[] { $"Bearer {validToken}", "Bearer not.a.jwt" });

        await mw.InvokeAsync(ctx);

        nextCalled().Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_MalformedToken_BubblesValidatorBearerMalformed()
    {
        using var builder = new TestJwtBuilder();
        var (mw, _) = MakeMiddleware(builder);
        var ctx = MakeContext(authorization: $"{_BEARER_PREFIX}not.a.jwt.too.many.parts");

        await mw.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(401);
        var problem = await ReadProblemAsync(ctx);
        problem.GetProperty("d2_error_code").GetString()
            .Should().Be(AuthErrorCodes.AUTH_BEARER_MALFORMED);
    }

    [Fact]
    public async Task InvokeAsync_ExpiredToken_BubblesValidatorJwtExpired()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            notBefore: DateTimeOffset.UtcNow.AddHours(-2),
            expires: DateTimeOffset.UtcNow.AddHours(-1));
        var (mw, _) = MakeMiddleware(builder);
        var ctx = MakeContext(authorization: $"{_BEARER_PREFIX}{token}");

        await mw.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(401);
        var problem = await ReadProblemAsync(ctx);
        problem.GetProperty("d2_error_code").GetString()
            .Should().Be(AuthErrorCodes.AUTH_JWT_EXPIRED);
    }

    [Fact]
    public async Task InvokeAsync_LivenessRevoked_Emits401SessionRevoked()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE);
        var liveness = new FakeSessionLivenessTracker
        {
            OutcomeForSession = _ => FakeSessionLivenessTracker.Revoked(),
        };
        var (mw, nextCalled) = MakeMiddleware(builder, liveness);
        var ctx = MakeContext(authorization: $"{_BEARER_PREFIX}{token}");

        await mw.InvokeAsync(ctx);

        nextCalled().Should().BeFalse();
        ctx.Response.StatusCode.Should().Be(401);
        var problem = await ReadProblemAsync(ctx);
        problem.GetProperty("d2_error_code").GetString()
            .Should().Be(AuthErrorCodes.AUTH_SESSION_REVOKED);
    }

    [Fact]
    public async Task InvokeAsync_LivenessUnavailable_Emits503()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE);
        var liveness = new FakeSessionLivenessTracker
        {
            OutcomeForSession = _ => FakeSessionLivenessTracker.Unavailable(),
        };
        var (mw, _) = MakeMiddleware(builder, liveness);
        var ctx = MakeContext(authorization: $"{_BEARER_PREFIX}{token}");

        await mw.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(503);
        var problem = await ReadProblemAsync(ctx);
        problem.GetProperty("d2_error_code").GetString()
            .Should().Be(AuthErrorCodes.AUTH_SESSION_LIVENESS_UNAVAILABLE);
    }

    [Fact]
    public async Task InvokeAsync_LivenessValidationFailed_FailsClosedAs401SessionRevoked()
    {
        // ValidationFailed from liveness is "shouldn't happen given a validated JWT"
        // — defensive 401 rather than letting through.
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE);
        var liveness = new FakeSessionLivenessTracker
        {
            OutcomeForSession = _ => FakeSessionLivenessTracker.ValidationFailed(),
        };
        var (mw, _) = MakeMiddleware(builder, liveness);
        var ctx = MakeContext(authorization: $"{_BEARER_PREFIX}{token}");

        await mw.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_NoSessionIdInToken_SkipsLivenessCheck()
    {
        // Service-identity token style — no d2_session_id claim. Validator
        // only accepts when RequireSessionIdClaim is false; we override
        // here so the validator passes through without the session claim.
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE, includeSessionId: false);
        var liveness = new FakeSessionLivenessTracker();
        var (mw, nextCalled) = MakeMiddleware(
            builder, liveness, configure: o => o.Validator = o.Validator with
            {
                RequireSessionIdClaim = false,
            });
        var ctx = MakeContext(authorization: $"{_BEARER_PREFIX}{token}");

        await mw.InvokeAsync(ctx);

        nextCalled().Should().BeTrue();
        liveness.InvocationCount.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_SuccessPath_PopulatesRequestContextOnHttpContextItems()
    {
        using var builder = new TestJwtBuilder();
        var sessionId = Guid.NewGuid();
        var token = builder.MintToken(_ISSUER, _AUDIENCE, sessionId: sessionId);
        var (mw, nextCalled) = MakeMiddleware(builder);
        var ctx = MakeContext(authorization: $"{_BEARER_PREFIX}{token}");

        await mw.InvokeAsync(ctx);

        nextCalled().Should().BeTrue();
        var stored = ctx.Items[D2HttpContextItems.REQUEST_CONTEXT] as IRequestContext;
        stored.Should().NotBeNull();
        stored.IsAuthenticated.Should().BeTrue();
        stored.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public async Task InvokeAsync_NoEndpointMetadata_PassesAnyAuthenticatedCaller()
    {
        // Deny-by-default lives in absence-of-metadata: any authenticated
        // caller passes the empty required-set check.
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE);
        var (mw, nextCalled) = MakeMiddleware(builder);
        var ctx = MakeContext(authorization: $"{_BEARER_PREFIX}{token}", metadata: null);

        await mw.InvokeAsync(ctx);

        nextCalled().Should().BeTrue();
    }

    // ── ScopeMatch.Any tests ───────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_AnyScope_RequiredButCallerHasNone_Emits401ScopeInsufficient()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE);
        var (mw, nextCalled) = MakeMiddleware(builder);
        var ctx = MakeContext(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata: EndpointScopeMetadata.ForScopes(
                new[] { "files.read" }, ScopeMatch.Any));

        await mw.InvokeAsync(ctx);

        nextCalled().Should().BeFalse();
        ctx.Response.StatusCode.Should().Be(401);
        var problem = await ReadProblemAsync(ctx);
        problem.GetProperty("d2_error_code").GetString()
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    [Fact]
    public async Task InvokeAsync_AnyScope_CallerHasOneOfRequired_PassesThrough()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = "files.read other.scope",
            });
        var (mw, nextCalled) = MakeMiddleware(builder);
        var ctx = MakeContext(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata: EndpointScopeMetadata.ForScopes(
                new[] { "files.read", "files.admin" }, ScopeMatch.Any));

        await mw.InvokeAsync(ctx);

        nextCalled().Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AnyScope_CallerHasAllRequired_PassesThrough()
    {
        // any-of semantics: a caller holding a SUPERSET of the required scopes
        // (i.e. all of them and more) must still pass — the predicate is
        // "at least one overlap", not "exactly one match".
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = "files.read files.admin extra.scope",
            });
        var (mw, nextCalled) = MakeMiddleware(builder);
        var ctx = MakeContext(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata: EndpointScopeMetadata.ForScopes(
                new[] { "files.read", "files.admin" }, ScopeMatch.Any));

        await mw.InvokeAsync(ctx);

        nextCalled().Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AnyScope_CallerHasOnlyDifferentScope_Emits401()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = "other.unrelated.scope",
            });
        var (mw, _) = MakeMiddleware(builder);
        var ctx = MakeContext(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata: EndpointScopeMetadata.ForScopes(
                new[] { "files.read" }, ScopeMatch.Any));

        await mw.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(401);
        var problem = await ReadProblemAsync(ctx);
        problem.GetProperty("d2_error_code").GetString()
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    // ── ScopeMatch.All tests ───────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_AllScopes_CallerHasAllRequired_PassesThrough()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = "files.read files.write other.scope",
            });
        var (mw, nextCalled) = MakeMiddleware(builder);
        var ctx = MakeContext(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata: EndpointScopeMetadata.ForScopes(
                new[] { "files.read", "files.write" }, ScopeMatch.All));

        await mw.InvokeAsync(ctx);

        nextCalled().Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AllScopes_CallerMissingOneRequired_Emits401ScopeInsufficient()
    {
        // Caller has "files.read" but NOT "files.write" — all-of fails.
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = "files.read",
            });
        var (mw, nextCalled) = MakeMiddleware(builder);
        var ctx = MakeContext(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata: EndpointScopeMetadata.ForScopes(
                new[] { "files.read", "files.write" }, ScopeMatch.All));

        await mw.InvokeAsync(ctx);

        nextCalled().Should().BeFalse();
        ctx.Response.StatusCode.Should().Be(401);
        var problem = await ReadProblemAsync(ctx);
        problem.GetProperty("d2_error_code").GetString()
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    [Fact]
    public async Task InvokeAsync_AllScopes_CallerHasNone_Emits401ScopeInsufficient()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE);
        var (mw, nextCalled) = MakeMiddleware(builder);
        var ctx = MakeContext(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata: EndpointScopeMetadata.ForScopes(
                new[] { "files.read", "files.write" }, ScopeMatch.All));

        await mw.InvokeAsync(ctx);

        nextCalled().Should().BeFalse();
        ctx.Response.StatusCode.Should().Be(401);
        var problem = await ReadProblemAsync(ctx);
        problem.GetProperty("d2_error_code").GetString()
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    [Fact]
    public async Task InvokeAsync_AllScopes_SingleRequiredScopePresent_PassesThrough()
    {
        // all-of with a single required scope is equivalent to any-of with one.
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = "files.admin extra.scope",
            });
        var (mw, nextCalled) = MakeMiddleware(builder);
        var ctx = MakeContext(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata: EndpointScopeMetadata.ForScopes(
                new[] { "files.admin" }, ScopeMatch.All));

        await mw.InvokeAsync(ctx);

        nextCalled().Should().BeTrue();
    }

    // ── Middleware construction + miscellaneous ───────────────────────────

    [Fact]
    public async Task InvokeAsync_NextCalledExactlyOnceOnSuccess()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE);
        var nextCount = 0;
        var validator = MakeValidator(builder);
        var liveness = new FakeSessionLivenessTracker();
        var mw = new JwtAuthMiddleware(
            _ =>
            {
                nextCount++;
                return Task.CompletedTask;
            },
            validator,
            liveness,
            NullLogger<JwtAuthMiddleware>.Instance);
        var ctx = MakeContext(authorization: $"{_BEARER_PREFIX}{token}");

        await mw.InvokeAsync(ctx);

        nextCount.Should().Be(1);
    }

    [Fact]
    public async Task InvokeAsync_CancellationPropagates()
    {
        // RequestAborted-driven cancellation surfaces from the validator
        // via the JWKS provider's honored CT — propagate, don't swallow.
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE);
        var (mw, _) = MakeMiddleware(
            builder,
            liveness: new FakeSessionLivenessTracker
            {
                OutcomeForSession = _ => throw new OperationCanceledException(),
            });
        var ctx = MakeContext(authorization: $"{_BEARER_PREFIX}{token}");
        using var cts = new CancellationTokenSource();
        ctx.Features.Set<IHttpRequestLifetimeFeature>(new TestLifetime(cts.Token));
        cts.Cancel();

        var act = async () => await mw.InvokeAsync(ctx);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Constructor_NullNext_Throws()
    {
        using var builder = new TestJwtBuilder();
        var validator = MakeValidator(builder);
        var liveness = new FakeSessionLivenessTracker();

        var act = () => new JwtAuthMiddleware(
            null!, validator, liveness, NullLogger<JwtAuthMiddleware>.Instance);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullValidator_Throws()
    {
        var liveness = new FakeSessionLivenessTracker();

        var act = () => new JwtAuthMiddleware(
            _ => Task.CompletedTask,
            null!,
            liveness,
            NullLogger<JwtAuthMiddleware>.Instance);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLivenessTracker_Throws()
    {
        using var builder = new TestJwtBuilder();
        var validator = MakeValidator(builder);

        var act = () => new JwtAuthMiddleware(
            _ => Task.CompletedTask,
            validator,
            null!,
            NullLogger<JwtAuthMiddleware>.Instance);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        using var builder = new TestJwtBuilder();
        var validator = MakeValidator(builder);
        var liveness = new FakeSessionLivenessTracker();

        var act = () => new JwtAuthMiddleware(
            _ => Task.CompletedTask, validator, liveness, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task InvokeAsync_NullContext_Throws()
    {
        using var builder = new TestJwtBuilder();
        var (mw, _) = MakeMiddleware(builder);

        var act = async () => await mw.InvokeAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task InvokeAsync_ProblemDetails_OmitsDetailField()
    {
        using var builder = new TestJwtBuilder();
        var (mw, _) = MakeMiddleware(builder);
        var ctx = MakeContext(authorization: null);

        await mw.InvokeAsync(ctx);

        var problem = await ReadProblemAsync(ctx);
        problem.TryGetProperty("detail", out _).Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_ProblemDetails_InstanceIsMethodPlusPathNotPathPlusQuery()
    {
        // The unified instance shape across emit paths (auth-http path A +
        // aspnetcore path B) is "{Method} {Path}" — query string is
        // deliberately stripped because it can carry secrets (?secret=...,
        // ?token=...). Method is included for operator-diagnostic value.
        using var builder = new TestJwtBuilder();
        var (mw, _) = MakeMiddleware(builder);
        var ctx = MakeContext(authorization: null);
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/api/x";
        ctx.Request.QueryString = new QueryString("?secret=should-not-leak");

        await mw.InvokeAsync(ctx);

        var problem = await ReadProblemAsync(ctx);
        problem.GetProperty("instance").GetString().Should().Be("GET /api/x");
    }

    private static (JwtAuthMiddleware Middleware, Func<bool> NextCalled) MakeMiddleware(
        TestJwtBuilder builder,
        FakeSessionLivenessTracker? liveness = null,
        Action<AuthOptions>? configure = null)
    {
        var nextCalled = false;
        var validator = MakeValidator(builder, configure);
        var mw = new JwtAuthMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            validator,
            liveness ?? new FakeSessionLivenessTracker(),
            NullLogger<JwtAuthMiddleware>.Instance);
        return (mw, () => nextCalled);
    }

    private static JwtValidator MakeValidator(
        TestJwtBuilder builder, Action<AuthOptions>? configure = null)
    {
        var options = new AuthOptions
        {
            Issuer = new Uri(_ISSUER),
            Audience = _AUDIENCE,
        };
        configure?.Invoke(options);
        return new JwtValidator(
            new FakeJwksProvider(builder.PublicKey),
            Options.Create(options),
            new ClaimsToContextMapper(),
            NullLogger<JwtValidator>.Instance);
    }

    private static DefaultHttpContext MakeContext(
        string? authorization,
        EndpointScopeMetadata? metadata = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/x";
        ctx.Response.Body = new MemoryStream();
        if (authorization is not null)
            ctx.Request.Headers.Authorization = authorization;
        if (metadata is not null)
        {
            var endpoint = new Endpoint(
                requestDelegate: _ => Task.CompletedTask,
                metadata: new EndpointMetadataCollection(metadata),
                displayName: "test-endpoint");
            ctx.SetEndpoint(endpoint);
        }

        return ctx;
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpContext ctx)
    {
        var ms = (MemoryStream)ctx.Response.Body;
        ms.Position = 0;
        ms.Length.Should().BeGreaterThan(0);
        using var doc = await JsonDocument.ParseAsync(ms);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Stand-in for <see cref="IHttpRequestLifetimeFeature"/> so tests can
    /// drive <see cref="HttpContext.RequestAborted"/> from a CT they own.
    /// </summary>
    private sealed class TestLifetime : IHttpRequestLifetimeFeature
    {
        public TestLifetime(CancellationToken token) => RequestAborted = token;

        public CancellationToken RequestAborted { get; set; }

        public void Abort() => throw new NotSupportedException();
    }
}
