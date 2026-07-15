// -----------------------------------------------------------------------
// <copyright file="LoggingTestHostBuilder.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Logging.Infrastructure;

using DcsvIo.D2.Logging;
using DcsvIo.D2.Logging.Destructuring;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

/// <summary>
/// Static helper that builds a fully wired AspNetCore test host suitable for
/// driving the Serilog request-logging middleware end-to-end through
/// <c>Microsoft.AspNetCore.TestHost</c>. Each call gets its own
/// <see cref="InMemorySink"/> so xUnit-parallel test classes don't see each
/// other's events; the host's local Serilog logger writes to the sink only
/// (no Console sink, no <c>Log.Logger</c> mutation) so static-Logger races
/// can't pollute these tests.
/// </summary>
internal static class LoggingTestHostBuilder
{
    /// <summary>
    /// Builds and starts an in-process AspNetCore host with:
    /// <list type="bullet">
    ///  <item>A local Serilog logger writing to a fresh
    ///   <see cref="InMemorySink"/> (returned in the result tuple), with the
    ///   <see cref="RedactDataDestructuringPolicy"/> wired so redaction is
    ///   enforced end-to-end.</item>
    ///  <item><c>UseRouting</c> + <c>UseD2RequestLogging</c> +
    ///   <c>UseEndpoints</c> in the canonical middleware order.</item>
    ///  <item>Default endpoints: <c>GET /api/echo</c> (returns "ok"),
    ///   <c>GET /health</c> + <c>GET /alive</c> + <c>GET /metrics</c> +
    ///   <c>GET /.well-known/openid-configuration</c> (each returns "ok"
    ///   so test paths exercise the infrastructure-suppression contract).
    ///  </item>
    ///  <item>An optional
    ///   <c>GET /api/leak/{template}</c> endpoint that captures-and-logs
    ///   a fixture instance using the supplied capture template (drives the
    ///   redaction integration tests).</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Tests that need to log redacted values resolve
    /// <see cref="ILogger{TCategoryName}"/> from
    /// <see cref="IHost.Services"/> — never via static <c>Log.*</c> — so the
    /// host's local logger captures the events. Logging via
    /// <c>Log.Logger</c> would route to whatever the previous test left
    /// pinned on the global static, NOT to the sink.
    /// </remarks>
    /// <param name="extraServices">
    /// Optional services-collection mutator invoked after the standard
    /// registrations — supports per-test overrides such as registering a
    /// scoped <c>IRequestContext</c>.
    /// </param>
    /// <param name="extraConfigure">
    /// Optional middleware-pipeline mutator invoked AFTER
    /// <c>UseD2RequestLogging</c> and BEFORE <c>UseEndpoints</c> — supports
    /// per-test middleware injection (e.g. a synthetic enricher that sets
    /// HttpContext.Items before the request-logging middleware records).
    /// </param>
    /// <param name="extraEndpoints">
    /// Optional endpoint-registration mutator invoked when the default
    /// endpoints are mapped — supports per-test custom endpoints
    /// (e.g. a leak endpoint that logs a fixture).
    /// </param>
    /// <returns>
    /// Tuple of the started <see cref="IHost"/> (caller disposes) and the
    /// freshly constructed <see cref="InMemorySink"/> the test asserts
    /// against. Tuple instead of <c>out</c> because <c>async</c> methods
    /// can't carry <c>out</c> parameters.
    /// </returns>
    internal static async Task<(IHost Host, InMemorySink Sink)> BuildAsync(
        Action<IServiceCollection>? extraServices = null,
        Action<IApplicationBuilder>? extraConfigure = null,
        Action<Microsoft.AspNetCore.Routing.IEndpointRouteBuilder>? extraEndpoints = null)
    {
        var sink = new InMemorySink();
        var localSink = sink;

        // The Serilog request-logging middleware writes via Log.Logger (the
        // static facade) — see RequestLoggingMiddleware in Serilog.AspNetCore.
        // To capture its events into our per-test sink, we MUST point
        // Log.Logger at our local logger for the duration of this host. The
        // [Collection("LogLoggerStaticState")] annotation on the integration
        // test classes serializes against any other test that touches the
        // same static.
        var localLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Destructure.With<RedactDataDestructuringPolicy>()
            .Enrich.FromLogContext()
            .WriteTo.Sink(localSink, restrictedToMinimumLevel: LogEventLevel.Verbose)
            .CreateLogger();
        Log.Logger = localLogger;

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();

                        // AddD2Logging registers IDiagnosticContext +
                        // AddOptions<D2LoggingOptions> + ValidateOnStart, then
                        // overwrites Log.Logger with its own configuration.
                        // We pin Log.Logger AFTER the call below so our
                        // localLogger captures the request-completion events.
                        services.AddD2Logging(
                            new ConfigurationBuilder().Build(),
                            opts =>
                            {
                                opts.ServiceName = "logging-tests";
                                opts.Environment = "Test";
                            });

                        // Re-pin Log.Logger to the per-test local logger so
                        // the Serilog request-logging middleware (which
                        // writes via the static facade) captures into our
                        // sink. This MUST happen after AddD2Logging
                        // (which sets Log.Logger to its own configuration).
                        Log.Logger = localLogger;

                        extraServices?.Invoke(services);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseD2RequestLogging();

                        extraConfigure?.Invoke(app);

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/api/echo", () => Results.Text("ok"));
                            endpoints.MapGet("/health", () => Results.Text("ok"));
                            endpoints.MapGet("/alive", () => Results.Text("ok"));
                            endpoints.MapGet("/metrics", () => Results.Text("ok"));
                            endpoints.MapGet(
                                "/.well-known/openid-configuration",
                                () => Results.Text("ok"));

                            extraEndpoints?.Invoke(endpoints);
                        });
                    });
            });

        var host = await hostBuilder.StartAsync();
        return (host, sink);
    }
}
