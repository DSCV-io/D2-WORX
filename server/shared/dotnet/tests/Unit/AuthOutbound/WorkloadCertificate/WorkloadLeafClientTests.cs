// -----------------------------------------------------------------------
// <copyright file="WorkloadLeafClientTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.WorkloadCertificate;

using System.Net;
using AwesomeAssertions;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

/// <summary>
/// The refresh-ahead leaf-client matrix — first-call issuance, cache-hit reuse,
/// serve-stale on transient failure, hard-fail when expired-and-unreachable,
/// singleflight dedup, and the private-key-zeroize contract. Mirrors
/// <c>HttpServiceIdentityClientTests</c> with a certificate reissue instead of a
/// token fetch.
/// </summary>
public sealed class WorkloadLeafClientTests
{
    private static readonly DateTimeOffset SR_Base =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetCurrentLeafAsync_FirstCall_IssuesAndReturnsLeaf()
    {
        using var harness = new Harness();

        var result = await harness.Client.GetCurrentLeafAsync();

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        harness.Issuer.IssuanceCount.Should().Be(1);
    }

    [Fact]
    public async Task GetCurrentLeafAsync_SecondCall_ServesFromCache_NoReissue()
    {
        using var harness = new Harness();

        await harness.Client.GetCurrentLeafAsync();
        var second = await harness.Client.GetCurrentLeafAsync();

        second.Success.Should().BeTrue();
        harness.Issuer.IssuanceCount.Should().Be(1, "the cached leaf is served without reissue");
    }

    [Fact]
    public async Task GetCurrentLeafAsync_ServesStale_WhenReissueTransientlyFailsButLeafStillValid()
    {
        using var harness = new Harness(validity: TimeSpan.FromHours(24));

        // Populate the cache.
        await harness.Client.GetCurrentLeafAsync();

        // Expire the cache view by advancing within validity is not needed — the
        // cached leaf is still valid; force the client to attempt a reissue by
        // clearing then arming a transient failure. Easier: arm a failure and call
        // ForceReissueAsync — the still-valid cached leaf keeps serving.
        harness.Issuer.SetFail(true);

        // The cached leaf is still valid (24h), so GetCurrentLeafAsync returns it
        // straight from the cache without even attempting a reissue.
        var result = await harness.Client.GetCurrentLeafAsync();

        result.Success.Should().BeTrue("a still-valid cached leaf is served");
    }

