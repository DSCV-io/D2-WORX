// -----------------------------------------------------------------------
// <copyright file="SingleflightLayerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.Pipeline;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.Pipeline;
using DcsvIo.D2.Resilience.Singleflight;
using Xunit;

public sealed class SingleflightLayerTests
{
    [Fact]
    public async Task WrapAsync_DelegatesToUnderlyingSingleflight()
    {
        // Concurrent identical calls funnel through the underlying SF, so
        // the inner operation runs exactly ONCE — proves the layer wires
        // the per-call key into Singleflight.ExecuteAsync correctly.
        var sf = new Singleflight<string, int>();
        var layer = new SingleflightLayer<string, int>(sf);
        var invocations = 0;
        var gate = new TaskCompletionSource();

        async ValueTask<int> Operation(CancellationToken ct)
        {
            Interlocked.Increment(ref invocations);
            await gate.Task;
            return 7;
        }

        var t1 = layer.WrapAsync("k", Operation, CancellationToken.None).AsTask();
        var t2 = layer.WrapAsync("k", Operation, CancellationToken.None).AsTask();

        await Task.Delay(20);
        gate.SetResult();

        var results = await Task.WhenAll(t1, t2);

        invocations.Should().Be(1);
        results.Should().Equal(7, 7);
    }

    [Fact]
    public async Task WrapAsync_DifferentKeys_RunIndependently()
    {
        // Layer must pass the key through to SF — different keys = different
        // dedup buckets = both operations execute.
        var sf = new Singleflight<string, int>();
        var layer = new SingleflightLayer<string, int>(sf);
        var invocations = 0;
        var gate = new TaskCompletionSource();

        async ValueTask<int> Operation(CancellationToken ct)
        {
            Interlocked.Increment(ref invocations);
            await gate.Task;
            return 1;
        }

        var ta = layer.WrapAsync("a", Operation, CancellationToken.None).AsTask();
        var tb = layer.WrapAsync("b", Operation, CancellationToken.None).AsTask();

        await Task.Delay(20);
        gate.SetResult();
        await Task.WhenAll(ta, tb);

        invocations.Should().Be(2);
    }
}
