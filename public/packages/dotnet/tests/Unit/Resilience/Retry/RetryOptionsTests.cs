// -----------------------------------------------------------------------
// <copyright file="RetryOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.Retry;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.Retry;
using Xunit;

public sealed class RetryOptionsTests
{
    [Fact]
    public void Defaults_MatchInternalConstants()
    {
        // RetryOptions<T> + RetryDefaults together are the single source of
        // truth for defaults (RetryHelper.RetryAsync defers to them). Verify
        // every property defaults to the corresponding internal constant /
        // cached delegate rather than a re-stated literal — would fail if
        // the two ever drift.
        var options = new RetryOptions<int>();

        options.MaxAttempts.Should().Be(RetryDefaults.MAX_ATTEMPTS);
        options.BaseDelayMs.Should().Be(RetryDefaults.BASE_DELAY_MS);
        options.BackoffMultiplier.Should().Be(RetryDefaults.BACKOFF_MULTIPLIER);
        options.MaxDelayMs.Should().Be(RetryDefaults.MAX_DELAY_MS);
        options.Jitter.Should().Be(RetryDefaults.JITTER);
        options.ShouldRetry.Should().BeSameAs(RetryOptions<int>.SR_DefaultShouldRetry);
        options.IsTransient.Should().BeSameAs(RetryDefaults.SR_IsTransient);
        options.DelayFunc.Should().BeSameAs(RetryDefaults.SR_DelayFunc);
    }

    [Fact]
    public void DefaultShouldRetry_NeverRetries()
    {
        // Sanity check on the cached default predicate's behavior — accepts
        // every returned value (i.e. retries are exception-driven only).
        RetryOptions<int>.SR_DefaultShouldRetry(0).Should().BeFalse();
        RetryOptions<int>.SR_DefaultShouldRetry(int.MaxValue).Should().BeFalse();
    }

    [Fact]
    public void DefaultIsTransient_DelegatesToHelperClassifier()
    {
        // The cached default delegate must point at RetryHelper.IsTransientException.
        RetryDefaults.SR_IsTransient(new TimeoutException()).Should().BeTrue();
        RetryDefaults.SR_IsTransient(new InvalidOperationException()).Should().BeFalse();
    }

    [Fact]
    public void DefaultDelayFunc_DelegatesToTaskDelay()
    {
        // Behavior check: zero delay completes ~immediately (matches Task.Delay).
        RetryDefaults.SR_DelayFunc(TimeSpan.Zero, CancellationToken.None)
            .IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void InitOnly_OverridesAreApplied()
    {
        Func<int, bool> shouldRetry = _ => false;
        Func<Exception, bool> isTransient = _ => false;
        Func<TimeSpan, CancellationToken, Task> delayFunc = (_, _) => Task.CompletedTask;

        var options = new RetryOptions<int>
        {
            MaxAttempts = 3,
            BaseDelayMs = 50,
            BackoffMultiplier = 1.5,
            MaxDelayMs = 5_000,
            Jitter = false,
            ShouldRetry = shouldRetry,
            IsTransient = isTransient,
            DelayFunc = delayFunc,
        };

        options.MaxAttempts.Should().Be(3);
        options.BaseDelayMs.Should().Be(50);
        options.BackoffMultiplier.Should().Be(1.5);
        options.MaxDelayMs.Should().Be(5_000);
        options.Jitter.Should().BeFalse();
        options.ShouldRetry.Should().BeSameAs(shouldRetry);
        options.IsTransient.Should().BeSameAs(isTransient);
        options.DelayFunc.Should().BeSameAs(delayFunc);
    }
}
