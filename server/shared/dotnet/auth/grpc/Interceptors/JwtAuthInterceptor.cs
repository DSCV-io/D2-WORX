// -----------------------------------------------------------------------
// <copyright file="JwtAuthInterceptor.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Grpc.Interceptors;

using System.Net;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Abstractions.Http;
using D2.Shared.Auth.Abstractions.Sessions;
using D2.Shared.Auth.Errors;
using D2.Shared.Auth.Grpc.Endpoints;
using D2.Shared.Auth.Grpc.Status;
using D2.Shared.Auth.Telemetry;
using D2.Shared.Auth.Validation;
using D2.Shared.Context.Abstractions;
using D2.Shared.Result;
using D2.Shared.Utilities.Extensions;
using global::Grpc.Core;
using global::Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// gRPC server <see cref="Interceptor"/> that runs the JWT validation
/// pipeline (signature + standard claims via <see cref="JwtValidator"/>) +
/// session liveness check (via <see cref="ISessionLivenessTracker"/>) on
/// inbound gRPC calls, enforces per-method scope requirements declared via
/// <see cref="MethodScopeMetadata"/> / <see cref="D2RequireAnyScopeAttribute"/>
/// / <see cref="D2RequireAllScopesAttribute"/>
/// / <see cref="D2HarmlessEndpointAttribute"/>, and emits
/// <see cref="RpcException"/> with translated <see cref="Status"/> +
/// <c>d2_error_code</c> / <c>d2_messages</c> / <c>traceid</c> trailers on
/// failure.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Pipeline order</strong> (per RPC, in order):
/// </para>
/// <list type="number">
///   <item>Resolve the matched gRPC method's <see cref="MethodScopeMetadata"/>
///     (or fall back to attribute pickup). If harmless-endpoint → invoke
///     <c>continuation</c> immediately, skipping all auth work.</item>
///   <item>Extract the bearer from the <c>authorization</c> metadata entry
///     (RFC 6750 §2.1; gRPC metadata keys are lowercased per HTTP/2).
///     Missing / wrong-prefix / empty-after-prefix →
///     <see cref="AuthFailures.BearerMissing"/> →
///     <see cref="StatusCode.Unauthenticated"/>.</item>
///   <item>Validate the token via <see cref="JwtValidator.ValidateAsync"/>.
///     Failure surface bubbles verbatim through
///     <see cref="D2RpcStatusExtensions"/>.</item>
///   <item>If the validated context carries a session id, check liveness via
///     <see cref="ISessionLivenessTracker.IsAliveAsync"/>. Revoked →
///     <see cref="StatusCode.Unauthenticated"/>. ServiceUnavailable →
///     <see cref="StatusCode.Unavailable"/> (fail-closed).</item>
///   <item>Enforce per-method scope set (any-of or all-of, per
///     <see cref="MethodScopeMetadata.Match"/>). Mismatch →
///     <see cref="AuthFailures.ScopeInsufficient"/> →
///     <see cref="StatusCode.Unauthenticated"/> (NOT
///     <see cref="StatusCode.PermissionDenied"/>; uniform 401-shape policy
///     mirrors HTTP middleware — see
///     <see cref="AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT"/> remarks). Absent metadata
///     admits any authenticated caller; a PRESENT non-harmless metadata with an EMPTY
///     scope set is a configuration anomaly and fails CLOSED (the public factories reject
///     empty sets, so only a serializer / clone / reflection path could produce one).</item>
///   <item>Set the populated <see cref="IRequestContext"/> on BOTH
///     <see cref="ServerCallContext.UserState"/> (under
///     <see cref="D2GrpcUserStateKeys.REQUEST_CONTEXT"/>; the gRPC-specific
///     hot-path accessor reads from here) AND the per-call
///     <c>HttpContext.Items</c> (under
///     <see cref="D2HttpContextItems.REQUEST_CONTEXT"/>; the cross-transport
///     scoped <see cref="IRequestContext"/> resolver lambda registered by
///     both <c>AddD2AuthHttp()</c> and <c>AddD2AuthGrpc()</c> reads from
///     here). The dual write is the cross-transport bridge: a host that
///     wires both transport extensions resolves <see cref="IRequestContext"/>
///     correctly under either transport because both write the same slot.
///     Continue.</item>
/// </list>
/// <para>
/// <strong>Streaming-method coverage invariant</strong>: ALL FOUR server-side
/// handler methods (<see cref="UnaryServerHandler{TRequest,TResponse}"/>,
/// <see cref="ClientStreamingServerHandler{TRequest,TResponse}"/>,
/// <see cref="ServerStreamingServerHandler{TRequest,TResponse}"/>,
/// <see cref="DuplexStreamingServerHandler{TRequest,TResponse}"/>) dispatch
/// to a single shared validation pipeline. A streaming method added later
/// cannot silently bypass auth.
/// </para>
/// <para>
/// <strong>PII discipline</strong>: bearer bytes, claim values, and scope
/// strings are NEVER logged / span-tagged / serialized into trailer fields /
/// exception interpolations. <see cref="Status.Detail"/> is empty;
/// <c>d2_error_code</c> trailer carries closed-enum constants only.
/// </para>
/// <para>
/// <strong>Cancellation</strong>: <see cref="ServerCallContext.CancellationToken"/>
/// is honored on the validator and liveness calls; an
/// <see cref="OperationCanceledException"/> propagates to the gRPC
/// infrastructure (which translates to
/// <see cref="StatusCode.Cancelled"/>). We don't catch and re-translate.
/// </para>
/// <para>
/// <strong>Thread-safety</strong>: registered as a singleton — interceptor
/// is stateless, all injected deps are singletons. No per-call mutable state
/// on the interceptor itself; per-call state lives on
/// <see cref="ServerCallContext.UserState"/>.
/// </para>
/// </remarks>
internal sealed class JwtAuthInterceptor : Interceptor
{
    private const string _AUTHORIZATION_METADATA_KEY = "authorization";
    private const string _BEARER_PREFIX = "Bearer ";

