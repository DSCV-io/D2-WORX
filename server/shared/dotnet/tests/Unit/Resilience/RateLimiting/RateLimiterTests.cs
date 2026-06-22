// -----------------------------------------------------------------------
// <copyright file="RateLimiterTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Resilience.RateLimiting;

using AwesomeAssertions;
using D2.Shared.Resilience.RateLimiting;
using Xunit;

public sealed class RateLimiterTests
{
    // ----------------------------------------------------------------------
    // Basic admit / reject
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_UnderConcurrencyLimit_RunsAndReturns()
    {
        using var limiter = new RateLimiter(new(maxConcurrency: 2));

        var result = await limiter.ExecuteAsync(_ => ValueTask.FromResult(42));

        result.Should().Be(42);
    }

    [Fact]
    public void Options_MaxConcurrencyBelowOne_ThrowsArgumentOutOfRange()
    {
        // F-2 regression pin: MaxConcurrency=0 (or negative) is a misconfiguration;
        // RateLimiterOptions ctor must throw at construction time, not silently propagate
        // to SemaphoreSlim where the error message is less informative.
        var act0 = () => new RateLimiterOptions(maxConcurrency: 0);
        var actNeg = () => new RateLimiterOptions(maxConcurrency: -1);

        act0.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*MaxConcurrency must be at least 1*");
        actNeg.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*MaxConcurrency must be at least 1*");

        // Minimum valid value constructs without throwing.
        var act1 = () => new RateLimiterOptions(maxConcurrency: 1);
        act1.Should().NotThrow();
    }

