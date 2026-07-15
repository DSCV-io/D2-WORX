// -----------------------------------------------------------------------
// <copyright file="FakeTieredCacheStub.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;

using DcsvIo.D2.Caching;
using DcsvIo.D2.Result;

/// <summary>
/// Stub <see cref="ITieredCache"/> — satisfies the transitive DI dependency of
/// <c>JwtValidator</c> (session liveness path) without touching any real cache.
/// Route-policy enforcement tests focus on scope checking; the stub no-ops
/// everything except <c>ExistsAsync</c> (returns <c>Ok(true)</c> so the session
/// check treats the session as live).
/// Local copy — the original in <c>DcsvIo.D2.Tests</c> is a <c>private sealed</c>
/// nested class and cannot be referenced from another assembly.
/// </summary>
internal sealed class FakeTieredCacheStub : ITieredCache
{
    public ValueTask<D2Result<bool>> ExistsAsync(
        string key, CancellationToken ct = default)
        => new(D2Result<bool>.Ok(true));

    public ValueTask<D2Result<T?>> GetAsync<T>(
        string key, CancellationToken ct = default)
        => throw new NotImplementedException();

    public ValueTask<D2Result<IReadOnlyDictionary<string, T?>>> GetManyAsync<T>(
        IReadOnlyCollection<string> keys, CancellationToken ct = default)
        => throw new NotImplementedException();

    public ValueTask<D2Result> SetAsync<T>(
        string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
        => throw new NotImplementedException();

    public ValueTask<D2Result> SetManyAsync<T>(
        IReadOnlyDictionary<string, T> entries,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
        => throw new NotImplementedException();

    public ValueTask<D2Result> RemoveAsync(
        string key, CancellationToken ct = default)
        => throw new NotImplementedException();

    public ValueTask<D2Result> RemoveManyAsync(
        IReadOnlyCollection<string> keys, CancellationToken ct = default)
        => throw new NotImplementedException();

    public ValueTask<D2Result<TimeSpan?>> GetTtlAsync(
        string key, CancellationToken ct = default)
        => throw new NotImplementedException();

    public ValueTask<D2Result<long>> IncrementAsync(
        string key,
        long delta = 1,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
        => throw new NotImplementedException();

    public ValueTask<D2Result<bool>> SetNxAsync<T>(
        string key,
        T value,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
        => throw new NotImplementedException();

    public ValueTask<D2Result<bool>> AcquireLockAsync(
        string key, string token, TimeSpan ttl, CancellationToken ct = default)
        => throw new NotImplementedException();

    public ValueTask<D2Result> ReleaseLockAsync(
        string key, string token, CancellationToken ct = default)
        => throw new NotImplementedException();

    public ValueTask<D2Result> SetAndBroadcastAsync<T>(
        string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
        => throw new NotImplementedException();

    public ValueTask<D2Result> SetManyAndBroadcastAsync<T>(
        IReadOnlyDictionary<string, T> entries,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
        => throw new NotImplementedException();

    public ValueTask<D2Result> RemoveAndBroadcastAsync(
        string key, CancellationToken ct = default)
        => throw new NotImplementedException();

    public ValueTask<D2Result> RemoveManyAndBroadcastAsync(
        IReadOnlyCollection<string> keys, CancellationToken ct = default)
        => throw new NotImplementedException();
}
