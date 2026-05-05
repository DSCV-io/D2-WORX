// -----------------------------------------------------------------------
// <copyright file="HandlerOptions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Handler.Abstractions;

using System;
using System.Collections.Generic;

/// <summary>
/// Per-call handler options — observability toggles, slow/critical time
/// thresholds, and per-handler scope requirements. Resolved at
/// <c>HandleAsync</c> entry: per-call <c>options</c> argument first, then
/// the handler's <c>DefaultOptions</c> override, then platform defaults.
/// </summary>
/// <remarks>
/// JWT signature / expiry / audience / fingerprint-binding validation are
/// transport-level concerns handled by auth middleware (HTTP / gRPC / AMQP)
/// BEFORE the handler runs — not per-handler. Per-handler scope requirements
/// vary by operation and live here.
/// </remarks>
public sealed record HandlerOptions
{
    /// <summary>
    /// Gets a value indicating whether to log the handler's input. Default
    /// true. Set false on handlers whose inputs carry PII that can't be
    /// expressed via <c>[RedactData]</c> (e.g., proto-generated DTOs).
    /// </summary>
    public bool LogInput { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether to log the handler's output. Default
    /// true. Set false on handlers whose output is always large / sensitive.
    /// </summary>
    public bool LogOutput { get; init; } = true;

    /// <summary>
    /// Gets the duration above which the handler logs a "slow" warning.
    /// Default 100ms so handlers that quietly drift into slowness surface
    /// in logs even when the author forgot to set a threshold. Set to
    /// <c>null</c> to disable, or override with a higher value on handlers
    /// explicitly designed to take longer (long-running queries, batch jobs,
    /// external API calls).
    /// </summary>
    public TimeSpan? SlowThreshold { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets the duration above which the handler logs a "critical-slow"
    /// error. Default 500ms — same rationale as <see cref="SlowThreshold"/>.
    /// Set to <c>null</c> to disable, or override with a higher value on
    /// long-running handlers.
    /// </summary>
    public TimeSpan? CriticalThreshold { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Gets the scopes the caller must have to invoke this handler. The
    /// handler returns <c>D2Result.Forbidden</c> at entry if any required
    /// scope is missing. Null / empty disables the check (the handler is
    /// responsible for its own scope assertions).
    /// </summary>
    public IReadOnlySet<string>? RequiredScopes { get; init; }
}
