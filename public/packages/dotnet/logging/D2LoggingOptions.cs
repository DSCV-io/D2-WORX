// -----------------------------------------------------------------------
// <copyright file="D2LoggingOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Logging;

using Serilog.Events;

/// <summary>
/// Configuration for <see cref="LoggingServiceCollectionExtensions.AddD2Logging"/>.
/// Use the parameterless ctor for all defaults; use the parameterized ctor
/// (with positional or named args) when you want to override one or more
/// values without the noise of an object initializer. The <c>with</c>-expression
/// also works for record-style selective overrides.
/// </summary>
/// <remarks>
/// <para>
/// All properties have computed defaults — no required fields. Validation
/// runs at the first <c>IOptions&lt;D2LoggingOptions&gt;.Value</c> resolution
/// (typically host-startup composition) via <c>ValidateOnStart()</c> —
/// fail-fast on invalid config.
/// </para>
/// <para>
/// <see cref="ServiceName"/> and <see cref="Environment"/> default to values
/// resolved by <see cref="LoggingServiceCollectionExtensions.AddD2Logging"/>
/// from <c>OTEL_SERVICE_NAME</c> / <c>IHostEnvironment.ApplicationName</c> /
/// <c>IHostEnvironment.EnvironmentName</c> when this options instance is
/// constructed directly without overrides.
/// </para>
/// </remarks>
public sealed record D2LoggingOptions
{
    /// <summary>
    /// Default minimum log level. Internal because consumers don't need to
    /// reference this — they either pass an override or accept the default
    /// via the parameterless ctor.
    /// </summary>
    internal const LogEventLevel DEFAULT_MINIMUM_LEVEL = LogEventLevel.Information;

    /// <summary>
    /// Default infrastructure-path prefixes excluded from request-completion
    /// log emission at <see cref="LogEventLevel.Information"/> (logged at
    /// <see cref="LogEventLevel.Verbose"/> instead so they're filtered out by
    /// the default minimum-level gate). Internal — see
    /// <see cref="DEFAULT_MINIMUM_LEVEL"/>.
    /// </summary>
    internal static readonly IReadOnlyList<string> SR_DefaultInfrastructurePaths =
    [
        "/health",
        "/alive",
        "/metrics",
        "/.well-known",
    ];

    /// <summary>
    /// Initializes a new <see cref="D2LoggingOptions"/> with all documented
    /// defaults. Equivalent to the parameterized ctor invoked with all
    /// <c>null</c> arguments.
    /// </summary>
    public D2LoggingOptions()
        : this(null, null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="D2LoggingOptions"/>. Each parameter is
    /// nullable; passing <c>null</c> (or omitting the argument) yields the
    /// documented default for that property.
    /// </summary>
    /// <param name="serviceName">
    /// Override for <see cref="ServiceName"/>; <c>null</c> = filled in by
    /// <see cref="LoggingServiceCollectionExtensions.AddD2Logging"/> from
    /// <c>OTEL_SERVICE_NAME</c> config or <c>IHostEnvironment.ApplicationName</c>.
    /// </param>
    /// <param name="environment">
    /// Override for <see cref="Environment"/>; <c>null</c> = filled in by
    /// <see cref="LoggingServiceCollectionExtensions.AddD2Logging"/> from
    /// <c>IHostEnvironment.EnvironmentName</c>.
    /// </param>
    /// <param name="minimumLevel">
    /// Override for <see cref="MinimumLevel"/>; <c>null</c> = default
    /// <see cref="LogEventLevel.Information"/>.
    /// </param>
    /// <param name="infrastructurePaths">
    /// Override for <see cref="InfrastructurePaths"/>; <c>null</c> = default
    /// (<c>/health</c>, <c>/alive</c>, <c>/metrics</c>, <c>/.well-known</c>).
    /// </param>
    public D2LoggingOptions(
        string? serviceName,
        string? environment,
        LogEventLevel? minimumLevel,
        IReadOnlyList<string>? infrastructurePaths)
    {
        ServiceName = serviceName;
        Environment = environment;
        MinimumLevel = minimumLevel ?? DEFAULT_MINIMUM_LEVEL;
        InfrastructurePaths = infrastructurePaths ?? SR_DefaultInfrastructurePaths;
    }

    /// <summary>
    /// Gets or sets the service name emitted on every log line via the
    /// <c>service_name</c> structured property. Settable so the
    /// <c>AddD2Logging(Action&lt;D2LoggingOptions&gt;)</c> configure lambda
    /// can populate it after the options instance is constructed by the DI
    /// container. When null at composition time,
    /// <see cref="LoggingServiceCollectionExtensions.AddD2Logging"/> fills it
    /// from the <c>OTEL_SERVICE_NAME</c> config value, then from
    /// <c>IHostEnvironment.ApplicationName</c>. Validated non-empty /
    /// non-whitespace at startup.
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Gets or sets the environment name emitted on every log line via the
    /// <c>environment</c> structured property. Settable for the same reason
    /// as <see cref="ServiceName"/>. When null at composition time,
    /// <see cref="LoggingServiceCollectionExtensions.AddD2Logging"/> fills it
    /// from <c>IHostEnvironment.EnvironmentName</c>. Validated non-empty /
    /// non-whitespace at startup.
    /// </summary>
    public string? Environment { get; set; }

    /// <summary>
    /// Gets the minimum <see cref="LogEventLevel"/> emitted by the configured
    /// logger. Default <see cref="LogEventLevel.Information"/>. Per-source
    /// overrides (<c>Microsoft.AspNetCore</c>, <c>System.Net.Http</c>, etc.)
    /// are applied independently inside
    /// <see cref="LoggingServiceCollectionExtensions.AddD2Logging"/>.
    /// </summary>
    public LogEventLevel MinimumLevel { get; init; }

    /// <summary>
    /// Gets the path prefixes treated as infrastructure endpoints by
    /// <see cref="WebApplicationLoggingExtensions.UseD2RequestLogging"/> —
    /// requests whose <c>HttpContext.Request.Path</c> starts with any of
    /// these segments are logged at <see cref="LogEventLevel.Verbose"/>
    /// instead of <see cref="LogEventLevel.Information"/>, so the default
    /// minimum-level gate filters them out. Defaults to <c>/health</c>,
    /// <c>/alive</c>, <c>/metrics</c>, <c>/.well-known</c>. Validated
    /// non-empty (collection) and per-entry non-empty / non-whitespace at
    /// startup.
    /// </summary>
    public IReadOnlyList<string> InfrastructurePaths { get; init; }
}
