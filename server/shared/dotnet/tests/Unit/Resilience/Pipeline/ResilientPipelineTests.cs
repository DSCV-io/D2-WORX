// -----------------------------------------------------------------------
// <copyright file="ResilientPipelineTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Resilience.Pipeline;

using AwesomeAssertions;
using D2.Shared.Resilience.CircuitBreaker;
using D2.Shared.Resilience.Pipeline;
using D2.Shared.Resilience.Retry;
using Xunit;
using SingleflightT = D2.Shared.Resilience.Singleflight.Singleflight<string, int>;

public sealed class ResilientPipelineTests
{
    // ----------------------------------------------------------------------
    // Layer composition + ordering
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_NoLayers_RunsOperationDirectly()
    {
        var pipeline = new ResilientPipeline<string, int>();

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(42));

        result.Success.Should().BeTrue();
        result.Data.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_LayersInvokedInOuterFirstOrder()
    {
        // Adversarial: layers should wrap each other outer-first. Trace each
        // layer's enter / exit order via a shared list to verify the canonical
        // composition: outer layer brackets the inner layer brackets the op.
        var trace = new List<string>();
        var pipeline = new ResilientPipeline<string, int>(
            new TracingLayer("outer", trace),
            new TracingLayer("inner", trace));

        await pipeline.ExecuteAsync("k", _ =>
        {
            trace.Add("op");
            return ValueTask.FromResult(1);
        });

        trace.Should().Equal("outer-enter", "inner-enter", "op", "inner-exit", "outer-exit");
    }

    // ----------------------------------------------------------------------
    // Exception → D2Result mapping
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_OperationSucceeds_ReturnsOkWithData()
    {
        var pipeline = new ResilientPipeline<string, int>();

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(7));

