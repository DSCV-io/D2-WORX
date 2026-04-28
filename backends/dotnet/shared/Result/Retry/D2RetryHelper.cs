// -----------------------------------------------------------------------
// <copyright file="D2RetryHelper.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Result.Retry;

using System.Net;

/// <summary>
/// D2Result-aware retry helpers with exponential backoff and optional jitter.
/// </summary>
/// <remarks>
/// Both <c>RetryResultAsync</c> (clean) and <c>RetryExternalAsync</c> (dirty) never throw.
/// They always return a <see cref="D2Result{TData}"/>.
/// </remarks>
public static class D2RetryHelper
{
    /// <summary>
    /// Determines whether a D2Result represents a transient failure that may succeed on retry.
    /// </summary>
    /// <param name="result">The result to evaluate.</param>
    /// <returns>True if the result represents a transient failure; otherwise, false.</returns>
    /// <remarks>
    /// <para>Transient error codes: SERVICE_UNAVAILABLE, UNHANDLED_EXCEPTION, RATE_LIMITED, CONFLICT.</para>
    /// <para>Transient status codes: any >= 500, or 429 (TooManyRequests).</para>
    /// <para>NOT transient: NOT_FOUND, UNAUTHORIZED, FORBIDDEN, VALIDATION_FAILED, SOME_FOUND.</para>
    /// </remarks>
    public static bool IsTransientResult(D2Result result)
    {
        if (result.Success)
        {
            return false;
        }

        // Check error codes first (more specific)
        return result.ErrorCode switch
        {
            ErrorCodes.SERVICE_UNAVAILABLE => true,
            ErrorCodes.UNHANDLED_EXCEPTION => true,
            ErrorCodes.RATE_LIMITED => true,
            ErrorCodes.CONFLICT => true,
            ErrorCodes.NOT_FOUND => false,
            ErrorCodes.UNAUTHORIZED => false,
            ErrorCodes.FORBIDDEN => false,
            ErrorCodes.VALIDATION_FAILED => false,
            ErrorCodes.SOME_FOUND => false,
            ErrorCodes.COULD_NOT_BE_SERIALIZED => false,
            ErrorCodes.COULD_NOT_BE_DESERIALIZED => false,
            ErrorCodes.PAYLOAD_TOO_LARGE => false,
            ErrorCodes.CANCELLED => false,
            _ => result.StatusCode is >= HttpStatusCode.InternalServerError
                or HttpStatusCode.TooManyRequests,
        };
    }

