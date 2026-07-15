// -----------------------------------------------------------------------
// <copyright file="AuthEndpointGuardStartupFilter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Startup;

using DcsvIo.D2.AspNetCore;
using DcsvIo.D2.Auth.Grpc.Endpoints;
using DcsvIo.D2.Auth.Http.Endpoints;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Fails host startup when any mapped <see cref="RouteEndpoint"/> lacks a
/// declared auth intent. Deny-by-default enforcement: an endpoint registered
/// with no auth declaration is a configuration error that MUST surface at boot
/// rather than silently permitting unauthenticated access at runtime.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Hosting mechanism</strong>: implemented as
/// <see cref="IStartupFilter"/> rather than <see cref="Microsoft.Extensions.Hosting.IHostedService"/>.
/// <c>IStartupFilter.Configure(next)</c> runs during HTTP-pipeline construction
/// inside <c>GenericWebHostService.StartAsync</c>, AFTER the
/// <c>WebApplication</c>'s <c>DataSources</c> have been wired into the routing
/// composite and BEFORE any request is served. This makes it production-faithful
/// in the <c>WebApplication</c> model: <c>app.MapXxx()</c> calls that happen
/// after <c>builder.Build()</c> write into <c>WebApplication.DataSources</c>,
/// which are merged into the DI-resolved <c>EndpointDataSource</c> composite
/// during pipeline construction — the window in which this filter runs. An
/// <c>IHostedService</c> by contrast starts AFTER pipeline construction and
/// injects a <c>EndpointDataSource</c> singleton captured at DI-resolve time
/// before the <c>WebApplication</c>'s sources are merged in, so it sees an
/// empty collection in the <c>WebApplication</c> model.
/// </para>
/// <para>
/// <strong>What "declared intent" means</strong>: any of the following on the
/// endpoint's <c>EndpointMetadataCollection</c>:
/// </para>
/// <list type="bullet">
///   <item><see cref="EndpointScopeMetadata"/> (HTTP fluent:
///     <c>RequireAnyScope</c> / <c>RequireAllScopes</c> /
///     <c>MarkAsD2HarmlessEndpoint</c>).</item>
///   <item><see cref="MethodScopeMetadata"/> (gRPC fluent:
///     <c>RequireAnyScope</c> / <c>RequireAllScopes</c> /
///     <c>MarkAsD2HarmlessEndpoint</c>).</item>
///   <item><see cref="D2RequireAnyScopeAttribute"/> on the service class or
///     method (gRPC attribute path).</item>
///   <item><see cref="D2RequireAllScopesAttribute"/> on the service class or
///     method (gRPC attribute path).</item>
///   <item><see cref="D2HarmlessEndpointAttribute"/> on the service class or
///     method (gRPC attribute path).</item>
/// </list>
/// <para>
/// <strong>Skipped endpoints</strong>: any <see cref="RouteEndpoint"/> whose
/// <see cref="RouteEndpoint.RoutePattern"/> raw text matches the canonical
/// infrastructure-path list
/// (<see cref="D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS"/>) is
/// skipped — <c>/health</c>, <c>/alive</c>, <c>/metrics</c>,
/// <c>/.well-known</c>. Non-<see cref="RouteEndpoint"/> entries
/// (e.g. <see cref="Endpoint"/> base instances with no route pattern) are
/// also skipped: they carry no route identity and can't be guarded by
/// convention.
/// </para>
/// <para>
/// <strong>Error message discipline</strong>: the thrown
/// <see cref="InvalidOperationException"/> message lists the route pattern
/// RAW TEXT only — no route values, no query string, no request data. This is
/// PII-safe: route templates (<c>/users/{id}</c>, <c>/files/{fileId}</c>)
/// contain no actual user data, only the structural shape of the URL.
/// </para>
/// <para>
/// <strong>gRPC attribute projection</strong>: D2 gRPC scope attributes
/// (<see cref="D2RequireAnyScopeAttribute"/> / <see cref="D2RequireAllScopesAttribute"/> /
/// <see cref="D2HarmlessEndpointAttribute"/>) placed on a service class or method
/// DO project onto endpoint metadata via <c>MapGrpcService&lt;T&gt;()</c>; the guard
/// reads them as the attribute path for declared intent.
/// The fluent path (<c>.RequireAnyScope(...)</c> / <c>.MarkAsD2HarmlessEndpoint()</c>
/// on the builder) adds <see cref="MethodScopeMetadata"/> and is the alternative
/// mechanism; the guard checks both.
/// </para>
/// <para>
/// <strong>gRPC infrastructure catch-all endpoints</strong>:
/// <c>MapGrpcService&lt;T&gt;()</c> also registers gRPC infrastructure catch-all
/// endpoints (e.g. <c>{pkg}.{Svc}/{unimplementedMethod:grpcunimplemented}</c>,
/// <c>{unimplementedService}/{unimplementedMethod:grpcunimplemented}</c>). These
/// carry NO auth metadata and are NOT real callable methods — they exist only to
/// return UNIMPLEMENTED for unknown routes. The guard identifies them by the
/// <c>grpcunimplemented</c> route constraint in the route pattern: gRPC
/// AspNetCore adds this constraint exclusively to its unimplemented-method
/// catch-all slots, so any endpoint whose route pattern parameters include this
/// constraint is infrastructure and is skipped.
/// </para>
/// </remarks>
internal sealed partial class AuthEndpointGuardStartupFilter : IStartupFilter
{
    private readonly ILogger<AuthEndpointGuardStartupFilter> r_logger;

