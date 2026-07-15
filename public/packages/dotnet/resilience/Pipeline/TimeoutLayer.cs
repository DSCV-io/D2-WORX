// -----------------------------------------------------------------------
// <copyright file="TimeoutLayer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Resilience.Pipeline;

using DcsvIo.D2.Resilience.Timeout;

/// <summary>
/// Pipeline layer that bounds the wrapped operation with a wall-clock timeout via a
/// linked <see cref="CancellationTokenSource"/>. On expiry the inner operation is
/// canceled and a <see cref="TimeoutException"/> is thrown — distinct from
/// caller-initiated cancellation — so:
/// <list type="bullet">
///   <item><description>
///     An outer <see cref="RetryLayer{TKey, TValue}"/> retries it (
///     <see cref="Retry.RetryHelper.IsTransientException"/> already classifies
///     <see cref="TimeoutException"/> as transient — zero classifier change needed).
///   </description></item>
///   <item><description>
///     A leaked timeout that reaches <see cref="ResilientPipeline{TKey, TValue}.ExecuteAsync"/>
///     maps to <c>D2Result.ServiceUnavailable()</c> (503, transient-retryable)
///     — NOT <c>D2Result.Canceled()</c>, which is reserved for caller-initiated cancellation.
///   </description></item>
/// </list>
/// </summary>
/// <remarks>
/// Place at TWO positions to express separate total-request and per-attempt
/// deadlines in the same pipeline:
/// <code>
/// .UseRateLimiter(...)               // outermost
/// .UseTimeout(new(totalBudget))      // total: bounds all retries combined
/// .UseRetries(...)
/// .UseCircuitBreaker(key)
/// .UseTimeout(new(perAttempt))       // per-attempt: inside retry loop
/// </code>
/// Setting <see cref="TimeoutOptions.Duration"/> to <see cref="TimeSpan.Zero"/> or
/// any value ≤ zero disables the timeout — the layer becomes a pass-through
/// (no <see cref="CancellationTokenSource"/> is allocated).
/// </remarks>
/// <typeparam name="TKey">Per-call key type (ignored by this layer).</typeparam>
/// <typeparam name="TValue">The value type produced by the operation.</typeparam>
public sealed class TimeoutLayer<TKey, TValue> : IResilientLayer<TKey, TValue>
    where TKey : notnull
{
    private readonly TimeoutOptions r_options;

    /// <summary>
    /// Initializes a <see cref="TimeoutLayer{TKey, TValue}"/> with the supplied
    /// <paramref name="options"/>. Pass <c>null</c> to use the documented
    /// <see cref="TimeoutOptions"/> defaults (10-second timeout).
    /// </summary>
    /// <param name="options">Timeout configuration; <c>null</c> = defaults.</param>
    public TimeoutLayer(TimeoutOptions? options = null)
        => r_options = options ?? new TimeoutOptions();

    /// <inheritdoc/>
    public async ValueTask<TValue> WrapAsync(
        TKey key,
        Func<CancellationToken, ValueTask<TValue>> next,
        CancellationToken ct)
    {
        // Duration <= Zero means "no timeout" — pass straight through without
        // allocating a CancellationTokenSource.
        if (r_options.Duration <= TimeSpan.Zero)
            return await next(ct);

        using var timeoutCts = new CancellationTokenSource(r_options.Duration);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            return await next(linked.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // Timeout fired (not the caller). Re-throw as TimeoutException so:
            //   (a) RetryHelper.IsTransientException classifies it transient →
            //       an outer RetryLayer re-attempts (per-attempt timeout semantic);
            //   (b) a leaked one maps to ServiceUnavailable at the pipeline
            //       boundary (TimeoutException is transient, not OCE-on-caller-ct).
            throw new TimeoutException(
                $"Operation exceeded the configured timeout of {r_options.Duration}.");
        }
    }
}
