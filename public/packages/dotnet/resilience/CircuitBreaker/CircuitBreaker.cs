// -----------------------------------------------------------------------
// <copyright file="CircuitBreaker.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Resilience.CircuitBreaker;

/// <summary>
/// Lightweight three-state circuit breaker (Closed / Open / Half-Open) for
/// protecting async operations against sustained downstream failures. Tracks
/// consecutive failures and fast-fails when a threshold is reached, avoiding
/// wasted timeout waits.
/// </summary>
/// <typeparam name="T">The return type of the protected operation.</typeparam>
/// <remarks>
/// Thread-safe via <see cref="Interlocked"/> operations — no locks required.
/// The probe-in-flight flag ensures only one Half-Open probe runs at a time;
/// concurrent callers during a probe receive the fallback (or
/// <see cref="CircuitOpenException"/> when no fallback is supplied).
/// </remarks>
public sealed class CircuitBreaker<T>
{
    private readonly Func<T, bool> r_isFailure;
    private readonly int r_failureThreshold;
    private readonly long r_cooldownMs;
    private readonly Func<long> r_now;
    private readonly Action<CircuitState, CircuitState>? r_onStateChange;

    private int _state; // CircuitState cast to int for Interlocked
    private int _failureCount;
    private long _openedAt;
    private int _probeInFlight; // 0 or 1

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreaker{T}"/> class.
    /// </summary>
    ///
    /// <param name="isFailure">
    /// Predicate that inspects a returned value to decide whether it counts
    /// as a failure. Thrown exceptions ALWAYS count as failures; this
    /// predicate adds value-based failures (e.g. <c>r => !r.Success</c>).
    /// </param>
    /// <param name="options">Optional configuration. Defaults applied if null.</param>
    /// <param name="onStateChange">
    /// Optional callback fired on state transitions. Invoked synchronously on
    /// the thread that triggered the transition.
    /// <para>
    /// <b>Footgun:</b> a throwing callback REPLACES the upstream exception that
    /// caused the transition. A buggy logger or metric emitter inside this
    /// callback can swap a meaningful "TimeoutException from upstream X"
    /// with its own "InvalidOperationException from logger" — making outage
    /// diagnosis painful. Wrap the callback body in your own try/catch (or
    /// keep it to plain log/metric calls that don't throw) to preserve the
    /// upstream exception for callers.
    /// </para>
    /// </param>
    public CircuitBreaker(
        Func<T, bool> isFailure,
        CircuitBreakerOptions? options = null,
        Action<CircuitState, CircuitState>? onStateChange = null)
    {
        // CircuitBreakerOptions is the single source of truth for defaults
        // — falling back to a parameterless ctor here delegates that work.
        options ??= new CircuitBreakerOptions();

        r_isFailure = isFailure;
        r_failureThreshold = options.FailureThreshold;
        r_cooldownMs = (long)options.CooldownDuration.TotalMilliseconds;
        r_now = options.NowFunc;
        r_onStateChange = onStateChange;
    }

    /// <summary>
    /// Gets the current circuit state.
    /// </summary>
    public CircuitState State => (CircuitState)Volatile.Read(ref _state);

    /// <summary>
    /// Gets the current consecutive failure count. Reset to zero on every
    /// observed success and on <see cref="Reset"/>.
    /// </summary>
    public int FailureCount => Volatile.Read(ref _failureCount);

    /// <summary>
    /// Executes an operation through the circuit breaker.
    /// </summary>
    ///
    /// <param name="operation">The async operation to protect.</param>
    /// <param name="fallback">
    /// Optional fallback invoked when the circuit is open or while a probe is
    /// already in flight.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    ///
    /// <returns>The operation result, or the fallback result when open.</returns>
    ///
    /// <exception cref="CircuitOpenException">
    /// Thrown when the circuit is open and no fallback is supplied.
    /// </exception>
    public async ValueTask<T> ExecuteAsync(
        Func<CancellationToken, ValueTask<T>> operation,
        Func<ValueTask<T>>? fallback = null,
        CancellationToken ct = default)
    {
        // Open → Half-Open transition once the cooldown has elapsed.
        if (State == CircuitState.Open)
        {
            var elapsed = r_now() - Interlocked.Read(ref _openedAt);
            if (elapsed >= r_cooldownMs)
            {
                if (Interlocked.CompareExchange(
                        ref _state,
                        (int)CircuitState.HalfOpen,
                        (int)CircuitState.Open) == (int)CircuitState.Open)
                    r_onStateChange?.Invoke(CircuitState.Open, CircuitState.HalfOpen);
            }
        }

        switch (State)
        {
            // Open: fast-fail.
            case CircuitState.Open when fallback is not null:
                return await fallback();

            case CircuitState.Open:
                throw new CircuitOpenException();

            // Half-Open: only one probe at a time; everyone else gets the fallback.
            case CircuitState.HalfOpen
                when Interlocked.CompareExchange(ref _probeInFlight, 1, 0) != 0:
            {
                if (fallback is not null)
                    return await fallback();

                throw new CircuitOpenException();
            }

            // Closed, or the Half-Open probe winner: actually run the operation.
            case CircuitState.Closed:
            default:
            {
                try
                {
                    var result = await operation(ct);

                    if (r_isFailure(result))
                    {
                        RecordFailure();
                        return result;
                    }

                    RecordSuccess();
                    return result;
                }
                catch
                {
                    RecordFailure();
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Manually resets the circuit to <see cref="CircuitState.Closed"/> and
    /// clears the failure count.
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _failureCount, 0);
        Interlocked.Exchange(ref _probeInFlight, 0);

        var prev = (CircuitState)Interlocked.Exchange(
            ref _state,
            (int)CircuitState.Closed);

        if (prev != CircuitState.Closed)
            r_onStateChange?.Invoke(prev, CircuitState.Closed);
    }

    private void RecordSuccess()
    {
        Interlocked.Exchange(ref _failureCount, 0);
        Interlocked.Exchange(ref _probeInFlight, 0);

        var prev = (CircuitState)Interlocked.Exchange(
            ref _state,
            (int)CircuitState.Closed);

        if (prev != CircuitState.Closed)
            r_onStateChange?.Invoke(prev, CircuitState.Closed);
    }

    private void RecordFailure()
    {
        var count = Interlocked.Increment(ref _failureCount);
        Interlocked.Exchange(ref _probeInFlight, 0);

        // Half-Open probe failed → straight back to Open.
        if (State == CircuitState.HalfOpen)
        {
            Interlocked.Exchange(ref _openedAt, r_now());

            var prev = (CircuitState)Interlocked.Exchange(
                ref _state,
                (int)CircuitState.Open);

            if (prev != CircuitState.Open)
                r_onStateChange?.Invoke(prev, CircuitState.Open);

            return;
        }

        // Closed → Open once the threshold is hit.
        if (count >= r_failureThreshold)
        {
            Interlocked.Exchange(ref _openedAt, r_now());

            var prev = (CircuitState)Interlocked.Exchange(
                ref _state,
                (int)CircuitState.Open);

            if (prev != CircuitState.Open)
                r_onStateChange?.Invoke(prev, CircuitState.Open);
        }
    }
}
