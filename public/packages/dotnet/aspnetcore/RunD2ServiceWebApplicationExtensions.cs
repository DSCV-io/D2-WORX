// -----------------------------------------------------------------------
// <copyright file="RunD2ServiceWebApplicationExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore;

using DcsvIo.D2.Utilities.Diagnostics;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.AspNetCore.Builder;
using Serilog;

/// <summary>
/// Startup wrapper for D² services — installs Serilog
/// <c>Log.Information</c> "Starting" + <c>Log.Fatal</c>-on-startup-fault +
/// <c>Log.CloseAndFlushAsync</c>-on-finally around
/// <see cref="WebApplication.RunAsync(string?)"/>.
/// </summary>
public static class RunD2ServiceWebApplicationExtensions
{
    /// <param name="app">The configured ASP.NET Core web application.</param>
    extension(WebApplication app)
    {
        /// <summary>
        /// Starts the host via <see cref="WebApplication.RunAsync(string?)"/>
        /// inside a try / catch / finally that:
        /// emits a <c>Log.Information</c> "Starting {ServiceName}
        /// ({EnvironmentName})" line at entry;
        /// catches every exception, logs <c>Log.Fatal</c> with PII-safe
        /// exception rendering (type FullName + first stack frame only —
        /// never <c>ex.Message</c>, since exception messages can carry
        /// arbitrary content interpolated from runtime data including
        /// connection strings, user input, and configured secrets), and
        /// re-throws so the host exit code reflects failure;
        /// awaits <c>Log.CloseAndFlushAsync</c> in <c>finally</c> to drain
        /// Serilog's buffered batch sink before process exit.
        /// </summary>
        /// <remarks>
        /// <para>
        /// PII discipline: this wrapper deliberately does NOT pass
        /// <c>ex.Message</c> to the structured-log line. Exception messages
        /// at host startup can carry connection strings, configured
        /// secrets, and host-environment specifics that must not flow
        /// through the log pipeline as a single property value where
        /// downstream sinks would render them verbatim. The PII-safe
        /// rendering captures the exception type FullName + the first
        /// stack frame's method + file:line — sufficient for operators to
        /// triage the failure class via dashboards + a deeper stack-trace
        /// lookup in the host's process logs.
        /// </para>
        /// <para>
        /// The wrapper is <c>async</c> so it captures both
        /// synchronously-faulted (rare; typically host build / hosted
        /// service <c>StartAsync</c> failure) AND asynchronously-faulted
        /// (post-startup, mid-request) exceptions. Await on the catch path
        /// re-throws the original exception preserving the stack.
        /// </para>
        /// </remarks>
        /// <param name="serviceName">
        /// Optional service name for the "Starting" log line. When null /
        /// empty / whitespace, falls back to
        /// <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.ApplicationName"/>.
        /// </param>
        /// <returns>A task that completes when the host shuts down.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="app"/> is null.
        /// </exception>
        public async Task RunD2ServiceAsync(string? serviceName = null)
        {
            ArgumentNullException.ThrowIfNull(app);

            var name = serviceName.ToNullIfEmpty() ?? app.Environment.ApplicationName;

            try
            {
                Log.Information(
                    "Starting {ServiceName} ({EnvironmentName})",
                    name,
                    app.Environment.EnvironmentName);
                await app.RunAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(
                    "Host terminated unexpectedly: {ServiceName} {ExceptionType} {FirstFrame}",
                    name,
                    SanitizedExceptionRender.TypeName(ex),
                    SanitizedExceptionRender.FirstFrame(ex));
                throw;
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }
    }
}
