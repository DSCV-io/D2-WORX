// -----------------------------------------------------------------------
// <copyright file="RetryHelper.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Resilience.Retry;

using System.Net;
using System.Net.Sockets;
using DcsvIo.D2.Resilience.CircuitBreaker;
using DcsvIo.D2.Result;

/// <summary>
/// Generic retry utility with exponential backoff and optional jitter. Knows
/// how to spot transient HTTP / socket / timeout exceptions out of the box,
/// and exposes a <see cref="D2Result"/>-aware overload via the
/// <see cref="D2Result.IsTransientRetryable"/> classifier.
/// </summary>
/// <remarks>
/// <para>
/// The operation receives a 1-based attempt number. On thrown exceptions,
/// <see cref="RetryOptions{T}.IsTransient"/> controls retry behavior. On
/// returned values, <see cref="RetryOptions{T}.ShouldRetry"/> controls retry
/// behavior. Either may be supplied; the defaults retry transient
/// exceptions and accept all returns.
/// </para>
/// <para>
/// After all attempts are exhausted: throws the last exception (when the
/// last attempt threw) or returns the last value (when the last attempt
/// returned).
/// </para>
/// </remarks>
public static class RetryHelper
{
    /// <summary>
    /// Default exception classifier. Returns true for HTTP responses with
    /// status >= 500, 429, or 408; <see cref="TaskCanceledException"/>;
    /// <see cref="TimeoutException"/>; <see cref="SocketException"/>; and
    /// <see cref="CircuitOpenException"/>.
    /// </summary>
    /// <param name="ex">The exception to classify.</param>
    /// <returns>True if the exception is transient; otherwise false.</returns>
    /// <remarks>
    /// <para>
    /// <see cref="CircuitOpenException"/> is treated as transient so that the
    /// retry-OUTSIDE-CB composition (<c>retry → CB → upstream</c>) recovers
    /// naturally when an upstream restarts: the breaker opens, the retry
    /// layer backs off, the breaker's cooldown elapses, and a later retry
    /// attempt finds the breaker probing / closed and succeeds. Callers
    /// MUST size <see cref="RetryOptions{T}.MaxAttempts"/> + backoff to span
    /// the breaker's <see cref="CircuitBreakerOptions.CooldownDuration"/>,
    /// otherwise retries exhaust before the breaker has a chance to recover.
    /// </para>
    /// <para>
    /// Does NOT include gRPC-specific checks — that would force a transitive
    /// gRPC dependency on every consumer of this lib. Callers needing gRPC
    /// awareness should pass a custom <see cref="RetryOptions{T}.IsTransient"/>
    /// predicate.
    /// </para>
    /// </remarks>
    public static bool IsTransientException(Exception ex)
    {
        return ex switch
        {
            HttpRequestException httpEx => httpEx.StatusCode switch
            {
                >= HttpStatusCode.InternalServerError => true,
                HttpStatusCode.TooManyRequests => true,
                HttpStatusCode.RequestTimeout => true,
                _ => false,
            },
            TaskCanceledException => true,
            TimeoutException => true,
            SocketException => true,
            CircuitOpenException => true,
            _ => false,
        };
    }

    /// <summary>
    /// Executes <paramref name="operation"/> with retry logic using
    /// exponential backoff.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">
    /// Async operation to execute. Receives a 1-based attempt number.
    /// </param>
    /// <param name="options">Retry configuration. Defaults applied if null.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the (last attempted) operation.</returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="ct"/> is canceled while waiting between
    /// attempts.
    /// </exception>
    public static async ValueTask<T> RetryAsync<T>(
        Func<int, CancellationToken, ValueTask<T>> operation,
        RetryOptions<T>? options = null,
        CancellationToken ct = default)
    {
        // RetryOptions<T> is the single source of truth for defaults
        // — falling back to a parameterless ctor here delegates that work.
        options ??= new RetryOptions<T>();

        // while(true) instead of for(...) — guarantees every path through the
        // loop body returns or throws. The compiler cannot prove the same of
        // a counted loop, which would force a defensive (unreachable) epilogue.
        var attempt = 0;
        while (true)
        {
            attempt++;
            ct.ThrowIfCancellationRequested();

            try
            {
                var result = await operation(attempt, ct);

                if (attempt < options.MaxAttempts && options.ShouldRetry(result))
                {
                    await options.DelayFunc(
                        CalculateDelay(
                            attempt - 1,
                            options.BaseDelayMs,
                            options.BackoffMultiplier,
                            options.MaxDelayMs,
                            options.Jitter),
                        ct);
                    continue;
                }

                return result;
            }
            catch (Exception ex) when (
                ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                if (attempt < options.MaxAttempts && options.IsTransient(ex))
                {
                    await options.DelayFunc(
                        CalculateDelay(
                            attempt - 1,
                            options.BaseDelayMs,
                            options.BackoffMultiplier,
                            options.MaxDelayMs,
                            options.Jitter),
                        ct);
                    continue;
                }

                throw;
            }
        }
    }

