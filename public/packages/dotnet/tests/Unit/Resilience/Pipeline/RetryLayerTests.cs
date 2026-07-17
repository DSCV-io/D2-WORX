// -----------------------------------------------------------------------
// <copyright file="RetryLayerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.Pipeline;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.Pipeline;
using DcsvIo.D2.Resilience.Retry;
using Xunit;

public sealed class RetryLayerTests
{
    [Fact]
    public async Task WrapAsync_TransientFailure_RetriesPerOptions()
    {
        var attempts = 0;
        var layer = new RetryLayer<string, int>(NoDelayOptions(maxAttempts: 5));

        var result = await layer.WrapAsync(
            "k",
            _ =>
            {
                Interlocked.Increment(ref attempts);
                if (attempts < 3)
                {
                    throw new TimeoutException();
                }

                return ValueTask.FromResult(99);
            },
            CancellationToken.None);

        result.Should().Be(99);
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task WrapAsync_NullOptions_AppliesDefaults()
    {
        // Null options → RetryHelper applies defaults via RetryOptions ctor.
        // Sanity check: a non-throwing operation completes on first attempt.
        var layer = new RetryLayer<string, int>(options: null);

        var result = await layer.WrapAsync(
            "k", _ => ValueTask.FromResult(7), CancellationToken.None);

        result.Should().Be(7);
    }

    [Fact]
    public async Task WrapAsync_KeyArgument_IsIgnored()
    {
        // Retry policy is per-operation, not per-key — the key is ignored.
        var layer = new RetryLayer<string, int>(NoDelayOptions());

        (await layer.WrapAsync("a", _ => ValueTask.FromResult(1), CancellationToken.None))
            .Should().Be(1);
        (await layer.WrapAsync("b", _ => ValueTask.FromResult(2), CancellationToken.None))
            .Should().Be(2);
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
}
