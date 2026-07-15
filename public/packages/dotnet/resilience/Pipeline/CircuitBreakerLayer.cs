// -----------------------------------------------------------------------
// <copyright file="CircuitBreakerLayer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Resilience.Pipeline;

using DcsvIo.D2.Resilience.CircuitBreaker;

/// <summary>
/// Pipeline layer that fast-fails through a wrapped
/// <see cref="CircuitBreaker{T}"/> when the breaker is open. The per-call
/// key is ignored — circuit breakers operate at the operation level, not
/// the key level.
/// </summary>
/// <typeparam name="TKey">Per-call key type (ignored by this layer).</typeparam>
/// <typeparam name="TValue">The value type produced by the operation.</typeparam>
public sealed class CircuitBreakerLayer<TKey, TValue> : IResilientLayer<TKey, TValue>
    where TKey : notnull
{
    private readonly CircuitBreaker<TValue> r_circuitBreaker;

    /// <summary>
    /// Initializes a <see cref="CircuitBreakerLayer{TKey, TValue}"/>
    /// wrapping the supplied <paramref name="circuitBreaker"/> instance.
    /// </summary>
    /// <param name="circuitBreaker">The CircuitBreaker to wrap.</param>
    public CircuitBreakerLayer(CircuitBreaker<TValue> circuitBreaker)
        => r_circuitBreaker = circuitBreaker;

    /// <inheritdoc/>
    public ValueTask<TValue> WrapAsync(
        TKey key,
        Func<CancellationToken, ValueTask<TValue>> next,
        CancellationToken ct)
        => r_circuitBreaker.ExecuteAsync(next, ct: ct);
}