        result.Success.Should().BeTrue();
        result.Data.Should().Be(7);
    }

    [Fact]
    public async Task ExecuteAsync_CircuitOpenException_MapsToServiceUnavailable()
    {
        var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 1));

        // Open the breaker.
        try
        {
            await cb.ExecuteAsync(_ => throw new InvalidOperationException());
        }
        catch (InvalidOperationException)
        {
            // expected
        }

        var pipeline = new ResilientPipeline<string, int>(
            new CircuitBreakerLayer<string, int>(cb));

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(1));

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_OperationCancelled_MapsToCancelled()
    {
        // Caller-cancelled token: the OCE has its source in `ct`, so the
        // pipeline maps to Cancelled (not UnhandledException).
        using var cts = new CancellationTokenSource();
        var pipeline = new ResilientPipeline<string, int>();

        var result = await pipeline.ExecuteAsync(
            "k",
            ct =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return ValueTask.FromResult(1);
            },
            cts.Token);

        result.Success.Should().BeFalse();
        result.IsCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_TransientExceptionSlipsThrough_MapsToServiceUnavailable()
    {
        // No Retry layer configured + a transient exception (TimeoutException)
        // → caught by the IsTransientException catch clause and converted to
        // ServiceUnavailable. Covers the "slipped past the layers" branch.
        var pipeline = new ResilientPipeline<string, int>();

        var result = await pipeline.ExecuteAsync(
            "k",
            _ => throw new TimeoutException("upstream slow"));

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_UnknownException_MapsToUnhandledException()
    {
        // Non-transient, non-CB, non-cancellation exception → caught by the
        // final catch-all and converted to UnhandledException.
        var pipeline = new ResilientPipeline<string, int>();

        var result = await pipeline.ExecuteAsync(
            "k",
            _ => throw new InvalidOperationException("programmer error"));

        result.Success.Should().BeFalse();
        result.IsUnhandledException.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_OceWithoutCancelledToken_MapsToUnhandledException()
    {
        // Adversarial: an OCE thrown by the operation when the supplied ct
        // was NOT actually cancelled — the `when` filter doesn't match, the
        // OCE flows to the catch-all → UnhandledException.
        var pipeline = new ResilientPipeline<string, int>();

        var result = await pipeline.ExecuteAsync(
            "k",
            _ => throw new OperationCanceledException("not from our ct"));

        result.Success.Should().BeFalse();
        result.IsUnhandledException.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // Realistic full-stack composition
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_FullStack_RetriesThroughCircuitBreaker_ReturnsOk()
    {
        // Composition: SF → CB → Retry. Operation throws transient on first
        // 2 attempts, succeeds on 3rd. Pipeline returns Ok with the result.
        var pipeline = new ResilientPipeline<string, int>(
            new CircuitBreakerLayer<string, int>(
                new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 100))),
            new RetryLayer<string, int>(NoDelayOptions(maxAttempts: 5)));

        var attempts = 0;
        var result = await pipeline.ExecuteAsync("k", _ =>
        {
            Interlocked.Increment(ref attempts);
            if (attempts < 3)
            {
                throw new TimeoutException();
            }

            return ValueTask.FromResult(42);
        });

        result.Success.Should().BeTrue();
        result.Data.Should().Be(42);
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_RetryOutsideCb_RecoversAcrossSimulatedUpstreamRestart()
    {
        // The canonical "service restart" scenario, retry-OUTSIDE-CB:
        //   retry → CB → upstream
        // Sequence:
        //   1. Operation throws on attempt 1 → CB threshold hit → CB opens
        //   2. Retry's backoff fires (uses fake DelayFunc that advances the
        //      fake clock past the CB cooldown)
        //   3. Attempt 2 finds CB Half-Open → probes → upstream is "back" →
        //      operation returns success → CB closes
        // Pipeline returns Ok. This proves CircuitOpenException is treated
        // as transient by the retry layer (the new default-classifier rule).
        var clock = new FakeClock();
        var cb = new CircuitBreaker<int>(
            isFailure: _ => false,
            options: new(failureThreshold: 1, cooldownDuration: TimeSpan.FromSeconds(1), nowFunc: clock.Now));

        var attempts = 0;
        var retryOptions = NoDelayOptions(maxAttempts: 3) with
        {
            DelayFunc = (_, _) =>
            {
                // Each retry "wait" advances simulated time past the cooldown
                // so the CB transitions to Half-Open by the next attempt.
                clock.Advance(TimeSpan.FromSeconds(2));
                return Task.CompletedTask;
            },
        };

        // Layer order: Retry OUTER, CircuitBreaker INNER.
        var pipeline = new ResilientPipeline<string, int>(
            new RetryLayer<string, int>(retryOptions),
            new CircuitBreakerLayer<string, int>(cb));

        var result = await pipeline.ExecuteAsync("k", _ =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new TimeoutException("upstream is restarting");
            }

            return ValueTask.FromResult(42);
        });

        result.Success.Should().BeTrue();
        result.Data.Should().Be(42);
        attempts.Should().Be(2);
        cb.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task ExecuteAsync_HighConcurrency_FullStack_AllSeeSameDedupedResult()
    {
        // End-to-end stress: 100 concurrent callers through SF + CB + Retry,
        // same key, operation transient-fails twice then succeeds. Verify:
        //  - operation runs exactly ONCE-PER-RETRY-ATTEMPT (3 total — SF
        //    dedupes the entire retry sequence: all 100 await one shared task)
        //  - all 100 callers receive the same Ok(7) result
        //  - CB stays Closed (one logical execution = one CB call, succeeded)
        //
        // Test discipline: the FIRST attempt awaits a gate so the SF in-flight
        // window stays open long enough for all 100 callers to land. Without
        // the gate the whole pipeline runs synchronously on the first caller —
        // SF removes the key before late callers arrive, and they each start a
        // fresh sequence (observable as attempts >> 3). Pre-warmed threadpool
        // because default growth rate can't deliver 100 ready workers in time.
        ThreadPool.GetMinThreads(out var origWorker, out var origIo);
        ThreadPool.SetMinThreads(Math.Max(origWorker, 200), Math.Max(origIo, 200));
        try
        {
            var sf = new SingleflightT();
            var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 100));
            var pipeline = new ResilientPipeline<string, int>(
                new SingleflightLayer<string, int>(sf),
                new CircuitBreakerLayer<string, int>(cb),
                new RetryLayer<string, int>(NoDelayOptions(maxAttempts: 5)));

            var attempts = 0;
            var gate = new TaskCompletionSource();
            const int concurrent_callers = 100;

            var tasks = Enumerable.Range(0, concurrent_callers)
                .Select(_ => Task.Run(async () => await pipeline.ExecuteAsync("k", async _ =>
                {
                    var n = Interlocked.Increment(ref attempts);
                    if (n == 1)
                    {
                        // Hold the in-flight task open until every caller has
                        // had a chance to dedup onto it.
                        await gate.Task;
                    }

                    if (n < 3)
                    {
                        throw new TimeoutException();
                    }

                    return 7;
                })))
                .ToArray();

            // Wait for SF to publish the in-flight entry, then give the
            // remaining 99 callers a window to land on it.
            SpinWait.SpinUntil(() => sf.Size == 1, TimeSpan.FromSeconds(5))
                .Should().BeTrue("singleflight should publish the in-flight entry quickly");
            await Task.Delay(200);

            sf.Size.Should().Be(1);
            gate.SetResult();

            var results = await Task.WhenAll(tasks);

            attempts.Should().Be(3);                   // SF dedupes — only one retry sequence ran
            results.Should().AllSatisfy(r =>
            {
                r.Success.Should().BeTrue();
                r.Data.Should().Be(7);                 // every caller got the same answer
            });
            cb.State.Should().Be(CircuitState.Closed); // CB saw one successful execution
        }
        finally
        {
            ThreadPool.SetMinThreads(origWorker, origIo);
        }
    }

    [Fact]
    public async Task ExecuteAsync_SingleflightInPipeline_OperationFails_AllConcurrentCallersGetSameFailure()
    {
        // Adversarial: when SF dedupes a failing operation, EVERY waiter
        // sees the same exception → same D2Result mapping. No caller gets
        // an inconsistent view (some Ok, some failure) of the shared run.
        var pipeline = new ResilientPipeline<string, int>(
            new SingleflightLayer<string, int>(new SingleflightT()));

        const int concurrent_callers = 50;
        var barrier = new Barrier(concurrent_callers);
        try
        {
            // ReSharper disable AccessToDisposedClosure -- await Task.WhenAll
            // synchronises all closures before the finally Dispose, which R#
            // can't prove statically.
            var tasks = Enumerable.Range(0, concurrent_callers).Select(_ => Task.Run(async () =>
            {
                barrier.SignalAndWait();
                return await pipeline.ExecuteAsync(
                    "k",
                    _ => throw new InvalidOperationException("upstream broke"));
            })).ToArray();

            var results = await Task.WhenAll(tasks);

            // ReSharper restore AccessToDisposedClosure
            results.Should().AllSatisfy(r =>
            {
                r.Success.Should().BeFalse();
                r.IsUnhandledException.Should().BeTrue();
            });
        }
        finally
        {
            barrier.Dispose();
        }
    }

    [Fact]
    public async Task ExecuteAsync_RetryOutsideCb_ExhaustsBeforeCooldown_ReturnsServiceUnavailable()
    {
        // Adversarial counterpart to the recovery test: retry budget is
        // SHORTER than the CB cooldown, so every retry attempt finds the
        // breaker still open. CO is transient so the retry loop runs to
        // exhaustion, then the final CO bubbles out to the pipeline → mapped
        // to ServiceUnavailable.
        var clock = new FakeClock();
        var cb = new CircuitBreaker<int>(
            isFailure: _ => false,
            options: new(failureThreshold: 1, cooldownDuration: TimeSpan.FromSeconds(60), nowFunc: clock.Now));

        // Force the CB open BEFORE running through the pipeline, so the
        // pipeline sees a breaker that's already tripped.
        try
        {
            await cb.ExecuteAsync(_ => throw new InvalidOperationException());
        }
        catch (InvalidOperationException)
        {
            // expected — opens the circuit
        }

        cb.State.Should().Be(CircuitState.Open);

        var pipeline = new ResilientPipeline<string, int>(
            new RetryLayer<string, int>(NoDelayOptions(maxAttempts: 3)),
            new CircuitBreakerLayer<string, int>(cb));

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(1));

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();
    }

    private static RetryOptions<int> NoDelayOptions(int maxAttempts = 3)
        => new()
        {
            MaxAttempts = maxAttempts,
            BaseDelayMs = 0,
            MaxDelayMs = 0,
            Jitter = false,
            DelayFunc = (_, _) => Task.CompletedTask,
        };

    private sealed class FakeClock
    {
        private long _now;

        public long Now() => Volatile.Read(ref _now);

        public void Advance(TimeSpan delta)
            => Interlocked.Add(ref _now, (long)delta.TotalMilliseconds);
    }

    /// <summary>
    /// Test-only layer that records enter/exit markers in a shared list so
    /// composition order can be asserted.
    /// </summary>
    private sealed class TracingLayer(string name, List<string> trace)
        : IResilientLayer<string, int>
    {
        public async ValueTask<int> WrapAsync(
            string key,
            Func<CancellationToken, ValueTask<int>> next,
            CancellationToken ct)
        {
            trace.Add($"{name}-enter");
            try
            {
                return await next(ct);
            }
            finally
            {
                trace.Add($"{name}-exit");
            }
        }
    }
}
