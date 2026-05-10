// -----------------------------------------------------------------------
// <copyright file="ServiceIdentityCacheTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.ServiceIdentity;

using AwesomeAssertions;
using D2.Shared.Auth.Outbound.ServiceIdentity;
using Xunit;

/// <summary>
/// Adversarial coverage for the per-process service-identity token cache.
/// Probes empty / freshness / atomic-swap / boundary semantics.
/// </summary>
public sealed class ServiceIdentityCacheTests
{
    // ----------------------------------------------------------------------
    // Empty / not-yet-set
    // ----------------------------------------------------------------------

    [Fact]
    public void TryGet_NeverSet_ReturnsNull()
    {
        var cache = new ServiceIdentityCache();

        cache.TryGet(DateTimeOffset.UtcNow).Should().BeNull();
    }

    [Fact]
    public void PeekRaw_NeverSet_ReturnsNull()
    {
        var cache = new ServiceIdentityCache();

        cache.PeekRaw().Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // Set + read happy path
    // ----------------------------------------------------------------------

    [Fact]
    public void Set_ThenTryGet_ReturnsSnapshot()
    {
        var cache = new ServiceIdentityCache();
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var snapshot = new ServiceIdentitySnapshot("token-1", now.AddMinutes(5));

        cache.Set(snapshot);

        cache.TryGet(now).Should().BeSameAs(snapshot);
    }

    [Fact]
    public void Set_ThenPeekRaw_ReturnsSnapshotEvenIfExpired()
    {
        // PeekRaw is the introspection path used by the refresh hosted
        // service to decide whether refresh is due — it MUST surface
        // expired snapshots so the service can detect "we have a stale
        // value and need a fresh one."
        var cache = new ServiceIdentityCache();
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var expired = new ServiceIdentitySnapshot("expired-token", now.AddMinutes(-5));

        cache.Set(expired);

        cache.PeekRaw().Should().BeSameAs(expired);
    }

    [Fact]
    public void Set_ReplacesPriorSnapshot()
    {
        var cache = new ServiceIdentityCache();
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        cache.Set(new("first", now.AddMinutes(5)));

        var newSnapshot = new ServiceIdentitySnapshot("second", now.AddMinutes(10));
        cache.Set(newSnapshot);

        cache.TryGet(now).Should().BeSameAs(newSnapshot);
    }

    // ----------------------------------------------------------------------
    // Expiry semantics
    // ----------------------------------------------------------------------

    [Fact]
    public void TryGet_ExpiredSnapshot_ReturnsNull()
    {
        var cache = new ServiceIdentityCache();
        var snapshotTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        cache.Set(new("token", snapshotTime.AddMinutes(5)));

        var afterExpiry = snapshotTime.AddMinutes(10);

        cache.TryGet(afterExpiry).Should().BeNull();
    }

    [Fact]
    public void TryGet_ExactExpiryBoundary_ReturnsNull()
    {
        // Adversarial: TryGet uses strict > comparison. When now == ExpiresAt,
        // the token is treated as expired. Documents the intended boundary so a
        // future refactor doesn't silently shift to >=.
        var cache = new ServiceIdentityCache();
        var expiresAt = new DateTimeOffset(2026, 1, 1, 0, 5, 0, TimeSpan.Zero);
        cache.Set(new("token", expiresAt));

        cache.TryGet(expiresAt).Should().BeNull();
    }

    [Fact]
    public void TryGet_OneTickBeforeExpiry_ReturnsSnapshot()
    {
        var cache = new ServiceIdentityCache();
        var expiresAt = new DateTimeOffset(2026, 1, 1, 0, 5, 0, TimeSpan.Zero);
        cache.Set(new("token", expiresAt));

        cache.TryGet(expiresAt.AddTicks(-1)).Should().NotBeNull();
    }

    // ----------------------------------------------------------------------
    // Null guard
    // ----------------------------------------------------------------------

    [Fact]
    public void Set_NullSnapshot_Throws()
    {
        var cache = new ServiceIdentityCache();

        var act = () => cache.Set(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ----------------------------------------------------------------------
    // Concurrency — atomic swap
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ConcurrentReadersAndWriters_NeverObserveTornState()
    {
        // Adversarial: the entire purpose of the volatile-ref design is that
        // readers see EITHER an old snapshot OR a new snapshot, never a
        // partial / null-during-swap state. Hammer the cache with concurrent
        // writers + readers; verify every reader saw a non-null snapshot.
        var cache = new ServiceIdentityCache();
        var baseline = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        cache.Set(new("seed", baseline.AddHours(1)));

        var sawNull = 0;
        const int writerIterations = 1000;
        const int readerCount = 8;

        var writer = Task.Run(() =>
        {
            for (var i = 0; i < writerIterations; i++)
                cache.Set(new($"token-{i}", baseline.AddHours(1)));
        });

        var readers = Enumerable.Range(0, readerCount).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < writerIterations; i++)
            {
                var snap = cache.TryGet(baseline);
                if (snap is null)
                    Interlocked.Increment(ref sawNull);
            }
        })).ToArray();

        await Task.WhenAll(readers.Concat([writer]));

        sawNull.Should().Be(0);
    }

    // ----------------------------------------------------------------------
    // Test seam
    // ----------------------------------------------------------------------

    [Fact]
    public void ClearForTesting_AfterSet_DropsSnapshot()
    {
        var cache = new ServiceIdentityCache();
        cache.Set(new("token", DateTimeOffset.UtcNow.AddMinutes(5)));

        cache.ClearForTesting();

        cache.PeekRaw().Should().BeNull();
        cache.TryGet(DateTimeOffset.UtcNow).Should().BeNull();
    }
}
