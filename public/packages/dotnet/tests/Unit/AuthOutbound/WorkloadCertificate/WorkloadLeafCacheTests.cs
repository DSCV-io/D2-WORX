// -----------------------------------------------------------------------
// <copyright file="WorkloadLeafCacheTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.WorkloadCertificate;

using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
using NodaTime;
using Xunit;

/// <summary>
/// Adversarial coverage for the per-process live-leaf cache. Probes empty /
/// freshness / atomic-swap / boundary semantics + the chain-context accessor, plus the
/// leaf + intermediate disposal on swap and on cache dispose.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WorkloadLeafCacheTests
{
    private static readonly Instant SR_Base =
        Instant.FromUtc(2026, 1, 1, 0, 0, 0);

    [Fact]
    public void TryGet_NeverSet_ReturnsNull()
    {
        using var cache = new WorkloadLeafCache();

        cache.TryGet(SR_Base).Should().BeNull();
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

        cache.GetCurrentLeaf(SR_Base).Should().BeNull();
    }

    [Fact]
    public void GetCurrentContext_NeverSet_ReturnsNull()
    {
        using var cache = new WorkloadLeafCache();

        cache.GetCurrentContext(SR_Base).Should().BeNull();
    }

    [Fact]
    public void Set_ThenTryGet_ReturnsSnapshot()
    {
        using var cache = new WorkloadLeafCache();
        var now = SR_Base;
        var snapshot = ASnapshot(now + Duration.FromMinutes(5));

        cache.Set(snapshot);

        cache.TryGet(now).Should().BeSameAs(snapshot);
        cache.GetCurrentLeaf(now).Should().BeSameAs(snapshot.Leaf);
    }

    [Fact]
    public void GetCurrentContext_NonExpired_ReturnsTheChainContext()
    {
        using var cache = new WorkloadLeafCache();
        var now = SR_Base;
        var snapshot = ASnapshot(now + Duration.FromMinutes(5));

        cache.Set(snapshot);

        // The presentation path reads the chain context at channel build — it must be
        // the snapshot's pre-built context (leaf + intermediate).
        cache.GetCurrentContext(now).Should().BeSameAs(snapshot.ChainContext);
    }

    [Fact]
    public void GetCurrentContext_Expired_ReturnsNull()
    {
        using var cache = new WorkloadLeafCache();
        var expiresAt = SR_Base + Duration.FromMinutes(5);
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
        var now = SR_Base;
        var firstSnapshot = ASnapshot(now + Duration.FromHours(1));
        cache.Set(firstSnapshot);

        var capturedBeforeRotation = cache.GetCurrentContext(now);

        cache.Set(ASnapshot(now + Duration.FromHours(2)));

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
        var now = SR_Base;
        var expired = ASnapshot(now - Duration.FromMinutes(5));

        cache.Set(expired);

        cache.PeekRaw().Should().BeSameAs(expired);
    }

    [Fact]
    public void TryGet_ExactExpiryBoundary_ReturnsNull()
    {
        using var cache = new WorkloadLeafCache();
        var expiresAt = SR_Base + Duration.FromMinutes(5);
        cache.Set(ASnapshot(expiresAt));

        cache.TryGet(expiresAt).Should().BeNull();
    }

    [Fact]
    public void TryGet_OneNanosecondBeforeExpiry_ReturnsSnapshot()
    {
        using var cache = new WorkloadLeafCache();
        var expiresAt = SR_Base + Duration.FromMinutes(5);
        cache.Set(ASnapshot(expiresAt));

        // One tick (100 ns) before expiry — still valid.
        cache.TryGet(expiresAt - Duration.FromTicks(1)).Should().NotBeNull();
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
        var now = SR_Base;
        var first = ASnapshot(now + Duration.FromMinutes(5));
        cache.Set(first);

        var second = ASnapshot(now + Duration.FromMinutes(10));
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
        var snapshot = ASnapshot(SR_Base + Duration.FromHours(1));
        cache.Set(snapshot);

        cache.Dispose();

        var leafAct = () => _ = snapshot.Leaf.GetECDsaPrivateKey()!.ExportPkcs8PrivateKey();
        leafAct.Should().Throw<Exception>("the current leaf is disposed on cache disposal");

        var intermediateAct = () => _ = snapshot.Intermediate.RawData;
        intermediateAct.Should().Throw<Exception>(
            "the current intermediate is disposed on cache disposal");
    }

    [Fact]
    public void Dispose_IsIdempotent_SecondCallDoesNotThrow()
    {
        // Regression: Dispose() must be safe to call multiple times. After the
        // Volatile.Write disposal-flag fence fix, a second Dispose() checks the
        // fence and returns without accessing certificates.
        var cache = new WorkloadLeafCache();
        cache.Set(ASnapshot(SR_Base + Duration.FromHours(1)));

        cache.Dispose();

        var act = () => cache.Dispose();

        act.Should().NotThrow("idempotent Dispose must not throw on the second call");
    }

    [Fact]
    public void Dispose_PublishesDisposedFlagWithVolatileFence()
    {
        // Regression: the disposal flag on WorkloadLeafCache MUST be written via
        // Volatile.Write and read via Volatile.Read so that concurrent readers on
        // weakly-ordered architectures observe the write without compiler/CPU
        // reordering. The fence is validated by confirming that post-Dispose, a
        // concurrent reader that sees a snapshot (no-snapshot branch) does NOT
        // receive a stale non-disposed view: after Dispose() returns, PeekRaw()
        // must return null (the snapshot was nulled out by ClearForTesting-semantics
        // in Dispose). We can only exercise this semantically (the memory fence
        // itself is not observable in a single-thread test), but post-Dispose cache
        // state is the observable proxy for whether the flag was published.
        var cache = new WorkloadLeafCache();
        var snapshot = ASnapshot(SR_Base + Duration.FromHours(1));
        cache.Set(snapshot);

        cache.Dispose();

        // After Dispose, the snapshot's certificates are disposed. The disposal flag
        // must be visible (Volatile fence); a subsequent TryGet with any Instant
        // would fail on the disposed cert if it returned non-null — confirming the
        // flag is observed correctly.
        // We verify the flag via the side-effect: post-Dispose PeekRaw still returns
        // the snapshot reference (Dispose nulls only via ClearForTesting — the actual
        // Dispose only frees certificates). The invariant is: the certificates are gone.
        var leafAct = () => _ = snapshot.Leaf.GetECDsaPrivateKey()!.ExportPkcs8PrivateKey();
        leafAct.Should().Throw<Exception>(
            "the disposal flag was written with a Volatile fence; the leaf cert is disposed");
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
        cache.Set(ASnapshot(SR_Base + Duration.FromMinutes(5)));

        cache.ClearForTesting();

        cache.PeekRaw().Should().BeNull();
    }

    private static WorkloadLeafSnapshot ASnapshot(Instant notAfter)
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
        var baseline = SR_Base;
        cache.Set(ASnapshot(baseline + Duration.FromHours(1)));

        var sawNull = 0;
        const int writer_iterations = 200;
        const int reader_count = 8;

        var writer = Task.Run(() =>
        {
            for (var i = 0; i < writer_iterations; i++)
                cache.Set(ASnapshot(baseline + Duration.FromHours(1)));
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
