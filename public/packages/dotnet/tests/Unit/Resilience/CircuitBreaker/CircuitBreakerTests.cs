// -----------------------------------------------------------------------
// <copyright file="CircuitBreakerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.CircuitBreaker;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.CircuitBreaker;
using Xunit;

public sealed class CircuitBreakerTests
{
    // ----------------------------------------------------------------------
    // Initial state + Closed-path behavior
    // ----------------------------------------------------------------------

    [Fact]
    public void InitialState_IsClosed()
    {
        var cb = NewBreaker();

        cb.State.Should().Be(CircuitState.Closed);
        cb.FailureCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ClosedAndSuccess_PassesThroughResult()
    {
        var cb = NewBreaker();

        var result = await cb.ExecuteAsync(_ => ValueTask.FromResult(42));

        result.Should().Be(42);
        cb.State.Should().Be(CircuitState.Closed);
        cb.FailureCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ClosedAndValueFailure_IncrementsCount()
    {
        // isFailure on the value: any 0 counts as failure.
        var cb = new CircuitBreaker<int>(r => r == 0);

        var result = await cb.ExecuteAsync(_ => ValueTask.FromResult(0));

        result.Should().Be(0);
        cb.FailureCount.Should().Be(1);
        cb.State.Should().Be(CircuitState.Closed); // threshold not yet reached
    }

    [Fact]
    public async Task ExecuteAsync_ClosedAndException_IncrementsCountAndRethrows()
    {
        var cb = NewBreaker();

        var act = async () => await cb.ExecuteAsync(
            _ => throw new InvalidOperationException("boom"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("boom");
        cb.FailureCount.Should().Be(1);
        cb.State.Should().Be(CircuitState.Closed);
    }

    // ----------------------------------------------------------------------
    // Closed → Open transition at threshold
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_HittingThreshold_TransitionsToOpen()
    {
        var cb = NewBreaker(options: new(3));

        for (var i = 0; i < 3; i++)
        {
            try
            {
                await cb.ExecuteAsync(_ => throw new InvalidOperationException());
            }
            catch (InvalidOperationException)
            {
                // expected
            }
        }

        cb.State.Should().Be(CircuitState.Open);
        cb.FailureCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_MixedExceptionAndValueFailures_HitThresholdToOpen()
    {
        // Adversarial: failure source mix is irrelevant — both count.
        var cb = new CircuitBreaker<int>(
            r => r == 0,
            options: new(3));

        try
        {
            await cb.ExecuteAsync(_ => throw new InvalidOperationException());
        }
        catch (InvalidOperationException)
        {
            // expected
        }

        await cb.ExecuteAsync(_ => ValueTask.FromResult(0)); // value failure
        await cb.ExecuteAsync(_ => ValueTask.FromResult(0)); // value failure → opens

        cb.State.Should().Be(CircuitState.Open);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessResetsFailureCount()
    {
        var cb = NewBreaker(options: new(5));

        try
        {
            await cb.ExecuteAsync(_ => throw new InvalidOperationException());
        }
        catch (InvalidOperationException)
        {
            // expected
        }

        cb.FailureCount.Should().Be(1);

        await cb.ExecuteAsync(_ => ValueTask.FromResult(1));

        cb.FailureCount.Should().Be(0);
        cb.State.Should().Be(CircuitState.Closed);
    }

    // ----------------------------------------------------------------------
    // Open path
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_OpenWithoutFallback_ThrowsCircuitOpenException()
    {
        var clock = new FakeClock();
        var cb = NewBreaker(
            options: new(1, TimeSpan.FromSeconds(30), clock.Now));

        await ForceOpen(cb);

        var act = async () => await cb.ExecuteAsync(_ => ValueTask.FromResult(1));

        await act.Should().ThrowAsync<CircuitOpenException>()
            .WithMessage("Circuit breaker is open");
    }

    [Fact]
    public async Task ExecuteAsync_OpenWithFallback_ReturnsFallback()
    {
        var clock = new FakeClock();
        var cb = NewBreaker(
            options: new(1, TimeSpan.FromSeconds(30), clock.Now));

        await ForceOpen(cb);

        var fallbackInvocations = 0;
        var result = await cb.ExecuteAsync(
            _ => ValueTask.FromResult(1),
            fallback: () =>
            {
                fallbackInvocations++;
                return ValueTask.FromResult(99);
            });

        result.Should().Be(99);
        fallbackInvocations.Should().Be(1);
    }

    // ----------------------------------------------------------------------
    // Open → HalfOpen transition (probe success closes)
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_AfterCooldown_HalfOpenProbeSuccessCloses()
    {
        var clock = new FakeClock();
        var transitions = new List<(CircuitState From, CircuitState To)>();
        var cb = NewBreaker(
            options: new(1, TimeSpan.FromMilliseconds(100), clock.Now),
            onStateChange: (from, to) => transitions.Add((from, to)));

        await ForceOpen(cb);
        cb.State.Should().Be(CircuitState.Open);

        clock.Advance(TimeSpan.FromMilliseconds(150));
        var result = await cb.ExecuteAsync(_ => ValueTask.FromResult(7));

        result.Should().Be(7);
        cb.State.Should().Be(CircuitState.Closed);
        cb.FailureCount.Should().Be(0);
        transitions.Should().Equal(
            (CircuitState.Closed, CircuitState.Open),
            (CircuitState.Open, CircuitState.HalfOpen),
            (CircuitState.HalfOpen, CircuitState.Closed));
    }

    [Fact]
    public async Task ExecuteAsync_AfterCooldown_HalfOpenProbeFailureReopens()
    {
        // Coverage: callback IS provided (non-null) on the HalfOpen→Open
        // re-open path inside RecordFailure. Companion to the
        // null-callback variant below.
        var clock = new FakeClock();
        var transitions = new List<(CircuitState From, CircuitState To)>();
        var cb = NewBreaker(
            options: new(1, TimeSpan.FromMilliseconds(100), clock.Now),
            onStateChange: (from, to) => transitions.Add((from, to)));

        await ForceOpen(cb);
        clock.Advance(TimeSpan.FromMilliseconds(150));

        // Probe throws → straight back to Open.
        var act = async () => await cb.ExecuteAsync(
            _ => throw new InvalidOperationException("probe failed"));

        await act.Should().ThrowAsync<InvalidOperationException>();
        cb.State.Should().Be(CircuitState.Open);
        transitions.Should().Equal(
            (CircuitState.Closed, CircuitState.Open),
            (CircuitState.Open, CircuitState.HalfOpen),
            (CircuitState.HalfOpen, CircuitState.Open));
    }

    [Fact]
    public async Task ExecuteAsync_HalfOpen_ProbeValueFailureReopens()
    {
        // Adversarial: probe returns a value-failure (not exception) → still
        // counts as failure → reopens.
        var clock = new FakeClock();
        var cb = new CircuitBreaker<int>(
            isFailure: r => r == 0,
            options: new(1, TimeSpan.FromMilliseconds(100), clock.Now));

        await cb.ExecuteAsync(_ => ValueTask.FromResult(0)); // open
        cb.State.Should().Be(CircuitState.Open);

        clock.Advance(TimeSpan.FromMilliseconds(150));

        var result = await cb.ExecuteAsync(_ => ValueTask.FromResult(0)); // probe value-fails

        result.Should().Be(0);
        cb.State.Should().Be(CircuitState.Open);
    }

    [Fact]
    public async Task ExecuteAsync_HalfOpen_SecondConcurrentCallerGetsFallback()
    {
        // Adversarial: only ONE probe runs at a time. A second caller during
        // the probe gets the fallback (or CircuitOpenException without one).
        var clock = new FakeClock();
        var cb = NewBreaker(
            options: new(1, TimeSpan.FromMilliseconds(100), clock.Now));

        await ForceOpen(cb);
        clock.Advance(TimeSpan.FromMilliseconds(150));

        var probeGate = new TaskCompletionSource();
        var probeTask = cb.ExecuteAsync(async _ =>
        {
            await probeGate.Task;
            return 7;
        }).AsTask();

        // Wait briefly for the probe to be in-flight.
        await Task.Delay(20);

        // Second caller while probe is in-flight → fallback path.
        var fallbackInvoked = false;
        var second = await cb.ExecuteAsync(
            _ => ValueTask.FromResult(1),
            fallback: () =>
            {
                fallbackInvoked = true;
                return ValueTask.FromResult(99);
            });

        second.Should().Be(99);
        fallbackInvoked.Should().BeTrue();

        // Release the probe; verify it succeeds and the breaker closes.
        probeGate.SetResult();
        var probeResult = await probeTask;
        probeResult.Should().Be(7);
        cb.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task ExecuteAsync_HalfOpen_SecondConcurrentCallerNoFallbackThrows()
    {
        var clock = new FakeClock();
        var cb = NewBreaker(
            options: new(1, TimeSpan.FromMilliseconds(100), clock.Now));

        await ForceOpen(cb);
        clock.Advance(TimeSpan.FromMilliseconds(150));

        var probeGate = new TaskCompletionSource();
        var probeTask = cb.ExecuteAsync(async _ =>
        {
            await probeGate.Task;
            return 7;
        }).AsTask();

        await Task.Delay(20);

        var act = async () => await cb.ExecuteAsync(_ => ValueTask.FromResult(1));
        await act.Should().ThrowAsync<CircuitOpenException>();

        probeGate.SetResult();
        await probeTask;
    }

    // ----------------------------------------------------------------------
    // Reset
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Reset_FromOpen_ReturnsToClosedAndFiresStateChange()
    {
        var clock = new FakeClock();
        var transitions = new List<(CircuitState From, CircuitState To)>();
        var cb = NewBreaker(
            options: new(failureThreshold: 1, nowFunc: clock.Now),
            onStateChange: (from, to) => transitions.Add((from, to)));

        await ForceOpen(cb);
        cb.Reset();

        cb.State.Should().Be(CircuitState.Closed);
        cb.FailureCount.Should().Be(0);
        transitions.Should().Equal(
            (CircuitState.Closed, CircuitState.Open),
            (CircuitState.Open, CircuitState.Closed));
    }

    [Fact]
    public async Task Reset_FromOpen_NoCallback_DoesNotThrow()
    {
        // Coverage: r_onStateChange?.Invoke(...) when the callback is null
        // (the second null-conditional branch on the Reset path).
        var clock = new FakeClock();
        var cb = NewBreaker(
            options: new(failureThreshold: 1, nowFunc: clock.Now),
            onStateChange: null);

        await ForceOpen(cb);
        cb.State.Should().Be(CircuitState.Open);

        cb.Reset();

        cb.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task ExecuteAsync_HalfOpen_ProbeFailureNoCallback_DoesNotThrow()
    {
        // Coverage: r_onStateChange?.Invoke(...) on the HalfOpen→Open
        // re-open path inside RecordFailure when callback is null.
        var clock = new FakeClock();
        var cb = NewBreaker(
            options: new(1, TimeSpan.FromMilliseconds(100), clock.Now),
            onStateChange: null);

        await ForceOpen(cb);
        clock.Advance(TimeSpan.FromMilliseconds(150));

        var act = async () => await cb.ExecuteAsync(
            _ => throw new InvalidOperationException("probe failed"));
        await act.Should().ThrowAsync<InvalidOperationException>();

        cb.State.Should().Be(CircuitState.Open);
    }

    [Fact]
    public async Task Reset_FromClosed_DoesNotFireStateChange()
    {
        // Adversarial: idempotent — no-op when already Closed.
        var transitions = new List<(CircuitState From, CircuitState To)>();
        var cb = NewBreaker(
            onStateChange: (from, to) => transitions.Add((from, to)));

        await cb.ExecuteAsync(_ => ValueTask.FromResult(1));

        cb.Reset();

        transitions.Should().BeEmpty();
    }

    // ----------------------------------------------------------------------
    // High-concurrency adversarial scenarios
    // ----------------------------------------------------------------------

    [Fact]
    public async Task HighConcurrentFailures_OpensExactlyOnce_FiresStateChangeOnce()
    {
        // Adversarial: 50 concurrent threads simultaneously throw failures
        // through the CB. The threshold is 5; once exceeded, multiple threads
        // race the Open transition. Verify the state-change callback fires
        // EXACTLY ONCE (CompareExchange ensures only one thread wins the
        // transition) even though many threads see FailureCount >= threshold.
        var openTransitions = 0;
        var cb = new CircuitBreaker<int>(
            isFailure: _ => false,
            options: new(failureThreshold: 5),
            onStateChange: (_, to) =>
            {
                if (to == CircuitState.Open)
                {
                    Interlocked.Increment(ref openTransitions);
                }
            });

        const int concurrent_threads = 50;
        var barrier = new Barrier(concurrent_threads);
        try
        {
            // ReSharper disable AccessToDisposedClosure -- await Task.WhenAll
            // synchronizes all closures before the finally Dispose, which R#
            // can't prove statically.
            await Task.WhenAll(Enumerable.Range(0, concurrent_threads)
                .Select(_ => Task.Run(async () =>
            {
                barrier.SignalAndWait();
                try
                {
                    await cb.ExecuteAsync(_ => throw new InvalidOperationException());
                }
                catch (InvalidOperationException)
                {
                    // expected
                }
                catch (CircuitOpenException)
                {
                    // expected once breaker opens
                }
            })));

            // ReSharper restore AccessToDisposedClosure
        }
        finally
        {
            barrier.Dispose();
        }

        cb.State.Should().Be(CircuitState.Open);
        openTransitions.Should().Be(1);
    }

    [Fact]
    public async Task HighConcurrentSuccesses_OnClosedBreaker_FailureCountStaysZero()
    {
        // Adversarial: 100 concurrent successes. RecordSuccess uses
        // Interlocked.Exchange(failureCount, 0) — verify it's never observed
        // as nonzero (no torn reads / ABA issues).
        var cb = NewBreaker(options: new(failureThreshold: 5));
        const int concurrent_threads = 100;
        var barrier = new Barrier(concurrent_threads);
        try
        {
            // ReSharper disable AccessToDisposedClosure -- await Task.WhenAll
            // synchronizes all closures before the finally Dispose, which R#
            // can't prove statically.
            await Task.WhenAll(Enumerable.Range(0, concurrent_threads)
                .Select(i => Task.Run(async () =>
            {
                barrier.SignalAndWait();
                await cb.ExecuteAsync(_ => ValueTask.FromResult(i));
            })));

            // ReSharper restore AccessToDisposedClosure
        }
        finally
        {
            barrier.Dispose();
        }

        cb.FailureCount.Should().Be(0);
        cb.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task OnStateChange_CallbackThrows_ExceptionPropagatesToCaller()
    {
        // Defensive: the onStateChange callback is invoked synchronously on
        // the thread triggering the transition, with no try/catch wrapper.
        // A throwing callback propagates to the caller — the test pins the
        // current behavior. (Could be argued either way; the explicit
        // contract documented here is "keep your callback fast and safe".)
        var cb = new CircuitBreaker<int>(
            isFailure: _ => false,
            options: new(failureThreshold: 1),
            onStateChange: (_, _) => throw new InvalidOperationException("callback boom"));

        var act = async () => await cb.ExecuteAsync(
            _ => throw new TimeoutException("upstream"));

        // The callback's InvalidOperationException replaces the original
        // upstream TimeoutException because RecordFailure → callback fires
        // BEFORE the catch's `throw;` rethrows the upstream exception.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("callback boom");
    }

    [Fact]
    public async Task ExecuteAsync_OperationThrowsOce_NotFromCt_StillCountsAsFailure()
    {
        // Adversarial: an operation that throws OperationCanceledException
        // for reasons UNRELATED to the supplied CT (e.g. an internal timeout
        // CTS that fired) MUST count as a failure — the CB has no way to
        // know it wasn't a "real" failure. Verify FailureCount increments.
        var cb = NewBreaker(options: new(failureThreshold: 5));

        try
        {
            await cb.ExecuteAsync(_ => throw new OperationCanceledException("internal timeout"));
        }
        catch (OperationCanceledException)
        {
            // expected
        }

        cb.FailureCount.Should().Be(1);
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static CircuitBreaker<int> NewBreaker(
        CircuitBreakerOptions? options = null,
        Action<CircuitState, CircuitState>? onStateChange = null)
    {
        return new CircuitBreaker<int>(
            isFailure: _ => false,
            options: options,
            onStateChange: onStateChange);
    }

    private static async ValueTask ForceOpen(CircuitBreaker<int> cb)
    {
        try
        {
            await cb.ExecuteAsync(_ => throw new InvalidOperationException());
        }
        catch (InvalidOperationException)
        {
            // expected
        }
    }

    private sealed class FakeClock
    {
        private long _now;

        public long Now() => Volatile.Read(ref _now);

        public void Advance(TimeSpan delta)
            => Interlocked.Add(ref _now, (long)delta.TotalMilliseconds);
    }
}
