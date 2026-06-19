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
/// survival, and clean cancellation. Mirrors
/// <c>ServiceIdentityRefreshHostedServiceTests</c>.
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

        await Harness.WaitUntil(() => harness.Cache.PeekRaw() is not null);

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
        await Harness.WaitUntil(() => harness.Service.ExecuteTask is not null);

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

        await Harness.WaitUntil(() => harness.Cache.PeekRaw() is not null);
        var countAfterStartup = harness.Issuer.IssuanceCount;

        // Drive the loop's FakeTimeProvider-based poll: advance into the lead-time
        // window (10 - 6 = 4 ≤ 5) and keep nudging the clock past successive poll
        // intervals until the proactive reissue fires (robust against the
        // advance-vs-delay-registration race a single advance is subject to).
        await harness.AdvanceUntil(
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

        await Harness.WaitUntil(() => harness.Cache.PeekRaw() is not null);
        var countAfterStartup = harness.Issuer.IssuanceCount;

        // Fire several poll ticks well short of the lead window (advance 31s ×3 ≈
        // 93s ≪ the 24h-minus-5min reissue threshold) and confirm none reissue.
        for (var i = 0; i < 3; i++)
        {
            await Task.Delay(30);
            harness.Clock.Advance(TimeSpan.FromSeconds(31));
        }

        await Task.Delay(50);

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

        await Harness.WaitUntil(() => harness.Cache.PeekRaw() is not null);
        var countAfterStartup = harness.Issuer.IssuanceCount;

        // Arm a failure, then drive the poll into the reissue window so a failing
        // tick actually runs (its reissue attempt increments the issuer count), and
        // confirm the loop neither faulted nor completed — the failure is swallowed.
        harness.Issuer.SetFail(true);
        await harness.AdvanceUntil(
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

        await Harness.WaitUntil(() => harness.Cache.PeekRaw() is not null);

        await cts.CancelAsync();
        await harness.Service.StopAsync(CancellationToken.None);
        await task;

        harness.Service.ExecuteTask!.IsCompletedSuccessfully.Should().BeTrue();
    }

    private sealed class Harness : IAsyncDisposable
    {
        public Harness(TimeSpan? validity = null, TimeSpan? leadTime = null)
        {
            Clock = new FakeTimeProvider(SR_Base);
            Issuer = new FakeWorkloadCertificateIssuer(Clock, validity: validity);
            Cache = new WorkloadLeafCache();
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

        public static async Task WaitUntil(Func<bool> condition, int timeoutMs = 1000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                    return;

                await Task.Delay(10);
            }

            condition().Should().BeTrue("condition was not met within the timeout");
        }

        public async Task AdvanceUntil(
            Func<bool> condition,
            TimeSpan firstAdvance,
            TimeSpan nudge,
            int timeoutMs = 3000)
        {
            // Settle so the loop registers its poll delay, advance once into the
            // target window, then keep nudging past successive poll intervals —
            // each nudge fires any due FakeTimeProvider timer the loop has armed.
            await Task.Delay(50);
            Clock.Advance(firstAdvance);

            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                    return;

                await Task.Delay(20);
                Clock.Advance(nudge);
            }

            condition().Should().BeTrue("condition was not met within the timeout");
        }

        public async ValueTask DisposeAsync()
        {
            Service.Dispose();
            Client.Dispose();
            Cache.Dispose();
            Issuer.Dispose();
            await ValueTask.CompletedTask;
        }
    }
}
