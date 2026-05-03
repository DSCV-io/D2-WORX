// -----------------------------------------------------------------------
// <copyright file="CircuitState.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Resilience.CircuitBreaker;

/// <summary>
/// Represents the state of a <see cref="CircuitBreaker{T}"/>.
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Normal operation — calls pass through and failures are tracked.
    /// </summary>
    Closed = 0,

    /// <summary>
    /// Fast-fail — calls are rejected immediately while waiting out the
    /// cooldown.
    /// </summary>
    Open = 1,

    /// <summary>
    /// Probing — exactly one call is allowed through to test recovery.
    /// </summary>
    HalfOpen = 2,
}
