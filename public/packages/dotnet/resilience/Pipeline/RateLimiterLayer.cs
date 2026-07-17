// -----------------------------------------------------------------------
// <copyright file="RateLimiterLayer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Resilience.Pipeline;

using DcsvIo.D2.Resilience.RateLimiting;

/// <summary>
/// Pipeline layer bounding the number of concurrent in-flight operations via a
/// <see cref="RateLimiting.RateLimiter"/>. Callers that cannot acquire a concurrency
/// permit within the configured acquisition window are rejected (
/// <see cref="RateLimitRejectedException"/> → mapped to <c>D2Result.TooManyRequests()</c>
/// at the pipeline boundary) rather than queued indefinitely.
/// </summary>
/// <remarks>
/// <para>
/// The per-call key is ignored — the limit is per-limiter-instance. Share a single
/// <see cref="RateLimiting.RateLimiter"/> keyed singleton across pipelines (via the
/// <c>UseRateLimiter(serviceKey)</c> builder overload, mirroring the shared-CB
/// pattern) for cross-pipeline concurrency accounting.
/// </para>
/// <para>
/// <b>Client-side, in-process only.</b> This layer is admission control for
/// outbound calls — it limits concurrent pressure from THIS process on an upstream.
/// It is NOT the server-side, distributed per-tier rate-limit middleware.
/// </para>
/// <para>
/// <b>Disposal:</b> this layer is <see cref="IDisposable"/>. In the
/// inline-options case (<c>UseRateLimiter(options)</c>), the layer OWNS the
/// <see cref="RateLimiting.RateLimiter"/> and its underlying
/// <see cref="System.Threading.SemaphoreSlim"/>; disposing this layer (which
/// happens when the owning <see cref="ResilientPipeline{TKey, TValue}"/>
/// is disposed) releases the semaphore. In the keyed-DI case
/// (<c>UseRateLimiter(serviceKey)</c>), the DI container owns the
/// <see cref="RateLimiting.RateLimiter"/> singleton and disposes it at
/// teardown; this layer holds a reference only and <see cref="Dispose"/> is
/// a no-op.
/// </para>
/// </remarks>
/// <typeparam name="TKey">Per-call key type (ignored by this layer).</typeparam>
/// <typeparam name="TValue">The value type produced by the operation.</typeparam>
public sealed class RateLimiterLayer<TKey, TValue> : IResilientLayer<TKey, TValue>, IDisposable
    where TKey : notnull
{
    private readonly RateLimiter r_limiter;
    private readonly bool r_ownsLimiter;

    /// <summary>
    /// Initializes a <see cref="RateLimiterLayer{TKey, TValue}"/> with an inline
    /// <see cref="RateLimiting.RateLimiter"/> instance constructed from
    /// <paramref name="options"/>. The layer OWNS the limiter and disposes it on
    /// <see cref="Dispose"/>.
    /// </summary>
    /// <param name="options">Rate-limiter configuration; <c>null</c> = defaults.</param>
    public RateLimiterLayer(RateLimiterOptions? options = null)
    {
        r_limiter = new RateLimiter(options);
        r_ownsLimiter = true;
    }

    /// <summary>
    /// Initializes a <see cref="RateLimiterLayer{TKey, TValue}"/> wrapping the
    /// supplied <paramref name="limiter"/> instance. Bypasses DI — use for tests or
    /// when the caller manages the limiter's lifetime externally. The layer does NOT
    /// own the limiter; <see cref="Dispose"/> is a no-op.
    /// </summary>
    /// <param name="limiter">The <see cref="RateLimiting.RateLimiter"/> to wrap.</param>
    public RateLimiterLayer(RateLimiter limiter)
    {
        r_limiter = limiter;
        r_ownsLimiter = false;
    }

    /// <inheritdoc/>
    public ValueTask<TValue> WrapAsync(
        TKey key,
        Func<CancellationToken, ValueTask<TValue>> next,
        CancellationToken ct)
        => r_limiter.ExecuteAsync(next, ct);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (r_ownsLimiter)
            r_limiter.Dispose();
    }
}
