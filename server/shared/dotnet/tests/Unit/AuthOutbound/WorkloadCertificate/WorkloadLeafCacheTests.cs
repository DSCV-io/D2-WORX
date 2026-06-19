// -----------------------------------------------------------------------
// <copyright file="WorkloadLeafCacheTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.WorkloadCertificate;

using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
using Xunit;

/// <summary>
/// Adversarial coverage for the per-process live-leaf cache. Probes empty /
/// freshness / atomic-swap / boundary semantics + the chain-context accessor — mirrors
/// <c>ServiceIdentityCacheTests</c>, with the leaf + intermediate disposal-on-swap
/// addition.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WorkloadLeafCacheTests
{
    [Fact]
    public void TryGet_NeverSet_ReturnsNull()
    {
        using var cache = new WorkloadLeafCache();

        cache.TryGet(DateTimeOffset.UtcNow).Should().BeNull();
    }

    [Fact]
    public void PeekRaw_NeverSet_ReturnsNull()
    {
        using var cache = new WorkloadLeafCache();

        cache.PeekRaw().Should().BeNull();
    }

    [Fact]
    public void GetCurrentLeaf_NeverSet_ReturnsNull()
    {
        using var cache = new WorkloadLeafCache();

        cache.GetCurrentLeaf(DateTimeOffset.UtcNow).Should().BeNull();
    }

    [Fact]
    public void GetCurrentContext_NeverSet_ReturnsNull()
    {
        using var cache = new WorkloadLeafCache();

        cache.GetCurrentContext(DateTimeOffset.UtcNow).Should().BeNull();
    }

    [Fact]
    public void Set_ThenTryGet_ReturnsSnapshot()
    {
        using var cache = new WorkloadLeafCache();
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var snapshot = ASnapshot(now.AddMinutes(5));

        cache.Set(snapshot);

        cache.TryGet(now).Should().BeSameAs(snapshot);
        cache.GetCurrentLeaf(now).Should().BeSameAs(snapshot.Leaf);
    }

    [Fact]
    public void GetCurrentContext_NonExpired_ReturnsTheChainContext()
    {
        using var cache = new WorkloadLeafCache();
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var snapshot = ASnapshot(now.AddMinutes(5));

        cache.Set(snapshot);

        // The presentation path reads the chain context at channel build — it must be
        // the snapshot's pre-built context (leaf + intermediate).
        cache.GetCurrentContext(now).Should().BeSameAs(snapshot.ChainContext);
    }

    [Fact]
    public void GetCurrentContext_Expired_ReturnsNull()
    {
        using var cache = new WorkloadLeafCache();
        var expiresAt = new DateTimeOffset(2026, 1, 1, 0, 5, 0, TimeSpan.Zero);
        cache.Set(ASnapshot(expiresAt));

        // An expired leaf must not be presented — the context accessor returns null,
        // so the channel presents no client cert (fail-closed).
        cache.GetCurrentContext(expiresAt).Should().BeNull();
    }

    [Fact]
    public void GetCurrentContext_AfterRotation_ReturnsTheNewContext_PriorReferenceUnaffected()
    {
        // Rotation semantics: a channel built BEFORE the rotation captured the old
        // chain context (it holds its own reference, which is unaffected by the swap);
        // a channel built AFTER the rotation reads the NEW context from the cache.
        // This is exactly why a long-lived channel must be rebuilt to adopt a rotated
        // leaf — the cache currency only reaches channels built after the swap.
        using var cache = new WorkloadLeafCache();
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var firstSnapshot = ASnapshot(now.AddHours(1));
        cache.Set(firstSnapshot);

        var capturedBeforeRotation = cache.GetCurrentContext(now);

        cache.Set(ASnapshot(now.AddHours(2)));

        var readAfterRotation = cache.GetCurrentContext(now);

        readAfterRotation.Should().NotBeSameAs(
            capturedBeforeRotation,
            "a channel built after rotation reads the freshly-issued chain context");
        capturedBeforeRotation.Should().BeSameAs(
            firstSnapshot.ChainContext,
            "a reference captured at an earlier channel build is not retroactively swapped");
    }

    [Fact]
    public void Set_ThenPeekRaw_ReturnsSnapshotEvenIfExpired()
    {
        using var cache = new WorkloadLeafCache();
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var expired = ASnapshot(now.AddMinutes(-5));

        cache.Set(expired);

        cache.PeekRaw().Should().BeSameAs(expired);
    }

    [Fact]
    public void TryGet_ExactExpiryBoundary_ReturnsNull()
    {
        using var cache = new WorkloadLeafCache();
        var expiresAt = new DateTimeOffset(2026, 1, 1, 0, 5, 0, TimeSpan.Zero);
        cache.Set(ASnapshot(expiresAt));

        cache.TryGet(expiresAt).Should().BeNull();
    }

    [Fact]
    public void TryGet_OneTickBeforeExpiry_ReturnsSnapshot()
    {
        using var cache = new WorkloadLeafCache();
        var expiresAt = new DateTimeOffset(2026, 1, 1, 0, 5, 0, TimeSpan.Zero);
        cache.Set(ASnapshot(expiresAt));

        cache.TryGet(expiresAt.AddTicks(-1)).Should().NotBeNull();
    }

    [Fact]
    public void Set_NullSnapshot_Throws()
    {
        using var cache = new WorkloadLeafCache();

        AssertSetNullThrows(cache);
    }

    [Fact]
    public void Set_ReplacesPriorSnapshot_AndDisposesSupersededLeafAndIntermediate()
    {
        using var cache = new WorkloadLeafCache();
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var first = ASnapshot(now.AddMinutes(5));
        cache.Set(first);

        var second = ASnapshot(now.AddMinutes(10));
        cache.Set(second);

        cache.TryGet(now).Should().BeSameAs(second);

        // The superseded leaf is disposed on swap — touching its key throws.
        var leafAct = () => _ = first.Leaf.GetECDsaPrivateKey()!.ExportPkcs8PrivateKey();
        leafAct.Should().Throw<Exception>("the superseded leaf is disposed on swap");

        // The superseded intermediate is disposed on swap too (no handle leak).
        var intermediateAct = () => _ = first.Intermediate.RawData;
        intermediateAct.Should().Throw<Exception>(
            "the superseded intermediate is disposed on swap");
    }

    [Fact]
    public void Dispose_DisposesCurrentLeafAndIntermediate()
    {
        var cache = new WorkloadLeafCache();
        var snapshot = ASnapshot(DateTimeOffset.UtcNow.AddHours(1));
        cache.Set(snapshot);

        cache.Dispose();

        var leafAct = () => _ = snapshot.Leaf.GetECDsaPrivateKey()!.ExportPkcs8PrivateKey();
        leafAct.Should().Throw<Exception>("the current leaf is disposed on cache disposal");

        var intermediateAct = () => _ = snapshot.Intermediate.RawData;
        intermediateAct.Should().Throw<Exception>(
            "the current intermediate is disposed on cache disposal");
    }

    [Fact]
    public async Task ConcurrentReadersAndWriters_NeverObserveTornState()
    {
        using var cache = new WorkloadLeafCache();

        var sawNull = await HammerConcurrently(cache);

        sawNull.Should().Be(0);
    }

    [Fact]
    public void ClearForTesting_AfterSet_DropsSnapshot()
    {
        using var cache = new WorkloadLeafCache();
        cache.Set(ASnapshot(DateTimeOffset.UtcNow.AddMinutes(5)));

        cache.ClearForTesting();

        cache.PeekRaw().Should().BeNull();
    }

    private static WorkloadLeafSnapshot ASnapshot(DateTimeOffset notAfter)
    {
        var leaf = ASelfSignedCert();
        var intermediate = ASelfSignedCert();
        var context = SslStreamCertificateContext.Create(leaf, [intermediate], offline: true);

        return new WorkloadLeafSnapshot(leaf, intermediate, context, notAfter);
    }

    private static X509Certificate2 ASelfSignedCert()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=leaf", key, HashAlgorithmName.SHA256);
        var now = DateTimeOffset.UtcNow;

        return request.CreateSelfSigned(now.AddMinutes(-5), now.AddHours(24));
    }

    // The cache is a method PARAMETER here (its disposal is the caller's concern),
    // so the lambdas below capture a parameter rather than a same-method disposed
    // local — keeping the capturing work out of a using-scope.
    private static void AssertSetNullThrows(WorkloadLeafCache cache)
    {
        var act = () => cache.Set(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static async Task<int> HammerConcurrently(WorkloadLeafCache cache)
    {
        var baseline = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        cache.Set(ASnapshot(baseline.AddHours(1)));

        var sawNull = 0;
        const int writer_iterations = 200;
        const int reader_count = 8;

        var writer = Task.Run(() =>
        {
            for (var i = 0; i < writer_iterations; i++)
                cache.Set(ASnapshot(baseline.AddHours(1)));
        });

        var readers = Enumerable.Range(0, reader_count).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < writer_iterations; i++)
            {
                var snap = cache.TryGet(baseline);
                if (snap is null)
                    Interlocked.Increment(ref sawNull);
            }
        })).ToArray();

        await Task.WhenAll(readers.Concat([writer]));

        return sawNull;
    }
}
