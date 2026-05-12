// -----------------------------------------------------------------------
// <copyright file="JwtAuthInterceptorTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Interceptors;

using AwesomeAssertions;
using D2.Shared.Auth;
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
        ReadTrailer(ex.Which.Trailers, D2RpcStatusExtensions.TRAILER_ERROR_CODE)
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
        ReadTrailer(ex.Which.Trailers, D2RpcStatusExtensions.TRAILER_ERROR_CODE)
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
        ReadTrailer(ex.Which.Trailers, D2RpcStatusExtensions.TRAILER_ERROR_CODE)
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
        ReadTrailer(ex.Which.Trailers, D2RpcStatusExtensions.TRAILER_ERROR_CODE)
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
        ReadTrailer(ex.Which.Trailers, D2RpcStatusExtensions.TRAILER_ERROR_CODE)
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
        ReadTrailer(ex.Which.Trailers, D2RpcStatusExtensions.TRAILER_ERROR_CODE)
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
        ReadTrailer(ex.Which.Trailers, D2RpcStatusExtensions.TRAILER_ERROR_CODE)
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
            metadata: MethodScopeMetadata.ForScopes(["any.scope"]));

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

    // ---- Scope enforcement ----

    [Fact]
    public async Task UnaryServerHandler_ScopeRequiredCallerHasNone_ThrowsScopeInsufficient()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(_ISSUER, _AUDIENCE);
        var interceptor = MakeInterceptor(builder);
        var ctx = MakeContext(
            authorization: $"{_BEARER_PREFIX}{token}",
            metadata: MethodScopeMetadata.ForScopes(new[] { "files.read" }));

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req", ctx, (_, _) => Task.FromResult("reply"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailer(ex.Which.Trailers, D2RpcStatusExtensions.TRAILER_ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    [Fact]
    public async Task UnaryServerHandler_ScopeRequiredCallerHasMatch_PassesThrough()
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
                new[] { "files.read", "files.admin" }));
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
    public async Task UnaryServerHandler_ScopeRequiredCallerHasWrongScope_ThrowsScopeInsufficient()
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
            metadata: MethodScopeMetadata.ForScopes(new[] { "files.read" }));

        var act = async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "req", ctx, (_, _) => Task.FromResult("reply"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ReadTrailer(ex.Which.Trailers, D2RpcStatusExtensions.TRAILER_ERROR_CODE)
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
