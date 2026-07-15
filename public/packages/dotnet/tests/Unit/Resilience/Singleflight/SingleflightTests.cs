// -----------------------------------------------------------------------
// <copyright file="SingleflightTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.Singleflight;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.Singleflight;
using Xunit;

public sealed class SingleflightTests
{
    // ----------------------------------------------------------------------
    // Single call — sanity
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_SingleCall_ReturnsOperationResult()
    {
        var sf = new Singleflight<string, int>();

        var result = await sf.ExecuteAsync("k", _ => ValueTask.FromResult(42));

        result.Should().Be(42);
        sf.Size.Should().Be(0); // key removed after completion
    }

    // ----------------------------------------------------------------------
    // Deduplication: same key → operation runs once
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ConcurrentCallersSameKey_OperationRunsOnce()
    {
        // Gate the operation so we KNOW callers are concurrent before it
        // completes — proves dedup, not just sequential reuse.
        var sf = new Singleflight<string, int>();
        var operationInvocations = 0;
        var gate = new TaskCompletionSource();

        async ValueTask<int> Operation(CancellationToken ct)
        {
            Interlocked.Increment(ref operationInvocations);
            await gate.Task;
            return 7;
        }

        var t1 = sf.ExecuteAsync("k", Operation).AsTask();
        var t2 = sf.ExecuteAsync("k", Operation).AsTask();
        var t3 = sf.ExecuteAsync("k", Operation).AsTask();

        // Allow tasks to enter the operation; gate is still closed so they
        // wait. Size briefly reflects the in-flight state.
        await Task.Delay(20);
        sf.Size.Should().Be(1);

        gate.SetResult();
        var results = await Task.WhenAll(t1, t2, t3);

        operationInvocations.Should().Be(1);
        results.Should().Equal(7, 7, 7);
        sf.Size.Should().Be(0);
    }

    // ----------------------------------------------------------------------
    // Different keys → independent operations
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ConcurrentCallersDifferentKeys_BothOperationsRun()
    {
        var sf = new Singleflight<string, int>();
        var invocations = 0;
        var gate = new TaskCompletionSource();

        async ValueTask<int> OperationA(CancellationToken ct)
        {
            Interlocked.Increment(ref invocations);
            await gate.Task;
            return 1;
        }

        async ValueTask<int> OperationB(CancellationToken ct)
        {
            Interlocked.Increment(ref invocations);
            await gate.Task;
            return 2;
        }

        var ta = sf.ExecuteAsync("a", OperationA).AsTask();
        var tb = sf.ExecuteAsync("b", OperationB).AsTask();

        await Task.Delay(20);
        sf.Size.Should().Be(2);

        gate.SetResult();
        var results = await Task.WhenAll(ta, tb);

        invocations.Should().Be(2);
        results.Should().BeEquivalentTo([1, 2]);
    }

    // ----------------------------------------------------------------------
    // Key removal after completion
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_SequentialCallsSameKey_OperationRunsTwice()
    {
        // Adversarial: NOT a cache. After the first completes, the second
        // call must re-run the operation.
        var sf = new Singleflight<string, int>();
        var invocations = 0;

        async ValueTask<int> Operation(CancellationToken ct)
        {
            Interlocked.Increment(ref invocations);
            await Task.Yield();
            return 1;
        }

        await sf.ExecuteAsync("k", Operation);
        await sf.ExecuteAsync("k", Operation);

        invocations.Should().Be(2);
        sf.Size.Should().Be(0);
    }

    // ----------------------------------------------------------------------
    // Exception propagation
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_OperationThrows_ExceptionPropagatesToAllWaiters()
    {
        // All concurrent waiters share the same Task, so all see the same
        // exception. Key is removed in the finally block so subsequent calls
        // can retry.
        var sf = new Singleflight<string, int>();
        var gate = new TaskCompletionSource();

        async ValueTask<int> Operation(CancellationToken ct)
        {
            await gate.Task;
            throw new InvalidOperationException("operation failed");
        }

        var t1 = sf.ExecuteAsync("k", Operation).AsTask();
        var t2 = sf.ExecuteAsync("k", Operation).AsTask();

        await Task.Delay(20);
        gate.SetResult();

        var act1 = async () => await t1;
        var act2 = async () => await t2;

        await act1.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("operation failed");
        await act2.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("operation failed");

        sf.Size.Should().Be(0); // key removed even on failure
    }

