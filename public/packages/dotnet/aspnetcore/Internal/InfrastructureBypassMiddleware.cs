// -----------------------------------------------------------------------
// <copyright file="InfrastructureBypassMiddleware.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Internal;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

/// <summary>
/// Internal middleware impl behind
/// <see cref="InfrastructureBypassApplicationBuilderExtensions.UseD2InfrastructureBypass"/>.
/// For every request, sets
/// <see cref="D2AspNetCoreConstants.INFRASTRUCTURE_HTTP_CONTEXT_ITEM_KEY"/>
/// to a boolean indicating whether the request path matches the configured
/// infrastructure-path list.
/// </summary>
/// <remarks>
/// <para>
/// Default behavior (<see cref="D2InfrastructureBypassOptions.TagOnly"/>
/// = <c>false</c>): when the request path matches the infrastructure-path
/// list AND a routing-matched endpoint is present on the context (i.e.
/// <c>UseRouting()</c> has already run and resolved the request to a
/// mapped endpoint), the middleware invokes the matched endpoint's
/// <see cref="Endpoint.RequestDelegate"/> directly and returns — bypassing
/// every middleware registered AFTER this one. This is the canonical
/// short-circuit so heavy business middleware (idempotency,
/// auth) does not execute on cheap probe / metrics / well-known requests.
/// When no endpoint has been routed yet (caller put bypass before
/// <c>UseRouting()</c>), the middleware falls through to the next delegate
/// instead of short-circuiting.
/// </para>
/// <para>
/// Tag-only mode (<see cref="D2InfrastructureBypassOptions.TagOnly"/>
/// = <c>true</c>): the middleware ONLY sets the HttpContext.Items flag and
/// continues the pipeline. Business middleware downstream still runs and
/// can opt-out by reading the flag. Used by services that want custom
/// middleware to execute on infrastructure paths (e.g. a per-service
/// rate-limit policy on <c>/metrics</c>).
/// </para>
/// </remarks>
internal sealed class InfrastructureBypassMiddleware
{
    private readonly RequestDelegate r_next;
    private readonly D2InfrastructureBypassOptions r_options;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="InfrastructureBypassMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="options">The configured bypass options snapshot.</param>
    public InfrastructureBypassMiddleware(
        RequestDelegate next,
        IOptions<D2InfrastructureBypassOptions> options)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(options);

        r_next = next;
        r_options = options.Value;
    }

    /// <summary>
    /// Pipeline entry point. Tags the request and (in default mode)
    /// short-circuits to the routing-matched endpoint for infrastructure
    /// paths.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the async pipeline continuation.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var isInfrastructure = InfrastructurePathMatcher.IsInfrastructurePath(
            context.Request.Path,
            r_options.InfrastructurePaths);

        context.Items[D2AspNetCoreConstants.INFRASTRUCTURE_HTTP_CONTEXT_ITEM_KEY] =
            isInfrastructure;

        if (!isInfrastructure || r_options.TagOnly)
            return r_next(context);

        // Short-circuit path: invoke the routing-matched endpoint directly,
        // bypassing every middleware registered AFTER this one. Falls through
        // to next() when no endpoint has been routed (caller put bypass
        // before UseRouting()) so the pipeline still completes correctly.
        var endpoint = context.GetEndpoint();
        var endpointDelegate = endpoint?.RequestDelegate;

        if (endpointDelegate is null)
            return r_next(context);

        return endpointDelegate(context);
    }
}
