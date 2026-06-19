// -----------------------------------------------------------------------
// <copyright file="WorkloadLeafCacheTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.WorkloadCertificate;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
using Xunit;

/// <summary>
/// Adversarial coverage for the per-process live-leaf cache. Probes empty /
/// freshness / atomic-swap / boundary semantics — mirrors
/// <c>ServiceIdentityCacheTests</c>, with the cert-disposal-on-swap addition.
/// </summary>
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
    public void Set_ThenTryGet_ReturnsSnapshot()
    {
        using var cache = new WorkloadLeafCache();
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var snapshot = new WorkloadLeafSnapshot(ASelfSignedCert(), now.AddMinutes(5));

        cache.Set(snapshot);

        cache.TryGet(now).Should().BeSameAs(snapshot);
        cache.GetCurrentLeaf(now).Should().BeSameAs(snapshot.Leaf);
    }

    [Fact]
    public void Set_ThenPeekRaw_ReturnsSnapshotEvenIfExpired()
    {
        using var cache = new WorkloadLeafCache();
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var expired = new WorkloadLeafSnapshot(ASelfSignedCert(), now.AddMinutes(-5));

        cache.Set(expired);

        cache.PeekRaw().Should().BeSameAs(expired);
    }

    [Fact]
    public void TryGet_ExactExpiryBoundary_ReturnsNull()
    {
        using var cache = new WorkloadLeafCache();
        var expiresAt = new DateTimeOffset(2026, 1, 1, 0, 5, 0, TimeSpan.Zero);
        cache.Set(new WorkloadLeafSnapshot(ASelfSignedCert(), expiresAt));

        cache.TryGet(expiresAt).Should().BeNull();
    }

    [Fact]
    public void TryGet_OneTickBeforeExpiry_ReturnsSnapshot()
    {
        using var cache = new WorkloadLeafCache();
        var expiresAt = new DateTimeOffset(2026, 1, 1, 0, 5, 0, TimeSpan.Zero);
        cache.Set(new WorkloadLeafSnapshot(ASelfSignedCert(), expiresAt));

        cache.TryGet(expiresAt.AddTicks(-1)).Should().NotBeNull();
    }

    [Fact]
    public void Set_NullSnapshot_Throws()
    {
        using var cache = new WorkloadLeafCache();

        AssertSetNullThrows(cache);
    }

    [Fact]
    public void Set_ReplacesPriorSnapshot_AndDisposesSupersededLeaf()
    {
        using var cache = new WorkloadLeafCache();
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var first = ASelfSignedCert();
        cache.Set(new WorkloadLeafSnapshot(first, now.AddMinutes(5)));

        var second = new WorkloadLeafSnapshot(ASelfSignedCert(), now.AddMinutes(10));
        cache.Set(second);

        cache.TryGet(now).Should().BeSameAs(second);

        // The superseded leaf is disposed on swap — touching its key throws.
        var act = () => _ = first.GetECDsaPrivateKey()!.ExportPkcs8PrivateKey();
        act.Should().Throw<Exception>("the superseded leaf is disposed on swap");
    }

    [Fact]
    public void Dispose_DisposesCurrentLeaf()
    {
        var cache = new WorkloadLeafCache();
        var leaf = ASelfSignedCert();
        cache.Set(new WorkloadLeafSnapshot(leaf, DateTimeOffset.UtcNow.AddHours(1)));

        cache.Dispose();

        var act = () => _ = leaf.GetECDsaPrivateKey()!.ExportPkcs8PrivateKey();
        act.Should().Throw<Exception>("the current leaf is disposed on cache disposal");
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
        cache.Set(new WorkloadLeafSnapshot(ASelfSignedCert(), DateTimeOffset.UtcNow.AddMinutes(5)));

        cache.ClearForTesting();

        cache.PeekRaw().Should().BeNull();
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
        cache.Set(new WorkloadLeafSnapshot(ASelfSignedCert(), baseline.AddHours(1)));

        var sawNull = 0;
        const int writer_iterations = 200;
        const int reader_count = 8;

        var writer = Task.Run(() =>
        {
            for (var i = 0; i < writer_iterations; i++)
                cache.Set(new WorkloadLeafSnapshot(ASelfSignedCert(), baseline.AddHours(1)));
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
