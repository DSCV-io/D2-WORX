// -----------------------------------------------------------------------
// <copyright file="ResilientPipelineTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.Pipeline;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.CircuitBreaker;
using DcsvIo.D2.Resilience.Pipeline;
using DcsvIo.D2.Resilience.RateLimiting;
using Xunit;
using SingleflightT = DcsvIo.D2.Resilience.Singleflight.Singleflight<string, int>;

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
    public async Task ExecuteAsync_OperationCanceled_MapsToCanceled()
    {
        // Caller-canceled token: the OCE has its source in `ct`, so the
        // pipeline maps to Canceled (not UnhandledException).
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
        result.IsCanceled.Should().BeTrue();
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
    public async Task ExecuteAsync_OceWithoutCanceledToken_MapsToUnhandledException()
    {
        // Adversarial: an OCE thrown by the operation when the supplied ct
        // was NOT actually canceled — the `when` filter doesn't match, the
        // OCE flows to the catch-all → UnhandledException.
        var pipeline = new ResilientPipeline<string, int>();

        var result = await pipeline.ExecuteAsync(
            "k",
            _ => throw new OperationCanceledException("not from our ct"));

        result.Success.Should().BeFalse();
        result.IsUnhandledException.Should().BeTrue();
    }

    [Fact]
    public async Task
    ExecuteAsync_TaskCanceledExceptionWithoutCanceledToken_MapsToServiceUnavailable()
    {
        // Pin the DIVERGENT classification: `TaskCanceledException` IS in
        // `IsTransientException`'s switch arms (line 73 of RetryHelper.cs),
        // unlike its base `OperationCanceledException`. So an unattributed
        // `TaskCanceledException` falls through the OCE-when-ct catch and
        // hits the IsTransientException catch instead → ServiceUnavailable.
        // Critical because TCE-without-ct is what surfaces from many
        // HttpClient timeouts internal to a downstream call.
        var pipeline = new ResilientPipeline<string, int>();

        var result = await pipeline.ExecuteAsync(
            "k",
            _ => throw new TaskCanceledException("inner timeout, not our ct"));

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();
        result.IsUnhandledException.Should().BeFalse();
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
            NoDelayOptions(maxAttempts: 5));

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
            options: new(
                failureThreshold: 1,
                cooldownDuration: TimeSpan.FromSeconds(1),
                nowFunc: clock.Now));

        var attempts = 0;

        // Layer order: Retry OUTER, CircuitBreaker INNER.
        var pipeline = new ResilientPipeline<string, int>(
            new RetryLayer<string, int>(new()
            {
                MaxAttempts = 3,
                BaseDelayMs = 0,
                MaxDelayMs = 0,
                Jitter = false,
                DelayFunc = (_, _) =>
                {
                    // Each retry "wait" advances simulated time past the cooldown
                    // so the CB transitions to Half-Open by the next attempt.
                    clock.Advance(TimeSpan.FromSeconds(2));
                    return Task.CompletedTask;
                },
            }),
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
                NoDelayOptions(maxAttempts: 5));

            var attempts = 0;
            var gate = new TaskCompletionSource();
            const int concurrent_callers = 100;

            // Determinism guarantee: ResilientPipeline.ExecuteAsync is async; its
            // first synchronous path builds the layer chain then calls wrapped(ct),
            // which invokes SingleflightLayer.WrapAsync → Singleflight.ExecuteAsync
            // → r_inflight.GetOrAdd synchronously — all before the first await.
            // Capturing the ValueTask<D2Result<int>> (without awaiting) therefore
            // proves the caller has joined the singleflight entry. Signalling AFTER
            // capture makes the wait on all N signals a guarantee that all callers
            // have joined before the gate opens — no SpinWait/Yield needed.
            var allJoined = new SemaphoreSlim(0, concurrent_callers);

            var tasks = Enumerable.Range(0, concurrent_callers)
                .Select(_ => Task.Run(async () =>
                {
                    var vt = pipeline.ExecuteAsync("k", async _ =>
                    {
                        var n = Interlocked.Increment(ref attempts);

                        if (n == 1)
                        {
                            // Hold the in-flight task open until every caller has
                            // had a chance to dedup onto it.
                            await gate.Task;
                        }

                        if (n < 3)
                            throw new TimeoutException();

                        return 7;
                    });
                    allJoined.Release(); // signal AFTER join, not before
                    return await vt;
                }))
                .ToArray();

            // Wait for all callers to have joined — guaranteed, not polled.
            for (var i = 0; i < concurrent_callers; i++)
                await allJoined.WaitAsync();

            // All callers are joined to the single in-flight entry.
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
    public async Task
        ExecuteAsync_SingleflightInPipeline_OperationFails_AllConcurrentCallersGetSameFailure()
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
            // synchronizes all closures before the finally Dispose, which R#
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
            options: new(
                failureThreshold: 1,
                cooldownDuration: TimeSpan.FromSeconds(60),
                nowFunc: clock.Now));

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
            NoDelayOptions(maxAttempts: 3),
            new CircuitBreakerLayer<string, int>(cb));

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(1));

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // New mappings: TimeoutException + RateLimitRejectedException
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_TimeoutException_MapsToServiceUnavailable()
    {
        // TimeoutException is classified transient by IsTransientException →
        // maps to ServiceUnavailable (503, IsTransientRetryable=true).
        var pipeline = new ResilientPipeline<string, int>();

        var result = await pipeline.ExecuteAsync("k", _ => throw new TimeoutException("slow"));

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();
        result.IsTransientRetryable.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_RateLimitRejectedException_MapsToTooManyRequests()
    {
        // Verifies the new RateLimitRejectedException → TooManyRequests catch arm.
        var pipeline = new ResilientPipeline<string, int>();

        var result = await pipeline.ExecuteAsync("k", _ => throw new RateLimitRejectedException());

        result.Success.Should().BeFalse();
        result.IsRateLimited.Should().BeTrue();
        result.IsTransientRetryable.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // PassThrough sentinel
    // ----------------------------------------------------------------------

    [Fact]
    public async Task PassThrough_IsZeroLayerPipeline_MapsExceptionsToD2Result()
    {
        // PassThrough performs exception→D2Result mapping (it IS a zero-layer pipeline).
        var result = await ResilientPipeline<string, int>.PassThrough.ExecuteAsync(
            "k",
            _ => throw new InvalidOperationException("programmer error"));

        result.Success.Should().BeFalse();
        result.IsUnhandledException.Should().BeTrue();
    }

    [Fact]
    public async Task PassThrough_SuccessfulOp_ReturnsOk()
    {
        var result = await ResilientPipeline<string, int>.PassThrough.ExecuteAsync(
            "k",
            _ => ValueTask.FromResult(99));

        result.Success.Should().BeTrue();
        result.Data.Should().Be(99);
    }

    [Fact]
    public void PassThrough_IsSingletonInstance()
    {
        var a = ResilientPipeline<string, int>.PassThrough;
        var b = ResilientPipeline<string, int>.PassThrough;

        a.Should().BeSameAs(b);
    }

    [Fact]
    public async Task PassThrough_DoesNotRetry_OperationCalledOnce()
    {
        // Zero layers = no retry, even if the exception is transient.
        var calls = 0;

        var result = await ResilientPipeline<string, int>.PassThrough.ExecuteAsync("k", _ =>
        {
            Interlocked.Increment(ref calls);
            throw new TimeoutException();
        });

        result.IsServiceUnavailable.Should().BeTrue();
        calls.Should().Be(1);
    }

    // ----------------------------------------------------------------------
    // Canonical full-stack ordering: RateLimiter → Timeout(total) → Retry → CB → Timeout(per-attempt)
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_CanonicalFullStack_LayersAppliedInOrder()
    {
        // Verify outer-first ordering with all five layer types via a tracing op.
        // The canonical order: RateLimiter → TotalTimeout → Retry → CB → PerAttemptTimeout.
        var trace = new List<string>();
        var pipeline = new ResilientPipeline<string, int>(
            new TracingLayer("rate-limiter", trace),
            new TracingLayer("total-timeout", trace),
            new TracingLayer("retry", trace),
            new TracingLayer("cb", trace),
            new TracingLayer("per-attempt-timeout", trace));

        await pipeline.ExecuteAsync("k", _ =>
        {
            trace.Add("op");
            return ValueTask.FromResult(1);
        });

        trace.Should().Equal(
            "rate-limiter-enter",
            "total-timeout-enter",
            "retry-enter",
            "cb-enter",
            "per-attempt-timeout-enter",
            "op",
            "per-attempt-timeout-exit",
            "cb-exit",
            "retry-exit",
            "total-timeout-exit",
            "rate-limiter-exit");
    }

    private static RetryLayer<string, int> NoDelayOptions(int maxAttempts = 3)
        => new(new()
        {
            MaxAttempts = maxAttempts,
            BaseDelayMs = 0,
            MaxDelayMs = 0,
            Jitter = false,
            DelayFunc = (_, _) => Task.CompletedTask,
        });

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
