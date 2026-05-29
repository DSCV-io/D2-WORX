// -----------------------------------------------------------------------
// <copyright file="JwtAuthInterceptor.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Grpc.Interceptors;

using System.Net;
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
using Microsoft.Extensions.Logging;

/// <summary>
/// gRPC server <see cref="Interceptor"/> that runs the JWT validation
/// pipeline (signature + standard claims via <see cref="JwtValidator"/>) +
/// session liveness check (via <see cref="ISessionLivenessTracker"/>) on
/// inbound gRPC calls, enforces per-method scope requirements declared via
/// <see cref="MethodScopeMetadata"/> / <see cref="D2RequireScopeAttribute"/>
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
///   <item>Enforce per-method scope set (any-of). Mismatch →
///     <see cref="AuthFailures.ScopeInsufficient"/> →
///     <see cref="StatusCode.Unauthenticated"/> (NOT
///     <see cref="StatusCode.PermissionDenied"/>; uniform 401-shape policy
///     mirrors HTTP middleware — see
///     <see cref="AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT"/> remarks).</item>
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

    // The string-typed key under which Grpc.AspNetCore.Server's
    // The string-keyed UserState slot historically used by older
    // Grpc.AspNetCore.Server versions to surface the per-call HttpContext.
    // Newer versions (2.27+) expose the same access via the canonical
    // Grpc.Core.ServerCallContext.GetHttpContext() extension method (which
    // casts to IServerCallContextFeature). The lookup below tries the
    // canonical extension FIRST, falling back to the UserState slot for
    // back-compat with hand-rolled test ServerCallContext subtypes that
    // pre-date the IServerCallContextFeature contract (e.g. unit-test stubs
    // in this codebase that seed UserState directly).
    private const string _HTTP_CONTEXT_USER_STATE_KEY = "__HttpContext__";

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

    private static MethodScopeMetadata? ResolveMethodScopeMetadata(ServerCallContext context)
    {
        // gRPC-on-AspNetCore: the canonical access path is
        // Grpc.Core.ServerCallContextExtensions.GetHttpContext() — it casts
        // to IServerCallContextFeature (which the production
        // Grpc.AspNetCore.Server.HttpContextServerCallContext implements)
        // and returns the per-call HttpContext. We MUST use this path because
        // the older UserState["__HttpContext__"] slot is no longer populated
        // by current Grpc.AspNetCore.Server releases. Wrapped in try/catch
        // because the canonical extension throws when the ServerCallContext
        // subtype doesn't implement IServerCallContextFeature (legacy hand-
        // rolled test contexts) — in that fallback case we read the historic
        // UserState slot directly. The matched endpoint carries the metadata
        // collection populated by both fluent extensions AND attribute
        // pickup during MapGrpcService<T>().
        HttpContext? httpContext = null;
        try
        {
            httpContext = context.GetHttpContext();
        }
        catch (InvalidOperationException)
        {
            // Fallback path for legacy / hand-rolled test ServerCallContext
            // subtypes that pre-date IServerCallContextFeature.
            if (context.UserState.TryGetValue(_HTTP_CONTEXT_USER_STATE_KEY, out var raw)
                && raw is HttpContext h)
            {
                httpContext = h;
            }
        }

        var endpoint = httpContext?.GetEndpoint();
        if (endpoint is null)
            return null;

        // Fluent path takes precedence over attribute path (deterministic
        // precedence: fluent > attribute > deny-by-default).
        var fluent = endpoint.Metadata.GetMetadata<MethodScopeMetadata>();
        if (fluent is not null)
            return fluent;

        // Attribute precedence (matches BCL [Authorize] / [AllowAnonymous]
        // semantics): a method-level [D2HarmlessEndpoint] overrides any
        // class-level [D2RequireScope]. ASP.NET routing pickup orders
        // metadata: class-level first, then method-level — so we walk the
        // collection in order and let the LAST matching entry win for the
        // harmless-endpoint case, while also surfacing a method-level
        // [D2HarmlessEndpoint] over any [D2RequireScope].
        var harmlessEndpoint = endpoint.Metadata.GetMetadata<D2HarmlessEndpointAttribute>();
        var require = endpoint.Metadata.GetMetadata<D2RequireScopeAttribute>();

        // [D2HarmlessEndpoint] wins regardless of source level (ASP.NET
        // metadata order ensures GetMetadata<T>() returns the LAST entry,
        // so a method-level attribute supersedes a class-level one for the
        // SAME attribute type). When both [D2HarmlessEndpoint] AND
        // [D2RequireScope] are present, harmless-endpoint wins to mirror the
        // BCL [AllowAnonymous]-over-[Authorize] precedence; the typical case
        // is a class-level [D2RequireScope] with a method-level
        // [D2HarmlessEndpoint] opting one method out.
        if (harmlessEndpoint is not null && IsHarmlessEndpointLastDeclaration(endpoint, require))
            return MethodScopeMetadata.HarmlessEndpoint;

        if (require is not null)
            return MethodScopeMetadata.ForScopes(require.Scopes);

        return harmlessEndpoint is not null ? MethodScopeMetadata.HarmlessEndpoint : null;
    }

    private static bool IsHarmlessEndpointLastDeclaration(
        Microsoft.AspNetCore.Http.Endpoint endpoint,
        D2RequireScopeAttribute? require)
    {
        // When BOTH attributes are present, ASP.NET metadata walking order
        // (class-level then method-level) lets the LAST one win. This mirrors
        // BCL [AllowAnonymous]-over-[Authorize] precedence — a method-level
        // [D2HarmlessEndpoint] on a class with [D2RequireScope] resolves to
        // harmless; a class-level [D2HarmlessEndpoint] with a method-level
        // [D2RequireScope] resolves to scope-required.
        if (require is null)
            return true;

        var lastHarmless = -1;
        var lastRequire = -1;
        var index = 0;
        foreach (var item in endpoint.Metadata)
        {
            if (item is D2HarmlessEndpointAttribute)
                lastHarmless = index;
            else if (item is D2RequireScopeAttribute)
                lastRequire = index;
            index++;
        }

        return lastHarmless > lastRequire;
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

    private static string SummarizeScopes(IReadOnlySet<string> required)
    {
        // Closed-enumeration-derived summary — count + first (sorted) scope
        // name. Avoids logging full scope sets verbatim (they can be large
        // and bloat log volume; mirrors the HTTP middleware's
        // ScopeRequirementUnmet log shape).
        if (required.Count == 0)
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
        var metadata = ResolveMethodScopeMetadata(context);

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

        // Per-method scope enforcement (any-of). Empty / null required set
        // = "any authenticated caller passes."
        if (metadata is { RequiredScopes.Count: > 0 } meta)
        {
            if (!RequestContextHasAnyScope(requestContext, meta.RequiredScopes))
            {
                r_logger.ScopeRequirementUnmet(SummarizeScopes(meta.RequiredScopes));
                throw AuthFailures.ScopeInsufficient().ToRpcException();
            }
        }

        // Plumb the populated context to downstream pipeline + continuation.
        // Dual write: UserState slot serves the gRPC-specific hot-path
        // accessor (ServerCallContext.GetD2RequestContext()); HttpContext.Items
        // slot serves the cross-transport scoped IRequestContext resolver
        // registered by both AddD2AuthHttp() and AddD2AuthGrpc().
        context.UserState[D2GrpcUserStateKeys.REQUEST_CONTEXT] = requestContext;

        // Resolve HttpContext via the canonical extension first; fall back to
        // the historic UserState slot for legacy hand-rolled test
        // ServerCallContext subtypes that pre-date IServerCallContextFeature
        // (mirrors the lookup pattern in ResolveMethodScopeMetadata).
        HttpContext? httpContext = null;
        try
        {
            httpContext = context.GetHttpContext();
        }
        catch (InvalidOperationException)
        {
            if (context.UserState.TryGetValue(_HTTP_CONTEXT_USER_STATE_KEY, out var raw)
                && raw is HttpContext h)
            {
                httpContext = h;
            }
        }

        if (httpContext is not null)
            httpContext.Items[D2HttpContextItems.REQUEST_CONTEXT] = requestContext;
    }
}
