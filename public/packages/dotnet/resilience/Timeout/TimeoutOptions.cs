// -----------------------------------------------------------------------
// <copyright file="TimeoutOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Resilience.Timeout;

/// <summary>
/// Configuration for <see cref="Pipeline.TimeoutLayer{TKey, TValue}"/>. Use the
/// parameterless ctor for the default 10-second timeout; use the parameterized ctor
/// when you want to override the duration. <see cref="TimeSpan.Zero"/> or any value
/// ≤ zero disables the timeout (pass-through — no <see cref="CancellationTokenSource"/>
/// is created, the operation's own deadline governs).
/// </summary>
/// <remarks>
/// This type is the single source of truth for timeout-layer defaults. The layer
/// defers to it via <c>options ??= new TimeoutOptions()</c>; no defaults are restated
/// in the layer itself.
/// </remarks>
public sealed record TimeoutOptions
{
    /// <summary>
    /// Default wall-clock timeout. Internal — consumers either pass an override or
    /// accept the default via the parameterless ctor.
    /// </summary>
    internal static readonly TimeSpan SR_DefaultDuration = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Initializes a new <see cref="TimeoutOptions"/> with the default 10-second timeout.
    /// Equivalent to the parameterized ctor invoked with <c>null</c>.
    /// </summary>
    public TimeoutOptions()
        : this(default(TimeSpan?))
    {
    }

    /// <summary>
    /// Initializes a new <see cref="TimeoutOptions"/>. Passing <c>null</c> (or
    /// omitting the argument) yields the documented 10-second default. An explicit
    /// non-null value (even <see cref="TimeSpan.Zero"/> or a negative value) is
    /// preserved as-is — zero/negative disables the timeout (pass-through).
    /// </summary>
    /// <param name="duration">
    /// Override for <see cref="Duration"/>; <c>null</c> = default 10 seconds.
    /// </param>
    public TimeoutOptions(TimeSpan? duration = null)
        => Duration = duration ?? SR_DefaultDuration;

    /// <summary>
    /// Gets the wall-clock timeout. <see cref="TimeSpan.Zero"/> or any value ≤ zero
    /// disables the timeout (the layer becomes a pass-through). Default: 10 seconds.
    /// </summary>
    public TimeSpan Duration { get; init; }
}
