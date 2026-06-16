// -----------------------------------------------------------------------
// <copyright file="FakeIdempotencyStore.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;

using D2.Edge.Tests.TypeSpecRoute.Generated;
using D2.Shared.Result;

/// <summary>
/// In-memory faithful double of <see cref="D2GeneratedIdempotencyStore"/> for
/// idempotency-gate route tests.
/// Stores entries with real TTL expiry; returns <c>NotFound</c> on miss,
/// <c>Ok(stored)</c> on hit, <c>ServiceUnavailable</c> when faulted.
/// Accepts an optional <see cref="TimeProvider"/> so tests can advance the
/// clock deterministically to exercise TTL-expiry behavior.
/// </summary>
internal sealed class FakeIdempotencyStore : D2GeneratedIdempotencyStore
{
    private readonly Dictionary<string, Entry> r_store = [];
    private readonly TimeProvider r_clock;

    private bool _faulted;

    /// <summary>
    /// Initializes a new instance of <see cref="FakeIdempotencyStore"/> with
    /// the given <paramref name="clock"/>. Pass a <c>FakeTimeProvider</c> to
    /// control "now" deterministically in TTL-expiry tests.
    /// </summary>
    /// <param name="clock">
    /// The time source used to evaluate entry TTLs. Defaults to
    /// <see cref="TimeProvider.System"/> when <see langword="null"/>.
    /// </param>
    public FakeIdempotencyStore(TimeProvider? clock = null)
    {
        r_clock = clock ?? TimeProvider.System;
    }

    /// <summary>Gets the keys stored so far (for assertion).</summary>
    public IReadOnlyCollection<string> StoredKeys => r_store.Keys;

    /// <summary>Gets the number of <see cref="TryGetAsync{TStored}"/> invocations.</summary>
    public int TryGetCallCount { get; private set; }

    /// <summary>Gets the number of <see cref="StoreAsync{TStored}"/> invocations.</summary>
    public int StoreCallCount { get; private set; }

    /// <summary>Simulate a store outage: every call returns <c>ServiceUnavailable</c>.</summary>
    public void SetFaulted() => _faulted = true;

    /// <inheritdoc/>
    public ValueTask<D2Result<TStored?>> TryGetAsync<TStored>(
        string key, CancellationToken ct = default)
    {
        TryGetCallCount++;
        if (_faulted)
            return new(D2Result<TStored?>.ServiceUnavailable());
        if (!r_store.TryGetValue(key, out var entry) || entry.ExpiresAt <= r_clock.GetUtcNow())
            return new(D2Result<TStored?>.NotFound());
        return new(D2Result<TStored?>.Ok((TStored?)entry.Value));
    }

    /// <inheritdoc/>
    public ValueTask<D2Result> StoreAsync<TStored>(
        string key, TStored value, TimeSpan ttl, CancellationToken ct = default)
    {
        StoreCallCount++;
        if (_faulted)
            return new(D2Result.ServiceUnavailable());
        r_store[key] = new Entry(value, r_clock.GetUtcNow() + ttl);
        return new(D2Result.Ok());
    }

    private readonly record struct Entry(object? Value, DateTimeOffset ExpiresAt);
}
