// -----------------------------------------------------------------------
// <copyright file="JwtAuthInterceptorTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Interceptors;

using AwesomeAssertions;
using D2.Shared.Auth;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Abstractions.Http;
using D2.Shared.Auth.Errors;
using D2.Shared.Auth.Grpc.Endpoints;
using D2.Shared.Auth.Grpc.Interceptors;
using D2.Shared.Auth.Grpc.Status;
using D2.Shared.Auth.Validation;
using D2.Shared.Context.Abstractions;
using D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Fixtures;
using global::Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using GrpcStatusCode = global::Grpc.Core.StatusCode;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "AccessToDisposedClosure",
    Justification = "Lambdas execute within the test method's using-scope; "
        + "the captured builders outlive the lambda's invocation.")]
public sealed class JwtAuthInterceptorTests
{
    private const string _ISSUER = "https://edge.internal";
    private const string _AUDIENCE = "files";
    private const string _BEARER_PREFIX = "Bearer ";

    // ---- Constructor null guards ----

    [Fact]
    public void Constructor_NullValidator_Throws()
    {
        var liveness = new FakeSessionLivenessTracker();

        var act = () => new JwtAuthInterceptor(
            null!, liveness, NullLogger<JwtAuthInterceptor>.Instance);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLivenessTracker_Throws()
    {
        using var builder = new TestJwtBuilder();
        var validator = MakeValidator(builder);

        var act = () => new JwtAuthInterceptor(
            validator, null!, NullLogger<JwtAuthInterceptor>.Instance);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        using var builder = new TestJwtBuilder();
        var validator = MakeValidator(builder);
        var liveness = new FakeSessionLivenessTracker();

        var act = () => new JwtAuthInterceptor(validator, liveness, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ---- Harmless-endpoint opt-in ----

    [Fact]
    public async Task UnaryServerHandler_HarmlessEndpointMetadata_SkipsValidatorAndCallsNext()
    {
        using var builder = new TestJwtBuilder();
        var liveness = new FakeSessionLivenessTracker();
        var interceptor = MakeInterceptor(builder, liveness);
        var ctx = MakeContext(authorization: null, metadata: MethodScopeMetadata.HarmlessEndpoint);
        var continuationCalled = false;

        await interceptor.UnaryServerHandler<string, string>(
            "req",
            ctx,
            (_, _) =>
            {
                continuationCalled = true;
                return Task.FromResult("reply");
            });

        continuationCalled.Should().BeTrue();
        liveness.InvocationCount.Should().Be(0);
        ctx.UserState.Should().NotContainKey(D2GrpcUserStateKeys.REQUEST_CONTEXT);
    }

    // ---- Bearer extraction across the four RPC kinds ----

    [Fact]
    public async Task UnaryServerHandler_NoAuthorizationMetadata_ThrowsUnauthenticated()
    {
        using var builder = new TestJwtBuilder();
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(authorization: null);

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req", ctx, (_, _) => Task.FromResult("reply"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
    }

    [Fact]
    public async Task ClientStreamingServerHandler_NoAuthorizationMetadata_ThrowsUnauthenticated()
    {
        using var builder = new TestJwtBuilder();
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(authorization: null);

        var act = async () =>
            await interceptor.ClientStreamingServerHandler<string, string>(
                new EmptyAsyncStreamReader<string>(),
                ctx,
                (_, _) => Task.FromResult("reply"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
    }

    [Fact]
    public async Task ServerStreamingServerHandler_NoAuthorizationMetadata_ThrowsUnauthenticated()
    {
        using var builder = new TestJwtBuilder();
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(authorization: null);

        var act = async () =>
            await interceptor.ServerStreamingServerHandler<string, string>(
                "req",
                new DiscardingServerStreamWriter<string>(),
                ctx,
                (_, _, _) => Task.CompletedTask);

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
    }

    [Fact]
    public async Task DuplexStreamingServerHandler_NoAuthorizationMetadata_ThrowsUnauthenticated()
    {
        using var builder = new TestJwtBuilder();
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(authorization: null);

        var act = async () =>
            await interceptor.DuplexStreamingServerHandler(
                new EmptyAsyncStreamReader<string>(),
                new DiscardingServerStreamWriter<string>(),
                ctx,
                (_, _, _) => Task.CompletedTask);

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
    }

    // ---- Bearer extraction edge cases ----

    [Fact]
    public async Task UnaryServerHandler_BasicAuthScheme_ThrowsBearerMissing()
    {
        using var builder = new TestJwtBuilder();
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(authorization: "Basic dXNlcjpwYXNz");

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req", ctx, (_, _) => Task.FromResult("reply"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
    }

    [Fact]
    public async Task UnaryServerHandler_LowercaseBearerPrefix_AcceptedCaseInsensitively()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE);
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(authorization: $"bearer {token}");
        var continuationCalled = false;

        await interceptor.UnaryServerHandler<string, string>(
            "req",
            ctx,
            (_, _) =>
            {
                continuationCalled = true;
                return Task.FromResult("reply");
            });

        continuationCalled.Should().BeTrue();
    }

    [Fact]
    public async Task UnaryServerHandler_EmptyTokenAfterPrefix_ThrowsBearerMissing()
    {
        using var builder = new TestJwtBuilder();
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(authorization: "Bearer ");

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req", ctx, (_, _) => Task.FromResult("reply"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
    }

    [Fact]
    public async Task UnaryServerHandler_MultipleAuthorizationHeaders_TakesFirst()
    {
        using var builder = new TestJwtBuilder();
        var validToken = builder.MintToken(_ISSUER, _AUDIENCE);
        var interceptor = MakeInterceptor(builder);

        // First entry is valid, second is garbage — first wins.
        var headers = new Metadata
        {
            { "authorization", $"Bearer {validToken}" },
            { "authorization", "Bearer not.a.jwt" },
        };
        var ctx = MakeContext(headers);
        var continuationCalled = false;

        await interceptor.UnaryServerHandler<string, string>(
            "req",
            ctx,
            (_, _) =>
            {
                continuationCalled = true;
                return Task.FromResult("reply");
            });

        continuationCalled.Should().BeTrue();
    }

    [Fact]
    public async Task UnaryServerHandler_MalformedToken_BubblesValidatorBearerMalformed()
    {
        using var builder = new TestJwtBuilder();
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(authorization: $"{_BEARER_PREFIX}not.a.jwt.too.many.parts");

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req", ctx, (_, _) => Task.FromResult("reply"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_BEARER_MALFORMED);
    }

    [Fact]
    public async Task UnaryServerHandler_ExpiredToken_BubblesValidatorJwtExpired()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            notBefore: DateTimeOffset.UtcNow.AddHours(-2),
            expires: DateTimeOffset.UtcNow.AddHours(-1));
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(authorization: $"{_BEARER_PREFIX}{token}");

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req", ctx, (_, _) => Task.FromResult("reply"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_JWT_EXPIRED);
    }

    // ---- Liveness ----

    [Fact]
    public async Task UnaryServerHandler_LivenessRevoked_ThrowsUnauthenticatedSessionRevoked()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE);
        var liveness = new FakeSessionLivenessTracker
        {
            OutcomeForSession = _ => FakeSessionLivenessTracker.Revoked(),
        };
        var interceptor = MakeInterceptor(builder, liveness);
        var ctx = MakeContext(authorization: $"{_BEARER_PREFIX}{token}");

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req", ctx, (_, _) => Task.FromResult("reply"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SESSION_REVOKED);
    }

    [Fact]
    public async Task UnaryServerHandler_LivenessUnavailable_ThrowsUnavailable()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE);
        var liveness = new FakeSessionLivenessTracker
        {
            OutcomeForSession = _ => FakeSessionLivenessTracker.Unavailable(),
        };
        var interceptor = MakeInterceptor(builder, liveness);
        var ctx = MakeContext(authorization: $"{_BEARER_PREFIX}{token}");

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req", ctx, (_, _) => Task.FromResult("reply"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unavailable);
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SESSION_LIVENESS_UNAVAILABLE);
    }

    [Fact]
    public async Task UnaryServerHandler_LivenessValidationFailed_FailsClosedAsUnauthenticated()
    {
        // Defensive fail-closed mapping: ValidationFailed is "shouldn't happen
        // given a validated JWT" — surface as Unauthenticated rather than
        // letting through.
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE);
        var liveness = new FakeSessionLivenessTracker
        {
            OutcomeForSession = _ => FakeSessionLivenessTracker.ValidationFailed(),
        };
        var interceptor = MakeInterceptor(builder, liveness);
        var ctx = MakeContext(authorization: $"{_BEARER_PREFIX}{token}");

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req", ctx, (_, _) => Task.FromResult("reply"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
    }

    [Fact]
    public async Task UnaryServerHandler_NoSessionIdInToken_SkipsLivenessCheck()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE, includeSessionId: false);
        var liveness = new FakeSessionLivenessTracker();
        var interceptor = MakeInterceptor(
            builder,
            liveness,
            configure: o => o.Validator = o.Validator with { RequireSessionIdClaim = false });
        var ctx = MakeContext(authorization: $"{_BEARER_PREFIX}{token}");
        var continuationCalled = false;

        await interceptor.UnaryServerHandler<string, string>(
            "req",
            ctx,
            (_, _) =>
            {
                continuationCalled = true;
                return Task.FromResult("reply");
            });

        continuationCalled.Should().BeTrue();
        liveness.InvocationCount.Should().Be(0);
    }

    // ---- Success path / UserState contract ----

    [Fact]
    public async Task UnaryServerHandler_SuccessPath_PopulatesRequestContextOnUserState()
    {
        using var builder = new TestJwtBuilder();
        var sessionId = Guid.NewGuid();
        var token = builder.MintToken(_ISSUER, _AUDIENCE, sessionId: sessionId);
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(authorization: $"{_BEARER_PREFIX}{token}");

        await interceptor.UnaryServerHandler<string, string>(
            "req", ctx, (_, _) => Task.FromResult("reply"));

        var stored = ctx.UserState[D2GrpcUserStateKeys.REQUEST_CONTEXT] as IRequestContext;
        stored.Should().NotBeNull();
        stored.IsAuthenticated.Should().BeTrue();
        stored.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public async Task UnaryServerHandler_SuccessPath_AlsoWritesRequestContextToHttpContextItems()
    {
        // Regression — interceptor's dual-write contract: UserState slot for
        // the gRPC-specific hot-path accessor + HttpContext.Items slot for
        // the cross-transport scoped IRequestContext resolver lambda
        // registered by both AddD2AuthHttp() and AddD2AuthGrpc(). MakeContext
        // provisions a synthetic HttpContext on the ServerCallContext when
        // metadata is non-null — required for the HttpContext.Items
        // dual-write to land somewhere observable.
        using var builder = new TestJwtBuilder();
        var sessionId = Guid.NewGuid();
        var token = builder.MintToken(
            _ISSUER,
            _AUDIENCE,
            sessionId: sessionId,
            extraClaims: new Dictionary<string, object>
            {
                [D2.Shared.Auth.Abstractions.JwtClaimTypes.SCOPE] = "any.scope",
            });
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata: MethodScopeMetadata.ForScopes(["any.scope"], ScopeMatch.Any));

        await interceptor.UnaryServerHandler<string, string>(
            "req", ctx, (_, _) => Task.FromResult("reply"));

        var userStateStored =
            ctx.UserState[D2GrpcUserStateKeys.REQUEST_CONTEXT] as IRequestContext;
        var httpContext =
            ctx.UserState[TestServerCallContext.HTTP_CONTEXT_USER_STATE_KEY] as HttpContext;
        httpContext.Should().NotBeNull();
        var httpItemsStored =
            httpContext.Items[D2HttpContextItems.REQUEST_CONTEXT] as IRequestContext;

        userStateStored.Should().NotBeNull();
        httpItemsStored.Should().NotBeNull();
        httpItemsStored.Should().BeSameAs(userStateStored);
        httpItemsStored.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public async Task UnaryServerHandler_SuccessPath_ContinuationCalledExactlyOnce()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE);
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(authorization: $"{_BEARER_PREFIX}{token}");
        var callCount = 0;

        await interceptor.UnaryServerHandler<string, string>(
            "req",
            ctx,
            (_, _) =>
            {
                callCount++;
                return Task.FromResult("reply");
            });

        callCount.Should().Be(1);
    }

    [Fact]
    public async Task UnaryServerHandler_AllFourRpcKinds_SuccessPath_CallsContinuation()
    {
        // Cross-kind smoke: a happy path on each of the four overrides.
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE);
        var interceptor = MakeInterceptor(builder);

        var unaryCalled = 0;
        await interceptor.UnaryServerHandler<string, string>(
            "req",
            MakeContext(authorization: $"{_BEARER_PREFIX}{token}"),
            (_, _) =>
            {
                unaryCalled++;
                return Task.FromResult("reply");
            });

        var clientStreamCalled = 0;
        await interceptor.ClientStreamingServerHandler<string, string>(
            new EmptyAsyncStreamReader<string>(),
            MakeContext(authorization: $"{_BEARER_PREFIX}{token}"),
            (_, _) =>
            {
                clientStreamCalled++;
                return Task.FromResult("reply");
            });

        var serverStreamCalled = 0;
        await interceptor.ServerStreamingServerHandler<string, string>(
            "req",
            new DiscardingServerStreamWriter<string>(),
            MakeContext(authorization: $"{_BEARER_PREFIX}{token}"),
            (_, _, _) =>
            {
                serverStreamCalled++;
                return Task.CompletedTask;
            });

        var duplexCalled = 0;
        await interceptor.DuplexStreamingServerHandler(
            new EmptyAsyncStreamReader<string>(),
            new DiscardingServerStreamWriter<string>(),
            MakeContext(authorization: $"{_BEARER_PREFIX}{token}"),
            (_, _, _) =>
            {
                duplexCalled++;
                return Task.CompletedTask;
            });

        unaryCalled.Should().Be(1);
        clientStreamCalled.Should().Be(1);
        serverStreamCalled.Should().Be(1);
        duplexCalled.Should().Be(1);
    }

    [Fact]
    public async Task UnaryServerHandler_NoMethodMetadata_PassesAnyAuthenticatedCaller()
    {
        // Deny-by-default lives in absence-of-metadata: any authenticated
        // caller passes the empty required-set check.
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE);
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(authorization: $"{_BEARER_PREFIX}{token}", metadata: null);
        var continuationCalled = false;

        await interceptor.UnaryServerHandler<string, string>(
            "req",
            ctx,
            (_, _) =>
            {
                continuationCalled = true;
                return Task.FromResult("reply");
            });

        continuationCalled.Should().BeTrue();
    }

    // ---- Scope enforcement — any-of (ScopeMatch.Any) ----

    [Fact]
    public async Task UnaryServerHandler_AnyScopeRequired_CallerHasNone_ThrowsScopeInsufficient()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE);
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata: MethodScopeMetadata.ForScopes(new[] { "files.read" }, ScopeMatch.Any));

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req", ctx, (_, _) => Task.FromResult("reply"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    [Fact]
    public async Task UnaryServerHandler_AnyScopeRequired_CallerHasOneOfMany_PassesThrough()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = "files.read other.scope",
            });
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata: MethodScopeMetadata.ForScopes(
                new[] { "files.read", "files.admin" }, ScopeMatch.Any));
        var continuationCalled = false;

        await interceptor.UnaryServerHandler<string, string>(
            "req",
            ctx,
            (_, _) =>
            {
                continuationCalled = true;
                return Task.FromResult("reply");
            });

        continuationCalled.Should().BeTrue();
    }

    [Fact]
    public async Task UnaryServerHandler_AnyScopeRequired_CallerHasWrongScope_ThrowsScopeInsufficient()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = "other.unrelated.scope",
            });
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata: MethodScopeMetadata.ForScopes(
                new[] { "files.read" }, ScopeMatch.Any));

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req", ctx, (_, _) => Task.FromResult("reply"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    // ---- Scope enforcement — all-of (ScopeMatch.All) ----

    [Fact]
    public async Task UnaryServerHandler_AllScopesRequired_CallerHasAll_PassesThrough()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = "files.read files.write",
            });
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata: MethodScopeMetadata.ForScopes(
                new[] { "files.read", "files.write" }, ScopeMatch.All));
        var continuationCalled = false;

        await interceptor.UnaryServerHandler<string, string>(
            "req",
            ctx,
            (_, _) =>
            {
                continuationCalled = true;
                return Task.FromResult("reply");
            });

        continuationCalled.Should().BeTrue();
    }

    [Fact]
    public async Task UnaryServerHandler_AllScopesRequired_CallerMissingOne_ThrowsScopeInsufficient()
    {
        using var builder = new TestJwtBuilder();

        // Caller has files.read but NOT files.write.
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = "files.read",
            });
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata: MethodScopeMetadata.ForScopes(
                new[] { "files.read", "files.write" }, ScopeMatch.All));

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req", ctx, (_, _) => Task.FromResult("reply"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    [Fact]
    public async Task UnaryServerHandler_AllScopesRequired_CallerHasNone_ThrowsScopeInsufficient()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE);
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata: MethodScopeMetadata.ForScopes(
                new[] { "files.read", "files.write" }, ScopeMatch.All));

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req", ctx, (_, _) => Task.FromResult("reply"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    [Fact]
    public async Task UnaryServerHandler_AllScopesRequired_CallerHasSupersetOfRequired_PassesThrough()
    {
        // Caller has more scopes than required — all-of still passes.
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = "files.read files.write files.admin extra.scope",
            });
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata: MethodScopeMetadata.ForScopes(
                new[] { "files.read", "files.write" }, ScopeMatch.All));
        var continuationCalled = false;

        await interceptor.UnaryServerHandler<string, string>(
            "req",
            ctx,
            (_, _) =>
            {
                continuationCalled = true;
                return Task.FromResult("reply");
            });

        continuationCalled.Should().BeTrue();
    }

    // ---- Attribute-based precedence resolution ----

    [Fact]
    public async Task UnaryServerHandler_AttributePath_AnyScope_CallerHasIt_PassesThrough()
    {
        // [D2RequireAnyScope] attribute attached via endpoint metadata
        // (simulating ASP.NET routing pickup).
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = "svc.scope",
            });
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContextWithAttributes(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata: [new D2RequireAnyScopeAttribute("svc.scope")]);
        var continuationCalled = false;

        await interceptor.UnaryServerHandler<string, string>(
            "req",
            ctx,
            (_, _) =>
            {
                continuationCalled = true;
                return Task.FromResult("reply");
            });

        continuationCalled.Should().BeTrue();
    }

    [Fact]
    public async Task UnaryServerHandler_AttributePath_AllScopes_CallerHasAll_PassesThrough()
    {
        // [D2RequireAllScopes] attribute: caller must have both scopes.
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = "files.read files.write",
            });
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContextWithAttributes(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata: [new D2RequireAllScopesAttribute("files.read", "files.write")]);
        var continuationCalled = false;

        await interceptor.UnaryServerHandler<string, string>(
            "req",
            ctx,
            (_, _) =>
            {
                continuationCalled = true;
                return Task.FromResult("reply");
            });

        continuationCalled.Should().BeTrue();
    }

    [Fact]
    public async Task UnaryServerHandler_AttributePath_AllScopes_CallerMissingOne_ThrowsScopeInsufficient()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = "files.read",
            });
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContextWithAttributes(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata: [new D2RequireAllScopesAttribute("files.read", "files.write")]);

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req", ctx, (_, _) => Task.FromResult("reply"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    [Fact]
    public async Task UnaryServerHandler_AttributePath_HarmlessEndpointWinsWhenLast_SkipsAuth()
    {
        // Class-level [D2RequireAnyScope] + method-level [D2HarmlessEndpoint]:
        // harmless is declared LAST → wins → short-circuits. Simulated by
        // ordering the metadata items the same way ASP.NET routes do (class
        // attrs first, method attrs second).
        using var builder = new TestJwtBuilder();
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContextWithAttributes(
            authorization: null,
            metadata:
            [
                new D2RequireAnyScopeAttribute("svc.scope"),   // class-level (first)
                new D2HarmlessEndpointAttribute(),              // method-level (last)
            ]);
        var continuationCalled = false;

        await interceptor.UnaryServerHandler<string, string>(
            "req",
            ctx,
            (_, _) =>
            {
                continuationCalled = true;
                return Task.FromResult("reply");
            });

        continuationCalled.Should().BeTrue();
    }

    [Fact]
    public async Task UnaryServerHandler_AttributePath_RequireScopeWinsWhenLast_EnforcesScope()
    {
        // Class-level [D2HarmlessEndpoint] + method-level [D2RequireAnyScope]:
        // require-scope is declared LAST → wins → scope is enforced. No bearer
        // provided so it rejects.
        using var builder = new TestJwtBuilder();
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContextWithAttributes(
            authorization: null,
            metadata:
            [
                new D2HarmlessEndpointAttribute(),            // class-level (first)
                new D2RequireAnyScopeAttribute("svc.scope"),  // method-level (last)
            ]);

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req", ctx, (_, _) => Task.FromResult("reply"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
    }

    [Fact]
    public async Task UnaryServerHandler_AttributePath_ConflictAnyThenAll_LastDeclarationWins()
    {
        // Both [D2RequireAnyScope] (first) and [D2RequireAllScopes] (last) present:
        // last-declared (all-scopes) wins. Caller has files.read but not files.write,
        // so all-of check fails → ScopeInsufficient. If any-of had won, it would
        // have passed (files.read overlaps).
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = "files.read",
            });
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContextWithAttributes(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata:
            [
                new D2RequireAnyScopeAttribute("files.read"),              // first (any-of)
                new D2RequireAllScopesAttribute("files.read", "files.write"), // last (all-of)
            ]);

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req", ctx, (_, _) => Task.FromResult("reply"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ReadTrailer(ex.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    [Fact]
    public async Task UnaryServerHandler_AttributePath_ConflictAllThenAny_LastDeclarationWins()
    {
        // Both [D2RequireAllScopes] (first) and [D2RequireAnyScope] (last) present:
        // last-declared (any-of) wins. Caller has only files.read, which would fail
        // all-of but passes any-of.
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = "files.read",
            });
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContextWithAttributes(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata:
            [
                new D2RequireAllScopesAttribute("files.read", "files.write"), // first (all-of)
                new D2RequireAnyScopeAttribute("files.read"),                 // last (any-of)
            ]);
        var continuationCalled = false;

        await interceptor.UnaryServerHandler<string, string>(
            "req",
            ctx,
            (_, _) =>
            {
                continuationCalled = true;
                return Task.FromResult("reply");
            });

        continuationCalled.Should().BeTrue();
    }

    [Fact]
    public async Task UnaryServerHandler_FluentPath_WinsOverAttribute()
    {
        // Fluent MethodScopeMetadata in endpoint metadata wins over any
        // attribute even if the attribute appears later in the collection.
        // MakeContext with metadata parameter seeds the fluent singleton.
        using var builder = new TestJwtBuilder();
        var interceptor = MakeInterceptor(builder);

        // Harmless fluent + require-scope attribute: harmless fluent wins.
        var ctx = MakeContext(
            authorization: null,
            metadata: MethodScopeMetadata.HarmlessEndpoint);
        var continuationCalled = false;

        await interceptor.UnaryServerHandler<string, string>(
            "req",
            ctx,
            (_, _) =>
            {
                continuationCalled = true;
                return Task.FromResult("reply");
            });

        continuationCalled.Should().BeTrue();
    }

    [Fact]
    public async Task UnaryServerHandler_FluentScopeMetadata_WinsOverPresentAttribute()
    {
        // Fluent MethodScopeMetadata wins even when an attribute is ALSO
        // present in the same metadata collection. The attribute requires
        // "attr.scope"; the fluent metadata requires "fluent.scope" (any-of).
        // A token carrying only "fluent.scope" must pass — proving the fluent
        // path is evaluated and the attribute is ignored.
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = "fluent.scope",
            });
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContextWithAttributes(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata:
            [
                MethodScopeMetadata.ForScopes(["fluent.scope"], ScopeMatch.Any),
                new D2RequireAnyScopeAttribute("attr.scope"),
            ]);
        var continuationCalled = false;

        await interceptor.UnaryServerHandler<string, string>(
            "req",
            ctx,
            (_, _) =>
            {
                continuationCalled = true;
                return Task.FromResult("reply");
            });

        continuationCalled.Should().BeTrue();
    }

    [Fact]
    public async Task AllFourRpcKinds_AllScopesUnmet_ThrowsScopeInsufficient()
    {
        // All-of requirement: ["files.read","files.write"]; token carries only
        // "files.read". Every RPC kind must throw with AUTH_SCOPE_INSUFFICIENT.
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = "files.read",
            });
        var interceptor = MakeInterceptor(builder);
        var metadata = MethodScopeMetadata.ForScopes(
            ["files.read", "files.write"], ScopeMatch.All);

        var unaryAct = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req",
                MakeContext(authorization: $"{_BEARER_PREFIX}{token}", metadata: metadata),
                (_, _) => Task.FromResult("reply"));

        var clientStreamAct = async () =>
            await interceptor.ClientStreamingServerHandler<string, string>(
                new EmptyAsyncStreamReader<string>(),
                MakeContext(authorization: $"{_BEARER_PREFIX}{token}", metadata: metadata),
                (_, _) => Task.FromResult("reply"));

        var serverStreamAct = async () =>
            await interceptor.ServerStreamingServerHandler<string, string>(
                "req",
                new DiscardingServerStreamWriter<string>(),
                MakeContext(authorization: $"{_BEARER_PREFIX}{token}", metadata: metadata),
                (_, _, _) => Task.CompletedTask);

        var duplexAct = async () =>
            await interceptor.DuplexStreamingServerHandler(
                new EmptyAsyncStreamReader<string>(),
                new DiscardingServerStreamWriter<string>(),
                MakeContext(authorization: $"{_BEARER_PREFIX}{token}", metadata: metadata),
                (_, _, _) => Task.CompletedTask);

        var unaryEx = await unaryAct.Should().ThrowAsync<RpcException>();
        unaryEx.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(unaryEx.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);

        var clientStreamEx = await clientStreamAct.Should().ThrowAsync<RpcException>();
        clientStreamEx.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(clientStreamEx.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);

        var serverStreamEx = await serverStreamAct.Should().ThrowAsync<RpcException>();
        serverStreamEx.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(serverStreamEx.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);

        var duplexEx = await duplexAct.Should().ThrowAsync<RpcException>();
        duplexEx.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(duplexEx.Which.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    // ---- Status.Detail info-leak parity ----

    [Fact]
    public async Task UnaryServerHandler_FailurePath_StatusDetailDeliberatelyEmpty()
    {
        using var builder = new TestJwtBuilder();
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(authorization: null);

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req", ctx, (_, _) => Task.FromResult("reply"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.Status.Detail.Should().BeEmpty();
    }

    // ---- Cancellation propagation ----

    [Fact]
    public async Task UnaryServerHandler_CancellationPropagates()
    {
        // The interceptor honors ServerCallContext.CancellationToken via the
        // validator + liveness calls. An OperationCanceledException from the
        // liveness tracker propagates verbatim — gRPC infra translates it to
        // Status.Cancelled at the outer boundary. We don't catch/translate.
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE);
        using var cts = new CancellationTokenSource();
        var liveness = new FakeSessionLivenessTracker
        {
            OutcomeForSession = _ => throw new OperationCanceledException(),
        };
        var interceptor = MakeInterceptor(builder, liveness);
        var ctx = MakeContext(
            authorization: $"{_BEARER_PREFIX}{token}",
            cancellationToken: cts.Token);
        cts.Cancel();

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req", ctx, (_, _) => Task.FromResult("reply"));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ---- Argument null guards on entry points ----

    [Fact]
    public async Task UnaryServerHandler_NullContext_Throws()
    {
        using var builder = new TestJwtBuilder();
        var interceptor = MakeInterceptor(builder);

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req", null!, (_, _) => Task.FromResult("reply"));

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UnaryServerHandler_NullContinuation_Throws()
    {
        using var builder = new TestJwtBuilder();
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(authorization: null);

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>("req", ctx, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ---- Helpers ----

    private static JwtAuthInterceptor MakeInterceptor(
        TestJwtBuilder builder,
        FakeSessionLivenessTracker? liveness = null,
        Action<AuthOptions>? configure = null)
    {
        var validator = MakeValidator(builder, configure);
        return new JwtAuthInterceptor(
            validator,
            liveness ?? new FakeSessionLivenessTracker(),
            NullLogger<JwtAuthInterceptor>.Instance);
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

    private static TestServerCallContext MakeContext(
        string? authorization,
        MethodScopeMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var headers = new Metadata();
        if (authorization is not null)
            headers.Add("authorization", authorization);
        return MakeContext(headers, metadata, cancellationToken);
    }

    private static TestServerCallContext MakeContext(
        Metadata headers,
        MethodScopeMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        HttpContext? httpContext = null;
        if (metadata is not null)
        {
            httpContext = new DefaultHttpContext();
            var endpoint = new Endpoint(
                requestDelegate: _ => Task.CompletedTask,
                metadata: new EndpointMetadataCollection(metadata),
                displayName: "test-grpc-endpoint");
            httpContext.SetEndpoint(endpoint);
        }

        return new TestServerCallContext(
            requestHeaders: headers,
            cancellationToken: cancellationToken,
            httpContext: httpContext);
    }

    /// <summary>
    /// Creates a <see cref="TestServerCallContext"/> with arbitrary attribute
    /// instances in the endpoint metadata, simulating the mixed-attribute
    /// collections that ASP.NET routing builds from class-level + method-level
    /// attribute pickup during <c>MapGrpcService&lt;T&gt;()</c>.
    /// </summary>
    private static TestServerCallContext MakeContextWithAttributes(
        string? authorization,
        object[] metadata,
        CancellationToken cancellationToken = default)
    {
        var headers = new Metadata();
        if (authorization is not null)
            headers.Add("authorization", authorization);

        var httpContext = new DefaultHttpContext();
        var endpoint = new Endpoint(
            requestDelegate: _ => Task.CompletedTask,
            metadata: new EndpointMetadataCollection(metadata),
            displayName: "test-grpc-endpoint-attrs");
        httpContext.SetEndpoint(endpoint);

        return new TestServerCallContext(
            requestHeaders: headers,
            cancellationToken: cancellationToken,
            httpContext: httpContext);
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

    /// <summary>Empty <see cref="IAsyncStreamReader{T}"/> stand-in.</summary>
    private sealed class EmptyAsyncStreamReader<T> : IAsyncStreamReader<T>
        where T : class
    {
        public T Current => null!;

        public Task<bool> MoveNext(CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    /// <summary>Discarding <see cref="IServerStreamWriter{T}"/> stand-in.</summary>
    private sealed class DiscardingServerStreamWriter<T> : IServerStreamWriter<T>
    {
        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(T message) => Task.CompletedTask;
    }
}
