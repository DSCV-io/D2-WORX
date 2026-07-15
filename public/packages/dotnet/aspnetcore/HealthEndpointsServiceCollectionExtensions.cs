// -----------------------------------------------------------------------
// <copyright file="HealthEndpointsServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

/// <summary>
/// DI registration entry point for the D² health-check baseline — registers
/// the canonical <c>"self"</c> check tagged
/// <see cref="D2AspNetCoreConstants.LIVE_HEALTH_TAG"/>.
/// </summary>
public static class HealthEndpointsServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the D² health-check baseline. Adds the
        /// <see cref="D2AspNetCoreConstants.SELF_HEALTH_CHECK_NAME"/> check that always returns
        /// <see cref="HealthStatus.Healthy"/> tagged
        /// <see cref="D2AspNetCoreConstants.LIVE_HEALTH_TAG"/> — so it
        /// participates in both <c>/health</c> (full status) and
        /// <c>/alive</c> (live-tag-only) endpoints when mapped via
        /// <see cref="HealthEndpointsRouteBuilderExtensions.MapD2HealthEndpoints"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Idempotent: the underlying
        /// <see cref="HealthCheckServiceCollectionExtensions.AddHealthChecks(IServiceCollection)"/>
        /// returns the same builder on repeat calls (TryAdd internally),
        /// but registering the same check name twice raises
        /// <see cref="ArgumentException"/> per ASP.NET Core convention. To
        /// avoid the duplicate registration, the helper checks if a check
        /// with the same name is already registered before adding it.
        /// </para>
        /// <para>
        /// Per-service infrastructure layers add their own checks (DB,
        /// Redis, RabbitMQ, KeyCustodian) by chaining
        /// <c>services.AddHealthChecks().AddDbContextCheck&lt;...&gt;()</c>
        /// etc. — those auto-flow into the <c>/health</c> endpoint via the
        /// shared underlying
        /// <see cref="HealthCheckService"/>. Checks tagged with
        /// <see cref="D2AspNetCoreConstants.LIVE_HEALTH_TAG"/> additionally
        /// participate in the <c>/alive</c> endpoint.
        /// </para>
        /// </remarks>
        /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> is null.
        /// </exception>
        public IServiceCollection AddD2HealthChecks()
        {
            ArgumentNullException.ThrowIfNull(services);

            var healthChecksBuilder = services.AddHealthChecks();

            // Idempotent guard: ASP.NET Core's HealthCheckRegistration set
            // throws on duplicate names, but AddD2HealthChecks IS designed
            // to be safe-to-call-twice. Walk existing registrations via
            // the IConfigureOptions<HealthCheckServiceOptions> registered
            // by AddHealthChecks; if a "self" check already exists, no-op.
            var alreadyRegistered = services.Any(d =>
                d.ServiceType
                    == typeof(IConfigureOptions<HealthCheckServiceOptions>)
                && d.ImplementationInstance
                    is HealthCheckRegistrationInstanceMarker marker
                && marker.CheckName
                    == D2AspNetCoreConstants.SELF_HEALTH_CHECK_NAME);

            if (alreadyRegistered)
                return services;

            healthChecksBuilder.AddCheck(
                D2AspNetCoreConstants.SELF_HEALTH_CHECK_NAME,
                () => HealthCheckResult.Healthy(),
                tags: [D2AspNetCoreConstants.LIVE_HEALTH_TAG]);

            services.AddSingleton<IConfigureOptions<HealthCheckServiceOptions>>(
                new HealthCheckRegistrationInstanceMarker(
                    D2AspNetCoreConstants.SELF_HEALTH_CHECK_NAME));

            return services;
        }
    }

    /// <summary>
    /// Marker registration recording that <c>AddD2HealthChecks</c> already
    /// added the "self" check — used by the idempotency guard above to
    /// skip duplicate registration on repeat calls. Implements the
    /// <c>IConfigureOptions</c> contract trivially (no-op Configure).
    /// </summary>
    private sealed class HealthCheckRegistrationInstanceMarker
        : IConfigureOptions<HealthCheckServiceOptions>
    {
        public HealthCheckRegistrationInstanceMarker(string checkName)
        {
            CheckName = checkName;
        }

        public string CheckName { get; }

        public void Configure(HealthCheckServiceOptions options)
        {
            // Marker only — actual check registration happens in
            // AddD2HealthChecks via healthChecksBuilder.AddCheck.
        }
    }
}
