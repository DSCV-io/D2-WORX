// -----------------------------------------------------------------------
// <copyright file="WebApplicationLoggingExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Logging;

using DcsvIo.D2.AspNetCore;
using DcsvIo.D2.Logging.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.AspNetCore;
using Serilog.Events;

/// <summary>
/// ASP.NET Core middleware extensions for <see cref="DcsvIo.D2.Logging"/> —
/// installs the Serilog request-completion log middleware with infrastructure
/// path suppression, conservative diagnostic-context enrichment, and
/// <see cref="Microsoft.AspNetCore.Http.HttpContext"/>-derived
/// <see cref="DcsvIo.D2.Context.Abstractions.IRequestContext"/> projection
/// onto the log line when registered.
/// </summary>
public static class WebApplicationLoggingExtensions
{
    /// <param name="app">The ASP.NET Core application builder.</param>
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Installs the Serilog request-completion log middleware. Each
        /// request emits one structured log event at request end carrying
        /// status code, elapsed milliseconds, request path, request method,
        /// and a curated set of <see cref="HttpRequest"/>-derived properties
        /// (<c>RequestScheme</c>, <c>UserAgent</c>, <c>TraceId</c>,
        /// <c>RequestHost</c>) plus the
        /// <see cref="DcsvIo.D2.Context.Abstractions.IRequestContext"/>
        /// projection (when registered).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Requests whose path matches any prefix in
        /// <see cref="D2LoggingOptions.InfrastructurePaths"/> are emitted at
        /// <see cref="LogEventLevel.Verbose"/> instead of
        /// <see cref="LogEventLevel.Information"/>, so the default
        /// minimum-level gate filters them out — keeps health probes,
        /// liveness probes, and metrics scrapes from drowning operators in
        /// log noise.
        /// </para>
        /// <para>
        /// Network/IP enrichment is deliberately conservative — the middleware
        /// does NOT log the request's connection-remote IP address (at internal
        /// services it's the upstream Edge IP, not the user's; at Edge it's
        /// PII). Geo / network-privacy / ASN fields are sourced from the
        /// spec-driven
        /// <see cref="DcsvIo.D2.Context.Abstractions.IRequestContext"/> via
        /// <see cref="D2RequestContextEnricher"/> — see the README's
        /// "Network/IP enrichment design" section.
        /// </para>
        /// <para>
        /// Callers can extend the diagnostic context with additional fields
        /// via the <paramref name="configure"/> callback (e.g.
        /// <c>opts.EnrichDiagnosticContext += (diag, ctx) =&gt; diag.Set("X", x);</c>).
        /// Caller-added structured fields BYPASS the
        /// <c>[RedactData]</c> destructuring policy (the diagnostic-context
        /// path emits <c>ScalarValue</c>s directly), so callers MUST own PII
        /// discipline for anything they add.
        /// </para>
        /// </remarks>
        /// <param name="configure">
        /// Optional <see cref="RequestLoggingOptions"/> customizer. Runs
        /// AFTER the D² defaults have been applied, so callers can append
        /// additional enrichers, override the level callback for specific
        /// paths, or supply a custom message template.
        /// </param>
        /// <returns>The same <paramref name="app"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="app"/> is null.
        /// </exception>
        public IApplicationBuilder UseD2RequestLogging(
            Action<RequestLoggingOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(app);

            // Snapshot the configured infrastructure paths — the level
            // callback runs on every request and we don't want to re-resolve
            // IOptions on the hot path. Captured in a closure for the
            // callbacks below.
            var loggingOptions = app.ApplicationServices
                .GetRequiredService<IOptions<D2LoggingOptions>>()
                .Value;
            var infraPaths = loggingOptions.InfrastructurePaths;

            return app.UseSerilogRequestLogging(opts =>
            {
                opts.GetLevel = (ctx, _, _) =>
                    InfrastructurePathMatcher.IsInfrastructurePath(
                        ctx.Request.Path,
                        infraPaths)
                        ? LogEventLevel.Verbose
                        : LogEventLevel.Information;

                opts.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                    diagnosticContext.Set(
                        "UserAgent",
                        httpContext.Request.Headers.UserAgent.ToString());
                    diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);

                    if (httpContext.Request.Host.Value is not null)
                        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);

                    D2RequestContextEnricher.Enrich(diagnosticContext, httpContext);
                };

                configure?.Invoke(opts);
            });
        }
    }
}
