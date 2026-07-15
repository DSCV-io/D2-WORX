// -----------------------------------------------------------------------
// <copyright file="SystemRequestContextBootstrap.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.Abstractions;

using System;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Time;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// The in-host system-worker establishment boundary: establishes a fresh
/// <see cref="RequestOrigin.System"/> context on a worker scope's
/// <see cref="MutableRequestContext"/>, for a background service with no inbound user
/// request. Identity is the host's own service id; the call-path STARTS with a single
/// <see cref="CallPathKind.System"/> entry. A System context is least-privilege —
/// it carries the host identity for audit/telemetry but grants no signing authority.
/// </summary>
public static class SystemRequestContextBootstrap
{
    /// <param name="scopedServices">The worker's per-iteration DI scope.</param>
    extension(IServiceProvider scopedServices)
    {
        /// <summary>
        /// Establishes <see cref="RequestOrigin.System"/> + the host's own identity + a
        /// fresh single-entry System call-path on the scope's
        /// <see cref="MutableRequestContext"/>. Requires the scope to register a
        /// <see cref="MutableRequestContext"/> (the worker resolves handlers into a scope
        /// that registers + populates it). Also logs the established call-path's entry
        /// count at Debug level (symmetric with the cross-process interceptor's
        /// establishment log) when the scope has a logging container registered — a
        /// scope built without one (e.g. a minimal test <see cref="IServiceProvider"/>)
        /// establishes the plane silently rather than throwing.
        /// </summary>
        /// <param name="hostServiceId">The host's own workload service id.</param>
        /// <param name="clock">The clock used to timestamp the System call-path entry.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="scopedServices"/> or <paramref name="clock"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="hostServiceId"/> is null, empty, or whitespace.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the scope does not register a <see cref="MutableRequestContext"/>.
        /// </exception>
        public void EstablishSystemContext(string hostServiceId, IClock clock)
        {
            ArgumentNullException.ThrowIfNull(scopedServices);
            ArgumentNullException.ThrowIfNull(clock);
            hostServiceId.ThrowIfFalsey();

            var ctx = scopedServices.GetRequiredService<MutableRequestContext>();
            var now = clock.GetCurrentInstant().ToDateTimeOffset();

            ctx.Origin = RequestOrigin.System;
            ctx.ImmediateCaller = hostServiceId;
            ctx.CallPath = CallPathOps.Append(null, hostServiceId, CallPathKind.System, now);

            // Best-effort: a scope without a registered logging container (e.g. a
            // minimal test IServiceProvider) still establishes the plane; only the
            // trace is skipped. A string-category logger is used (not ILogger<T>)
            // because this container is a static class — the category matches what
            // ILogger<SystemRequestContextBootstrap> would emit, staying symmetric
            // with the cross-process interceptor's establishment log.
            var logger = scopedServices.GetService<ILoggerFactory>()?
                .CreateLogger("DcsvIo.D2.Context.Abstractions.SystemRequestContextBootstrap");
            logger?.SystemContextEstablished(ctx.CallPath.Count, hostServiceId);
        }
    }
}