    /// <summary>
    /// Initializes the startup filter.
    /// </summary>
    /// <param name="logger">Logger for the pre-throw diagnostic message.</param>
    public AuthEndpointGuardStartupFilter(
        ILogger<AuthEndpointGuardStartupFilter> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        r_logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The returned action calls <paramref name="next"/> FIRST, which triggers
    /// the rest of the middleware pipeline construction — including
    /// <c>UseRouting()</c> which merges all registered endpoint data sources
    /// (including <c>WebApplication.DataSources</c>) into the DI-resolved
    /// <see cref="EndpointDataSource"/> composite. Only after <c>next</c>
    /// returns does the action resolve and walk the now-fully-populated
    /// endpoint set.
    /// </para>
    /// <para>
    /// In the <c>WebApplication</c> production model, <c>app.MapXxx()</c>
    /// calls happen before <c>StartAsync</c> and write into
    /// <c>WebApplication.DataSources</c>. Those sources are picked up by
    /// <c>UseRouting()</c> during pipeline construction (which is part of the
    /// <c>next</c> chain). In the generic-host + <c>UseEndpoints</c> model,
    /// <c>UseEndpoints</c> itself registers the endpoint data sources, which
    /// are similarly picked up before this filter's post-<c>next</c> walk.
    /// Both models surface a fully populated <see cref="EndpointDataSource"/>
    /// by the time the walk runs.
    /// </para>
    /// <para>
    /// Fail-before-traffic is preserved: the action throws
    /// <see cref="InvalidOperationException"/> when a violation is found,
    /// which propagates up through <c>BuildApplication()</c> and aborts
    /// <c>GenericWebHostService.StartAsync</c> before Kestrel accepts
    /// connections.
    /// </para>
    /// </remarks>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            // Call next first — this completes the middleware pipeline
            // construction including UseRouting() which merges all endpoint
            // data sources into the DI composite. After next returns, the
            // EndpointDataSource is fully populated.
            next(app);

            var endpointDataSource = app.ApplicationServices
                .GetRequiredService<EndpointDataSource>();

            var offenders = new List<string>();

            foreach (var endpoint in endpointDataSource.Endpoints)
            {
                if (endpoint is not RouteEndpoint routeEndpoint)
                    continue;

                var rawText = routeEndpoint.RoutePattern.RawText ?? string.Empty;

                if (InfrastructurePathMatcher.IsInfrastructurePath(
                        new PathString("/" + rawText.TrimStart('/')),
                        D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS))
                    continue;

                // Skip gRPC infrastructure catch-all endpoints registered by
                // MapGrpcService<T>() for unknown-method / unknown-service routing.
                // These carry no auth metadata and are not real callable methods.
                if (IsGrpcCatchAllEndpoint(routeEndpoint))
                    continue;

                if (!HasDeclaredIntent(routeEndpoint))
                    offenders.Add(rawText.Length > 0 ? rawText : "(no route pattern)");
            }

            if (offenders.Falsey())
                return;

            var routeList = string.Join(
                ", ",
                offenders.Select(r => $"'{r}'"));

            LogUndeclaredEndpoints(r_logger, routeList);

            throw new InvalidOperationException(
                "One or more endpoints are missing a declared auth intent. "
                + "Every mapped RouteEndpoint must carry one of: "
                + "RequireAnyScope / RequireAllScopes / MarkAsD2HarmlessEndpoint "
                + "(HTTP fluent or gRPC fluent), "
                + "or [D2RequireAnyScope] / [D2RequireAllScopes] / [D2HarmlessEndpoint] "
                + "(gRPC attribute path). "
                + "Undeclared routes: " + routeList + ". "
                + "To fix: add the appropriate fluent call on the endpoint builder "
                + "(e.g. app.MapGet(...).RequireAnyScope(\"scope\")) "
                + "or add the corresponding attribute to the gRPC service class / method. "
                + "To opt out of this guard entirely, "
                + "set D2ServiceDefaultsOptions.SkipAuthEndpointGuard = true "
                + "(test hosts, anonymous-only admin tools).");
        };
    }

    /// <summary>
    /// Returns <see langword="true"/> when the endpoint is a gRPC infrastructure
    /// catch-all registered by <c>MapGrpcService&lt;T&gt;()</c> to return
    /// UNIMPLEMENTED for unknown routes. These endpoints carry no auth metadata
    /// and are not real callable methods; the guard must skip them.
    /// </summary>
    /// <remarks>
    /// The discriminator is the <c>grpcunimplemented</c> route constraint that
    /// gRPC AspNetCore adds exclusively to its catch-all route parameters (e.g.
    /// <c>{unimplementedMethod:grpcunimplemented}</c>). Real gRPC method endpoints
    /// use literal route patterns like <c>/pkg.Service/Method</c> (no parameters,
    /// no constraint); HTTP business endpoints never carry this constraint. This
    /// check reliably separates catch-alls from both categories without requiring
    /// a dependency on the <c>Grpc.AspNetCore.Server</c> internal metadata types.
    /// </remarks>
    private static bool IsGrpcCatchAllEndpoint(RouteEndpoint endpoint)
    {
        // gRPC infrastructure catch-alls are identified by the grpcunimplemented
        // route constraint that gRPC AspNetCore adds exclusively to its
        // unimplemented-method catch-all slots. HTTP business endpoints never
        // carry this constraint. Real gRPC method endpoints use literal route
        // patterns like /pkg.Service/Method (no parameters, no constraint).
        // This check reliably separates catch-alls from both categories.
        foreach (var parameter in endpoint.RoutePattern.Parameters)
        {
            foreach (var policy in parameter.ParameterPolicies)
            {
                if (string.Equals(
                    policy.Content,
                    "grpcunimplemented",
                    StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static bool HasDeclaredIntent(RouteEndpoint endpoint)
    {
        var metadata = endpoint.Metadata;

        // HTTP fluent path → EndpointScopeMetadata
        if (metadata.GetMetadata<EndpointScopeMetadata>() is not null)
            return true;

        // gRPC fluent path → MethodScopeMetadata
        if (metadata.GetMetadata<MethodScopeMetadata>() is not null)
            return true;

        // gRPC attribute path — ASP.NET Core routing auto-pulls class-level
        // and method-level attributes onto endpoint metadata for services
        // registered via MapGrpcService<T>() when the service class carries
        // [D2RequireAnyScope] / [D2RequireAllScopes] / [D2HarmlessEndpoint].
        //
        // RESIDUAL — attribute-on-HTTP edge: these three attributes are
        // accepted here transport-agnostically (the guard only checks for
        // presence). JwtAuthMiddleware enforces only EndpointScopeMetadata,
        // so a gRPC scope attribute mistakenly placed on an HTTP endpoint
        // would pass this guard but NOT be enforced at runtime. The gRPC
        // FLUENT cross-transport path is compile-prevented by the
        // GrpcServiceEndpointConventionBuilder receiver constraint on
        // RequireD2GrpcScopeExtensions; this attribute-on-HTTP residual is
        // an accepted narrow edge: the gRPC attributes live in a
        // gRPC-namespaced package and HTTP endpoints use minimal APIs (where
        // attributes on route handlers are not projected onto endpoint metadata
        // by default).
        if (metadata.GetMetadata<D2RequireAnyScopeAttribute>() is not null)
            return true;

        if (metadata.GetMetadata<D2RequireAllScopesAttribute>() is not null)
            return true;

        if (metadata.GetMetadata<D2HarmlessEndpointAttribute>() is not null)
            return true;

        return false;
    }

    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Error,
        Message = "AuthEndpointGuard: host startup BLOCKED — undeclared endpoints: {Routes}")]
    private static partial void LogUndeclaredEndpoints(
        ILogger logger,
        string routes);
}
