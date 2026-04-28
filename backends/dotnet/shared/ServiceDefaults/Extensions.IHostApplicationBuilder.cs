// -----------------------------------------------------------------------
// <copyright file="Extensions.IHostApplicationBuilder.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.ServiceDefaults;

using D2.Shared.ServiceDefaults.Logging;
using D2.Shared.Utilities.Configuration;
using D2.Shared.Utilities.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

public static partial class Extensions
{
    /// <summary>
    /// Extension method for builders that implement <see cref="IHostApplicationBuilder"/>.
    /// </summary>
    ///
    /// <param name="builder">
    /// The application builder.
    /// </param>
    ///
    /// <typeparam name="TBuilder">
    /// The type of the application builder.
    /// </typeparam>
    extension<TBuilder>(TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        /// <summary>
        /// Adds structured logging using Serilog to the application.
        /// </summary>
        private void AddStructuredLogging()
        {
            var serviceName = builder.Configuration["OTEL_SERVICE_NAME"]
                              ?? builder.Environment.ApplicationName;
            var environment = builder.Environment.EnvironmentName;

            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override(
                    "Microsoft.Extensions.Http",
                    Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("System.Net.Http", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("D2", Serilog.Events.LogEventLevel.Debug)
                .Destructure.With<RedactDataDestructuringPolicy>()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("service_name", serviceName)
                .Enrich.WithProperty("environment", environment)
                .Enrich.WithMachineName()
                .WriteTo.Console(new CompactJsonFormatter());

            Log.Logger = loggerConfig.CreateLogger();

            // writeToProviders: true routes Serilog output to other registered
            // ILoggerProviders (i.e. the OTel OTLP log exporter), so logs reach
            // Alloy → Loki via the OTLP pipeline.
            // preserveStaticLogger: true keeps our manually-built Log.Logger above.
            builder.Services.AddSerilog(
                configureLogger: _ => { },
                preserveStaticLogger: true,
                writeToProviders: true);
        }

        /// <summary>
        /// Configures OpenTelemetry for tracing and metrics.
        /// </summary>
        private void ConfigureOpenTelemetry()
        {
            var otlpLogsUri = builder.Configuration["OTEL_EXPORTER_OTLP_LOGS_ENDPOINT"];

            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;

                if (otlpLogsUri.Truthy())
                {
                    logging.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpLogsUri!);
                        options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    });
                }
            });

            // Use the Container-assigned service name (OTEL_SERVICE_NAME) when available
            // so that traces, logs, and metrics use consistent names (d2-geo, d2-rest, etc.)
            // instead of the .NET project name (Geo.API, REST, etc.).
            var otelServiceName = builder.Configuration["OTEL_SERVICE_NAME"]
                                  ?? builder.Environment.ApplicationName;

            builder.Services.AddOpenTelemetry()
                .WithMetrics(metrics =>
                {
                    metrics.AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation()
                        .AddProcessInstrumentation()
                        .AddMeter("D2.Shared.Handler")
                        .AddPrometheusExporter();
                })
                .WithTracing(tracing =>
                {
                    tracing.AddSource(otelServiceName)
                        .AddSource(builder.Environment.ApplicationName)
                        .AddSource("D2.Shared.Handler")
                        .SetResourceBuilder(ResourceBuilder
                            .CreateDefault()
                            .AddService(otelServiceName))
                        .AddAspNetCoreInstrumentation(x =>
                        {
                            x.Filter = context =>
                                !context.Request.Path.StartsWithSegments(_HEALTH_ENDPOINT_PATH) &&
                                !context.Request.Path.StartsWithSegments(_ALIVE_ENDPOINT_PATH) &&
                                !context.Request.Path.StartsWithSegments(_METRICS_ENDPOINT_PATH);

                            x.RecordException = true;

                            x.EnrichWithHttpRequest = (activity, request) =>
                            {
                                activity.SetTag(
                                    "http.request_id",
                                    request.HttpContext.TraceIdentifier);
                            };

                            x.EnrichWithHttpResponse = (activity, response) =>
                            {
                                activity.SetTag("http.response.status_code", response.StatusCode);
                                activity.SetTag(
                                    "http.request_id",
                                    response.HttpContext.TraceIdentifier);
                            };
                        })
                        .AddGrpcClientInstrumentation()
                        .AddHttpClientInstrumentation(x =>
                        {
                            x.RecordException = true;
                            x.FilterHttpRequestMessage = message =>
                            {
                                // Get the request URI.
                                var requestUri = message.RequestUri?.AbsoluteUri ?? string.Empty;

                                // Ensure this is not a request to our traces' collection.
                                var tracesCollUri = builder.Configuration["OTEL_EXPORTER_OTLP_TRACES_ENDPOINT"];
                                if (tracesCollUri.Truthy() && IsOtlp(tracesCollUri!))
                                {
                                    return false;
                                }

                                // Ensure this is not a request to our OTLP logs collection.
                                if (otlpLogsUri.Truthy() && IsOtlp(otlpLogsUri!))
                                {
                                    return false;
                                }

                                // Otherwise, allow it.
                                return true;

                                // Local function to determine if the URI starts with the given
                                // OTLP endpoint.
                                bool IsOtlp(string otlp)
                                {
                                    return requestUri.StartsWith(
                                        otlp,
                                        StringComparison.OrdinalIgnoreCase);
                                }
                            };
                        });
                });

            builder.AddOpenTelemetryExporters();
        }

        /// <summary>
        /// Adds OpenTelemetry exporters based on configuration.
        /// Note: Log exporter is configured inline in <see cref="ConfigureOpenTelemetry"/>
        /// because the logging builder does not support deferred configuration.
        /// </summary>
        private void AddOpenTelemetryExporters()
        {
            var tracesCollUri = builder.Configuration["OTEL_EXPORTER_OTLP_TRACES_ENDPOINT"];

            if (tracesCollUri.Falsey())
            {
                return;
            }

            builder.Services.ConfigureOpenTelemetryTracerProvider(tracing =>
            {
                tracing.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(tracesCollUri!);
                    options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                });
            });
        }

        /// <summary>
        /// Adds default health checks to the application.
        /// </summary>
        private void AddDefaultHealthChecks()
        {
            builder.Services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        }

        /// <summary>
        /// Adds default services and configurations to the application builder.
        /// </summary>
        public void AddServiceDefaults()
        {
            D2Env.Load();
            builder.Configuration.AddEnvironmentVariables();

            builder.AddStructuredLogging();

            // OTel 1.15.0 throws NullReferenceException in OpenTelemetryMetricsListener
            // when OTEL_SDK_DISABLED=true (e.g., E2E tests). Skip registration entirely.
            if (!string.Equals(
                    Environment.GetEnvironmentVariable("OTEL_SDK_DISABLED"),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                builder.ConfigureOpenTelemetry();
            }

            builder.AddDefaultHealthChecks();

            builder.Services.ConfigureHttpClientDefaults(http =>
            {
                http.AddStandardResilienceHandler();
            });
        }
    }
}
