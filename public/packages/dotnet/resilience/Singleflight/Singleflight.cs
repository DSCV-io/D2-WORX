// -----------------------------------------------------------------------
// <copyright file="Singleflight.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Resilience.Singleflight;

using System.Collections.Concurrent;

/// <summary>
/// Deduplicates concurrent in-flight async operations by key. The first
/// caller for a given key executes the operation; subsequent callers for
/// the same key share the same <see cref="Task{TValue}"/>. Once the
/// operation completes (success or failure), the key is removed — this is
/// NOT a cache.
/// </summary>
/// <typeparam name="TKey">The deduplication key type. Must be non-null.</typeparam>
/// <typeparam name="TValue">The result value type produced by the operation.</typeparam>
/// <remarks>
/// <para>
/// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/> and
/// <see cref="Lazy{T}"/> with
/// <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>.
/// </para>
/// <para>
/// Per-caller cancellation only affects the wait, not the shared operation
/// — one caller bailing must not affect siblings sharing the same task.
/// The shared operation always runs with
/// <see cref="CancellationToken.None"/>.
/// </para>
/// </remarks>
public sealed class Singleflight<TKey, TValue>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, Lazy<Task<TValue>>> r_inflight = [];

    /// <summary>
    /// Gets the number of currently in-flight operations.
    /// </summary>
    public int Size => r_inflight.Count;

    /// <summary>
    /// Executes the operation for the given key. If an operation is already
    /// in-flight for that key, returns the existing result instead of
    /// starting a new one.
    /// </summary>
    /// <param name="key">The deduplication key.</param>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="ct">
    /// Per-caller cancellation token. Only cancels this caller's wait — the
    /// shared operation runs with <see cref="CancellationToken.None"/> so
    /// one caller's cancellation cannot affect other callers sharing it.
    /// </param>
    /// <returns>The operation result.</returns>
    public async ValueTask<TValue> ExecuteAsync(
        TKey key,
        Func<CancellationToken, ValueTask<TValue>> operation,
        CancellationToken ct = default)
    {
        var lazy = r_inflight.GetOrAdd(
            key,
            _ => new Lazy<Task<TValue>>(
                () => RunAsync(key, operation),
                LazyThreadSafetyMode.ExecutionAndPublication));

        var sharedTask = lazy.Value;

        // Apply per-caller cancellation only to the wait, not the shared run.
        return ct.CanBeCanceled
            ? await sharedTask.WaitAsync(ct)
            : await sharedTask;
    }

    private async Task<TValue> RunAsync(
        TKey key,
        Func<CancellationToken, ValueTask<TValue>> operation)
    {
        try
        {
            // Run with CancellationToken.None — no single caller's
            // cancellation may affect siblings sharing this operation.
            return await operation(CancellationToken.None);
        }
        finally
        {
            r_inflight.TryRemove(key, out _);
        }
    }
}
