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
        // Leaf TTL 10 min, lead-time 5 min. After startup the cache holds a leaf
        // valid for 10 min; advance to within the 5-min lead window → the next tick
        // reissues (a second issuance).
        await using var harness = new Harness(
            validity: TimeSpan.FromMinutes(10),
            leadTime: TimeSpan.FromMinutes(5));

        using var cts = new CancellationTokenSource();
        var task = harness.Service.StartAsync(cts.Token);

        // Wait for startup issuance (count = 1).
        await harness.Issuer.WaitForInvocationCountAsync(1);
        var countAfterStartup = harness.Issuer.IssuanceCount;

        // Advance the fake clock into the lead-time window (10 - 6 = 4 min left ≤ 5
        // min lead-time). The single advance schedules the FakeTimeProvider delay; a
        // Task.Yield lets the loop wake and register the re-check delay as needed.
        // The issuer invocation signal (count > countAfterStartup) is the gate — no
        // iteration budget, no wall-clock deadline.
        await Task.Yield();
        harness.Clock.Advance(TimeSpan.FromMinutes(6));

        // Nudge the clock forward in small steps until the reissue tick fires.
        // Each nudge may be needed to fire successive poll delays; the issuer signal
        // terminates the nudge loop the moment the reissue attempt is observed.
        await harness.AdvanceUntilIssuerCountAsync(
            targetCount: countAfterStartup + 1,
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

        await Task.Yield();
        harness.Clock.Advance(TimeSpan.FromMinutes(6));

        await harness.AdvanceUntilIssuerCountAsync(
            targetCount: countAfterStartup + 1,
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
            => _cacheSetSignal.WaitAsync(TimeSpan.FromSeconds(30));

        /// <summary>
        /// Nudges the fake clock forward in <paramref name="nudge"/> increments,
        /// yielding between each nudge, until the issuer has been invoked at least
        /// <paramref name="targetCount"/> times. The issuer-invocation channel
        /// (<see cref="FakeWorkloadCertificateIssuer.WaitForInvocationCountAsync"/>)
        /// terminates the wait the instant the target is reached — no iteration
        /// budget, no false-negative on a slow scheduler.
        /// </summary>
        /// <remarks>
        /// Nudging is still necessary here because the background loop registers a
        /// <c>FakeTimeProvider</c> delay; each <c>Clock.Advance</c> fires that delay
        /// so the loop wakes. Without the nudge the loop never sees elapsed time and
        /// never invokes the issuer. The issuer signal replaces the old condition-poll
        /// — it eliminates the race between "did the loop run yet?" and the iteration
        /// budget expiring.
        /// </remarks>
        public async Task AdvanceUntilIssuerCountAsync(
            int targetCount,
            TimeSpan nudge,
            TimeSpan? safetyTimeout = null)
        {
            using var cts = new CancellationTokenSource(safetyTimeout ?? TimeSpan.FromSeconds(30));

            // Run the signal-wait and the clock-nudger concurrently. The nudger keeps
            // advancing the fake clock (waking the loop's registered delay) until the
            // issuer-invocation signal fires and cancels it.
            var signalTask = Issuer.WaitForInvocationCountAsync(targetCount, ct: cts.Token);

            var nudgeToken = cts.Token;
            var nudgerTask = Task.Run(
                async () =>
                {
                    while (!nudgeToken.IsCancellationRequested)
                    {
                        await Task.Yield();
                        Clock.Advance(nudge);
                    }
                },
                nudgeToken);

            await signalTask;

            // Signal received — cancel the nudger and let it finish.
            await cts.CancelAsync();

            try
            {
                await nudgerTask;
            }
            catch (OperationCanceledException)
            {
                // Expected — the nudger loop was cancelled after the signal fired.
            }
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
