// -----------------------------------------------------------------------
// <copyright file="RetryLayer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Resilience.Pipeline;

using DcsvIo.D2.Resilience.Retry;

/// <summary>
/// Pipeline layer that retries the wrapped operation per a configured
/// <see cref="RetryOptions{TValue}"/>. The per-call key is ignored —
/// retry policy is per-operation, not per-key.
/// </summary>
/// <typeparam name="TKey">Per-call key type (ignored by this layer).</typeparam>
/// <typeparam name="TValue">The value type produced by the operation.</typeparam>
public sealed class RetryLayer<TKey, TValue> : IResilientLayer<TKey, TValue>
    where TKey : notnull
{
    private readonly RetryOptions<TValue> r_options;

    /// <summary>
    /// Initializes a <see cref="RetryLayer{TKey, TValue}"/> with the supplied
    /// <paramref name="options"/>. Pass <c>null</c> to use the documented
    /// <see cref="RetryOptions{TValue}"/> defaults.
    /// </summary>
    /// <param name="options">Retry configuration; <c>null</c> = defaults.</param>
    public RetryLayer(RetryOptions<TValue>? options = null)
        => r_options = options ?? new RetryOptions<TValue>();

    /// <inheritdoc/>
    public ValueTask<TValue> WrapAsync(
        TKey key,
        Func<CancellationToken, ValueTask<TValue>> next,
        CancellationToken ct)
        => RetryHelper.RetryAsync((_, c) => next(c), r_options, ct);
}
