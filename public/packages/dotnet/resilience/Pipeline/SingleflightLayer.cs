// -----------------------------------------------------------------------
// <copyright file="SingleflightLayer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Resilience.Pipeline;

using DcsvIo.D2.Resilience.Singleflight;

/// <summary>
/// Pipeline layer that dedupes concurrent in-flight operations by the
/// per-call key via the wrapped <see cref="Singleflight{TKey, TValue}"/>
/// instance.
/// </summary>
/// <typeparam name="TKey">The deduplication key type.</typeparam>
/// <typeparam name="TValue">The value type produced by the operation.</typeparam>
public sealed class SingleflightLayer<TKey, TValue> : IResilientLayer<TKey, TValue>
    where TKey : notnull
{
    private readonly Singleflight<TKey, TValue> r_singleflight;

    /// <summary>
    /// Initializes a <see cref="SingleflightLayer{TKey, TValue}"/> wrapping
    /// the supplied <paramref name="singleflight"/> instance.
    /// </summary>
    /// <param name="singleflight">The Singleflight to wrap.</param>
    public SingleflightLayer(Singleflight<TKey, TValue> singleflight)
        => r_singleflight = singleflight;

    /// <inheritdoc/>
    public ValueTask<TValue> WrapAsync(
        TKey key,
        Func<CancellationToken, ValueTask<TValue>> next,
        CancellationToken ct)
        => r_singleflight.ExecuteAsync(key, next, ct);
}
