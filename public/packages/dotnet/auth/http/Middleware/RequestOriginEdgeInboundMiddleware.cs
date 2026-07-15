// -----------------------------------------------------------------------
// <copyright file="RequestOriginEdgeInboundMiddleware.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Http.Middleware;

using System;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Abstractions.Http;
using D2.Shared.Auth.Http.Telemetry;
using D2.Shared.Context.Abstractions;
using D2.Shared.Time;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// ASP.NET Core convention-based middleware that establishes the
/// <see cref="RequestOrigin.EdgeInbound"/> plane — the external trust boundary, the
/// START of the call-path — on the scoped <see cref="IRequestContext"/> for an inbound
/// HTTP request. Runs AFTER the auth middleware has populated
/// <c>HttpContext.Items[D2HttpContextItems.REQUEST_CONTEXT]</c>; sets
/// <see cref="IRequestContext.Origin"/> = <see cref="RequestOrigin.EdgeInbound"/>,
/// <see cref="IRequestContext.ImmediateCaller"/> = <see langword="null"/> (the external
/// client is not an internal workload), and STARTS the call-path with a single
/// <see cref="CallPathKind.Edge"/> entry carrying the edge's own service id.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Pipeline placement</strong>: register via <c>app.UseD2RequestOriginEdge()</c>
/// AFTER <c>app.UseD2Auth()</c> so the request-context slot is already populated; a
/// request with no established context (e.g. a harmless endpoint the auth middleware
/// short-circuited) is a no-op here.
/// </para>
/// <para>
/// <strong>Thread-safety</strong>: convention-based middleware is instantiated once per
/// process (singleton-shaped); all injected deps are singletons. No per-request mutable
/// state on the middleware itself.
/// </para>
/// <para>
/// <strong>Establishment log</strong>: logs the started call-path's entry count at Debug
/// level (symmetric with the cross-process interceptor's establishment log), mirroring
/// <c>RequestOriginGrpcLog.CallPathReceived</c>'s shape. The <c>logger</c> constructor
/// parameter is optional (defaults to <see langword="null"/>) so a harness that
/// hand-constructs the middleware without a logging container still works; DI-resolved
/// construction always supplies it.
/// </para>
/// </remarks>
internal sealed class RequestOriginEdgeInboundMiddleware
{
    private readonly RequestDelegate r_next;
    private readonly string r_selfServiceId;
    private readonly IClock r_clock;
    private readonly ILogger<RequestOriginEdgeInboundMiddleware>? r_logger;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="RequestOriginEdgeInboundMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="workloadIdentity">The host's own workload identity (its service id).</param>
    /// <param name="clock">The clock used to timestamp the Edge call-path entry.</param>
    /// <param name="logger">
    /// The logger used to record the Debug-level establishment trace. Optional
    /// (defaults to <see langword="null"/>, in which case no establishment log is
    /// emitted) so direct construction outside a DI container remains valid.
    /// </param>
    public RequestOriginEdgeInboundMiddleware(
        RequestDelegate next,
        IOptions<D2WorkloadIdentityOptions> workloadIdentity,
        IClock clock,
        ILogger<RequestOriginEdgeInboundMiddleware>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(workloadIdentity);
        ArgumentNullException.ThrowIfNull(clock);

        r_next = next;
        r_selfServiceId = workloadIdentity.Value.ServiceId;
        r_clock = clock;
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

        // Runs AFTER the auth middleware populated the request-context slot. A request
        // with no established context (harmless endpoint short-circuit) is a no-op.
        if (context.Items[D2HttpContextItems.REQUEST_CONTEXT] is MutableRequestContext ctx)
        {
            var now = r_clock.GetCurrentInstant().ToDateTimeOffset();

            ctx.Origin = RequestOrigin.EdgeInbound;
            ctx.ImmediateCaller = null;
            ctx.CallPath = CallPathOps.Append(null, r_selfServiceId, CallPathKind.Edge, now);

            r_logger?.CallPathStarted(ctx.CallPath.Count, r_selfServiceId);
        }

        await r_next(context).ConfigureAwait(false);
    }
}
