// -----------------------------------------------------------------------
// <copyright file="ResilientPipeline.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Resilience.Pipeline;

using DcsvIo.D2.Resilience.CircuitBreaker;
using DcsvIo.D2.Resilience.RateLimiting;
using DcsvIo.D2.Resilience.Retry;
using DcsvIo.D2.Result;

/// <summary>
/// Composes <see cref="IResilientLayer{TKey, TValue}"/> layers into a single
/// callable surface. Each call to <see cref="ExecuteAsync"/> walks the
/// configured layers in outer-first order and converts every terminating
/// exception into a <see cref="D2Result{TValue}"/>, so callers never need
/// try/catch blocks at the call site.
/// </summary>
/// <typeparam name="TKey">
/// Per-call key type (used by Singleflight; ignored by other layers).
/// </typeparam>
/// <typeparam name="TValue">The value type produced by the operation.</typeparam>
/// <remarks>
/// <para>
/// <b>Order matters.</b> The order layers are passed to the constructor IS
/// the protection semantic. The two canonical full-stack compositions:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <c>[Singleflight, CircuitBreaker, Retry]</c> — retry INSIDE the
///     breaker. CB sees one execution per full retry budget. Protects the
///     upstream from sustained pressure (good for fragile upstreams where
///     backoff between attempts gives them air).
///   </description></item>
///   <item><description>
///     <c>[Singleflight, Retry, CircuitBreaker]</c> — retry OUTSIDE the
///     breaker. Each retry is a separate CB execution; when CB opens, the
///     <see cref="CircuitOpenException"/> is treated as transient by the
///     default classifier so the retry layer backs off through it. If the
///     retry budget spans the breaker's cooldown, a later attempt finds the
///     breaker probing / closed and succeeds. Recovers from upstream
///     restarts at the cost of additional caller-side latency. Callers MUST
///     size <see cref="RetryOptions{TValue}.MaxAttempts"/> + backoff to
///     span <see cref="CircuitBreakerOptions.CooldownDuration"/>; otherwise
///     retries exhaust on perpetual <see cref="CircuitOpenException"/> and
///     the pipeline returns
///     <see cref="D2Result{TValue}.ServiceUnavailable"/>.
///   </description></item>
/// </list>
/// <para>
/// <b>Exception → result mapping:</b>
/// </para>
/// <list type="bullet">
///   <item><description>
///     <see cref="CircuitOpenException"/> →
///     <see cref="D2Result{TValue}.ServiceUnavailable"/>
///   </description></item>
///   <item><description>
///     <see cref="RateLimitRejectedException"/> →
///     <see cref="D2Result{TValue}.TooManyRequests"/>
///     (429 / <c>RATE_LIMITED</c>, <c>IsTransientRetryable = true</c>)
///   </description></item>
///   <item><description>
///     <see cref="OperationCanceledException"/> when the supplied token
///     was the source → <see cref="D2Result{TValue}.Canceled"/>
///   </description></item>
///   <item><description>
///     Any other exception classified as transient by
///     <see cref="RetryHelper.IsTransientException"/> →
///     <see cref="D2Result{TValue}.ServiceUnavailable"/>
///     (covers transient errors that slipped past the configured layers,
///     e.g. when no Retry layer is configured; <see cref="TimeoutException"/>
///     from <see cref="TimeoutLayer{TKey, TValue}"/> takes this path)
///   </description></item>
///   <item><description>
///     Anything else →
///     <see cref="D2Result{TValue}.UnhandledException"/>
///   </description></item>
/// </list>
/// <para>
/// <b>Disposal:</b> implements <see cref="IDisposable"/>. On dispose, walks
/// the layer list and calls <see cref="IDisposable.Dispose"/> on any layer
/// that implements it. Inline-options layers (e.g. a
/// <see cref="RateLimiterLayer{TKey, TValue}"/> constructed via
/// <c>UseRateLimiter(options)</c>) own their underlying primitives and
/// release them on dispose. Keyed-DI layers hold a reference only — their
/// primitives are registered directly with the container and disposed by it.
/// Dispose is idempotent. The DI container calls it automatically when the
/// <c>ServiceProvider</c> is disposed, provided the pipeline is registered
/// as a keyed singleton (the only supported registration path via
/// <see cref="ResilientPipelineServiceCollectionExtensions"/>).
/// </para>
/// </remarks>
public sealed class ResilientPipeline<TKey, TValue> : IDisposable
    where TKey : notnull
{
    private readonly IResilientLayer<TKey, TValue>[] r_layersOuterFirst;
    private bool _disposed;

    /// <summary>
    /// Initializes a <see cref="ResilientPipeline{TKey, TValue}"/> with the
    /// supplied layers in outer-first order. Layer at index 0 wraps every
    /// subsequent layer; the last layer wraps the operation directly.
    /// Passing zero layers yields a pass-through pipeline that still
    /// performs exception → result mapping.
    /// </summary>
    /// <param name="layersOuterFirst">
    /// Layers in outer-first execution order (see type-level remarks for the
    /// canonical compositions).
    /// </param>
    public ResilientPipeline(params IResilientLayer<TKey, TValue>[] layersOuterFirst)
        => r_layersOuterFirst = layersOuterFirst;

    /// <summary>
    /// Gets a zero-layer pipeline that performs ONLY the exception → <see cref="D2Result{TValue}"/>
    /// boundary mapping — no retry, no circuit-breaker, no timeout, no rate-limiter.
    /// Use this as the per-call "bypass" override when a caller explicitly wants no
    /// resilience but still needs the consistent <see cref="D2Result{TValue}"/> return shape.
    /// </summary>
    /// <remarks>
    /// Equivalent to <c>new ResilientPipeline&lt;TKey, TValue&gt;()</c> (zero-arg
    /// <c>params</c> ctor), exposed as a named sentinel to make the bypass intent
    /// explicit in generated clients and per-call override sites.
    /// </remarks>
    public static ResilientPipeline<TKey, TValue> PassThrough { get; } = new();

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var layer in r_layersOuterFirst)
        {
            if (layer is IDisposable d)
                d.Dispose();
        }
    }

    /// <summary>
    /// Executes <paramref name="operation"/> through the configured layer
    /// stack. Never throws — every terminating exception is converted to a
    /// <see cref="D2Result{TValue}"/> per the type-level mapping.
    /// </summary>
    /// <param name="key">Per-call key (used by Singleflight, ignored by other layers).</param>
    /// <param name="operation">The async operation to protect.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="D2Result{TValue}"/> wrapping the outcome.</returns>
    public async ValueTask<D2Result<TValue>> ExecuteAsync(
        TKey key,
        Func<CancellationToken, ValueTask<TValue>> operation,
        CancellationToken ct = default)
    {
        try
        {
            // Walk inner-to-outer, wrapping the operation with each layer.
            // Each iteration captures its own copies of `layer` and `inner`
            // so closure semantics are correct.
            var wrapped = operation;
            for (var i = r_layersOuterFirst.Length - 1; i >= 0; i--)
            {
                var layer = r_layersOuterFirst[i];
                var inner = wrapped;
                wrapped = c => layer.WrapAsync(key, inner, c);
            }

            return D2Result<TValue>.Ok(await wrapped(ct));
        }
        catch (CircuitOpenException)
        {
            return D2Result<TValue>.ServiceUnavailable();
        }
        catch (RateLimitRejectedException)
        {
            return D2Result<TValue>.TooManyRequests();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Intentional: filter on `ct.IsCancellationRequested`, not on
            // `ex.CancellationToken == ct`. Matches BCL convention (e.g.
            // `Task.WaitAsync(ct)`) — if the caller's token is canceled,
            // any OCE (even one whose source token is unrelated) surfaces
            // as Canceled. Without ct cancellation: derived
            // `TaskCanceledException` falls through to `IsTransientException`
            // (transient → ServiceUnavailable); base `OperationCanceledException`
            // is NOT classified transient and falls to the catch-all
            // (UnhandledException).
            return D2Result<TValue>.Canceled();
        }
        catch (Exception ex) when (RetryHelper.IsTransientException(ex))
        {
            return D2Result<TValue>.ServiceUnavailable();
        }
        catch
        {
            return D2Result<TValue>.UnhandledException();
        }
    }
}
