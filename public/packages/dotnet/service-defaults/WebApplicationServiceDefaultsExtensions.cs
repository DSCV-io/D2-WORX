// -----------------------------------------------------------------------
// <copyright file="WebApplicationServiceDefaultsExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.ServiceDefaults;

using D2.Shared.AspNetCore;
using D2.Shared.Auth.Http;
using D2.Shared.Logging;
using D2.Shared.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Pipeline + endpoint + startup aggregator extensions — companions to
/// <see cref="ServiceDefaultsServiceCollectionExtensions.AddD2ServiceDefaults"/>.
/// Three public surfaces:
/// <see cref="WebApplicationServiceDefaultsExtensions.UseD2DefaultPipeline"/>
/// (LOCKED middleware ordering),
/// <see cref="WebApplicationServiceDefaultsExtensions.MapD2DefaultEndpoints"/>
/// (health + Prometheus), and the
/// <see cref="WebApplicationServiceDefaultsExtensions.RunD2ServiceAsync"/>
/// re-export.
/// </summary>
public static class WebApplicationServiceDefaultsExtensions
{
    /// <param name="app">The ASP.NET Core application builder.</param>
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Installs the D² default middleware pipeline in the LOCKED order:
        /// <c>UseD2SecurityHeaders</c> → <c>UseD2RequestLogging</c> →
        /// <c>UseD2Cors</c> → <c>UseRouting</c> →
        /// <c>UseD2InfrastructureBypass</c> → <c>UseAuthentication</c> →
        /// <c>UseD2Auth</c> → <c>UseAuthorization</c>. No insertion points
        /// — services that need bespoke ordering call the underlying lib
        /// extensions themselves and skip this method.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why this exact order</b>:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>
        ///     <c>UseD2SecurityHeaders</c> first so OWASP headers apply on
        ///     EVERY response, including ones produced by middleware that
        ///     short-circuits the pipeline (CORS preflight, infrastructure
        ///     bypass).
        ///   </description></item>
        ///   <item><description>
        ///     <c>UseD2RequestLogging</c> early (before routing) so even
        ///     early-pipeline failures emit a structured request-completion
        ///     line.
        ///   </description></item>
        ///   <item><description>
        ///     <c>UseD2Cors</c> after RequestLogging, before Routing — CORS
        ///     preflight (OPTIONS) responses must short-circuit before
        ///     routing tries to match a verb-specific endpoint.
        ///   </description></item>
        ///   <item><description>
        ///     <c>UseRouting</c> then <c>UseD2InfrastructureBypass</c> —
        ///     bypass needs the routing-resolved endpoint on the context to
        ///     invoke the matched <see cref="Microsoft.AspNetCore.Http.RequestDelegate"/>
        ///     directly when short-circuiting.
        ///   </description></item>
        ///   <item><description>
        ///     <c>UseAuthentication</c> → <c>UseD2Auth</c> →
        ///     <c>UseAuthorization</c>: the JWT auth middleware
        ///     (<c>UseD2Auth</c>) requires the AspNetCore authentication
        ///     feature on the context (<c>UseAuthentication</c>) and runs
        ///     BEFORE <c>UseAuthorization</c> so the authorization stage
        ///     fires scope / policy gates against the populated
        ///     <c>IRequestContext</c>.
        ///   </description></item>
        /// </list>
        /// <para>
        /// Skipped when the corresponding component was not registered —
        /// e.g. when <c>SkipAuthAutoWiring</c> was set on
        /// <see cref="D2ServiceDefaultsOptions"/>, the
        /// <c>UseAuthentication</c> + <c>UseD2Auth</c> +
        /// <c>UseAuthorization</c> calls still execute against the
        /// framework's own no-op stubs (no JWT validation registered →
        /// every request is anonymous, the AspNetCore-canonical behavior).
        /// </para>
        /// </remarks>
        /// <returns>The same <paramref name="app"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="app"/> is null.
        /// </exception>
        public IApplicationBuilder UseD2DefaultPipeline()
        {
            ArgumentNullException.ThrowIfNull(app);

            // Per-component pass-through configure callbacks are read from
            // the options instance bound at AddD2ServiceDefaults time
            // (the aggregator binds them into DI so this lookup succeeds).
            // Defaults to a fresh instance when no IOptions registration
            // exists, in which case every Skip* flag is false and every
            // pass-through delegate is null — each underlying middleware
            // uses its own defaults.
            var optionsAccessor = app.ApplicationServices
                .GetService<IOptions<D2ServiceDefaultsOptions>>();
            var options = optionsAccessor?.Value ?? new D2ServiceDefaultsOptions();

            app.UseD2SecurityHeaders(options.SecurityHeadersConfigure);
            app.UseD2RequestLogging();
            app.UseD2Cors();
            app.UseRouting();
            app.UseD2InfrastructureBypass(options.InfrastructureBypassConfigure);

            // Auth middleware skipped when AddD2ServiceDefaults skipped
            // the auth registrations — calling UseD2Auth without
            // JwtValidator in DI would crash on first request.
            if (options.SkipAuthAutoWiring is false)
            {
                app.UseAuthentication();
                app.UseD2Auth();
                app.UseAuthorization();
            }

            return app;
        }
    }

    /// <param name="endpoints">The endpoint route builder.</param>
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Maps the D² default endpoint set:
        /// <see cref="HealthEndpointsRouteBuilderExtensions.MapD2HealthEndpoints"/>
        /// (<c>/health</c> + <c>/alive</c>) and
        /// <see cref="WebApplicationTelemetryExtensions.MapD2PrometheusEndpoint"/>
        /// (<c>/metrics</c>, IP-restricted, honors
        /// <c>OTEL_SDK_DISABLED</c>).
        /// </summary>
        /// <returns>
        /// The same <paramref name="endpoints"/> for fluent chaining.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="endpoints"/> is null.
        /// </exception>
        public IEndpointRouteBuilder MapD2DefaultEndpoints()
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            endpoints.MapD2HealthEndpoints();
            endpoints.MapD2PrometheusEndpoint();

            return endpoints;
        }
    }

    /// <param name="app">The configured ASP.NET Core web application.</param>
    extension(WebApplication app)
    {
        /// <summary>
        /// Re-exports
        /// <see cref="RunD2ServiceWebApplicationExtensions.RunD2ServiceAsync"/>
        /// at the aggregator namespace so a single
        /// <c>using D2.Shared.ServiceDefaults;</c> directive at a
        /// composition root makes every default surface available without
        /// importing each underlying lib's namespace separately.
        /// </summary>
        /// <param name="serviceName">
        /// Optional service name for the "Starting" log line. When null /
        /// empty / whitespace, falls back to
        /// <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.ApplicationName"/>.
        /// </param>
        /// <returns>A task that completes when the host shuts down.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="app"/> is null.
        /// </exception>
        public Task RunD2ServiceAsync(string? serviceName = null)
        {
            ArgumentNullException.ThrowIfNull(app);

            return RunD2ServiceWebApplicationExtensions.RunD2ServiceAsync(
                app,
                serviceName);
        }
    }
}
