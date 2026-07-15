// -----------------------------------------------------------------------
// <copyright file="WorkloadLeafRefreshHostedServiceTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.WorkloadCertificate;

using AwesomeAssertions;
using D2.Shared.Auth.Outbound;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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

        // Await the deterministic "cache was written" signal — no polling.
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

        // Wait until the startup IssueAsync call completes — this is the startup
        // acquire attempt (count will be 1). The issuer signals on every IssueAsync
        // invocation so this is deterministic regardless of scheduler pressure.
        await harness.Issuer.WaitForInvocationCountAsync(1);

        // Give the hosted service one yield to process the result and enter the loop.
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
        // Leaf TTL 10 min, lead-time 5 min. After startup the cache holds a leaf valid
        // for 10 min; as the fake clock advances the loop reissues the aging leaf (a
        // second issuance), keeping the cache populated.
        await using var harness = new Harness(
            validity: TimeSpan.FromMinutes(10),
            leadTime: TimeSpan.FromMinutes(5));

        using var cts = new CancellationTokenSource();
        var task = harness.Service.StartAsync(cts.Token);

        // Wait for startup issuance (count = 1).
        await harness.Issuer.WaitForInvocationCountAsync(1);
        var countAfterStartup = harness.Issuer.IssuanceCount;

        // Drive the poll loop deterministically until the aging leaf is reissued. The
        // driver advances the fake clock exactly once per completed poll tick (paced by
        // the loop registering its next Task.Delay), so the drive never races the
        // thread-pool scheduler. A 6-min nudge walks the leaf from inside the lead-time
        // window (tick 1: 4 min left ≤ 5-min lead) across expiry (tick 2), where the
        // reissue fires — no wall-clock deadline, no free-running nudger.
        await harness.AdvanceUntilIssuerCountAsync(
            targetCount: countAfterStartup + 1,
            nudge: TimeSpan.FromMinutes(6));

        harness.Issuer.IssuanceCount.Should().BeGreaterThan(
            countAfterStartup, "the leaf is reissued as it ages toward expiry");

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

        // Wait for startup issuance (count = 1) before sampling the baseline.
        await harness.Issuer.WaitForInvocationCountAsync(1);
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

        // Wait for startup issuance (count = 1).
        await harness.Issuer.WaitForInvocationCountAsync(1);
        var countAfterStartup = harness.Issuer.IssuanceCount;

        // Arm a failure, then drive the poll into the reissue window. The issuer is
        // invoked even on failure — its invocation signal is the gate. No cache write
        // occurs on failure, so this test does NOT rely on WaitForCacheSetAsync;
        // instead it awaits the issuer-call count which increments regardless of
        // success/failure. This makes the test deterministic under any scheduler load.
        harness.Issuer.SetFail(true);

        // Drive the poll loop deterministically across the leaf's expiry (as in
        // ExecuteAsync_ReissuesBeforeExpiry). The reissue tick invokes the issuer even
        // though the armed failure makes it return ServiceUnavailable — the invocation
        // count increments regardless, so the drive stays fully deterministic.
        await harness.AdvanceUntilIssuerCountAsync(
            targetCount: countAfterStartup + 1,
            nudge: TimeSpan.FromMinutes(6));

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
            Clock = new DrivableFakeTimeProvider(SR_Base);
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

        public DrivableFakeTimeProvider Clock { get; }

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
            => _cacheSetSignal.WaitAsync(TimeSpan.FromSeconds(30));

        /// <summary>
        /// Advances the fake clock — one poll tick at a time — until the issuer has been
        /// invoked at least <paramref name="targetCount"/> times, then returns. The drive
        /// is fully DETERMINISTIC and starvation-proof: it advances the clock EXACTLY once
        /// per poll timer the background loop registers, so the clock can never race ahead
        /// of the loop and the drive never depends on the thread-pool scheduler
        /// co-scheduling a concurrent nudger. There is NO wall-clock deadline anywhere on
        /// this path.
        /// </summary>
        /// <remarks>
        /// The background loop registers exactly one poll timer per iteration
        /// (<c>Task.Delay(pollInterval, clock, …)</c>), and it registers the NEXT timer
        /// only AFTER the prior tick has fully completed. <see cref="DrivableFakeTimeProvider"/>
        /// raises a permit on every registration, so this loop parks on that permit, then
        /// fires the just-registered timer with a single advance — one advance per completed
        /// tick, never more. A saturated thread pool merely delays the park signal; it can
        /// never produce a spurious advance, a lost tick, or a false timeout (the failure
        /// mode of the previous free-running concurrent nudger, whose progress hinged on the
        /// scheduler and could trip under full-suite load). The nudge budget is a
        /// genuine-stuck guard ONLY: a healthy loop reissues within a couple of ticks —
        /// unreachably far below the budget — so it trips only when the loop ticks forever
        /// WITHOUT ever reissuing (a real defect), surfaced as a fast, explicit failure.
        /// </remarks>
        /// <param name="targetCount">The cumulative issuer-invocation count to reach.</param>
        /// <param name="nudge">
        /// The clock increment per tick. Must exceed the loop's poll interval so a single
        /// advance fires the registered poll delay.
        /// </param>
        /// <param name="maxNudges">Count-based safety budget for a stuck SUT (see remarks).</param>
        public async Task AdvanceUntilIssuerCountAsync(
            int targetCount,
            TimeSpan nudge,
            int maxNudges = 100_000)
        {
            // Fast-path: already there (e.g. the caller advanced before invoking).
            if (Issuer.IssuanceCount >= targetCount)
                return;

            for (var nudged = 0; nudged < maxNudges; nudged++)
            {
                // Park until the loop has registered its next poll delay — which happens
                // only after the prior tick fully completed, so on return the issuer count
                // already reflects that tick. Buffered, so a registration that raced ahead
                // of this wait is not missed. No wall-clock deadline.
                await Clock.WaitForTimerRegisteredAsync();

                if (Issuer.IssuanceCount >= targetCount)
                    return;

                // Fire that poll timer; the loop wakes, re-checks reissue-due, and — once
                // the aging leaf is due — invokes the issuer, incrementing the count.
                Clock.Advance(nudge);
            }

            if (Issuer.IssuanceCount >= targetCount)
                return;

            throw new InvalidOperationException(
                $"Issuer invocation count {Issuer.IssuanceCount} did not reach {targetCount} "
                + $"after {maxNudges} deterministic poll ticks — the background reissue loop appears stuck.");
        }

        public async ValueTask DisposeAsync()
        {
            Service.Dispose();
            Client.Dispose();
            Cache.Dispose();
            Issuer.Dispose();
            Clock.Dispose();
            _cacheSetSignal.Dispose();
            await ValueTask.CompletedTask;
        }
    }
}
