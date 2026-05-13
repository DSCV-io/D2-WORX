// -----------------------------------------------------------------------
// <copyright file="D2AspNetCoreConstants.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.AspNetCore;

/// <summary>
/// Public constants exposed by <see cref="D2.Shared.AspNetCore"/> — endpoint
/// route paths, header names, configuration keys, and the canonical
/// infrastructure-path list shared with consumers
/// (<see cref="D2.Shared.AspNetCore.InfrastructurePathMatcher"/>'s default
/// path set, the <c>UseD2InfrastructureBypass</c> default, the
/// <c>D2.Shared.Logging</c> request-logging middleware default, and the
/// <c>D2.Shared.Telemetry</c> AspNetCore-instrumentation filter default).
/// </summary>
/// <remarks>
/// Consumers that want to reference the canonical infrastructure paths or
/// header / config-key strings by symbol (per the codebase's
/// nameof-discipline rule for codegen / wire-format member references) read
/// from this class so a literal-value drift surfaces as a compile break + a
/// spec-pinning test failure rather than a silent operational regression.
/// </remarks>
public static class D2AspNetCoreConstants
{
    /// <summary>
    /// Canonical health-probe route path. Returns the full health-check
    /// status (every check, regardless of tag) when mapped via
    /// <see cref="HealthEndpointsRouteBuilderExtensions.MapD2HealthEndpoints"/>.
    /// </summary>
    public const string HEALTH_ENDPOINT_PATH = "/health";

    /// <summary>
    /// Canonical liveness-probe route path. Returns only health checks
    /// tagged <c>"live"</c> (the kubernetes-conventional liveness split) when
    /// mapped via
    /// <see cref="HealthEndpointsRouteBuilderExtensions.MapD2HealthEndpoints"/>.
    /// </summary>
    public const string ALIVE_ENDPOINT_PATH = "/alive";

    /// <summary>
    /// Canonical Prometheus scraping route path. Mapped by
    /// <c>D2.Shared.Telemetry.WebApplicationTelemetryExtensions.MapD2PrometheusEndpoint</c>
    /// when the in-process Prometheus exporter is enabled.
    /// </summary>
    public const string METRICS_ENDPOINT_PATH = "/metrics";

    /// <summary>
    /// Canonical well-known-doc prefix — covers OIDC discovery
    /// (<c>/.well-known/openid-configuration</c>), JWKS
    /// (<c>/.well-known/jwks.json</c>), security.txt, etc.
    /// </summary>
    public const string WELL_KNOWN_ENDPOINT_PATH = "/.well-known";

    /// <summary>
    /// Canonical health-check tag identifying checks that participate in the
    /// liveness probe (<see cref="ALIVE_ENDPOINT_PATH"/>). The
    /// <c>"self"</c> check registered by
    /// <see cref="HealthEndpointsServiceCollectionExtensions.AddD2HealthChecks"/>
    /// carries this tag; per-service infrastructure layers can register
    /// additional liveness-relevant checks under the same tag.
    /// </summary>
    public const string LIVE_HEALTH_TAG = "live";

    /// <summary>
    /// Name of the always-healthy self-check registered by
    /// <see cref="HealthEndpointsServiceCollectionExtensions.AddD2HealthChecks"/>.
    /// Tagged <see cref="LIVE_HEALTH_TAG"/> so it participates in both
    /// <c>/health</c> and <c>/alive</c> endpoints.
    /// </summary>
    public const string SELF_HEALTH_CHECK_NAME = "self";

    /// <summary>
    /// Canonical correlation-id header name consumed by
    /// <see cref="ProblemDetailsServiceCollectionExtensions.AddD2ProblemDetails"/>
    /// — when present on the request, its value flows to the response
    /// <c>extensions["correlationId"]</c>; when absent, a fresh GUID is
    /// generated and echoed back via the response header.
    /// </summary>
    public const string CORRELATION_ID_HEADER = "X-Correlation-Id";

    /// <summary>
    /// Canonical idempotency-key header name. Listed in
    /// <see cref="D2CorsOptions.AllowedHeaders"/>'s default so cross-origin
    /// requests can carry the header without a CORS preflight rejection.
    /// </summary>
    public const string IDEMPOTENCY_KEY_HEADER = "X-Idempotency-Key";

    /// <summary>
    /// Canonical configuration key for the CORS allowed-origins list.
    /// Bound via <c>configuration.GetSection(CORS_ORIGINS_CONFIG_KEY).Get&lt;string[]&gt;()</c>;
    /// supports the indexed env-var convention
    /// (<c>D2_CORS_ORIGINS__0</c>, <c>D2_CORS_ORIGINS__1</c>, ...).
    /// </summary>
    public const string CORS_ORIGINS_CONFIG_KEY = "D2_CORS_ORIGINS";

    /// <summary>
    /// Canonical CORS policy name registered by
    /// <see cref="CorsServiceCollectionExtensions.AddD2Cors"/> + applied by
    /// <see cref="CorsApplicationBuilderExtensions.UseD2Cors"/>.
    /// </summary>
    public const string DEFAULT_CORS_POLICY_NAME = "D2_DEFAULT";

    /// <summary>
    /// Maximum byte length the
    /// <see cref="ProblemDetailsServiceCollectionExtensions.AddD2ProblemDetails"/>
    /// customizer accepts from the inbound
    /// <see cref="CORRELATION_ID_HEADER"/> header. Values exceeding the cap
    /// are treated as absent (a fresh GUID is generated). Prevents an
    /// arbitrary-length user header from inflating the response body.
    /// </summary>
    public const int MAX_CORRELATION_ID_LENGTH = 128;

    /// <summary>
    /// HttpContext.Items key set by the
    /// <see cref="InfrastructureBypassApplicationBuilderExtensions.UseD2InfrastructureBypass"/>
    /// middleware to <c>true</c> for requests whose path matches the
    /// infrastructure-path list. Downstream business middleware can read
    /// the flag to no-op early (rate limiting, idempotency, request
    /// enrichment, etc.). Public so consumers reference the key by symbol.
    /// </summary>
    public const string INFRASTRUCTURE_HTTP_CONTEXT_ITEM_KEY = "D2.IsInfrastructure";

    /// <summary>
    /// Default infrastructure-path prefix list — consumed by
    /// <see cref="InfrastructurePathMatcher"/>, by
    /// <see cref="D2InfrastructureBypassOptions.InfrastructurePaths"/>'s
    /// default, by the <c>D2.Shared.Logging</c> request-logging middleware
    /// default, and by the <c>D2.Shared.Telemetry</c>
    /// AspNetCore-instrumentation filter default. Single source of truth so
    /// the four consumers stay aligned without per-lib duplication.
    /// </summary>
    public static readonly IReadOnlyList<string> DEFAULT_INFRASTRUCTURE_PATHS =
    [
        HEALTH_ENDPOINT_PATH,
        ALIVE_ENDPOINT_PATH,
        METRICS_ENDPOINT_PATH,
        WELL_KNOWN_ENDPOINT_PATH,
    ];
}
