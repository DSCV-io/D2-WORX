// -----------------------------------------------------------------------
// <copyright file="SyntheticEndpoints.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.ServiceDefaults.Infrastructure;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using D2.Shared.Auth.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

/// <summary>
/// Synthetic endpoint set installed by
/// <see cref="CompositeTestHostBuilder"/> via the test-server's
/// <see cref="IEndpointRouteBuilder"/>. Single source of truth for the
/// endpoints exercised by the composed-pipeline E2E tests:
/// <list type="bullet">
///  <item><c>GET /probe</c> — body <c>"ok"</c>; the canonical 200
///   business endpoint (mapped by the underlying
///   <see cref="ServiceDefaultsTestHostBuilder"/>).</item>
///  <item><c>GET /echo</c> — echoes the <c>?msg=</c> query value as the
///   response body.</item>
///  <item><c>GET /throw</c> — throws
///   <see cref="InvalidOperationException"/> to drive the
///   ProblemDetails / unhandled-exception path.</item>
///  <item><c>GET /emit-activity</c> — starts an
///   <see cref="Activity"/> from the <c>D2.Shared.Auth</c> aggregated
///   <see cref="ActivitySource"/> so tests can verify aggregated sources
///   flow through the OTel SDK inside the composed pipeline.</item>
///  <item><c>GET /emit-counter</c> — increments a
///   <see cref="Counter{T}"/> on a <see cref="Meter"/> whose name matches
///   the <c>D2.Shared.Auth</c> aggregated meter.</item>
///  <item><c>GET /log-redacted</c> — captures + logs a
///   <see cref="RedactedTestObject"/> via the static
///   <see cref="Serilog.Log"/> facade (so the
///   <see cref="D2.Shared.Logging.Destructuring.RedactDataDestructuringPolicy"/>
///   safety-net runs against the destructured payload).</item>
///  <item><c>GET /log-mel-info</c> — emits an
///   <see cref="LogLevel.Information"/> log line via the low-level
///   <c>ILogger.Log</c> overload (sidestepping <c>CA1848</c>) so the
///   MEL → OTel log-record bridge can be observed.</item>
///  <item><c>GET /order-marker-A</c> / <c>/order-marker-B</c> /
///   <c>/order-marker-C</c> — record their label on the per-host
///   <see cref="MiddlewareOrderRecorder"/> and return <c>"ok"</c>; used
///   by the middleware-ordering tests to capture pipeline-execution
///   sequence.</item>
/// </list>
/// </summary>
internal static class SyntheticEndpoints
{
    /// <param name="endpoints">The endpoint route builder.</param>
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Registers every synthetic endpoint described on
        /// <see cref="SyntheticEndpoints"/>. Idempotent at the route-tree
        /// level — calling twice raises
        /// <see cref="InvalidOperationException"/> from the routing layer
        /// (callers SHOULD invoke exactly once per pipeline build).
        /// </summary>
        /// <returns>
        /// The same <paramref name="endpoints"/> for fluent chaining.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="endpoints"/> is null.
        /// </exception>
        public IEndpointRouteBuilder MapSyntheticEndpoints()
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            endpoints.MapGet("/echo", (HttpContext ctx) =>
            {
                var msg = ctx.Request.Query["msg"].ToString();
                return Results.Text(msg.Length == 0 ? "ok" : msg);
            });

            endpoints.MapGet("/throw", () =>
            {
                throw new InvalidOperationException(
                    "Synthetic /throw failure (do not log).");
            });

            endpoints.MapGet("/emit-activity", () =>
            {
                using var source = new ActivitySource(
                    AuthTelemetry.ACTIVITY_SOURCE_NAME);

                // Explicit name arg overrides [CallerMemberName] default.
                // ReSharper disable once ExplicitCallerInfoArgument
                using var activity = source.StartActivity("synthetic-emit-activity");
                activity?.SetTag("synthetic", "true");
                return Results.Text("ok");
            });

            endpoints.MapGet("/emit-counter", () =>
            {
                using var meter = new Meter(AuthTelemetry.METER_NAME);
                var counter = meter.CreateCounter<long>(
                    "synthetic_emit_counter_total");
                counter.Add(1);
                return Results.Text("ok");
            });

            endpoints.MapGet("/log-redacted", () =>
            {
                var fixture = new RedactedTestObject(
                    Email: "alice@example.com",
                    Phone: "+1-555-0100",
                    Address: "742 Evergreen Terrace");

                // Use the static Serilog facade so the per-host pinned
                // Log.Logger (which carries the
                // RedactDataDestructuringPolicy) is the one capturing —
                // same path as the production request-completion
                // middleware. The MEL ILogger pipeline bypasses Serilog
                // destructuring policies, so a redaction assertion via
                // ILogger would not exercise the actual safety-net.
                Log.Information(
                    SyntheticEndpointsConstants.SYNTHETIC_REDACTION_MARKER
                    + " {@Fixture}",
                    fixture);
                return Results.Text("ok");
            });

            endpoints.MapGet("/log-mel-info", (HttpContext ctx) =>
            {
                var loggerFactory = ctx.RequestServices
                    .GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger(
                    SyntheticEndpointsConstants.SYNTHETIC_LOGGER_CATEGORY);

                // Drive ILogger via the lowest-level Log overload to
                // sidestep CA1848 / CA1727 (per the LogsExporterTests
                // precedent — production code uses LoggerMessage
                // source-generated delegates; this exists to pin the
                // pipeline plumbing, not the log-call ergonomics).
                logger.Log(
                    logLevel: LogLevel.Information,
                    eventId: 0,
                    state: SyntheticEndpointsConstants.SYNTHETIC_MEL_BRIDGE_MARKER,
                    exception: null,
                    formatter: (s, _) => s);
                return Results.Text("ok");
            });

            foreach (var label in (string[])["A", "B", "C"])
            {
                var capturedLabel = label;
                endpoints.MapGet(
                    $"/order-marker-{capturedLabel}",
                    (HttpContext ctx) =>
                    {
                        var recorder = ctx.RequestServices
                            .GetRequiredService<MiddlewareOrderRecorder>();
                        recorder.Record($"endpoint-{capturedLabel}");
                        return Results.Text("ok");
                    });
            }

            return endpoints;
        }
    }
}