    [Fact]
    public async Task ExecuteAsync_AtMaxConcurrency_NPlus1Rejected_WhenAcquisitionTimeoutZero()
    {
        const int max_concurrency = 2;
        var limiter = new RateLimiter(new(maxConcurrency: max_concurrency, acquisitionTimeout: TimeSpan.Zero));

        try
        {
            var gate = new TaskCompletionSource();

            // One TCS per in-flight op so we know each permit is held before the +1 call.
            var acquiredSignals = Enumerable.Range(0, max_concurrency)
                .Select(_ => new TaskCompletionSource())
                .ToArray();

            // Hold max_concurrency operations in-flight; each signals when it has the permit.
            var inFlight = acquiredSignals
                .Select(sig => limiter.ExecuteAsync(async _ =>
                {
                    sig.SetResult();
                    await gate.Task;
                    return 1;
                }).AsTask())
                .ToArray();

            // Wait until all max_concurrency permits are demonstrably held (deterministic).
            await Task.WhenAll(acquiredSignals.Select(s => s.Task));

            // N+1 caller should be immediately rejected (timeout = zero).
            await Assert.ThrowsAsync<RateLimitRejectedException>(
                () => limiter.ExecuteAsync(_ => ValueTask.FromResult(99)).AsTask());

            gate.SetResult();
            await Task.WhenAll(inFlight);
        }
        finally
        {
            limiter.Dispose();
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithPositiveAcquisitionTimeout_AdmitsOnRelease()
    {
        using var limiter = new RateLimiter(new(
            maxConcurrency: 1,
            acquisitionTimeout: TimeSpan.FromMilliseconds(500)));

        var gate = new TaskCompletionSource();
        var acquired = new TaskCompletionSource(); // signals: permit is held by first op
        var first = limiter.ExecuteAsync(async _ =>
        {
            acquired.SetResult();
            await gate.Task;
            return 1;
        }).AsTask();

        // Wait until the first op demonstrably holds the permit before the second queues.
        await acquired.Task;

        // Second caller should queue (500ms window). Release first immediately.
        gate.SetResult();

        var second = await limiter.ExecuteAsync(_ => ValueTask.FromResult(2));

        second.Should().Be(2);
        await first;
    }

    [Fact]
    public async Task ExecuteAsync_WithPositiveAcquisitionTimeout_RejectsWhenTimeoutElapses()
    {
        var limiter = new RateLimiter(new(
            maxConcurrency: 1,
            acquisitionTimeout: TimeSpan.FromMilliseconds(30)));

        try
        {
            var gate = new TaskCompletionSource();
            var acquired = new TaskCompletionSource(); // signals: permit is held by first op
            var first = limiter.ExecuteAsync(async _ =>
            {
                acquired.SetResult();
                await gate.Task;
                return 1;
            }).AsTask();

            // Wait until the first op demonstrably holds the permit before attempting the second.
            await acquired.Task;

            // Acquisition window is 30ms; gate won't release — second caller should be rejected.
            await Assert.ThrowsAsync<RateLimitRejectedException>(
                () => limiter.ExecuteAsync(_ => ValueTask.FromResult(99)).AsTask());

            gate.SetResult();
            await first;
        }
        finally
        {
            limiter.Dispose();
        }
    }

    // ----------------------------------------------------------------------
    // Semaphore release discipline
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_OpThrows_PermitReleasedForNextCaller()
    {
        using var limiter = new RateLimiter(new(maxConcurrency: 1, acquisitionTimeout: TimeSpan.Zero));

        // First call throws.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => limiter.ExecuteAsync<int>(_ => throw new InvalidOperationException("boom")).AsTask());

        // The permit must have been released — second call should succeed.
        var result = await limiter.ExecuteAsync(_ => ValueTask.FromResult(7));
        result.Should().Be(7);
    }

    [Fact]
    public async Task ExecuteAsync_CallerCancels_BeforeAcquire_ThrowsOce_NoPermitLeaked()
    {
        // F-3 regression pin: caller cancels the WaitAsync before it can acquire —
        // OperationCanceledException (or its subtype TaskCanceledException) propagates;
        // no permit is acquired so the gate count should remain correct.
        //
        // Deterministic handshake: instead of Task.Delay(20) (a race), we use a
        // TaskCompletionSource inside the op to signal back to the test once the op
        // has actually acquired the permit — only then do we issue the pre-cancelled call.
        using var limiter = new RateLimiter(new(
            maxConcurrency: 1,
            acquisitionTimeout: TimeSpan.FromMilliseconds(500)));

        var gate = new TaskCompletionSource();
        var acquired = new TaskCompletionSource(); // signals: "permit is held"
        var first = limiter.ExecuteAsync(async _ =>
        {
            acquired.SetResult(); // permit is now held; test may proceed
            await gate.Task;
            return 1;
        }).AsTask();

        // Wait until the op has provably acquired the permit before issuing the cancelled call.
        await acquired.Task;

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // WaitAsync(timeout, alreadyCanceledToken) must propagate OCE (or TaskCanceledException,
        // which IS-A OperationCanceledException). Use ThrowsAsync which accepts subtypes.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => limiter.ExecuteAsync(_ => ValueTask.FromResult(99), cts.Token).AsTask());

        // Release the first holder — a fresh (non-canceled) call should succeed,
        // proving no permit was leaked by the cancelled call.
        gate.SetResult();
        await first;

        var follow = await limiter.ExecuteAsync(_ => ValueTask.FromResult(5));
        follow.Should().Be(5);
    }

    // ----------------------------------------------------------------------
    // Concurrency stress
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_MaxConcurrencyRespected_UnderStress()
    {
        const int max_concurrency = 5;
        const int total_callers = 50;

        var limiter = new RateLimiter(new(
            maxConcurrency: max_concurrency,
            acquisitionTimeout: TimeSpan.FromSeconds(5)));

        var maxObserved = 0;
        var active = 0;

        var tasks = Enumerable.Range(0, total_callers).Select(_ => Task.Run(async () =>
        {
            await limiter.ExecuteAsync(async _ =>
            {
                var current = Interlocked.Increment(ref active);
                if (current > Volatile.Read(ref maxObserved))
                    Interlocked.Exchange(ref maxObserved, current);

                await Task.Delay(10, CancellationToken.None);

                Interlocked.Decrement(ref active);
                return 1;
            });
        })).ToArray();

        await Task.WhenAll(tasks);

        maxObserved.Should().BeLessThanOrEqualTo(
            max_concurrency,
            "no more than MaxConcurrency operations should be in-flight simultaneously");
    }

    // ----------------------------------------------------------------------
    // Dispose
    // ----------------------------------------------------------------------

    [Fact]
    public void Dispose_ReleasesResources_NoThrow()
    {
        var limiter = new RateLimiter(new(maxConcurrency: 5));

        var act = limiter.Dispose;

        act.Should().NotThrow();
    }
}
