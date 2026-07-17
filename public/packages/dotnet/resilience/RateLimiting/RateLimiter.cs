// -----------------------------------------------------------------------
// <copyright file="RateLimiter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Resilience.RateLimiting;

/// <summary>
/// Hand-rolled, <see cref="SemaphoreSlim"/>-based concurrency limiter. Bounds the
/// number of concurrent in-flight operations to <see cref="RateLimiterOptions.MaxConcurrency"/>.
/// A caller that cannot acquire a permit within
/// <see cref="RateLimiterOptions.AcquisitionTimeout"/> is rejected immediately via
/// <see cref="RateLimitRejectedException"/> rather than queued indefinitely.
/// </summary>
/// <remarks>
/// <para>
/// <b>Client-side, in-process only.</b> This primitive is admission control for
/// outbound calls — it limits concurrent pressure from THIS process on an upstream.
/// It is NOT the server-side, distributed rate-limit middleware.
/// </para>
/// <para>
/// <b>Disposal:</b> The underlying <see cref="SemaphoreSlim"/> is owned by this
/// instance. Dispose via the DI container (register as a keyed singleton) or,
/// for inline use, ensure the owning <see cref="Pipeline.RateLimiterLayer{TKey, TValue}"/>
/// is disposed at container teardown. The semaphore is not finalized — failing to
/// dispose leaks a kernel handle on platforms where the semaphore uses one (most
/// production environments do not create enough concurrent limiters for this to
/// matter in practice, but the analyzer will flag it otherwise).
/// </para>
/// </remarks>
public sealed class RateLimiter : IDisposable
{
    private readonly SemaphoreSlim r_gate;
    private readonly TimeSpan r_acquisitionTimeout;

    /// <summary>
    /// Initializes a new <see cref="RateLimiter"/> with the supplied
    /// <paramref name="options"/>. Pass <c>null</c> to use the documented
    /// <see cref="RateLimiterOptions"/> defaults.
    /// </summary>
    /// <param name="options">Rate-limiter configuration; <c>null</c> = defaults.</param>
    public RateLimiter(RateLimiterOptions? options = null)
    {
        var o = options ?? new RateLimiterOptions();
        r_gate = new SemaphoreSlim(o.MaxConcurrency, o.MaxConcurrency);
        r_acquisitionTimeout = o.AcquisitionTimeout;
    }

    /// <summary>
    /// Executes <paramref name="operation"/> under the concurrency gate. Acquires a
    /// permit before calling <paramref name="operation"/>, releases it in a
    /// <c>finally</c> block (on both success and exception), and throws
    /// <see cref="RateLimitRejectedException"/> when the gate is full and the
    /// acquisition timeout elapses.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The async operation to execute under the gate.</param>
    /// <param name="ct">Cancellation token (passed to the operation).</param>
    /// <returns>The result of <paramref name="operation"/>.</returns>
    /// <exception cref="RateLimitRejectedException">
    /// Thrown when the gate is full and <see cref="RateLimiterOptions.AcquisitionTimeout"/>
    /// elapses before a permit becomes available.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Propagated from the acquisition wait when <paramref name="ct"/> is canceled
    /// before a permit is acquired (the semaphore was not acquired, so no release
    /// is needed — no permit is leaked).
    /// </exception>
    public async ValueTask<T> ExecuteAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken ct = default)
    {
        // r_acquisitionTimeout <= Zero ⇒ WaitAsync with TimeSpan.Zero = non-blocking
        // tryEnter. Reject-fast without allocating a timer.
        var acquired = await r_gate.WaitAsync(r_acquisitionTimeout, ct);
        if (!acquired)
            throw new RateLimitRejectedException();

        try
        {
            return await operation(ct);
        }
        finally
        {
            r_gate.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose() => r_gate.Dispose();
}