    private readonly JwtValidator r_validator;
    private readonly ISessionLivenessTracker r_livenessTracker;
    private readonly ILogger<JwtAuthInterceptor> r_logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtAuthInterceptor"/>
    /// class.
    /// </summary>
    /// <param name="validator">The JWT signature + claims validator.</param>
    /// <param name="livenessTracker">The session liveness tracker.</param>
    /// <param name="logger">The logger.</param>
    public JwtAuthInterceptor(
        JwtValidator validator,
        ISessionLivenessTracker livenessTracker,
        ILogger<JwtAuthInterceptor> logger)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(livenessTracker);
        ArgumentNullException.ThrowIfNull(logger);

        r_validator = validator;
        r_livenessTracker = livenessTracker;
        r_logger = logger;
    }

    /// <inheritdoc/>
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(continuation);

        await RunAuthAsync(context).ConfigureAwait(false);
        return await continuation(request, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(requestStream);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(continuation);

        await RunAuthAsync(context).ConfigureAwait(false);
        return await continuation(requestStream, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(responseStream);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(continuation);

        await RunAuthAsync(context).ConfigureAwait(false);
        await continuation(request, responseStream, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(requestStream);
        ArgumentNullException.ThrowIfNull(responseStream);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(continuation);

        await RunAuthAsync(context).ConfigureAwait(false);
        await continuation(requestStream, responseStream, context).ConfigureAwait(false);
    }

    private static bool RequestContextHasAnyScope(
        IRequestContext requestContext,
        IReadOnlySet<string> required)
    {
        var granted = requestContext.Scopes;
        foreach (var scope in required)
        {
            if (granted.Contains(scope))
                return true;
        }

        return false;
    }

    private static bool RequestContextHasAllScopes(
        IRequestContext requestContext,
        IReadOnlySet<string> required)
    {
        var granted = requestContext.Scopes;
        foreach (var scope in required)
        {
            if (!granted.Contains(scope))
                return false;
        }

        return true;
    }

    private static string SummarizeScopes(IReadOnlySet<string> required)
    {
        // Closed-enumeration-derived summary — count + first (sorted) scope
        // name. Avoids logging full scope sets verbatim (they can be large
        // and bloat log volume; mirrors the HTTP middleware's
        // ScopeRequirementUnmet log shape).
        if (required.Falsey())
            return "0 scopes required";

        string? first = null;
        foreach (var scope in required)
        {
            if (first is null || string.CompareOrdinal(scope, first) < 0)
                first = scope;
        }

        return $"{required.Count} scopes required, first={first}";
    }

    private static D2Result<string> TryExtractBearer(ServerCallContext context)
    {
        // gRPC metadata keys are lowercased per HTTP/2 spec. ServerCallContext.
        // RequestHeaders is an ordered Metadata collection — first match wins
        // per the multi-Authorization-header convention (parity with HTTP
        // middleware's first-wins rule).
        Metadata.Entry? first = null;
        foreach (var entry in context.RequestHeaders)
        {
            if (string.Equals(
                    entry.Key,
                    _AUTHORIZATION_METADATA_KEY,
                    StringComparison.OrdinalIgnoreCase)
                && !entry.IsBinary)
            {
                first = entry;
                break;
            }
        }

        if (first is null)
            return D2Result<string>.BubbleFail(AuthFailures.BearerMissing());

        var headerValue = first.Value;
        if (headerValue.Falsey())
            return D2Result<string>.BubbleFail(AuthFailures.BearerMissing());

        // RFC 6750 §2.1: case-insensitive `Bearer` prefix + single space.
        if (!headerValue.StartsWith(_BEARER_PREFIX, StringComparison.OrdinalIgnoreCase))
            return D2Result<string>.BubbleFail(AuthFailures.BearerMissing());

        var token = headerValue.Substring(_BEARER_PREFIX.Length);

        // Empty after prefix = "missing" (semantically nothing to validate)
        // — distinct from "malformed" (validator's job for actual bytes).
        // Whitespace inside the token is NOT trimmed: JWTs are 3 base64url
        // segments separated by dots; whitespace mid-token is invalid by
        // construction. Trimming would mask client bugs.
        if (token.Length == 0)
            return D2Result<string>.BubbleFail(AuthFailures.BearerMissing());

        return D2Result<string>.Ok(token);
    }

    /// <summary>
    /// Single shared auth pipeline used by all four RPC kind dispatch
    /// overrides. Throws <see cref="RpcException"/> on failure (preventing
    /// continuation invocation); on success, populates the per-call request
    /// context on <see cref="ServerCallContext.UserState"/>.
    /// </summary>
    /// <param name="context">The gRPC server call context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="RpcException">
    /// Thrown when bearer extraction, JWT validation, liveness check, or
    /// scope enforcement fails. The exception's <see cref="Status"/> +
    /// <see cref="RpcException.Trailers"/> carry the failure surface.
    /// </exception>
    private async ValueTask RunAuthAsync(ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var metadata = MethodScopeMetadataResolver.TryResolve(context);

        // Harmless-endpoint opt-in short-circuit: skip validator + liveness entirely.
        if (metadata is { IsHarmlessEndpoint: true })
            return;

        // Bearer extraction.
        var bearerResult = TryExtractBearer(context);
        if (bearerResult.Failed)
        {
            r_logger.BearerHeaderMissing();
            throw bearerResult.ToRpcException();
        }

        // JWT validation.
        var validationResult = await r_validator
            .ValidateAsync(bearerResult.Data!, ct)
            .ConfigureAwait(false);
        if (validationResult.Failed)
            throw validationResult.ToRpcException();

        var requestContext = validationResult.Data!;

        // Session liveness — only when the validated context surfaces a
        // session id. RequireSessionIdClaim defaults to true on the validator,
        // so absence here is a service-identity-token-style exception path
        // (the validator already accepted the token).
        if (requestContext.SessionId is { } sessionId && sessionId.Truthy())
        {
            var livenessResult = await r_livenessTracker
                .IsAliveAsync(sessionId, ct)
                .ConfigureAwait(false);

            if (livenessResult.Failed)
            {
                D2Result mapped;
                if (livenessResult.StatusCode == HttpStatusCode.ServiceUnavailable)
                    mapped = AuthFailures.SessionLivenessUnavailable();
                else
                    mapped = AuthFailures.SessionRevoked();
                throw mapped.ToRpcException();
            }

            if (livenessResult.Data is false)
            {
                r_logger.LivenessRevoked();
                throw AuthFailures.SessionRevoked().ToRpcException();
            }
        }

        // Per-method scope enforcement (any-of or all-of, per meta.Match). Absent
        // metadata = "any authenticated caller passes" (deny-by-default lives in the
        // ABSENCE of metadata, not an empty scope set). A PRESENT, non-harmless metadata
        // with an EMPTY scope set is a configuration anomaly: the public factories reject
        // empty sets, so only a serializer / record-clone / reflection path could produce
        // one — fail CLOSED rather than silently admit any authenticated caller.
        if (metadata is { IsHarmlessEndpoint: false } meta)
        {
            if (meta.Scopes.Falsey())
            {
                r_logger.ScopeMetadataEmptyAnomaly();
                throw AuthFailures.ScopeInsufficient().ToRpcException();
            }

            var passes = meta.Match == ScopeMatch.All
                ? RequestContextHasAllScopes(requestContext, meta.Scopes)
                : RequestContextHasAnyScope(requestContext, meta.Scopes);

            if (!passes)
            {
                r_logger.ScopeRequirementUnmet(SummarizeScopes(meta.Scopes));
                throw AuthFailures.ScopeInsufficient().ToRpcException();
            }
        }

        // Plumb the populated context to downstream pipeline + continuation.
        // Dual write: UserState slot serves the gRPC-specific hot-path
        // accessor (ServerCallContext.GetD2RequestContext()); HttpContext.Items
        // slot serves the cross-transport scoped IRequestContext resolver
        // registered by both AddD2AuthHttp() and AddD2AuthGrpc().
        context.UserState[D2GrpcUserStateKeys.REQUEST_CONTEXT] = requestContext;

        // Dual-write Items via the shared HttpContext resolver (canonical
        // feature cast + legacy UserState fallback for hand-rolled tests).
        var httpContext = MethodScopeMetadataResolver.TryResolveHttpContext(context);

        if (httpContext is not null)
        {
            httpContext.Items[D2HttpContextItems.REQUEST_CONTEXT] = requestContext;

            // Stash the validated raw bearer in the request-scoped forwarded-JWT
            // holder (resolved from the per-call request scope) for outbound
            // replay. Capture happens AFTER ALL inbound auth gates pass:
            // (1) harmless-endpoint short-circuit did not fire, (2) bearer
            // extraction succeeded, (3) JWT signature + claims validation
            // passed, (4) session-liveness check passed (revoked/unavailable
            // paths both throw before reaching here), and (5) per-method scope
            // enforcement passed. Only a fully-authenticated, live-session,
            // scope-cleared token reaches this point. Best-effort: a host that
            // does not register the holder simply no-ops; a null
            // RequestServices also no-ops rather than throwing. The bearer is
            // never logged. RequestServices is non-null-annotated but can be
            // null at runtime (e.g. a hand-rolled test context), so guard
            // explicitly.
            var serviceProvider = (IServiceProvider?)httpContext.RequestServices;

            if (serviceProvider is not null)
                serviceProvider.GetService<IForwardedJwtAccessor>()?.Capture(bearerResult.Data!);
        }
    }
}
