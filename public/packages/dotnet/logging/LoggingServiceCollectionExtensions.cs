// -----------------------------------------------------------------------
// <copyright file="LoggingServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Logging;

using DcsvIo.D2.Logging.Destructuring;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

/// <summary>
/// DI registration entry point for <see cref="DcsvIo.D2.Logging"/> — wires
/// the Serilog pipeline (per-source minimum-level overrides, the
/// <see cref="RedactDataDestructuringPolicy"/> safety net for the
/// <c>[RedactData]</c> attribute, environment + service-name + machine-name
/// enrichers, and a <see cref="CompactJsonFormatter"/>-formatted console
/// sink) into the host's MEL pipeline so every <c>ILogger</c> resolved from
/// DI emits structured JSON to stdout.
/// </summary>
public static class LoggingServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the D² Serilog pipeline + its MEL bridge. Idempotent —
        /// safe to call multiple times (option configurations stack via the
        /// standard <c>IOptions</c> pipeline; the static <c>Log.Logger</c> is
        /// rebuilt on each call but inherits the same final option values).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Reads <see cref="D2LoggingOptions.ServiceName"/> default from the
        /// <c>OTEL_SERVICE_NAME</c> config value, then from
        /// <see cref="IHostEnvironment.ApplicationName"/>;
        /// <see cref="D2LoggingOptions.Environment"/> default from
        /// <see cref="IHostEnvironment.EnvironmentName"/>. Configure-callback
        /// values override the defaults.
        /// </para>
        /// <para>
        /// Validates options at the first
        /// <c>IOptions&lt;D2LoggingOptions&gt;.Value</c> resolution via
        /// <see cref="OptionsBuilderExtensions.ValidateOnStart"/> — invalid
        /// configuration fails the host build, never propagates as a
        /// runtime log.
        /// </para>
        /// </remarks>
        /// <param name="configuration">
        /// The host's <see cref="IConfiguration"/>, used to source the
        /// <c>OTEL_SERVICE_NAME</c> default for
        /// <see cref="D2LoggingOptions.ServiceName"/>.
        /// </param>
        /// <param name="configure">
        /// Optional configuration delegate applied AFTER the env-derived
        /// defaults so callers can override any field.
        /// </param>
        /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> or
        /// <paramref name="configuration"/> is null.
        /// </exception>
        public IServiceCollection AddD2Logging(
            IConfiguration configuration,
            Action<D2LoggingOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddOptions<D2LoggingOptions>()
                .Configure<IServiceProvider>((opts, sp) =>
                {
                    // Resolve env-derived defaults BEFORE applying the caller
                    // override, so the override always wins on conflict.
                    var env = sp.GetService<IHostEnvironment>();

                    if (opts.ServiceName.Falsey())
                    {
                        var fromConfig = configuration[
                            D2LoggingConstants.OTEL_SERVICE_NAME_CONFIG_KEY];
                        opts.ServiceName = fromConfig.Truthy()
                            ? fromConfig
                            : env?.ApplicationName;
                    }

                    if (opts.Environment.Falsey())
                        opts.Environment = env?.EnvironmentName;

                    configure?.Invoke(opts);
                })
                .Validate(
                    o => o.ServiceName.Truthy(),
                    "D2LoggingOptions.ServiceName must be set (via OTEL_SERVICE_NAME, "
                    + "IHostEnvironment.ApplicationName, or the configure callback).")
                .Validate(
                    o => o.Environment.Truthy(),
                    "D2LoggingOptions.Environment must be set (via "
                    + "IHostEnvironment.EnvironmentName or the configure callback).")
                .Validate(
                    o => o.InfrastructurePaths.Count > 0,
                    "D2LoggingOptions.InfrastructurePaths must contain at least one entry.")
                .Validate(
                    o => o.InfrastructurePaths.All(p => p.Truthy()),
                    "D2LoggingOptions.InfrastructurePaths entries must not be "
                    + "empty / whitespace.")
                .ValidateOnStart();

            // Build the Serilog logger eagerly using the resolved options. We
            // can't defer to first-resolution because Serilog's Log.Logger is
            // a static and AddSerilog wires the MEL bridge against whatever
            // logger is set at registration time.
            //
            // To pick up the env-derived defaults + the configure override,
            // we build a temporary ServiceProvider, materialize the options,
            // then dispose it. This is the same pattern .NET Aspire's
            // service-defaults uses for its own startup-time settings.
            var serviceName = ResolveServiceName(services, configuration, configure);
            var environment = ResolveEnvironment(services, configure);
            var minimumLevel = ResolveMinimumLevel(configure);

            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Is(minimumLevel)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Extensions.Http", LogEventLevel.Warning)
                .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
                .MinimumLevel.Override("D2", LogEventLevel.Debug)
                .Destructure.With<RedactDataDestructuringPolicy>()
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithProperty("service_name", serviceName ?? "unknown")
                .Enrich.WithProperty("environment", environment ?? "unknown")
                .WriteTo.Console(new CompactJsonFormatter());

            Log.Logger = loggerConfig.CreateLogger();

            // AddLogging seeds the MEL infrastructure (LoggerFactory + the
            // open-generic ILogger<T> registration). Idempotent — the Logging
            // package internally TryAdds, so callers who already have it
            // wired pay no penalty.
            services.AddLogging();

            // writeToProviders: true routes Serilog output to other registered
            // ILoggerProviders (e.g. an OTel OTLP log exporter, when one is
            // registered by separate observability infrastructure) so logs
            // reach an OTLP collector via the MEL pipeline.
            // preserveStaticLogger: true keeps the Log.Logger we just built so
            // callers using the static facade see consistent output.
            services.AddSerilog(
                configureLogger: _ => { },
                preserveStaticLogger: true,
                writeToProviders: true);

            return services;
        }
    }

    private static string? ResolveServiceName(
        IServiceCollection services,
        IConfiguration configuration,
        Action<D2LoggingOptions>? configure)
    {
        var probe = new D2LoggingOptions();
        configure?.Invoke(probe);

        if (probe.ServiceName.Truthy())
            return probe.ServiceName;

        var fromConfig = configuration[D2LoggingConstants.OTEL_SERVICE_NAME_CONFIG_KEY];
        if (fromConfig.Truthy())
            return fromConfig;

        return TryGetHostEnvironment(services)?.ApplicationName;
    }

    private static string? ResolveEnvironment(
        IServiceCollection services,
        Action<D2LoggingOptions>? configure)
    {
        var probe = new D2LoggingOptions();
        configure?.Invoke(probe);

        if (probe.Environment.Truthy())
            return probe.Environment;

        return TryGetHostEnvironment(services)?.EnvironmentName;
    }

    private static LogEventLevel ResolveMinimumLevel(Action<D2LoggingOptions>? configure)
    {
        var probe = new D2LoggingOptions();
        configure?.Invoke(probe);
        return probe.MinimumLevel;
    }

    private static IHostEnvironment? TryGetHostEnvironment(IServiceCollection services)
    {
        // Walk ServiceDescriptors directly — at AddD2Logging time the
        // ServiceProvider has not been built yet and we don't want to
        // materialize one (BuildServiceProvider inside an extension method is
        // the canonical "container leak" anti-pattern). The host registers
        // IHostEnvironment as a singleton-instance descriptor, so we can read
        // its .ImplementationInstance directly without provider construction.
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(IHostEnvironment)
                && descriptor.ImplementationInstance is IHostEnvironment env)
            {
                return env;
            }
        }

        return null;
    }
}