    /// <summary>
    /// <see cref="D2Result"/>-aware retry overload: retries when the
    /// returned <see cref="D2Result{TData}"/> is failed AND
    /// <see cref="D2Result.IsTransientRetryable"/>. Caller-supplied
    /// <see cref="RetryOptions{T}.ShouldRetry"/> takes precedence when set.
    /// </summary>
    /// <typeparam name="TData">The payload type carried by the result.</typeparam>
    /// <param name="operation">
    /// Async operation to execute. Receives a 1-based attempt number.
    /// </param>
    /// <param name="options">
    /// Retry configuration. Defaults applied if null. When
    /// <see cref="RetryOptions{T}.ShouldRetry"/> is null on the supplied
    /// options, it is wired to the transient-retryable predicate.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the (last attempted) operation.</returns>
    public static ValueTask<D2Result<TData>> RetryD2ResultAsync<TData>(
        Func<int, CancellationToken, ValueTask<D2Result<TData>>> operation,
        RetryOptions<D2Result<TData>>? options = null,
        CancellationToken ct = default)
    {
        var effective = options ?? new RetryOptions<D2Result<TData>>();

        // Detect "caller didn't customize ShouldRetry" via reference-equality
        // against the cached default delegate. Any explicit predicate (even
        // one that happens to be `_ => false`) is a different delegate and
        // wins over the smart D2Result-aware substitution.
        if (ReferenceEquals(
                effective.ShouldRetry,
                RetryOptions<D2Result<TData>>.SR_DefaultShouldRetry))
        {
            effective = effective with
            {
                ShouldRetry = r => r is { Failed: true, IsTransientRetryable: true },
            };
        }

        return RetryAsync(operation, effective, ct);
    }

    /// <summary>
    /// Computes the delay for a given (zero-based) retry index using
    /// exponential backoff with optional full jitter.
    /// </summary>
    /// <param name="retryIndex">Zero-based retry index.</param>
    /// <param name="baseDelayMs">Base delay in milliseconds.</param>
    /// <param name="backoffMultiplier">Multiplier per retry.</param>
    /// <param name="maxDelayMs">Maximum delay in milliseconds.</param>
    /// <param name="jitter">Apply full jitter when true.</param>
    /// <returns>The calculated delay.</returns>
    /// <remarks>
    /// <code>
    /// calculatedDelay = min(baseDelayMs * (backoffMultiplier ^ retryIndex), maxDelayMs)
    /// actualDelay     = jitter ? random(0, calculatedDelay) : calculatedDelay
    /// </code>
    /// The exponent is clamped to 63 so <c>Math.Pow</c> cannot overflow into
    /// a signaling NaN; <c>Math.Min(double.PositiveInfinity, maxDelayMs)</c>
    /// safely returns <c>maxDelayMs</c>.
    /// </remarks>
    internal static TimeSpan CalculateDelay(
        int retryIndex,
        int baseDelayMs,
        double backoffMultiplier,
        int maxDelayMs,
        bool jitter)
    {
        var calculated = Math.Min(
            baseDelayMs * Math.Pow(backoffMultiplier, Math.Min(retryIndex, 63)),
            maxDelayMs);
        var actual = jitter ? Random.Shared.NextDouble() * calculated : calculated;
        return TimeSpan.FromMilliseconds(actual);
    }
}
