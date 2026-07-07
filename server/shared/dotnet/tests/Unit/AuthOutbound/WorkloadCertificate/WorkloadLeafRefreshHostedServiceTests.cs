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
        /// Advances the fake clock in <paramref name="nudge"/> increments until the
        /// issuer has been invoked at least <paramref name="targetCount"/> times, then
        /// returns. Non-starvable: termination is driven ONLY by the issuer-invocation
        /// signal (<see cref="FakeWorkloadCertificateIssuer.WaitForInvocationCountAsync"/>)
        /// or, for a genuinely-stuck SUT, by a COUNT-based nudge budget — never by a
        /// wall-clock deadline. A starved thread pool merely costs more nudges; it can
        /// never produce a false timeout on a healthy SUT.
        /// </summary>
        /// <remarks>
        /// The nudger runs on a pool thread (<c>Task.Run</c>) while
        /// this method parks on the invocation signal, so the background service's
        /// <c>Task.Delay</c> continuation always gets a scheduler slot to wake, re-check
        /// reissue-due, and invoke the issuer — the advance MUST run concurrently with a
        /// parked awaiter for the loop to make progress under the test scheduler. This is
        /// the same drive mechanism the old helper used, MINUS the 30 s wall-clock
        /// cancellation source that could trip under full-suite thread-pool starvation
        /// and spuriously fail a passing test. The nudge budget replaces that deadline:
        /// a healthy reissue fires within a handful of nudges, so the budget is
        /// unreachable for a passing test under ANY load; it trips ONLY when the SUT is
        /// genuinely stuck (never reissues) — a true defect — so the test fails fast with
        /// a clear message instead of hanging.
        /// </remarks>
        /// <param name="targetCount">The cumulative issuer-invocation count to reach.</param>
        /// <param name="nudge">
        /// The clock increment per nudge. Must exceed the loop's poll interval so a
        /// single advance fires the registered poll delay.
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

            using var budgetCts = new CancellationTokenSource();

            // Park on the issuer signal with NO wall-clock deadline — only budgetCts
            // (nudge-count exhaustion) or the target being reached ends the wait.
            var signalTask = Issuer.WaitForInvocationCountAsync(
                targetCount, timeout: Timeout.InfiniteTimeSpan, ct: budgetCts.Token);

            // Concurrent nudger on a real pool thread: advances the fake clock so the
            // background loop's registered delay fires and it re-checks reissue-due.
            // Bounded by a COUNT (never wall-clock); exhausting it cancels the parked
            // signal wait so a stuck SUT surfaces as a fast, explicit failure.
            // ReSharper disable AccessToDisposedClosure -- the finally below awaits nudgerTask
            // to completion BEFORE the using-scoped budgetCts is disposed at method exit, so the
            // closure never touches a disposed CTS; R# cannot prove that ordering statically.
            var nudgerTask = Task.Run(
                async () =>
                {
                    for (var nudged = 0; nudged < maxNudges && !budgetCts.IsCancellationRequested; nudged++)
                    {
                        Clock.Advance(nudge);
                        await Task.Yield();
                    }

                    await budgetCts.CancelAsync();
                });

            // ReSharper restore AccessToDisposedClosure

            try
            {
                await signalTask;
            }
            catch (OperationCanceledException) when (budgetCts.IsCancellationRequested)
            {
                // The budget canceled the wait. If the target was in fact reached in the
                // same instant, that is a benign cancel/reach race — treat it as success.
                if (Issuer.IssuanceCount >= targetCount)
                    return;

                throw new InvalidOperationException(
                    $"Issuer invocation count {Issuer.IssuanceCount} did not reach {targetCount} "
                    + $"after {maxNudges} clock nudges — the background reissue loop appears stuck.");
            }
            finally
            {
                await budgetCts.CancelAsync();
                await nudgerTask;
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
