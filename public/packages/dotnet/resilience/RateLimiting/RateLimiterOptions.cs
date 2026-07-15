// -----------------------------------------------------------------------
// <copyright file="RateLimiterOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Resilience.RateLimiting;

/// <summary>
/// Configuration for <see cref="RateLimiter"/> and
/// <see cref="Pipeline.RateLimiterLayer{TKey, TValue}"/>. Use the parameterless ctor
/// for all defaults; use the parameterized ctor when overriding one or more values.
/// </summary>
/// <remarks>
/// <para>
/// This type is the single source of truth for rate-limiter defaults. Both the
/// primitive and the layer defer to it via <c>options ??= new RateLimiterOptions()</c>.
/// </para>
/// <para>
/// <b>Client-side, in-process limiter only.</b> This configures the
/// <see cref="Pipeline.RateLimiterLayer{TKey, TValue}"/>'s concurrency gate —
/// a caller-side admission-control mechanism that limits the number of concurrent
/// outbound calls. It is NOT the server-side, distributed per-tier rate-limit
/// middleware (which uses <c>IDistributedCache</c> counters).
/// </para>
/// </remarks>
public sealed record RateLimiterOptions
{
    /// <summary>
    /// Default maximum concurrent in-flight operations. Internal — consumers either
    /// pass an override or accept the default via the parameterless ctor.
    /// </summary>
    internal const int DEFAULT_MAX_CONCURRENCY = 100;

    /// <summary>
    /// Default acquisition timeout: zero means reject-fast (don't wait for a permit).
    /// Internal — see <see cref="DEFAULT_MAX_CONCURRENCY"/>.
    /// </summary>
    internal static readonly TimeSpan SR_DefaultAcquisitionTimeout = TimeSpan.Zero;

    /// <summary>
    /// Initializes a new <see cref="RateLimiterOptions"/> with all documented defaults
    /// (MaxConcurrency = 100, AcquisitionTimeout = Zero / reject-fast). Equivalent to
    /// the parameterized ctor invoked with all <c>null</c> arguments.
    /// </summary>
    public RateLimiterOptions()
        : this(null, null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="RateLimiterOptions"/>. Each parameter is nullable;
    /// passing <c>null</c> (or omitting the argument) yields the documented default for
    /// that property. Explicit non-null values (even <see cref="TimeSpan.Zero"/> for
    /// <paramref name="acquisitionTimeout"/>) are preserved as-is.
    /// </summary>
    /// <param name="maxConcurrency">
    /// Override for <see cref="MaxConcurrency"/>; <c>null</c> = default 100. Must be ≥ 1
    /// when supplied — a zero-permit limiter (one that admits nobody) is a misconfiguration;
    /// use a circuit breaker or a feature flag to fully close a path instead.
    /// </param>
    /// <param name="acquisitionTimeout">
    /// Override for <see cref="AcquisitionTimeout"/>; <c>null</c> = default
    /// <see cref="TimeSpan.Zero"/> (reject-fast).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxConcurrency"/> is supplied and is less than 1.
    /// </exception>
    public RateLimiterOptions(int? maxConcurrency = null, TimeSpan? acquisitionTimeout = null)
    {
        var max = maxConcurrency ?? DEFAULT_MAX_CONCURRENCY;
        if (max < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxConcurrency),
                max,
                "MaxConcurrency must be at least 1. A zero-permit limiter admits nobody and is a " +
                "misconfiguration — use a circuit breaker or a feature flag to fully close a path.");
        }

        MaxConcurrency = max;
        AcquisitionTimeout = acquisitionTimeout ?? SR_DefaultAcquisitionTimeout;
    }

    /// <summary>
    /// Gets the maximum number of concurrent in-flight operations. Must be ≥ 1. Default: 100.
    /// </summary>
    public int MaxConcurrency { get; init; }

    /// <summary>
    /// Gets how long to wait for a permit before rejecting the caller.
    /// <see cref="TimeSpan.Zero"/> = reject-fast (no waiting). Default: <see cref="TimeSpan.Zero"/>.
    /// </summary>
    public TimeSpan AcquisitionTimeout { get; init; }
}
