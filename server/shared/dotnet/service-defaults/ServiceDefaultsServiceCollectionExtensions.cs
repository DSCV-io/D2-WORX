// -----------------------------------------------------------------------
// <copyright file="ServiceDefaultsServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.ServiceDefaults;

using D2.Shared.AspNetCore;
using D2.Shared.Auth;
using D2.Shared.Auth.Grpc;
using D2.Shared.Auth.Http;
using D2.Shared.Auth.Startup;
using D2.Shared.Caching.Local.Default;
using D2.Shared.Handler;
using D2.Shared.I18n;
using D2.Shared.Logging;
using D2.Shared.Telemetry;
using D2.Shared.Utilities.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI registration aggregator — single entry point that wires every prior
/// shared lib's <c>AddD2*</c> extension into the host's service collection
/// in one call. Mirrors .NET Aspire's <c>Microsoft.Extensions.ServiceDefaults</c>
/// convention adapted for the D² shared-lib stack.
/// </summary>
public static class ServiceDefaultsServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Wires the canonical D² service-defaults stack in this order:
        /// <c>D2Env.Load()</c> (env-file discovery) first;
        /// <c>AddD2Logging(configuration, options.LoggingConfigure)</c>;
        /// <c>AddD2Telemetry(configuration, options.TelemetryConfigure)</c>;
        /// <c>AddD2I18n(configuration)</c>;
        /// <c>AddD2Handler()</c>;
        /// <c>AddD2Auth(options.AuthConfigure).AddD2AuthHttp().AddD2AuthGrpc()</c>
        /// (skipped when
        /// <see cref="D2ServiceDefaultsOptions.SkipAuthAutoWiring"/> is
        /// <c>true</c>);
        /// <c>AddD2LocalCache(options.LocalCacheConfigure)</c>
        /// (skipped when
        /// <see cref="D2ServiceDefaultsOptions.SkipLocalCacheAutoWiring"/>
        /// is <c>true</c>);
        /// <c>AddD2HealthChecks()</c>;
        /// <c>AddD2ProblemDetails(options.ProblemDetailsConfigure)</c>;
        /// <c>AddD2Cors(configuration, options.CorsConfigure)</c>;
        /// <c>ConfigureHttpClientDefaults(http =&gt; http.AddStandardResilienceHandler())</c>
        /// (skipped when
        /// <see cref="D2ServiceDefaultsOptions.SkipHttpClientResilienceDefaults"/>
        /// is <c>true</c>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIN AGGREGATOR — ZERO logic of its own. Each registration
        /// delegates to the owning lib's existing extension; per-component
        /// configuration flows through pass-through
        /// <see cref="Action{T}"/> delegates on
        /// <see cref="D2ServiceDefaultsOptions"/>.
        /// </para>
        /// <para>
        /// <b>Auth wiring contract</b>: when
        /// <see cref="D2ServiceDefaultsOptions.SkipAuthAutoWiring"/> is
        /// <c>false</c> (the default), <see cref="D2ServiceDefaultsOptions.AuthConfigure"/>
        /// MUST be non-null — the aggregator throws
        /// <see cref="InvalidOperationException"/> with a remediation
        /// message otherwise. The fail-fast prevents services from
        /// accidentally shipping without auth wiring; opt out of auth
        /// entirely by setting
        /// <see cref="D2ServiceDefaultsOptions.SkipAuthAutoWiring"/> to
        /// <c>true</c>.
        /// </para>
        /// <para>
        /// Idempotent at the IServiceCollection level — calling twice
        /// doesn't throw, but the second call's options stack via the
        /// standard <c>IOptions</c> pipeline. Auth registration is
        /// idempotent via the underlying libs' own <c>TryAdd*</c>
        /// guards; <see cref="HealthEndpointsServiceCollectionExtensions.AddD2HealthChecks"/>
        /// is idempotent via its internal marker.
        /// </para>
        /// </remarks>
        /// <param name="configuration">
        /// The host's <see cref="IConfiguration"/>, forwarded to the
        /// underlying libs that take it (<c>AddD2Logging</c>,
        /// <c>AddD2Telemetry</c>, <c>AddD2I18n</c>, <c>AddD2Cors</c>).
        /// </param>
        /// <param name="configure">
        /// Optional configuration delegate populating
        /// <see cref="D2ServiceDefaultsOptions"/> — opt-out flags +
        /// per-component pass-through configure callbacks.
        /// </param>
        /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> or
        /// <paramref name="configuration"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when
        /// <see cref="D2ServiceDefaultsOptions.SkipAuthAutoWiring"/> is
        /// <c>false</c> AND
        /// <see cref="D2ServiceDefaultsOptions.AuthConfigure"/> is null.
        /// </exception>
        public IServiceCollection AddD2ServiceDefaults(
            IConfiguration configuration,
            Action<D2ServiceDefaultsOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            // Discover + load .env / .env.local / .env.secrets BEFORE any
            // env-var-derived defaults are resolved by downstream
            // AddD2X(configuration, ...) calls. Idempotent (s_loaded flag
            // inside D2Env). Container deploys are no-ops here (Compose
            // injects env vars before host start).
            D2Env.Load();

            // Snapshot the resolved options into a local — every
            // downstream call needs to read the opt-out flags + the
            // per-component pass-through delegates without re-running the
            // configure callback. The pattern mirrors the probe used by
            // the per-lib AddD2X extensions. ALSO bind the options into
            // DI so UseD2DefaultPipeline can read them at pipeline-
            // installation time (e.g. to skip UseD2Auth when
            // SkipAuthAutoWiring is true; otherwise the middleware would
            // attempt to resolve JwtValidator from an empty DI graph).
            var options = new D2ServiceDefaultsOptions();
            configure?.Invoke(options);

            services.AddOptions<D2ServiceDefaultsOptions>()
                .Configure(opts => configure?.Invoke(opts));

            // Fail-fast on the auth wiring contract: when auto-wire is
            // requested (default) but no AuthConfigure delegate was
            // supplied, the aggregator can't call AddD2Auth (which
            // requires a non-null Action<AuthOptions>). Better to fail at
            // host build with a clear remediation message than to skip
            // auth silently and surface as a confusing 401 / null-ref
            // mid-request.
            if (options.SkipAuthAutoWiring is false && options.AuthConfigure is null)
            {
                throw new InvalidOperationException(
                    "D2ServiceDefaultsOptions.AuthConfigure is required when "
                    + "SkipAuthAutoWiring is false. Either set AuthConfigure to "
                    + "wire auth, or set SkipAuthAutoWiring to true to opt out "
                    + "of auth wiring entirely.");
            }

            services.AddD2Logging(configuration, options.LoggingConfigure);
            services.AddD2Telemetry(configuration, options.TelemetryConfigure);
            services.AddD2I18n(configuration);
            services.AddD2Handler();

            if (options.SkipAuthAutoWiring is false)
            {
                // AuthConfigure is guaranteed non-null at this point by
                // the fail-fast above. Chain the three calls so the order
                // (Auth then Auth.Http then Auth.Grpc) is locked — the
                // sibling libs' fail-fast preconditions already enforce
                // it, but the explicit chain documents the requirement
                // at the call site.
                services.AddD2Auth(options.AuthConfigure!)
                    .AddD2AuthHttp()
                    .AddD2AuthGrpc();

                // Deny-by-default boot guard: fail startup when any
                // RouteEndpoint lacks a declared auth intent. Gated on
                // SkipAuthAutoWiring (auth opt-out implies endpoint-guard
                // opt-out — anonymous-only tools don't declare scopes) AND
                // on SkipAuthEndpointGuard for the explicit opt-out case
                // (test hosts that register synthetic unannotated endpoints).
                if (options.SkipAuthEndpointGuard is false)
                    services.AddD2AuthEndpointGuard();
            }

            if (options.SkipLocalCacheAutoWiring is false)
                services.AddD2LocalCache(options.LocalCacheConfigure);

            services.AddD2HealthChecks();
            services.AddD2ProblemDetails(options.ProblemDetailsConfigure);
            services.AddD2Cors(configuration, options.CorsConfigure);

            if (options.SkipAuthAutoWiring is false)
            {
                // The LOCKED pipeline (UseD2DefaultPipeline) calls
                // app.UseAuthentication() + app.UseAuthorization() in the
                // auth-wired path — those middleware require their
                // framework service registrations
                // (AuthenticationCoreService + IAuthorizationService).
                // Register them here so the pipeline always finds what
                // it needs; both are idempotent at the framework level.
                services.AddAuthentication();
                services.AddAuthorization();
            }

            if (options.SkipHttpClientResilienceDefaults is false)
            {
                services.ConfigureHttpClientDefaults(http =>
                    http.AddStandardResilienceHandler());
            }

            return services;
        }
    }
}
