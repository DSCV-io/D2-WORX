// -----------------------------------------------------------------------
// <copyright file="CorsServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore;

using DcsvIo.D2.Utilities.Extensions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// DI registration entry point for the D² CORS policy. Reads the canonical
/// indexed env-var convention
/// (<see cref="D2AspNetCoreConstants.CORS_ORIGINS_CONFIG_KEY"/>) and
/// validates fail-closed at host build via <c>ValidateOnStart()</c>.
/// </summary>
public static class CorsServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the D² CORS policy named
        /// <see cref="D2AspNetCoreConstants.DEFAULT_CORS_POLICY_NAME"/>
        /// (<c>"D2_DEFAULT"</c>). Reads
        /// <see cref="D2AspNetCoreConstants.CORS_ORIGINS_CONFIG_KEY"/>
        /// (<c>"D2_CORS_ORIGINS"</c>) from <paramref name="configuration"/>
        /// — supports the indexed env-var convention
        /// (<c>D2_CORS_ORIGINS__0</c>, <c>D2_CORS_ORIGINS__1</c>, ...) per
        /// .NET <c>IConfiguration</c> array binding. Validates options at
        /// host build via <c>ValidateOnStart()</c> — fail-closed when zero
        /// origins resolve.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The configure callback runs AFTER env-derived defaults so callers
        /// can override or augment the bound origins list, alter the
        /// allowed-headers / allowed-methods set, toggle credentials
        /// support, or change the preflight TTL.
        /// </para>
        /// <para>
        /// CORS spec rejects <c>AllowCredentials = true</c> combined with
        /// <c>Origins = ["*"]</c> — the validator rejects this combination
        /// at host build so a misconfigured production deploy fails fast
        /// rather than degrading to silent permissive behavior.
        /// </para>
        /// </remarks>
        /// <param name="configuration">
        /// The host's <see cref="IConfiguration"/>, used to source the
        /// canonical indexed-env-var origin list when
        /// <see cref="D2CorsOptions.Origins"/> is left empty.
        /// </param>
        /// <param name="configure">
        /// Optional <see cref="D2CorsOptions"/> customizer applied AFTER
        /// env-derived defaults.
        /// </param>
        /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> or
        /// <paramref name="configuration"/> is null.
        /// </exception>
        public IServiceCollection AddD2Cors(
            IConfiguration configuration,
            Action<D2CorsOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddOptions<D2CorsOptions>()
                .Configure(opts =>
                {
                    if (opts.Origins.Falsey())
                    {
                        var fromConfig = configuration
                            .GetSection(D2AspNetCoreConstants.CORS_ORIGINS_CONFIG_KEY)
                            .Get<string[]>()
                            ?.Where(o => o.Truthy())
                            .Select(o => o.Trim())
                            .ToArray();

                        if (fromConfig is { Length: > 0 })
                            opts.Origins = fromConfig;
                    }

                    configure?.Invoke(opts);
                })
                .Validate(
                    o => o.Origins.Truthy(),
                    "D2CorsOptions.Origins must contain at least one origin "
                    + "(set via D2_CORS_ORIGINS__0 / __1 / ... env vars OR "
                    + "the configure callback). Empty list is fail-closed.")
                .Validate(
                    o => o.Origins.All(x => x.Truthy()),
                    "D2CorsOptions.Origins entries must not be empty / "
                    + "whitespace.")
                .Validate(
                    o => o.AllowedHeaders.All(x => x.Truthy()),
                    "D2CorsOptions.AllowedHeaders entries must not be "
                    + "empty / whitespace.")
                .Validate(
                    o => o.AllowedMethods.All(x => x.Truthy()),
                    "D2CorsOptions.AllowedMethods entries must not be "
                    + "empty / whitespace.")
                .Validate(
                    o => !(o.AllowCredentials && o.Origins.Contains("*")),
                    "D2CorsOptions.AllowCredentials = true is incompatible "
                    + "with wildcard \"*\" in Origins per CORS spec. Either "
                    + "list explicit origins or set AllowCredentials = false.")
                .Validate(
                    o => o.PreflightMaxAgeSeconds > 0,
                    "D2CorsOptions.PreflightMaxAgeSeconds must be > 0.")
                .ValidateOnStart();

            services.AddCors();

            // Late-bind the policy via IConfigureOptions<CorsOptions> so
            // the AddCors policy registration can resolve
            // IOptions<D2CorsOptions> at Configure time (which runs AFTER
            // the AddOptions configure delegate above and thus reflects
            // the env-derived defaults + the configure callback overrides).
            services.AddSingleton<IConfigureOptions<CorsOptions>, D2CorsPolicyConfigure>();

            return services;
        }
    }

    /// <summary>
    /// Configures the named D² CORS policy with the resolved
    /// <see cref="D2CorsOptions"/> snapshot. Registered against
    /// <see cref="IConfigureOptions{TOptions}"/> for
    /// <see cref="CorsOptions"/> so the policy delegate is invoked when the
    /// CORS pipeline builds its policy table at first request — by which
    /// point AddOptions's Configure pipeline has completed and produced
    /// the final D2CorsOptions snapshot.
    /// </summary>
    private sealed class D2CorsPolicyConfigure : IConfigureOptions<CorsOptions>
    {
        private readonly IOptions<D2CorsOptions> r_d2Options;

        public D2CorsPolicyConfigure(IOptions<D2CorsOptions> d2Options)
        {
            ArgumentNullException.ThrowIfNull(d2Options);
            r_d2Options = d2Options;
        }

        public void Configure(CorsOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var d2 = r_d2Options.Value;

            options.AddPolicy(
                D2AspNetCoreConstants.DEFAULT_CORS_POLICY_NAME,
                policy =>
                {
                    policy.WithOrigins([.. d2.Origins])
                          .WithHeaders([.. d2.AllowedHeaders])
                          .WithMethods([.. d2.AllowedMethods])
                          .SetPreflightMaxAge(TimeSpan.FromSeconds(d2.PreflightMaxAgeSeconds));

                    if (d2.AllowCredentials)
                        policy.AllowCredentials();
                    else
                        policy.DisallowCredentials();
                });
        }
    }
}
