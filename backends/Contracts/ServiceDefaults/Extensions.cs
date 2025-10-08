using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Grafana.Loki;

namespace D2.Contracts.ServiceDefaults;

public static class Extensions
{
    private const string _HEALTH_ENDPOINT_PATH = "/health";
    private const string _ALIVE_ENDPOINT_PATH = "/alive";
    private const string _METRICS_ENDPOINT_PATH = "/metrics";

    public static void AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.AddStructuredLogging();
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });
    }

    public static void UseStructuredRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (ctx, _, _) =>
            {
                // Exclude infrastructure endpoints from logs
                if (ctx.Request.Path.StartsWithSegments(_HEALTH_ENDPOINT_PATH) ||
                    ctx.Request.Path.StartsWithSegments(_ALIVE_ENDPOINT_PATH) ||
                    ctx.Request.Path.StartsWithSegments(_METRICS_ENDPOINT_PATH))
                {
                    // This will log at Verbose level, which is typically not recorded.
                    return Serilog.Events.LogEventLevel.Verbose;
                }
                return Serilog.Events.LogEventLevel.Information;
            };

            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);

                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());

                diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);

                if (httpContext.Request.Host.Value is not null)
                    diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);

                if (httpContext.Connection.RemoteIpAddress is not null)
                    diagnosticContext.Set("RemoteIp", httpContext.Connection.RemoteIpAddress.ToString());
            };
        });
    }

    public static void MapDefaultEndpoints(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return;

        app.MapHealthChecks(_HEALTH_ENDPOINT_PATH);

        app.MapHealthChecks(_ALIVE_ENDPOINT_PATH, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });
    }

    public static void MapPrometheusEndpointWithIpRestriction(this WebApplication app)
    {
        app.MapPrometheusScrapingEndpoint()
            .AddEndpointFilter(async (context, next) =>
            {
                var httpContext = context.HttpContext;
                var remoteIp = httpContext.Connection.RemoteIpAddress;

                if (IsAllowedIpForMetrics(remoteIp))
                    return await next(context);

                httpContext.Response.StatusCode = 403;
                return Results.Text("Forbidden");

            });
    }

    private static bool IsAllowedIpForMetrics(IPAddress? remoteIp)
    {
        if (remoteIp == null) return false;

        // Check localhost
        if (IPAddress.IsLoopback(remoteIp))
            return true;

        // Check Docker networks (172.17-20.0.0/16)
        var bytes = remoteIp.GetAddressBytes();
        if (bytes.Length == 4 &&
            bytes[0] == 172 &&
            bytes[1] >= 17 &&
            bytes[1] <= 20)
        {
            return true;
        }

        return false;
    }

    private static void AddStructuredLogging<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var logsEndpoint = builder.Configuration["LOGS_URI"];
        var serviceName = builder.Environment.ApplicationName;
        var environment = builder.Environment.EnvironmentName;

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Extensions.Http",
                Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service_name", serviceName)
            .Enrich.WithProperty("environment", environment)
            .Enrich.WithMachineName()
            .WriteTo.Console(new CompactJsonFormatter());

        if (!string.IsNullOrWhiteSpace(logsEndpoint))
        {
            var lokiLabels = new List<LokiLabel>
            {
                new() { Key = "app", Value = serviceName },
                new() { Key = "environment", Value = environment }
            };

            loggerConfig.WriteTo.GrafanaLoki(
                logsEndpoint,
                labels: lokiLabels,
                textFormatter: new CompactJsonFormatter(),
                batchPostingLimit: 1000,
                period: TimeSpan.FromSeconds(2)
            );
        }

        Log.Logger = loggerConfig.CreateLogger();

        builder.Services.AddSerilog();
    }

    private static void ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddPrometheusExporter();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .SetResourceBuilder(ResourceBuilder
                        .CreateDefault()
                        .AddService(builder.Environment.ApplicationName))
                    .AddAspNetCoreInstrumentation(x =>
                    {
                        x.Filter = context =>
                            !context.Request.Path.StartsWithSegments(_HEALTH_ENDPOINT_PATH) &&
                            !context.Request.Path.StartsWithSegments(_ALIVE_ENDPOINT_PATH) &&
                            !context.Request.Path.StartsWithSegments(_METRICS_ENDPOINT_PATH);

                        x.RecordException = true;

                        x.EnrichWithHttpRequest = (activity, request) =>
                        {
                            activity.SetTag("http.request_id", request.HttpContext.TraceIdentifier);
                        };

                        x.EnrichWithHttpResponse = (activity, response) =>
                        {
                            activity.SetTag("http.response.status_code", response.StatusCode);
                        };
                    })
                    .AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation(x =>
                    {
                        x.RecordException = true;
                        x.FilterHttpRequestMessage = (message =>
                        {
                            // Get the request URI.
                            var requestUri = message.RequestUri?.AbsoluteUri ?? string.Empty;

                            // Ensure this is not a request to logs collection.
                            var logsCollUri = builder.Configuration["LOGS_URI"];
                            if (logsCollUri is not null && IsOtlp(logsCollUri))
                                return false;

                            // Ensure this is not a request to our traces collection.
                            var tracesCollUri = builder.Configuration["TRACES_URI"];
                            if (tracesCollUri is not null && IsOtlp(tracesCollUri))
                                return false;

                            // Otherwise, allow it.
                            return true;

                            // Local function to determine if the URI starts with the given
                            // OTLP endpoint.
                            bool IsOtlp (string otlp)
                            {
                                return requestUri.StartsWith(otlp, StringComparison.OrdinalIgnoreCase);
                            }
                        });
                    });
            });

        builder.AddOpenTelemetryExporters();
    }

    private static void AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var tracesCollUri = builder.Configuration["TRACES_URI"];

        if (string.IsNullOrWhiteSpace(tracesCollUri)) return;

        builder.Services.ConfigureOpenTelemetryTracerProvider(tracing =>
        {
            tracing.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(tracesCollUri);
                options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
            });
        });
    }

    private static void AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
    }
}
