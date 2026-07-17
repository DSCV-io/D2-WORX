// -----------------------------------------------------------------------
// <copyright file="JwtAuthMiddleware.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Http.Middleware;

using System.Net;
using System.Text.Json;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Abstractions.Http;
using DcsvIo.D2.Auth.Abstractions.Sessions;
using DcsvIo.D2.Auth.Errors;
using DcsvIo.D2.Auth.Http.Endpoints;
using DcsvIo.D2.Auth.Http.ProblemDetails;
using DcsvIo.D2.Auth.Telemetry;
using DcsvIo.D2.Auth.Validation;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Headers.Http;
using DcsvIo.D2.ProblemDetails;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

/// <summary>
/// ASP.NET Core convention-based middleware that runs the JWT validation
/// pipeline (signature + standard claims via <see cref="JwtValidator"/>) +
/// session liveness check (via <see cref="ISessionLivenessTracker"/>) on
/// inbound HTTP requests, enforces per-endpoint scope requirements declared
/// via <see cref="EndpointScopeMetadata"/>, and emits RFC 7807 ProblemDetails
/// on failure.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Pipeline order</strong> (per request, in order):
/// </para>
/// <list type="number">
///   <item>Resolve the matched endpoint's <see cref="EndpointScopeMetadata"/>.
///     If harmless-endpoint → call <c>next</c> immediately, skipping all auth work.</item>
///   <item>Extract the bearer token from the <c>Authorization</c> header
///     (RFC 6750 §2.1). Missing / wrong-prefix / empty-after-prefix →
///     <see cref="AuthFailures.BearerMissing"/> 401.</item>
///   <item>Validate the token via <see cref="JwtValidator.ValidateAsync"/>.
///     Failure surface bubbles verbatim through ProblemDetails.</item>
///   <item>If the validated context carries a session id, check liveness via
///     <see cref="ISessionLivenessTracker.IsAliveAsync"/>. Revoked → 401.
///     ServiceUnavailable → 503 (fail-closed).</item>
///   <item>Enforce per-endpoint scope set (any-of or all-of, per
///     <see cref="EndpointScopeMetadata.Match"/>). Mismatch →
///     <see cref="AuthFailures.ScopeInsufficient"/> 401 (NOT 403; see
///     <see cref="AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT"/> remarks). Absent metadata
///     admits any authenticated caller; a PRESENT non-harmless metadata with an EMPTY
///     scope set is a configuration anomaly and fails CLOSED (the public factories
///     reject empty sets, so only a serializer / clone / reflection path could produce
///     one).</item>
///   <item>Stash the raw bearer in the request-scoped
///     <see cref="IForwardedJwtAccessor"/> (best-effort). Capture is the LAST
///     pre-continuation operation — placed AFTER the scope gate so a token that fails
///     harmless, bearer-missing, validation, liveness, OR scope enforcement never enters
///     the holder (mirrors the gRPC <c>JwtAuthInterceptor</c>).</item>
///   <item>Set the populated <see cref="IRequestContext"/> on
///     <see cref="HttpContext.Items"/> under
///     <see cref="D2HttpContextItems.REQUEST_CONTEXT"/> and continue.</item>
/// </list>
/// <para>
/// <strong>Pipeline placement invariant</strong>: register via
/// <c>app.UseD2Auth()</c> AFTER <c>app.UseRouting()</c> (so the matched
/// endpoint's metadata is available) and BEFORE the endpoint dispatcher
/// (<c>app.UseEndpoints()</c> / <c>app.MapXxx()</c>).
/// </para>
/// <para>
/// <strong>PII discipline</strong>: bearer bytes, claim values, scope strings
/// are NEVER logged / span-tagged / serialized into ProblemDetails fields.
/// The <c>Title</c> field is locale-neutral coarse; <c>Detail</c> is omitted
/// (info-leak avoidance); <c>Instance</c> is <see cref="HttpRequest.Path"/>
/// only (no query string).
/// </para>
/// <para>
/// <strong>Cancellation</strong>: <see cref="HttpContext.RequestAborted"/>
/// is honored on the validator and liveness calls; an
/// <see cref="OperationCanceledException"/> propagates to the host (we don't
/// swallow / translate cancellations into 5xx responses).
/// </para>
/// <para>
/// <strong>Thread-safety</strong>: convention-based middleware is
/// instantiated ONCE per process (singleton-shaped). All injected deps are
/// singletons. No per-request mutable state on the middleware itself; per-
/// request state lives on <see cref="HttpContext"/>.
/// </para>
/// </remarks>
internal sealed class JwtAuthMiddleware
{
    private const string _BEARER_PREFIX = "Bearer ";

    // Pinned so JsonSerializerOptions caching applies; default options match
    // ASP.NET Core's MVC defaults for ProblemDetails (camelCase property
    // naming, ignore null, etc.). Static so we allocate once.
    private static readonly JsonSerializerOptions sr_jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate r_next;
    private readonly JwtValidator r_validator;
    private readonly ISessionLivenessTracker r_livenessTracker;
    private readonly ILogger<JwtAuthMiddleware> r_logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtAuthMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="validator">The JWT signature + claims validator.</param>
    /// <param name="livenessTracker">The session liveness tracker.</param>
    /// <param name="logger">The logger.</param>
    public JwtAuthMiddleware(
        RequestDelegate next,
        JwtValidator validator,
        ISessionLivenessTracker livenessTracker,
        ILogger<JwtAuthMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(livenessTracker);
        ArgumentNullException.ThrowIfNull(logger);

        r_next = next;
        r_validator = validator;
        r_livenessTracker = livenessTracker;
        r_logger = logger;
    }