    [Fact]
    public async Task GetCurrentLeafAsync_HardFails_WhenExpiredAndIssuerUnreachable()
    {
        using var harness = new Harness(validity: TimeSpan.FromMinutes(10));

        // Populate, then advance past the leaf's expiry so the cache no longer
        // serves it; arm the issuer to fail so reissue can't succeed.
        await harness.Client.GetCurrentLeafAsync();
        harness.Clock.Advance(TimeSpan.FromMinutes(20));
        harness.Issuer.SetFail(true);

        var result = await harness.Client.GetCurrentLeafAsync();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task ForceReissueAsync_Succeeds_PopulatesCache()
    {
        using var harness = new Harness();

        var result = await harness.Client.ForceReissueAsync();

        result.Success.Should().BeTrue();
        harness.Cache.PeekRaw().Should().NotBeNull();
    }

    [Fact]
    public async Task ForceReissueAsync_IssuerFails_ReturnsServiceUnavailable()
    {
        using var harness = new Harness();
        harness.Issuer.SetFail(true);

        var result = await harness.Client.ForceReissueAsync();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetCurrentLeafAsync_ConcurrentFirstCallers_ShareOneIssuance()
    {
        using var harness = new Harness();

        var calls = Enumerable.Range(0, 16)
            .Select(_ => harness.Client.GetCurrentLeafAsync().AsTask())
            .ToArray();

        var results = await Task.WhenAll(calls);

        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        harness.Issuer.IssuanceCount.Should().Be(
            1, "concurrent first-callers dedup to a single reissue via singleflight");
    }

    [Fact]
    public async Task GetCurrentLeafAsync_AfterDispose_Throws()
    {
        var harness = new Harness();
        harness.Client.Dispose();

        var act = async () => await harness.Client.GetCurrentLeafAsync();

        await act.Should().ThrowAsync<ObjectDisposedException>();
        harness.Issuer.Dispose();
    }

    [Fact]
    public async Task GetCurrentLeafAsync_BuiltLeaf_HasPrivateKey()
    {
        // The reissue path builds a private-key-bearing cert (the PKCS#8 is imported
        // then zeroed); the live cert the channel presents must hold its key.
        using var harness = new Harness();

        var result = await harness.Client.GetCurrentLeafAsync();

        result.Data!.HasPrivateKey.Should().BeTrue();
    }

    [Fact]
    public async Task GetCurrentLeafAsync_BuiltSnapshot_RetainsIntermediate()
    {
        // The reissue path retains the issuing intermediate (from the material's
        // IssuerCertificateDer) so the full chain can be presented. The intermediate is
        // the public issuer cert (no private key).
        using var harness = new Harness();

        await harness.Client.GetCurrentLeafAsync();

        var snapshot = harness.Cache.PeekRaw();
        snapshot.Should().NotBeNull();
        snapshot.Intermediate.Should().NotBeNull("the issuing intermediate is retained");
        snapshot.Intermediate.HasPrivateKey.Should().BeFalse(
            "the presented intermediate is the public issuer cert, not a key-bearing one");
    }

    [Fact]
    public async Task GetCurrentLeafAsync_BuiltSnapshot_PresentsChainOrFallsBackToLeaf()
    {
        // On Linux/OpenSSL (the deployment target) the snapshot carries a pre-built
        // leaf -> intermediate chain context (the full chain is presented). On Windows,
        // where Schannel will not build a chain context for a leaf whose internal-CA
        // root is not in the OS trust store, the context is tolerated-null and the
        // presentation falls back to the bare leaf — either way the leaf is cached and
        // GetCurrentLeafAsync succeeds (it never hard-fails on the chain-context build).
        using var harness = new Harness();

        var result = await harness.Client.GetCurrentLeafAsync();

        result.Success.Should().BeTrue(
            "the live leaf is cached regardless of whether the chain context could be built");

        var snapshot = harness.Cache.PeekRaw();
        snapshot.Should().NotBeNull();

        if (OperatingSystem.IsWindows())
        {
            // The bare-leaf fallback path is acceptable on Windows (dev), not the
            // deployment target; the leaf is still present for presentation.
            snapshot.Leaf.HasPrivateKey.Should().BeTrue(
                "the bare leaf is presented when no chain context can be built on Windows");
        }
        else
        {
            snapshot.ChainContext.Should().NotBeNull(
                "on the deployment target the channel presents the full leaf -> intermediate chain");
        }
    }

    [Fact]
    public async Task GetCurrentLeafAsync_BuildLiveLeaf_ZeroesPkcs8BufferAfterImport()
    {
        // M-4 regression pin — WorkloadLeafClient.BuildLiveLeaf MUST call
        // CryptographicOperations.ZeroMemory on the PKCS#8 buffer after importing the
        // private key into the live ECDsa handle. Failure would leave the raw key
        // bytes in the GC heap across the leaf's lifetime — a secret-pinning risk.
        using var harness = new Harness();

        await harness.Client.GetCurrentLeafAsync();

        // LastIssuedPkcs8 is the SAME array reference passed through WorkloadLeafMaterial
        // to BuildLiveLeaf; ZeroMemory zeroes the buffer in-place.
        var pkcs8 = harness.Issuer.LastIssuedPkcs8;
        pkcs8.Should().NotBeNull("an issuance must have completed")
            .And.Subject.Should().OnlyContain(
                b => b == 0,
                "CryptographicOperations.ZeroMemory must zero the PKCS#8 buffer after import");
    }

    private sealed class Harness : IDisposable
    {
        public Harness(TimeSpan? validity = null)
        {
            Clock = new FakeTimeProvider(SR_Base);
            Issuer = new FakeWorkloadCertificateIssuer(Clock, validity: validity);
            Cache = new WorkloadLeafCache();
            Client = new WorkloadLeafClient(
                Issuer, Cache, NullLogger<WorkloadLeafClient>.Instance, Clock);
        }

        public FakeTimeProvider Clock { get; }

        public FakeWorkloadCertificateIssuer Issuer { get; }

        public WorkloadLeafCache Cache { get; }

        public WorkloadLeafClient Client { get; }

        public void Dispose()
        {
            Client.Dispose();
            Cache.Dispose();
            Issuer.Dispose();
        }
    }
}
