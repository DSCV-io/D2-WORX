// -----------------------------------------------------------------------
// <copyright file="InProcessModuleBoundary.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.Abstractions;

using System;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Time;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// The in-host establishment boundary: positively establishes the
/// in-process-module plane on the current scoped request-context before an in-host
/// leaf call (the generated <c>I&lt;Module&gt;Api</c> seam). Sets
/// <see cref="RequestOrigin.InProcessModule"/>, records the calling module id as the
/// telemetry-grade <see cref="IRequestContext.ImmediateCaller"/>, and appends a
/// <see cref="CallPathKind.ModuleHop"/> entry. The validated request-context is
/// passed directly — no serialization, no wire — so there is no spoofing surface;
/// the whole process is trusted.
/// </summary>
public static class InProcessModuleBoundary
{
    /// <param name="context">The current scoped request-context.</param>
    extension(IRequestContext context)
    {
        /// <summary>
        /// Establishes the in-process-module plane on the current scoped context before
        /// delegating to an in-host leaf: <see cref="RequestOrigin.InProcessModule"/>,
        /// <see cref="IRequestContext.ImmediateCaller"/> = the calling module id
        /// (telemetry-grade), and a <see cref="CallPathKind.ModuleHop"/> appended for
        /// <paramref name="targetModuleId"/>. A no-op-safe guard: only a mutable context
        /// is mutated, so a read-only test double is left untouched.
        /// </summary>
        /// <param name="callingModuleId">The id of the module/host making the
        /// in-process call.</param>
        /// <param name="targetModuleId">The id of the in-host module being entered
        /// (the hop's own identity).</param>
        /// <param name="clock">The clock used to timestamp the appended hop.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="context"/> or <paramref name="clock"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="callingModuleId"/> or <paramref name="targetModuleId"/>
        /// is null, empty, or whitespace.
        /// </exception>
        /// <remarks>
        /// <strong>No establishment log (intentional asymmetry)</strong>: unlike the
        /// sibling Edge / System / cross-process establishment boundaries, this
        /// extension's ONLY receiver is <see cref="IRequestContext"/> — there is no
        /// <see cref="IServiceProvider"/> or other DI handle flowing through it to
        /// resolve a logger from. Adding one would require a breaking signature change
        /// to this shipped <see langword="public"/> API (new required parameter breaks
        /// every existing call site; an unused optional one never fires). A caller that
        /// needs to observe an in-process-module hop can inspect the appended
        /// <see cref="CallPathKind.ModuleHop"/> entry on <see cref="IRequestContext.CallPath"/>
        /// instead.
        /// </remarks>
        public void EstablishInProcessModule(
            string callingModuleId, string targetModuleId, IClock clock)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(clock);
            callingModuleId.ThrowIfFalsey();
            targetModuleId.ThrowIfFalsey();

            if (context is not MutableRequestContext ctx)
                return;

            var now = clock.GetCurrentInstant().ToDateTimeOffset();

            ctx.Origin = RequestOrigin.InProcessModule;
            ctx.ImmediateCaller = callingModuleId;

            ctx.CallPath = CallPathOps.Append(
                ctx.CallPath, targetModuleId, CallPathKind.ModuleHop, now);
        }
    }
}