    /// <summary>
    /// Per-request entry point. Convention-based middleware contract.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ct = context.RequestAborted;
        var endpointMetadata = context.GetEndpoint()?.Metadata.GetMetadata<EndpointScopeMetadata>();

        // Harmless-endpoint opt-in short-circuit: skip validator + liveness entirely.
        if (endpointMetadata is { IsHarmlessEndpoint: true })
        {
            await r_next(context).ConfigureAwait(false);
            return;
        }

        // Bearer extraction.
        var bearerResult = TryExtractBearer(context);
        if (bearerResult.Failed)
        {
            r_logger.BearerHeaderMissing();
            await WriteProblemAsync(context, bearerResult, ct).ConfigureAwait(false);
            return;
        }

        // JWT validation.
        var validationResult = await r_validator
            .ValidateAsync(bearerResult.Data!, ct)
            .ConfigureAwait(false);
        if (validationResult.Failed)
        {
            await WriteProblemAsync(context, validationResult, ct).ConfigureAwait(false);
            return;
        }

        var requestContext = validationResult.Data!;

        // Session liveness — only when the validated context surfaces a
        // session id. RequireSessionIdClaim defaults to true on the
        // validator, so absence here is a service-identity-token-style
        // exception path (the validator already accepted the token).
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
                await WriteProblemAsync(context, mapped, ct).ConfigureAwait(false);
                return;
            }

            if (livenessResult.Data is false)
            {
                r_logger.LivenessRevoked();
                await WriteProblemAsync(
                    context, AuthFailures.SessionRevoked(), ct).ConfigureAwait(false);
                return;
            }
        }

        // Per-endpoint scope enforcement (any-of or all-of, per meta.Match). Absent
        // metadata = "any authenticated caller passes" (deny-by-default lives in the
        // ABSENCE of metadata, not an empty scope set). A PRESENT, non-harmless metadata
        // with an EMPTY scope set is a configuration anomaly: the public factories reject
        // empty sets, so only a serializer / record-clone / reflection path could produce
        // one — fail CLOSED rather than silently admit any authenticated caller.
        if (endpointMetadata is { IsHarmlessEndpoint: false } meta)
        {
            if (meta.Scopes.Falsey())
            {
                r_logger.ScopeMetadataEmptyAnomaly();
                await WriteProblemAsync(
                    context, AuthFailures.ScopeInsufficient(), ct).ConfigureAwait(false);
                return;
            }

            var passes = meta.Match == ScopeMatch.All
                ? RequestContextHasAllScopes(requestContext, meta.Scopes)
                : RequestContextHasAnyScope(requestContext, meta.Scopes);

            if (!passes)
            {
                r_logger.ScopeRequirementUnmet(SummarizeScopes(meta.Scopes));
                await WriteProblemAsync(
                    context, AuthFailures.ScopeInsufficient(), ct).ConfigureAwait(false);
                return;
            }
        }

        // Stash the validated raw bearer in the request-scoped forwarded-JWT holder so an
        // outbound hop can replay it byte-for-byte. Capture is the LAST pre-continuation
        // operation — placed AFTER every inbound gate (harmless short-circuit, bearer
        // extraction, JWT validation, session liveness, AND per-endpoint scope
        // enforcement), mirroring the gRPC JwtAuthInterceptor. Only a token that cleared
        // ALL gates ever enters the holder, so a scope-insufficient (or revoked, or
        // otherwise rejected) token never populates it — no transient holder population
        // during the error-response phase. Best-effort: a host that does not register the
        // holder (does not forward) simply no-ops; a null RequestServices (outside a DI
        // scope) also no-ops rather than throwing. The bearer is never logged.
        // RequestServices is non-null-annotated but can be null at runtime (e.g.
        // resolution outside a DI scope), so guard explicitly.
        var serviceProvider = (IServiceProvider?)context.RequestServices;

        if (serviceProvider is not null)
            serviceProvider.GetService<IForwardedJwtAccessor>()?.Capture(bearerResult.Data!);

        // Plumb the populated context to downstream handlers + continue.
        context.Items[D2HttpContextItems.REQUEST_CONTEXT] = requestContext;
        await r_next(context).ConfigureAwait(false);
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
        // and bloat log volume).
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

    private static D2Result<string> TryExtractBearer(HttpContext context)
    {
        // StringValues.Count is zero-alloc; .Falsey() would box an enumerator
        // on the request hot path — direct Count check is the correct pattern here.
        if (!context.Request.Headers.TryGetValue(HttpHeaders.AUTHORIZATION, out StringValues raw)
            || raw.Count == 0)
        {
            return D2Result<string>.BubbleFail(AuthFailures.BearerMissing());
        }

        // RFC 7230 §3.2.2: multiple values for the same header field are
        // semantically equivalent to a comma-joined single value. For
        // Authorization the convention is exactly one — defensively take
        // the FIRST and pass it through.
        var headerValue = raw[0];
        if (headerValue.Falsey())
            return D2Result<string>.BubbleFail(AuthFailures.BearerMissing());

        // RFC 6750 §2.1: case-insensitive `Bearer` prefix + single space.
        if (!headerValue!.StartsWith(_BEARER_PREFIX, StringComparison.OrdinalIgnoreCase))
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

    private static async Task WriteProblemAsync(
        HttpContext context,
        D2Result failure,
        CancellationToken ct)
    {
        var problem = failure.ToProblemDetails(context);
        context.Response.StatusCode = problem.Status ?? (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = D2ProblemDetailsKeys.CONTENT_TYPE;
        await JsonSerializer
            .SerializeAsync(context.Response.Body, problem, sr_jsonOptions, ct)
            .ConfigureAwait(false);
    }
}
