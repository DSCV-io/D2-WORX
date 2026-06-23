// -----------------------------------------------------------------------
// <copyright file="WorkloadLeafRefreshHostedServiceTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.WorkloadCertificate;

using AwesomeAssertions;
using D2.Shared.Auth.Outbound;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

/// <summary>
/// Background-service coverage for <see cref="WorkloadLeafRefreshHostedService"/>:
/// startup acquisition, the reissue-due (refresh-ahead) loop, transient-failure
/// survival, and clean cancellation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WorkloadLeafRefreshHostedServiceTests
{
    private static readonly DateTimeOffset SR_Base =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsync_StartupAcquireSucceeds_PopulatesCache()
    {
        await using var harness = new Harness();

        using var cts = new CancellationTokenSource();
        var task = harness.Service.StartAsync(cts.Token);

        // Await the deterministic "cache was written" signal — no wall-clock polling.
        await harness.WaitForCacheSetAsync();

        await cts.CancelAsync();
        await harness.Service.StopAsync(CancellationToken.None);
        await task;

        harness.Cache.PeekRaw().Should().NotBeNull();
        harness.Issuer.IssuanceCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ExecuteAsync_StartupAcquireFails_DoesNotThrow_LoopSurvives()
    {
        await using var harness = new Harness();
        harness.Issuer.SetFail(true);

        using var cts = new CancellationTokenSource();
        var task = harness.Service.StartAsync(cts.Token);

        // Wait until the loop is up (ExecuteTask assigned), then confirm it neither
        // populated the cache (the issuer fails) nor died — it must keep polling.
        // Bounded iteration + Task.Yield() — no wall-clock deadline.
        for (var i = 0; i < 500 && harness.Service.ExecuteTask is null; i++)
            await Task.Yield();

        harness.Service.ExecuteTask.Should().NotBeNull(
            "ExecuteAsync must be running by now");

        harness.Cache.PeekRaw().Should().BeNull();
        harness.Service.ExecuteTask!.IsFaulted.Should().BeFalse(
            "a failed startup acquire is logged + swallowed, never propagated");
        harness.Service.ExecuteTask!.IsCompleted.Should().BeFalse(
            "the loop survives the failed startup acquire and keeps polling");

        await cts.CancelAsync();
        await harness.Service.StopAsync(CancellationToken.None);
        await task;
    }

    [Fact]
    public async Task ExecuteAsync_ReissuesBeforeExpiry_WhenWithinLeadTime()
    {
        // Leaf TTL 10 min, lead-time 5 min. After startup the cache holds a leaf
        // valid for 10 min; advance to within the 5-min lead window → the next tick
        // reissues (a second issuance).
        await using var harness = new Harness(
            validity: TimeSpan.FromMinutes(10),
            leadTime: TimeSpan.FromMinutes(5));

        using var cts = new CancellationTokenSource();
        var task = harness.Service.StartAsync(cts.Token);

        await harness.WaitForCacheSetAsync();
        var countAfterStartup = harness.Issuer.IssuanceCount;

        // Drive the loop's FakeTimeProvider-based poll: advance into the lead-time
        // window (10 - 6 = 4 ≤ 5) and keep nudging the clock past successive poll
        // intervals until the proactive reissue fires (robust against the
        // advance-vs-delay-registration race a single advance is subject to).
        await harness.AdvanceUntilAsync(
            () => harness.Issuer.IssuanceCount > countAfterStartup,
            firstAdvance: TimeSpan.FromMinutes(6),
            nudge: TimeSpan.FromSeconds(31));

        harness.Issuer.IssuanceCount.Should().BeGreaterThan(
            countAfterStartup, "the leaf is reissued ahead of expiry");

        await cts.CancelAsync();
        await harness.Service.StopAsync(CancellationToken.None);
        await task;
    }

    [Fact]
    public async Task ExecuteAsync_NotDueYet_DoesNotReissueOnTick()
    {
        // Leaf TTL 24h, lead-time 5 min. After startup, a 1-min advance is nowhere
        // near the lead window → no reissue on the tick.
        await using var harness = new Harness(
            validity: TimeSpan.FromHours(24),
            leadTime: TimeSpan.FromMinutes(5));

        using var cts = new CancellationTokenSource();
        var task = harness.Service.StartAsync(cts.Token);

        await harness.WaitForCacheSetAsync();
        var countAfterStartup = harness.Issuer.IssuanceCount;

        // Fire several poll ticks well short of the lead window (advance 31s ×3 ≈
        // 93s ≪ the 24h-minus-5min reissue threshold) and confirm none reissue.
        // Each advance fires any FakeTimeProvider delay the loop has registered;
        // Task.Yield() between advances lets the loop process the fired timer.
        for (var i = 0; i < 3; i++)
        {
            harness.Clock.Advance(TimeSpan.FromSeconds(31));
            await Task.Yield();
        }

        // One final yield to let any in-progress tick complete.
        await Task.Yield();

        harness.Issuer.IssuanceCount.Should().Be(
            countAfterStartup, "reissue is not due — the leaf has 24h left");

        await cts.CancelAsync();
        await harness.Service.StopAsync(CancellationToken.None);
        await task;
    }

    [Fact]
    public async Task ExecuteAsync_TickFailureSwallowed_LoopSurvives()
    {
        await using var harness = new Harness(
            validity: TimeSpan.FromMinutes(10),
            leadTime: TimeSpan.FromMinutes(5));

        using var cts = new CancellationTokenSource();
        var task = harness.Service.StartAsync(cts.Token);

        await harness.WaitForCacheSetAsync();
        var countAfterStartup = harness.Issuer.IssuanceCount;

        // Arm a failure, then drive the poll into the reissue window so a failing
        // tick actually runs (its reissue attempt increments the issuer count), and
        // confirm the loop neither faulted nor completed — the failure is swallowed.
        harness.Issuer.SetFail(true);
        await harness.AdvanceUntilAsync(
            () => harness.Issuer.IssuanceCount > countAfterStartup,
            firstAdvance: TimeSpan.FromMinutes(6),
            nudge: TimeSpan.FromSeconds(31));

        harness.Service.ExecuteTask!.IsFaulted.Should().BeFalse(
            "a failed reissue tick is logged + swallowed, never propagated");
        harness.Service.ExecuteTask!.IsCompleted.Should().BeFalse(
            "the loop survives the failed tick and keeps polling");

        await cts.CancelAsync();
        await harness.Service.StopAsync(CancellationToken.None);
        await task;
    }

    [Fact]
    public async Task ExecuteAsync_CancellationDuringLoop_StopsCleanly()
    {
        await using var harness = new Harness();

        using var cts = new CancellationTokenSource();
        var task = harness.Service.StartAsync(cts.Token);

        await harness.WaitForCacheSetAsync();

        await cts.CancelAsync();
        await harness.Service.StopAsync(CancellationToken.None);
        await task;

        harness.Service.ExecuteTask!.IsCompletedSuccessfully.Should().BeTrue();
    }

    private sealed class Harness : IAsyncDisposable
    {
        // Signals once every time WorkloadLeafCache.Set() completes.
        private readonly SemaphoreSlim _cacheSetSignal = new(0);

        public Harness(TimeSpan? validity = null, TimeSpan? leadTime = null)
        {
            Clock = new FakeTimeProvider(SR_Base);
            Issuer = new FakeWorkloadCertificateIssuer(Clock, validity: validity);
            Cache = new WorkloadLeafCache();

            // Wire the deterministic cache-written signal — no wall-clock polling.
            Cache.OnSetForTesting = () => _cacheSetSignal.Release();

            var options = Options.Create(new AuthOutboundOptions
            {
                WorkloadLeafRefreshLeadTime = leadTime ?? TimeSpan.FromMinutes(5),
            });
            Client = new WorkloadLeafClient(
                Issuer, Cache, NullLogger<WorkloadLeafClient>.Instance, Clock);
            Service = new WorkloadLeafRefreshHostedService(
                Client,
                Cache,
                options,
                NullLogger<WorkloadLeafRefreshHostedService>.Instance,
                Clock);
        }

        public FakeTimeProvider Clock { get; }

        public FakeWorkloadCertificateIssuer Issuer { get; }

        public WorkloadLeafCache Cache { get; }

        public WorkloadLeafClient Client { get; }

        public WorkloadLeafRefreshHostedService Service { get; }

        /// <summary>
        /// Awaits the next <see cref="WorkloadLeafCache.Set"/> completion.
        /// Deterministic — driven by the production code's write, not a wall-clock
        /// poll. Times out only if the SUT never calls Set (a true defect).
        /// </summary>
        public Task WaitForCacheSetAsync()
            => _cacheSetSignal.WaitAsync(TimeSpan.FromSeconds(10));

        /// <summary>
        /// Advances the fake clock until <paramref name="condition"/> is true.
        /// Bounded by iteration count (not wall-clock) — load-independent.
        /// First advance jumps into the target window; subsequent nudges fire
        /// successive FakeTimeProvider timers until the condition is observed.
        /// </summary>
        public async Task AdvanceUntilAsync(
            Func<bool> condition,
            TimeSpan firstAdvance,
            TimeSpan nudge,
            int maxIterations = 100)
        {
            // Yield once so the loop registers its poll delay before we advance.
            await Task.Yield();
            Clock.Advance(firstAdvance);

            for (var i = 0; i < maxIterations; i++)
            {
                if (condition())
                    return;

                await Task.Yield();
                Clock.Advance(nudge);
            }

            condition().Should().BeTrue("condition was not met within the iteration budget");
        }

        public async ValueTask DisposeAsync()
        {
            Service.Dispose();
            Client.Dispose();
            Cache.Dispose();
            Issuer.Dispose();
            _cacheSetSignal.Dispose();
            await ValueTask.CompletedTask;
        }
    }
}
