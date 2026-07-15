// -----------------------------------------------------------------------
// <copyright file="HandlerOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler.Abstractions;

using System;

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
/// vary by operation and live here as defense-in-depth.
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
    /// Gets the per-handler scope requirement evaluated by the handler
    /// pipeline before <c>ExecuteAsync</c> runs. The pipeline returns
    /// <c>D2Result.Forbidden</c> when the caller's <c>IRequestContext.Scopes</c>
    /// does not satisfy the requirement. <see langword="null"/> (the default)
    /// disables the per-handler check — any authenticated caller that passed
    /// the transport-layer auth middleware / interceptor may invoke the handler.
    /// </summary>
    /// <remarks>
    /// The <see cref="ScopeRequirement.Match"/> field controls whether the
    /// caller must hold any one of the declared scopes
    /// (<see cref="HandlerScopeMatch.Any"/>) or every declared scope
    /// (<see cref="HandlerScopeMatch.All"/>). An empty
    /// <see cref="ScopeRequirement.Scopes"/> set is rejected at construction
    /// time — the <see cref="ScopeRequirement"/> constructor throws
    /// <see cref="ArgumentException"/> if <c>Scopes</c> is empty. Pass a
    /// <see langword="null"/> <see cref="ScopeRequirement"/> to disable the
    /// per-handler check. The pipeline guard
    /// (<c>is { Scopes.Count: &gt; 0 }</c>) remains as defense-in-depth for a
    /// now-unreachable branch.
    /// </remarks>
    public ScopeRequirement? ScopeRequirement { get; init; }
}
