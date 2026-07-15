// -----------------------------------------------------------------------
// <copyright file="IResilientLayer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Resilience.Pipeline;

/// <summary>
/// A single decoration step in a <see cref="ResilientPipeline{TKey, TValue}"/>.
/// Each layer wraps the next inner callable, optionally consulting the
/// per-call key (used by Singleflight; ignored by other layers).
/// </summary>
/// <typeparam name="TKey">
/// Per-call key type. Used by layers that dedupe / route by key
/// (Singleflight); other layers receive but ignore it.
/// </typeparam>
/// <typeparam name="TValue">The value type produced by the operation.</typeparam>
public interface IResilientLayer<TKey, TValue>
    where TKey : notnull
{
    /// <summary>
    /// Wraps <paramref name="next"/> with this layer's behavior and invokes it.
    /// </summary>
    /// <param name="key">The per-call key (used by some layers, ignored by others).</param>
    /// <param name="next">The inner callable to wrap.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The (possibly transformed) operation result.</returns>
    ValueTask<TValue> WrapAsync(
        TKey key,
        Func<CancellationToken, ValueTask<TValue>> next,
        CancellationToken ct);
}
