// -----------------------------------------------------------------------
// <copyright file="PipelineCompositionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.Pipeline;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.CircuitBreaker;
using DcsvIo.D2.Resilience.Pipeline;
using DcsvIo.D2.Resilience.RateLimiting;
using DcsvIo.D2.Resilience.Timeout;
using Xunit;
using SingleflightT = DcsvIo.D2.Resilience.Singleflight.Singleflight<string, int>;

/// <summary>
/// Adversarial nesting tests covering all six pipeline layers in varied
/// orderings. Asserts order-of-operations behavior — not just that the
/// pipeline returns a result, but that the specific layer semantics are
/// enforced by the order chosen.
/// <para>
/// Six layers under test:
/// SF = Singleflight (outermost when used),
/// RL = RateLimiter, Tt = Total-Timeout, R = Retry,
/// CB = CircuitBreaker, Ta = Per-Attempt-Timeout.
/// </para>
/// </summary>
public sealed class PipelineCompositionTests
{
    // ----------------------------------------------------------------
    // 1. Canonical full stack: RL → Tt → R → CB → Ta
    //    Flaky op (throws-transient-then-succeeds) → succeeds via R.
    //    Permanently-down op → CB trips, R exhausts → ServiceUnavailable.
    // ----------------------------------------------------------------

    [Fact]
    public async Task FullStack_RL_Tt_R_CB_Ta_FlakyOp_RecoversViaRetry()
    {
        // RL → Tt → R → CB → Ta. The op throws transiently twice then succeeds.
        // Assertions: Success=true, call-count == 3 (1 initial + 2 retries),
        // CB stays Closed (each attempt was a separate CB call; 2 failures
        // increment its counter to 2 which is below threshold=10).
        var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 10, nowFunc: static () => 0L));
        using var pipeline = new ResilientPipeline<string, int>(
            new RateLimiterLayer<string, int>(new RateLimiterOptions(maxConcurrency: 10)),
            new TimeoutLayer<string, int>(new TimeoutOptions(TimeSpan.FromSeconds(30))),
            NoDelayOptions(maxAttempts: 5),
            new CircuitBreakerLayer<string, int>(cb),
            new TimeoutLayer<string, int>(new TimeoutOptions(TimeSpan.FromSeconds(5))));

        var calls = 0;
        var result = await pipeline.ExecuteAsync("k", _ =>
        {
            var n = Interlocked.Increment(ref calls);
            if (n < 3)
                throw new TimeoutException("slow upstream");
            return ValueTask.FromResult(42);
        });

        result.Success.Should().BeTrue();
        result.Data.Should().Be(42);
        calls.Should().Be(3);
        cb.State.Should().Be(CircuitState.Closed);

        // CB saw 3 separate executions; 2 failures → still below threshold=10;
        // final success reset the counter to 0.
        cb.FailureCount.Should().Be(0);
    }

    [Fact]
    public async Task FullStack_RL_Tt_R_CB_Ta_PermanentlyDown_CbTrips_RExhausts_ServiceUnavailable()
    {
        // CB threshold=2; Retry budget=4. Op always throws TimeoutException (transient).
        // CB opens after 2 real-op failures; the 3rd and 4th retry attempts
        // find CB open → CircuitOpenException (also transient) → R retries but
        // CB remains open → retries exhaust → ServiceUnavailable (the last
        // exception is CircuitOpenException → ServiceUnavailable at boundary).
        var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 2, nowFunc: static () => 0L));
        using var pipeline = new ResilientPipeline<string, int>(
            new RateLimiterLayer<string, int>(new RateLimiterOptions(maxConcurrency: 10)),
            new TimeoutLayer<string, int>(new TimeoutOptions(TimeSpan.Zero)),
            NoDelayOptions(maxAttempts: 4),
            new CircuitBreakerLayer<string, int>(cb),
            new TimeoutLayer<string, int>(new TimeoutOptions(TimeSpan.Zero)));

        var realOpCalls = 0;
        var result = await pipeline.ExecuteAsync("k", _ =>
        {
            Interlocked.Increment(ref realOpCalls);

            // TimeoutException is transient — R retries; also CB records failures.
            throw new TimeoutException("permanently slow");
        });

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();

        // CB should have opened (2 real failures hit the threshold).
        cb.State.Should().Be(CircuitState.Open);

        // Real op called exactly 2 times (then CB opened and subsequent
        // retries got CircuitOpenException without reaching the real op).
        realOpCalls.Should().Be(2);
    }

    // ----------------------------------------------------------------
    // 2. CB↔R order sensitivity: the key "order matters" proof.
    //
    //    CB outside R  (R → CB):  each attempt is a separate CB execution.
    //    R outside CB  (CB → R):  one full retry sequence = one CB execution.
    //
    //    With threshold=1 and a 3-attempt retry budget:
    //    - R→CB:  the very first failure opens CB; subsequent retries get CO
    //             (transient) → retry keeps going → budget exhausts → SU.
    //    - CB→R:  the inner R retries twice more, all within one CB execution;
    //             CB counts them as a single sequence and counts 0 successes
    //             + 1 sequence failure → CB opens only after the full sequence
    //             fails. Final CO exits R → SU.
    //
    //    The concrete observable difference: how many times the real op is
    //    called before the pipeline surfaces ServiceUnavailable.
    // ----------------------------------------------------------------

    [Fact]
    public async Task RetryOutsideCb_EachAttemptIsSeparateCbExecution_CbOpensEarly()
    {
        // R → CB (Retry outermost, CB innermost).
        // Op always throws. CB threshold=1 → opens on the very first real-op
        // failure. Subsequent retries hit CircuitOpenException (transient)
        // without reaching the real op at all.
        // Real op call-count == 1; subsequent retries see only CO.
        var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 1, nowFunc: static () => 0L));
        using var pipeline = new ResilientPipeline<string, int>(
            NoDelayOptions(maxAttempts: 4),
            new CircuitBreakerLayer<string, int>(cb));

        var realOpCalls = 0;
        var result = await pipeline.ExecuteAsync("k", _ =>
        {
            Interlocked.Increment(ref realOpCalls);

            // TimeoutException is transient — R retries; CB records each as a failure.
            // After threshold=1 failures, CB opens; subsequent retries get
            // CircuitOpenException (also transient) → R retries but CB stays open
            // → budget exhausts → ServiceUnavailable.
            throw new TimeoutException("down");
        });

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();

        // CB sees one real-op execution; opens on failure; remaining retry
        // attempts never reach the real op.
        realOpCalls.Should().Be(1);
        cb.State.Should().Be(CircuitState.Open);
    }

    [Fact]
    public async Task CbOutsideRetry_FullRetryBudgetIsOneCbExecution_CbOpensAfterFullSequence()
    {
        // CB → R (CB outermost, Retry innermost = upstream-protecting).
        // CB threshold=1 → opens only after ONE full CB-level execution fails.
        // Inner R budget=3 → 3 real-op calls all within that single CB execution.
        // CB opens AFTER the full retry sequence exhausts (because CB sees it as
        // one execution that ultimately threw).
        var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 1, nowFunc: static () => 0L));
        using var pipeline = new ResilientPipeline<string, int>(
            new CircuitBreakerLayer<string, int>(cb),
            NoDelayOptions(maxAttempts: 3));

        var realOpCalls = 0;
        var result = await pipeline.ExecuteAsync("k", _ =>
        {
            Interlocked.Increment(ref realOpCalls);

            // TimeoutException is transient — inner R retries all 3 attempts;
            // they're INSIDE CB so CB counts them as ONE execution.
            // When R exhausts, re-throws TimeoutException (transient) to CB;
            // CB records 1 execution-failure → opens; re-throws → SU at boundary.
            throw new TimeoutException("down");
        });

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();

        // All 3 retry attempts reached the real op (they're INSIDE the CB).
        realOpCalls.Should().Be(3);

        // CB opened only after the full sequence failed (it saw 1 execution).
        cb.State.Should().Be(CircuitState.Open);
    }

    // ----------------------------------------------------------------
    // 3. Total-timeout OUTSIDE retry vs per-attempt-timeout INSIDE retry.
    //
    //    Tt → R: total-timeout fires across the retry loop → terminal SU.
    //    R → Ta: a slow attempt times out → R RETRIES it → eventual success.
    // ----------------------------------------------------------------

    [Fact]
    public async Task TotalTimeoutOutsideRetry_FiresAcrossRetryLoop_TerminalServiceUnavailable()
    {
        // Tt → R. Total-timeout fires before the retry loop completes.
        // TimeoutLayer converts the OCE → TimeoutException when the timeout
        // fired and caller ct was NOT canceled. The pipeline's
        // IsTransientException arm catches TimeoutException → ServiceUnavailable.
        // Very short total budget (50 ms) fires during the first slow operation.
        // This proves the total-timeout terminates the retry loop.
        using var pipeline = new ResilientPipeline<string, int>(
            new TimeoutLayer<string, int>(new TimeoutOptions(TimeSpan.FromMilliseconds(50))),
            NoDelayOptions(maxAttempts: 5));

        var calls = 0;
        var result = await pipeline.ExecuteAsync("k", async ct =>
        {
            Interlocked.Increment(ref calls);

            // Infinite delay (-1 ms): the operation blocks until the timeout CTS fires.
            // Using -1 instead of a finite 500 ms removes the timing race: the timeout
            // fires deterministically before an infinite wait completes, regardless of
            // scheduler pressure or CPU contention. Note: `Timeout` is ambiguous here
            // (DcsvIo.D2.Tests.Unit.Resilience.Timeout namespace shadows System.Threading.Timeout),
            // so the literal -1 is used directly — identical to Timeout.Infinite.
            await Task.Delay(-1, ct);
            return 1;
        });

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();

        // Only 1 call — total-timeout fires during the first attempt, terminating
        // the retry loop before it can schedule a second attempt.
        calls.Should().Be(1);
    }

    [Fact]
    public async Task PerAttemptTimeoutInsideRetry_TimedOutAttemptIsRetried_EventualSuccess()
    {
        // R → Ta. Per-attempt-timeout fires on the first slow attempt.
        // TimeoutLayer converts the OCE → TimeoutException (transient).
        // The outer RetryLayer retries it (TimeoutException IS in IsTransientException).
        // The second attempt is fast → succeeds.
        // Call-count > 1 proves the retry happened after the per-attempt timeout.
        using var pipeline = new ResilientPipeline<string, int>(
            NoDelayOptions(maxAttempts: 3),
            new TimeoutLayer<string, int>(new TimeoutOptions(TimeSpan.FromMilliseconds(50))));

        var calls = 0;
        var result = await pipeline.ExecuteAsync("k", async ct =>
        {
            var n = Interlocked.Increment(ref calls);

            if (n == 1)
            {
                // Infinite delay (-1 ms): blocks until the per-attempt timeout CTS fires.
                // Using -1 instead of a finite 500 ms removes the timing race — the
                // 50 ms timeout fires deterministically before an infinite wait completes,
                // regardless of scheduler jitter. Note: `Timeout` is ambiguous here
                // (DcsvIo.D2.Tests.Unit.Resilience.Timeout namespace shadows System.Threading.Timeout),
                // so the literal -1 is used directly — identical to Timeout.Infinite.
                await Task.Delay(-1, ct);
            }

            return 42; // Second attempt returns immediately.
        });

        result.Success.Should().BeTrue();
        result.Data.Should().Be(42);

        // First attempt timed out and was retried; second attempt succeeded.
        calls.Should().Be(2);
    }

    // ----------------------------------------------------------------
    // 4. RateLimiter outermost short-circuits before inner layers execute.
    //    RL (full) → R → CB: rejected caller gets TooManyRequests;
    //    inner op and CB never execute for the rejected call.
    // ----------------------------------------------------------------

    [Fact]
    public async Task RateLimiter_Outermost_GateFull_ShortCircuits_InnerLayersNeverExecute()
    {
        // RL (maxConcurrency=1) → R → CB. Gate is held by a first concurrent
        // call. Second call can't acquire a permit (zero acquisition timeout) →
        // RateLimitRejectedException → TooManyRequests.
        // The inner R+CB layers never execute for the rejected call.
        var cb = new CircuitBreaker<int>(
            _ => false,
            options: new(failureThreshold: 100));

        using var rateLimiter = new DcsvIo.D2.Resilience.RateLimiting.RateLimiter(
            new RateLimiterOptions(maxConcurrency: 1, acquisitionTimeout: TimeSpan.Zero));

        using var pipeline = new ResilientPipeline<string, int>(
            new RateLimiterLayer<string, int>(rateLimiter),
            NoDelayOptions(maxAttempts: 3),
            new CircuitBreakerLayer<string, int>(cb));

        var innerOpCalls = 0;
        var gate = new TaskCompletionSource();
        var acquired = new TaskCompletionSource(); // signals: permit is held

        // Hold the gate with the first caller.
        var first = pipeline.ExecuteAsync("k", async _ =>
        {
            acquired.SetResult();
            await gate.Task;
            Interlocked.Increment(ref innerOpCalls);
            return 1;
        }).AsTask();

        // Wait until the permit is demonstrably held before the rejected call.
        await acquired.Task;

        // Second caller: cannot acquire → TooManyRequests immediately.
        var rejected = await pipeline.ExecuteAsync("k", _ =>
        {
            Interlocked.Increment(ref innerOpCalls);
            return ValueTask.FromResult(99);
        });

        gate.SetResult();
        await first;

        rejected.Success.Should().BeFalse();
        rejected.IsRateLimited.Should().BeTrue();

        // Only the first caller's op ran; the rejected call's inner op was never reached.
        innerOpCalls.Should().Be(1);
        cb.FailureCount.Should().Be(0); // CB never saw the rejection.
    }

    // ----------------------------------------------------------------
    // 5. RateLimiter rejection is NOT transient-retried.
    //    R → RL (RL inside Retry): RateLimitRejectedException is not in
    //    IsTransientException → terminal TooManyRequests after ONE inner attempt.
    // ----------------------------------------------------------------

    [Fact]
    public async Task RetryOutsideRateLimiter_Rejection_IsNotRetried_TerminalTooManyRequests()
    {
        // R → RL (Retry outermost, RateLimiter innermost).
        // Gate is permanently full (maxConcurrency=1, held by a background op,
        // acquisitionTimeout=Zero). RateLimitRejectedException is NOT in
        // IsTransientException → R does NOT retry.
        // Result: TooManyRequests after a single attempt.
        using var rateLimiter = new DcsvIo.D2.Resilience.RateLimiting.RateLimiter(
            new RateLimiterOptions(maxConcurrency: 1, acquisitionTimeout: TimeSpan.Zero));

        using var pipeline = new ResilientPipeline<string, int>(
            NoDelayOptions(maxAttempts: 5),
            new RateLimiterLayer<string, int>(rateLimiter));

        var gate = new TaskCompletionSource();
        var acquired = new TaskCompletionSource(); // signals: permit is held

        // Hold the only permit.
        var holder = rateLimiter.ExecuteAsync(
            async _ =>
            {
                acquired.SetResult();
                await gate.Task;
                return 0;
            },
            CancellationToken.None).AsTask();

        // Wait until the permit is demonstrably held before the pipeline call.
        await acquired.Task;

        var innerCalls = 0;
        var result = await pipeline.ExecuteAsync("k", _ =>
        {
            Interlocked.Increment(ref innerCalls);
            return ValueTask.FromResult(1);
        });

        gate.SetResult();
        await holder;

        result.Success.Should().BeFalse();
        result.IsRateLimited.Should().BeTrue();

        // RateLimitRejectedException is not transient → R does not retry → inner op never reached.
        innerCalls.Should().Be(0);
    }

    // ----------------------------------------------------------------
    // 6. Singleflight outermost deduplicates across the full stack.
    //    N concurrent same-key callers → stack executes ONCE, all share result.
    //    Distinct keys → distinct executions.
    // ----------------------------------------------------------------

    [Fact]
    public async Task Singleflight_Outermost_DedupsAcrossFullStack_ConcurrentSameKey()
    {
        // SF → RL → R → CB → Ta. 10 concurrent callers with the same key.
        // The full inner stack (RL+R+CB+Ta) should execute ONCE for the shared key.
        var sf = new SingleflightT();
        var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 100));
        var pipeline = new ResilientPipeline<string, int>(
            new SingleflightLayer<string, int>(sf),
            new RateLimiterLayer<string, int>(new RateLimiterOptions(maxConcurrency: 10)),
            NoDelayOptions(maxAttempts: 3),
            new CircuitBreakerLayer<string, int>(cb),
            new TimeoutLayer<string, int>(new TimeoutOptions(TimeSpan.FromSeconds(5))));

        ThreadPool.GetMinThreads(out var origWorker, out var origIo);
        ThreadPool.SetMinThreads(Math.Max(origWorker, 30), Math.Max(origIo, 30));
        try
        {
            var innerOpCalls = 0;
            var gate = new TaskCompletionSource();

            // Determinism guarantee: ResilientPipeline.ExecuteAsync is async; its
            // first synchronous path builds the layer chain then calls wrapped(ct),
            // which invokes SingleflightLayer.WrapAsync → Singleflight.ExecuteAsync
            // → r_inflight.GetOrAdd synchronously — all before the first await.
            // Capturing the ValueTask<D2Result<int>> (without awaiting) therefore
            // proves the caller has joined the singleflight entry. Signalling AFTER
            // capture makes the wait on all N signals a guarantee that all callers
            // have joined before the gate opens — no SpinWait/Yield needed.
            const int concurrent_callers = 10;
            var allJoined = new SemaphoreSlim(0, concurrent_callers);

            var tasks = Enumerable.Range(0, concurrent_callers)
                .Select(_ => Task.Run(async () =>
                {
                    var vt = pipeline.ExecuteAsync("same-key", async _ =>
                    {
                        Interlocked.Increment(ref innerOpCalls);
                        await gate.Task;
                        return 77;
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

            // Inner stack executed exactly once.
            innerOpCalls.Should().Be(1);

            // All callers receive the same result.
            results.Should().AllSatisfy(r =>
            {
                r.Success.Should().BeTrue();
                r.Data.Should().Be(77);
            });
        }
        finally
        {
            ThreadPool.SetMinThreads(origWorker, origIo);
        }
    }

    [Fact]
    public async Task Singleflight_Outermost_DistinctKeys_ExecuteDistinctly()
    {
        // SF outermost. Two distinct keys → two independent executions.
        var sf = new SingleflightT();
        using var pipeline = new ResilientPipeline<string, int>(
            new SingleflightLayer<string, int>(sf),
            NoDelayOptions(maxAttempts: 2));

        var calls = 0;
        var r1 = await pipeline.ExecuteAsync("key-A", _ =>
        {
            Interlocked.Increment(ref calls);
            return ValueTask.FromResult(1);
        });
        var r2 = await pipeline.ExecuteAsync("key-B", _ =>
        {
            Interlocked.Increment(ref calls);
            return ValueTask.FromResult(2);
        });

        r1.Success.Should().BeTrue();
        r2.Success.Should().BeTrue();
        calls.Should().Be(2); // Each key executed its own op.
    }

    // ----------------------------------------------------------------
    // 7. Caller-cancellation through a deep stack is NOT masked.
    //    RL → Tt → R → CB → Ta: caller cancels mid-op → Canceled.
    // ----------------------------------------------------------------

    [Fact]
    public async Task CallerCancellation_ThroughDeepStack_MapsToCancel_NotServiceUnavailable()
    {
        // RL → Tt → R → CB → Ta. Caller cancels their CancellationToken while
        // the inner op is in progress. The OCE propagates out through all layers
        // (TimeoutLayer's when-guard: !ct.IsCancellationRequested is false →
        // passes through as OCE; RetryHelper catches OCE from ct and re-throws;
        // pipeline boundary catches OCE when ct.IsCancellationRequested → Canceled).
        var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 100));
        using var pipeline = new ResilientPipeline<string, int>(
            new RateLimiterLayer<string, int>(new RateLimiterOptions(maxConcurrency: 10)),
            new TimeoutLayer<string, int>(new TimeoutOptions(TimeSpan.FromSeconds(10))),
            NoDelayOptions(maxAttempts: 3),
            new CircuitBreakerLayer<string, int>(cb),
            new TimeoutLayer<string, int>(new TimeoutOptions(TimeSpan.FromSeconds(5))));

        using var cts = new CancellationTokenSource();
        var result = await pipeline.ExecuteAsync(
            "k",
            async ct =>
            {
                await cts.CancelAsync();
                ct.ThrowIfCancellationRequested();
                return 1;
            },
            cts.Token);

        result.Success.Should().BeFalse();
        result.IsCanceled.Should().BeTrue();
        result.IsServiceUnavailable.Should().BeFalse();
        result.IsRateLimited.Should().BeFalse();
    }

    // ----------------------------------------------------------------
    // 8. PassThrough (zero-layer) maps exceptions to D2Result, applies no
    //    retry/break/limit/timeout.
    // ----------------------------------------------------------------

    [Fact]
    public async Task PassThrough_ZeroLayers_MapsExceptionNoRetry_CallCountOne()
    {
        // Zero layers → no retry, no CB, no timeout, no RL.
        // A transient exception (TimeoutException) is mapped to ServiceUnavailable
        // with exactly ONE op invocation.
        var calls = 0;

        var result = await ResilientPipeline<string, int>.PassThrough.ExecuteAsync("k", _ =>
        {
            Interlocked.Increment(ref calls);
            throw new TimeoutException("no retry in PassThrough");
        });

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();
        calls.Should().Be(1); // Zero retry layers — only one attempt.
    }

    // ----------------------------------------------------------------
    // Helper factories
    // ----------------------------------------------------------------

    private static RetryLayer<string, int> NoDelayOptions(int maxAttempts = 3)
        => new(new()
        {
            MaxAttempts = maxAttempts,
            BaseDelayMs = 0,
            MaxDelayMs = 0,
            Jitter = false,
            DelayFunc = (_, _) => Task.CompletedTask,
        });
}