    [Fact]
    public async Task ExecuteAsync_AfterException_NextCallStartsFreshOperation()
    {
        var sf = new Singleflight<string, int>();
        var invocations = 0;

        async ValueTask<int> Operation(CancellationToken ct)
        {
            await Task.Yield();
            Interlocked.Increment(ref invocations);
            throw new InvalidOperationException();
        }

        for (var i = 0; i < 2; i++)
        {
            try
            {
                await sf.ExecuteAsync("k", Operation);
            }
            catch (InvalidOperationException)
            {
                // expected
            }
        }

        invocations.Should().Be(2); // proves the key was cleared after each failure
    }

    // ----------------------------------------------------------------------
    // Per-caller cancellation
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_PerCallerCancellation_DoesNotAffectSiblings()
    {
        // Adversarial property: caller A cancels, but the shared operation
        // and caller B keep running. This is the defining promise of the
        // singleflight design.
        var sf = new Singleflight<string, int>();
        var gate = new TaskCompletionSource();
        var operationCanceledFlag = false;

        async ValueTask<int> Operation(CancellationToken ct)
        {
            await gate.Task;

            // The shared operation receives CancellationToken.None — it
            // ignores per-caller cancellations.
            operationCanceledFlag = ct.IsCancellationRequested;
            return 7;
        }

        using var ctsA = new CancellationTokenSource();
        var taskA = sf.ExecuteAsync("k", Operation, ctsA.Token).AsTask();
        var taskB = sf.ExecuteAsync("k", Operation, CancellationToken.None).AsTask();

        await Task.Delay(20);
        await ctsA.CancelAsync();

        var actA = async () => await taskA;
        await actA.Should().ThrowAsync<OperationCanceledException>();

        // Sibling B still pending; release the gate.
        gate.SetResult();
        var resultB = await taskB;

        resultB.Should().Be(7);
        operationCanceledFlag.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_NoCancellableToken_TakesFastPath()
    {
        // Coverage: ct.CanBeCanceled == false branch (the awaiter on the
        // shared task itself, no WaitAsync wrapper).
        var sf = new Singleflight<string, int>();

        var result = await sf.ExecuteAsync(
            "k",
            _ => ValueTask.FromResult(11),
            CancellationToken.None);

        result.Should().Be(11);
    }

    // ----------------------------------------------------------------------
    // Generic key types — TKey: notnull constraint
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_NonStringKey_DedupesByEquatableValue()
    {
        // Adversarial: prove TKey isn't shoehorned to string. (Guid keys
        // are hashable + equatable just like strings.)
        var sf = new Singleflight<Guid, int>();
        var key = Guid.NewGuid();
        var invocations = 0;
        var gate = new TaskCompletionSource();

        async ValueTask<int> Operation(CancellationToken ct)
        {
            Interlocked.Increment(ref invocations);
            await gate.Task;
            return 5;
        }

        var t1 = sf.ExecuteAsync(key, Operation).AsTask();
        var t2 = sf.ExecuteAsync(key, Operation).AsTask();

        await Task.Delay(20);
        gate.SetResult();
        await Task.WhenAll(t1, t2);

        invocations.Should().Be(1);
    }

    // ----------------------------------------------------------------------
    // High-concurrency stress + memory cleanup
    // ----------------------------------------------------------------------

    [Fact]
    public async Task HighConcurrency_ManyCallersSameKey_OperationRunsExactlyOnce()
    {
        // 200 concurrent waiters racing to add the same key. Lazy<>'s
        // ExecutionAndPublication mode + ConcurrentDictionary.GetOrAdd
        // together must guarantee exactly ONE operation invocation. All
        // 200 callers must see the same returned value.
        //
        // Determinism guarantee: Singleflight.ExecuteAsync is an async method
        // whose first synchronous operation is r_inflight.GetOrAdd (the join
        // into the shared in-flight entry). No await precedes GetOrAdd, so
        // calling ExecuteAsync and capturing the returned ValueTask — without
        // awaiting it — is proof the caller has already joined. Signalling
        // AFTER capturing the ValueTask therefore guarantees all 200 callers
        // are joined before the gate opens: no SpinWait/Yield timing guess
        // is needed. Pre-warm the threadpool because the default growth rate
        // (one worker per ~0.5s) would let late callers arrive AFTER the
        // operation already returned, each starting their own invocation.
        ThreadPool.GetMinThreads(out var origWorker, out var origIo);
        ThreadPool.SetMinThreads(Math.Max(origWorker, 256), Math.Max(origIo, 256));
        try
        {
            var sf = new Singleflight<string, int>();
            var invocations = 0;
            var gate = new TaskCompletionSource<int>();
            const int concurrent_threads = 200;

            // Signal AFTER capturing the ValueTask from ExecuteAsync (not before
            // calling it). GetOrAdd runs synchronously as the first operation
            // inside ExecuteAsync, so capturing the ValueTask proves the join has
            // already happened. All 200 signals == all 200 callers joined.
            var allJoined = new SemaphoreSlim(0, concurrent_threads);

            async ValueTask<int> Operation(CancellationToken ct)
            {
                Interlocked.Increment(ref invocations);
                return await gate.Task;
            }

            var tasks = Enumerable.Range(0, concurrent_threads)
                .Select(_ => Task.Run(async () =>
                {
                    var vt = sf.ExecuteAsync("k", Operation); // join is synchronous; GetOrAdd already ran
                    allJoined.Release();                       // signal AFTER join, not before
                    return await vt;
                }))
                .ToArray();

            // Wait for all 200 callers to have joined — now guaranteed, not polled.
            for (var i = 0; i < concurrent_threads; i++)
                await allJoined.WaitAsync();

            // All callers are joined to the single in-flight entry.
            sf.Size.Should().Be(1);
            gate.SetResult(42);

            var results = await Task.WhenAll(tasks);

            invocations.Should().Be(1);
            results.Should().AllBeEquivalentTo(42);
            sf.Size.Should().Be(0);
        }
        finally
        {
            ThreadPool.SetMinThreads(origWorker, origIo);
        }
    }

    [Fact]
    public async Task ManyUniqueKeys_AfterAllComplete_SizeReturnsToZero()
    {
        // Adversarial: 500 distinct keys execute concurrently. After all
        // complete, the in-flight dictionary must be empty — proves the
        // RunAsync finally-block reliably removes keys even under load.
        var sf = new Singleflight<string, int>();
        const int unique_keys = 500;

        var tasks = Enumerable.Range(0, unique_keys).Select(i => Task.Run(async () =>
        {
            return await sf.ExecuteAsync($"key-{i}", async _ =>
            {
                await Task.Yield();
                return i;
            });
        })).ToArray();

        await Task.WhenAll(tasks);

        sf.Size.Should().Be(0);
    }

    [Fact]
    public async Task Reentrancy_OperationCallsBackWithDifferentKey_Works()
    {
        // Adversarial property: an operation may safely call back into the
        // same Singleflight instance with a DIFFERENT key. (Same-key
        // reentrancy would deadlock — Lazy.Value blocks waiting for the
        // outer call to complete, which is itself waiting for the inner.
        // Tested separately is too risky; this test pins the safe case.)
        var sf = new Singleflight<string, int>();

        var result = await sf.ExecuteAsync("outer", async outerCt =>
        {
            var inner = await sf.ExecuteAsync(
                "inner",
                _ => ValueTask.FromResult(7),
                outerCt);
            return inner * 2;
        });

        result.Should().Be(14);
        sf.Size.Should().Be(0);
    }
}
