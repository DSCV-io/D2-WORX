// -----------------------------------------------------------------------
// <copyright file="CircuitBreakerOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Resilience.CircuitBreaker;

/// <summary>
/// Configuration for <see cref="CircuitBreaker{T}"/>. Use the parameterless
/// ctor for all defaults; use the parameterized ctor (with positional args
/// or named args) when you want to override one or more values without the
/// noise of an object initializer. The <c>with</c>-expression also works
/// for record-style selective overrides.
/// </summary>
/// <remarks>
/// This type is the single source of truth for circuit-breaker defaults.
/// <see cref="CircuitBreaker{T}"/> defers to it via
/// <c>options ??= new CircuitBreakerOptions()</c>; no defaults are restated
/// in the breaker itself.
/// </remarks>
public sealed record CircuitBreakerOptions
{
    /// <summary>
    /// Default consecutive-failure count before the circuit opens. Internal
    /// because consumers don't need to reference this — they either pass an
    /// override or accept the default via the parameterless ctor.
    /// </summary>
    internal const int DEFAULT_FAILURE_THRESHOLD = 5;

    /// <summary>
    /// Default cooldown duration before a Half-Open probe is allowed.
    /// Internal — see <see cref="DEFAULT_FAILURE_THRESHOLD"/>.
    /// </summary>
    internal static readonly TimeSpan SR_DefaultCooldownDuration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Default monotonic-millisecond clock. Cached as a static delegate so
    /// every Options instance shares the same callable (no per-construction
    /// allocation). Internal — see <see cref="DEFAULT_FAILURE_THRESHOLD"/>.
    /// </summary>
    internal static readonly Func<long> SR_DefaultNowFunc = static () => Environment.TickCount64;

    /// <summary>
    /// Initializes a new <see cref="CircuitBreakerOptions"/> with all
    /// documented defaults. Equivalent to the parameterized ctor invoked
    /// with all <c>null</c> arguments.
    /// </summary>
    public CircuitBreakerOptions()
        : this(null, null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="CircuitBreakerOptions"/>. Each parameter
    /// is nullable; passing <c>null</c> (or omitting the argument) yields the
    /// documented default for that property. Explicit non-null values (even
    /// <c>0</c> / <see cref="TimeSpan.Zero"/>) are preserved as-is.
    /// </summary>
    /// <param name="failureThreshold">
    /// Override for <see cref="FailureThreshold"/>; <c>null</c> = default 5.
    /// </param>
    /// <param name="cooldownDuration">
    /// Override for <see cref="CooldownDuration"/>; <c>null</c> = default
    /// 30 seconds.
    /// </param>
    /// <param name="nowFunc">
    /// Override for <see cref="NowFunc"/>; <c>null</c> = default
    /// (<see cref="Environment.TickCount64"/>).
    /// </param>
    public CircuitBreakerOptions(
        int? failureThreshold = null,
        TimeSpan? cooldownDuration = null,
        Func<long>? nowFunc = null)
    {
        FailureThreshold = failureThreshold ?? DEFAULT_FAILURE_THRESHOLD;
        CooldownDuration = cooldownDuration ?? SR_DefaultCooldownDuration;
        NowFunc = nowFunc ?? SR_DefaultNowFunc;
    }

    /// <summary>
    /// Gets the number of consecutive failures required before the circuit
    /// opens. Default: 5.
    /// </summary>
    public int FailureThreshold { get; init; }

    /// <summary>
    /// Gets the duration the circuit stays open before allowing a probe.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan CooldownDuration { get; init; }

    /// <summary>
    /// Gets the monotonic-millisecond clock used for cooldown timing.
    /// Default: <see cref="Environment.TickCount64"/>. Override only for
    /// tests that need deterministic time control.
    /// </summary>
    public Func<long> NowFunc { get; init; }
}
