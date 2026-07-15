// -----------------------------------------------------------------------
// <copyright file="D2InfrastructureBypassOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore;

/// <summary>
/// Configuration for
/// <see cref="InfrastructureBypassApplicationBuilderExtensions.UseD2InfrastructureBypass"/>.
/// </summary>
/// <remarks>
/// <para>
/// The middleware checks each incoming request's path against
/// <see cref="InfrastructurePaths"/> via
/// <see cref="InfrastructurePathMatcher.IsInfrastructurePath"/>. On match
/// it sets <see cref="D2AspNetCoreConstants.INFRASTRUCTURE_HTTP_CONTEXT_ITEM_KEY"/>
/// to <c>true</c> (so any business middleware downstream can read the flag
/// to no-op early) AND, when <see cref="TagOnly"/> is <c>false</c> (the
/// default), short-circuits the pipeline to skip business middleware
/// registered after this one.
/// </para>
/// <para>
/// Default behavior is short-circuit: heavy middleware
/// (rate limiting, idempotency-check, request-context enrichment, auth
/// middleware that runs full JWT validation) does NOT execute on cheap
/// infrastructure endpoints (<c>/health</c>, <c>/alive</c>, <c>/metrics</c>,
/// <c>/.well-known/*</c>). Services that need custom middleware to run on
/// infrastructure paths (a custom rate-limit policy on <c>/metrics</c>, for
/// example) opt in to <see cref="TagOnly"/>=<c>true</c> and inspect the
/// HttpContext.Items flag in their middleware.
/// </para>
/// </remarks>
public sealed record D2InfrastructureBypassOptions
{
    /// <summary>
    /// Gets or sets the path-prefix list to treat as infrastructure. Default
    /// <see cref="D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS"/>.
    /// Settable so the configure callback on
    /// <see cref="InfrastructureBypassApplicationBuilderExtensions.UseD2InfrastructureBypass"/>
    /// can override after the options instance is constructed by the DI
    /// container.
    /// </summary>
    public IReadOnlyList<string> InfrastructurePaths { get; set; } =
        D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS;

    /// <summary>
    /// Gets or sets a value indicating whether the middleware ONLY tags the
    /// request — <c>true</c> sets
    /// <see cref="D2AspNetCoreConstants.INFRASTRUCTURE_HTTP_CONTEXT_ITEM_KEY"/>
    /// and continues the pipeline (downstream business middleware still
    /// runs but can opt-out by reading the flag). Default <c>false</c> =
    /// short-circuit: infrastructure-path requests bypass any middleware
    /// registered AFTER <c>UseD2InfrastructureBypass</c> and proceed
    /// directly to the matched endpoint via the routing-resolved
    /// <see cref="Microsoft.AspNetCore.Http.Endpoint"/>.
    /// </summary>
    public bool TagOnly { get; set; }
}