    /// <summary>
    /// Retries a D2Result-returning operation with exponential backoff (clean version).
    /// </summary>
    /// <typeparam name="TData">The data type of the result.</typeparam>
    /// <param name="operation">
    /// The async operation to execute. Receives a 1-based attempt number.
    /// </param>
    /// <param name="options">Retry configuration options. Uses defaults if null.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The D2Result from the operation.</returns>
    /// <remarks>
    /// <para>Never throws. Always returns a D2Result.</para>
    /// <list type="bullet">
    ///   <item>Success → return immediately.</item>
    ///   <item>Transient failure → delay + retry.</item>
    ///   <item>Permanent failure → return immediately.</item>
    ///   <item>Operation throws → catch, return UnhandledException D2Result.</item>
    ///   <item>After all attempts → return last D2Result.</item>
    /// </list>
    /// </remarks>
    public static async ValueTask<D2Result<TData>> RetryResultAsync<TData>(
        Func<int, CancellationToken, ValueTask<D2Result<TData>>> operation,
        D2RetryOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new D2RetryOptions();
        var checkTransient = options.IsTransientResult ?? IsTransientResult;
        var delayFunc = options.DelayFunc ?? Task.Delay;

        D2Result<TData>? lastResult = null;

        for (var attempt = 1; attempt <= options.MaxAttempts; attempt++)
        {
            if (ct.IsCancellationRequested)
            {
                return lastResult ?? D2Result<TData>.Cancelled();
            }

            try
            {
                lastResult = await operation(attempt, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastResult = D2Result<TData>.UnhandledException();
            }

            // Success → return
            if (lastResult.Success)
            {
                return lastResult;
            }

            // Permanent failure → return immediately
            if (!checkTransient(lastResult))
            {
                return lastResult;
            }

            // Transient failure → delay + retry (unless last attempt)
            if (attempt < options.MaxAttempts)
            {
                await delayFunc(
                    CalculateDelay(attempt - 1, options.BaseDelayMs, options.BackoffMultiplier, options.MaxDelayMs, options.Jitter),
                    ct);
            }
        }

        return lastResult ?? D2Result<TData>.Cancelled();
    }

    /// <summary>
    /// Retries an external operation with mapping to D2Result (dirty version).
    /// </summary>
    /// <typeparam name="TRaw">The raw return type of the external operation.</typeparam>
    /// <typeparam name="TData">The data type of the mapped D2Result.</typeparam>
    /// <param name="operation">
    /// The async operation to execute. Receives a 1-based attempt number.
    /// </param>
    /// <param name="mapResult">Maps a raw response to a D2Result.</param>
    /// <param name="options">Retry configuration options. Uses defaults if null.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The mapped D2Result.</returns>
    /// <remarks>
    /// <para>Never throws. Always returns a D2Result.</para>
    /// <para>
    /// After mapping via <paramref name="mapResult"/> (for returned values) or
    /// <c>MapError</c> (for exceptions), the same transient detection as
    /// <see cref="RetryResultAsync{TData}"/> is used.
    /// </para>
    /// </remarks>
    public static async ValueTask<D2Result<TData>> RetryExternalAsync<TRaw, TData>(
        Func<int, CancellationToken, ValueTask<TRaw>> operation,
        Func<TRaw, D2Result<TData>> mapResult,
        D2RetryExternalOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new D2RetryExternalOptions();
        var mapError = options.MapError ?? (_ => D2Result.UnhandledException());
        var checkTransient = options.IsTransientResult ?? IsTransientResult;
        var delayFunc = options.DelayFunc ?? Task.Delay;

        D2Result<TData>? lastResult = null;

        for (var attempt = 1; attempt <= options.MaxAttempts; attempt++)
        {
            if (ct.IsCancellationRequested)
            {
                return lastResult ?? D2Result<TData>.Cancelled();
            }

            D2Result mapped;

            try
            {
                var raw = await operation(attempt, ct);
                var d2 = mapResult(raw);
                mapped = d2;

                // Success → return the properly typed result
                if (d2.Success)
                {
                    return d2;
                }

                lastResult = d2;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                mapped = mapError(ex);
                lastResult = D2Result<TData>.BubbleFail(mapped);
            }

            // Permanent failure → return immediately
            if (!checkTransient(mapped))
            {
                return lastResult;
            }

            // Transient failure → delay + retry (unless last attempt)
            if (attempt < options.MaxAttempts)
            {
                await delayFunc(
                    CalculateDelay(attempt - 1, options.BaseDelayMs, options.BackoffMultiplier, options.MaxDelayMs, options.Jitter),
                    ct);
            }
        }

        return lastResult ?? D2Result<TData>.Cancelled();
    }

    /// <summary>
    /// Calculates the delay for a given retry index (0-based).
    /// </summary>
    /// <param name="retryIndex">Zero-based retry index.</param>
    /// <param name="baseDelayMs">Base delay in milliseconds.</param>
    /// <param name="backoffMultiplier">Backoff multiplier.</param>
    /// <param name="maxDelayMs">Maximum delay in milliseconds.</param>
    /// <param name="jitter">Whether to apply full jitter.</param>
    /// <returns>The calculated delay as a <see cref="TimeSpan"/>.</returns>
    internal static TimeSpan CalculateDelay(
        int retryIndex,
        int baseDelayMs,
        double backoffMultiplier,
        int maxDelayMs,
        bool jitter)
    {
        var calculated = Math.Min(baseDelayMs * Math.Pow(backoffMultiplier, retryIndex), maxDelayMs);
        var actual = jitter ? Random.Shared.NextDouble() * calculated : calculated;
        return TimeSpan.FromMilliseconds(actual);
    }
}
