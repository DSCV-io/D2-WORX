// -----------------------------------------------------------------------
// <copyright file="RetryHelperTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.Retry;

using System.Net;
using System.Net.Sockets;
using AwesomeAssertions;
using DcsvIo.D2.Resilience.CircuitBreaker;
using DcsvIo.D2.Resilience.Retry;
using DcsvIo.D2.Result;
using Xunit;

public sealed class RetryHelperTests
{
    // ----------------------------------------------------------------------
    // IsTransientException — classifier matrix
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public void IsTransientException_KnownTransientHttpStatuses_AreTransient(HttpStatusCode status)
    {
        var ex = new HttpRequestException("transient", inner: null, statusCode: status);

        RetryHelper.IsTransientException(ex).Should().BeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    public void IsTransientException_NonTransientHttpStatuses_AreNotTransient(
        HttpStatusCode status)
    {
        var ex = new HttpRequestException("non-transient", inner: null, statusCode: status);

        RetryHelper.IsTransientException(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransientException_HttpRequestExceptionWithNullStatus_IsNotTransient()
    {
        var ex = new HttpRequestException("no status");

        RetryHelper.IsTransientException(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransientException_TaskCanceledException_IsTransient()
    {
        RetryHelper.IsTransientException(new TaskCanceledException()).Should().BeTrue();
    }

    [Fact]
    public void IsTransientException_TimeoutException_IsTransient()
    {
        RetryHelper.IsTransientException(new TimeoutException()).Should().BeTrue();
    }

    [Fact]
    public void IsTransientException_SocketException_IsTransient()
    {
        RetryHelper.IsTransientException(
            new SocketException((int)SocketError.ConnectionRefused))
            .Should().BeTrue();
    }

    [Fact]
    public void IsTransientException_CircuitOpenException_IsTransient()
    {
        // Critical for the retry-OUTSIDE-CB composition — the caller's retry
        // layer must back off through CO so that an upstream restart can
        // resolve naturally as the breaker cools down between attempts.
        RetryHelper.IsTransientException(new CircuitOpenException()).Should().BeTrue();
    }

    [Fact]
    public void IsTransientException_OtherException_IsNotTransient()
    {
        RetryHelper.IsTransientException(
            new InvalidOperationException("nope"))
            .Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // RetryAsync — happy path + exhaustion
    // ----------------------------------------------------------------------

    [Fact]
    public async Task RetryAsync_NullOptions_AppliesDefaults()
    {
        // Coverage: the `options ??= new RetryOptions<T>()` branch must be
        // hit when caller passes null.
        var result = await RetryHelper.RetryAsync(
            (_, _) => ValueTask.FromResult(7),
            options: null,
            CancellationToken.None);

        result.Should().Be(7);
    }

    [Fact]
    public async Task RetryAsync_SuccessOnFirstAttempt_ReturnsImmediately()
    {
        var attemptsObserved = 0;

        var result = await RetryHelper.RetryAsync(
            (attempt, _) =>
            {
                attemptsObserved = attempt;
                return ValueTask.FromResult(42);
            },
            options: NoDelayOptions(),
            CancellationToken.None);

        result.Should().Be(42);
        attemptsObserved.Should().Be(1);
    }

    [Fact]
    public async Task RetryAsync_ThrowsTransientThenSucceeds_RetriesAndReturns()
    {
        var attempts = 0;

        var result = await RetryHelper.RetryAsync(
            (attempt, _) =>
            {
                attempts = attempt;
                if (attempt < 3)
                {
                    throw new TimeoutException($"try {attempt}");
                }

                return ValueTask.FromResult(99);
            },
            options: NoDelayOptions(maxAttempts: 5),
            CancellationToken.None);

        result.Should().Be(99);
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task RetryAsync_NonTransientException_ReThrowsImmediately()
    {
        var attempts = 0;

        var act = async () =>
        {
            await RetryHelper.RetryAsync(
                (attempt, _) =>
                {
                    attempts = attempt;
                    throw new InvalidOperationException("permanent");
                },
                options: NoDelayOptions(maxAttempts: 5),
                CancellationToken.None);
        };

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("permanent");
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task RetryAsync_TransientThrownEveryAttempt_ThrowsAfterExhaustion()
    {
        var attempts = 0;

        var act = async () =>
        {
            await RetryHelper.RetryAsync(
                (attempt, _) =>
                {
                    attempts = attempt;
                    throw new TimeoutException();
                },
                options: NoDelayOptions(maxAttempts: 3),
                CancellationToken.None);
        };

        await act.Should().ThrowAsync<TimeoutException>();
        attempts.Should().Be(3);
    }

    // ----------------------------------------------------------------------
    // RetryAsync — value-failure (ShouldRetry) path
    // ----------------------------------------------------------------------

    [Fact]
    public async Task RetryAsync_ShouldRetryReturnsTrueThenFalse_RetriesUntilAccepted()
    {
        var attempts = 0;

        var options = NoDelayOptions(maxAttempts: 5) with
        {
            ShouldRetry = r => r < 3,
        };

        var result = await RetryHelper.RetryAsync(
            (attempt, _) =>
            {
                attempts = attempt;
                return ValueTask.FromResult(attempt);
            },
            options,
            CancellationToken.None);

        result.Should().Be(3);
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task RetryAsync_ShouldRetryAlwaysTrue_ReturnsLastValueAfterExhaustion()
    {
        // Adversarial: caller signals "always retry" but options exhaust.
        // Last-returned value wins (final attempt was a return, not a throw).
        var options = NoDelayOptions(maxAttempts: 3) with
        {
            ShouldRetry = _ => true,
        };

        var result = await RetryHelper.RetryAsync(
            (attempt, _) => ValueTask.FromResult(attempt),
            options,
            CancellationToken.None);

        result.Should().Be(3); // 3rd attempt returned 3
    }

    [Fact]
    public async Task RetryAsync_ReturnedThenThrew_ThrowsLastException()
    {
        // Adversarial: alternating return → throw across attempts. The last
        // attempt threw, so the throw wins on exhaustion.
        var options = NoDelayOptions(maxAttempts: 4) with
        {
            ShouldRetry = _ => true,
        };

        var act = async () => await RetryHelper.RetryAsync(
            (attempt, _) => attempt switch
            {
                4 => throw new TimeoutException("final"),
                _ => ValueTask.FromResult(attempt),
            },
            options,
            CancellationToken.None);

        await act.Should().ThrowAsync<TimeoutException>().WithMessage("final");
    }

    // ----------------------------------------------------------------------
    // RetryAsync — cancellation
    // ----------------------------------------------------------------------

    [Fact]
    public async Task RetryAsync_PreCanceled_ThrowsImmediately()
    {
        // Capture the token by VALUE before the closure so the closure never
        // closes over the IDisposable cts itself (R# AccessToDisposedClosure).
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var token = cts.Token;

        var act = async () =>
        {
            await RetryHelper.RetryAsync(
                (_, _) => ValueTask.FromResult(1),
                NoDelayOptions(),
                token);
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RetryAsync_OperationCanceledExceptionFromCt_PropagatesNotAsTransient()
    {
        // Adversarial: when ct is the SOURCE of cancellation, the catch
        // filter intentionally lets OCE through as cancellation, not as a
        // transient retry candidate. Direct-await (no fluent ThrowAsync
        // lambda) keeps the cts capture inside the using scope, eliminating
        // any AccessToDisposedClosure ambiguity.
        using var cts = new CancellationTokenSource();
        Exception? captured = null;

        try
        {
            await RetryHelper.RetryAsync(
                (_, ct) =>
                {
                    cts.Cancel();
                    ct.ThrowIfCancellationRequested();
                    return ValueTask.FromResult(1);
                },
                NoDelayOptions(),
                cts.Token);
        }
        catch (OperationCanceledException ex)
        {
            captured = ex;
        }

        captured.Should().NotBeNull();
    }

    [Fact]
    public async Task RetryAsync_CtCanceledOce_BypassesCatch_EvenWhenClassifierSaysTransient()
    {
        // Pin: the catch filter `ex is not OperationCanceledException ||
        // !ct.IsCancellationRequested` is what bails out — NOT the IsTransient
        // classifier. Override IsTransient to ALWAYS say "transient." Without
        // the catch-filter exclusion, the OCE would be retried — which would
        // be wrong (caller asked to cancel; retrying is hostile).
        using var cts = new CancellationTokenSource();
        var attempts = 0;
        Exception? captured = null;

        try
        {
            await RetryHelper.RetryAsync(
                (_, ct) =>
                {
                    attempts++;
                    cts.Cancel();
                    ct.ThrowIfCancellationRequested();
                    return ValueTask.FromResult(1);
                },
                NoDelayOptions() with { IsTransient = _ => true },
                cts.Token);
        }
        catch (OperationCanceledException ex)
        {
            captured = ex;
        }

        captured.Should().NotBeNull();
        attempts.Should().Be(1, "the catch filter must bail out, not the classifier");
    }

    // ----------------------------------------------------------------------
    // RetryAsync — DelayFunc invoked between attempts
    // ----------------------------------------------------------------------

    [Fact]
    public async Task RetryAsync_DelayFunc_InvokedBetweenRetries()
    {
        var delayInvocations = 0;
        var observedDelays = new List<TimeSpan>();
        var options = new RetryOptions<int>
        {
            MaxAttempts = 3,
            BaseDelayMs = 10,
            BackoffMultiplier = 2.0,
            MaxDelayMs = 100,
            Jitter = false,
            DelayFunc = (delay, _) =>
            {
                Interlocked.Increment(ref delayInvocations);
                observedDelays.Add(delay);
                return Task.CompletedTask;
            },
        };

        try
        {
            await RetryHelper.RetryAsync(
                (_, _) => throw new TimeoutException(),
                options,
                CancellationToken.None);
        }
        catch (TimeoutException)
        {
            // expected — exhaustion throws
        }

        // 3 attempts → 2 delays between them.
        delayInvocations.Should().Be(2);
        observedDelays.Should().HaveCount(2);
    }

    // ----------------------------------------------------------------------
    // RetryD2ResultAsync — D2Result-aware overload
    // ----------------------------------------------------------------------

    [Fact]
    public async Task RetryD2ResultAsync_DefaultPredicate_RetriesTransientRetryable()
    {
        var attempts = 0;

        var result = await RetryHelper.RetryD2ResultAsync(
            (attempt, _) =>
            {
                attempts = attempt;
                return attempt < 2
                    ? ValueTask.FromResult(D2Result<int>.ServiceUnavailable())
                    : ValueTask.FromResult(D2Result<int>.Ok(7));
            },
            options: NoDelayOptions<D2Result<int>>(maxAttempts: 5),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(7);
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task RetryD2ResultAsync_DefaultPredicate_DoesNotRetryNonTransientFailure()
    {
        // Adversarial: NotFound is failed but NOT transient → no retries.
        var attempts = 0;

        var result = await RetryHelper.RetryD2ResultAsync(
            (attempt, _) =>
            {
                attempts = attempt;
                return ValueTask.FromResult(D2Result<int>.NotFound());
            },
            options: NoDelayOptions<D2Result<int>>(maxAttempts: 5),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.IsNotFound.Should().BeTrue();
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task RetryD2ResultAsync_CallerShouldRetryOverride_TakesPrecedence()
    {
        var attempts = 0;

        var options = NoDelayOptions<D2Result<int>>(maxAttempts: 3) with
        {
            ShouldRetry = _ => false, // never retry, even if transient retryable
        };

        await RetryHelper.RetryD2ResultAsync(
            (attempt, _) =>
            {
                attempts = attempt;
                return ValueTask.FromResult(D2Result<int>.ServiceUnavailable());
            },
            options,
            CancellationToken.None);

        attempts.Should().Be(1);
    }

    [Fact]
    public async Task RetryD2ResultAsync_NullOptions_StillUsesDefaultPredicate()
    {
        // Adversarial: caller passes null options. Default classifier still
        // wires up — but with default delays (1s+), so prove behavior with a
        // single attempt + ShouldRetry never invoked because attempt == max.
        var nullOptionsForceSingleAttempt = new RetryOptions<D2Result<int>>
        {
            MaxAttempts = 1,
            BaseDelayMs = 0,
            DelayFunc = (_, _) => Task.CompletedTask,
        };

        var result = await RetryHelper.RetryD2ResultAsync(
            (_, _) => ValueTask.FromResult(D2Result<int>.ServiceUnavailable()),
            nullOptionsForceSingleAttempt,
            CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task RetryD2ResultAsync_DefaultOptions_AppliesPredicate()
    {
        // Specifically exercises the `options is null → defaults applied`
        // branch in the helper.
        var result = await RetryHelper.RetryD2ResultAsync(
            (_, _) => ValueTask.FromResult(D2Result<int>.Ok(1)),
            options: null,
            CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // CalculateDelay — internal math
    // ----------------------------------------------------------------------

    [Fact]
    public void CalculateDelay_FirstRetry_NoJitter_EqualsBaseDelay()
    {
        var d = RetryHelper.CalculateDelay(
            retryIndex: 0,
            baseDelayMs: 100,
            backoffMultiplier: 2.0,
            maxDelayMs: 10_000,
            jitter: false);

        d.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void CalculateDelay_NoJitter_AppliesBackoffMultiplier()
    {
        var d = RetryHelper.CalculateDelay(
            retryIndex: 3,
            baseDelayMs: 100,
            backoffMultiplier: 2.0,
            maxDelayMs: 10_000,
            jitter: false);

        // 100 * 2^3 = 800ms
        d.Should().Be(TimeSpan.FromMilliseconds(800));
    }

    [Fact]
    public void CalculateDelay_NoJitter_ClampsToMaxDelay()
    {
        var d = RetryHelper.CalculateDelay(
            retryIndex: 20,
            baseDelayMs: 100,
            backoffMultiplier: 2.0,
            maxDelayMs: 5_000,
            jitter: false);

        d.Should().Be(TimeSpan.FromMilliseconds(5_000));
    }

    [Fact]
    public void CalculateDelay_LargeRetryIndex_ClampsExponentSafely()
    {
        // Adversarial: int.MaxValue retryIndex would overflow Math.Pow and
        // produce +Infinity. The helper clamps the exponent to 63 and then
        // Math.Min(+Inf, max) → max, so we never produce a weird TimeSpan.
        var d = RetryHelper.CalculateDelay(
            retryIndex: int.MaxValue,
            baseDelayMs: 100,
            backoffMultiplier: 2.0,
            maxDelayMs: 5_000,
            jitter: false);

        d.Should().Be(TimeSpan.FromMilliseconds(5_000));
    }

    // ----------------------------------------------------------------------
    // Boundary + adversarial edge cases
    // ----------------------------------------------------------------------

    [Fact]
    public async Task RetryAsync_MaxAttemptsZero_ExecutesOnce_ThenPropagates()
    {
        // Boundary: MaxAttempts=0 means `attempt < MaxAttempts` is false on
        // attempt 1 (since attempt starts at 0 then increments to 1), so the
        // first attempt's outcome is the FINAL outcome — no retry loop body
        // executes the continue path. Observable behavior identical to
        // MaxAttempts=1.
        var attempts = 0;
        var act = async () => await RetryHelper.RetryAsync(
            (_, _) =>
            {
                Interlocked.Increment(ref attempts);
                throw new TimeoutException();
            },
            options: NoDelayOptions(maxAttempts: 0),
            CancellationToken.None);

        await act.Should().ThrowAsync<TimeoutException>();
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task RetryAsync_DelayFuncThrows_ExceptionPropagates()
    {
        // Adversarial: a buggy DelayFunc (e.g. test-time fake that throws
        // on a particular delay value, or one that surfaces an unrelated
        // bug) must propagate out of RetryAsync — the lib makes no attempt
        // to suppress unexpected delegate failures.
        var attempts = 0;
        var options = new RetryOptions<int>
        {
            MaxAttempts = 5,
            BaseDelayMs = 100,
            BackoffMultiplier = 1.0,
            MaxDelayMs = 100,
            Jitter = false,
            DelayFunc = (_, _) => throw new InvalidOperationException("delay func boom"),
        };

        var act = async () => await RetryHelper.RetryAsync(
            (_, _) =>
            {
                Interlocked.Increment(ref attempts);
                throw new TimeoutException("transient");
            },
            options,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("delay func boom");

        // Only the FIRST attempt ran; the buggy DelayFunc broke the retry loop.
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task RetryAsync_CtCanceledDuringDelayFunc_PropagatesOce()
    {
        // Adversarial: caller cancels the CT mid-backoff. The DelayFunc
        // (Task.Delay or override) honors the CT; the OCE escapes RetryAsync
        // because the catch filter excludes "OCE when ct is canceled."
        //
        // Note: cts is disposed manually AFTER `act` is awaited so the
        // captured-disposable lambda discipline holds (no `using` here would
        // race with the lambda's reference).
        var cts = new CancellationTokenSource();
        try
        {
            // ReSharper disable AccessToDisposedClosure -- await act below
            // completes the lambda before the finally Dispose, which R# can't
            // prove statically.
            var attempts = 0;
            var options = new RetryOptions<int>
            {
                MaxAttempts = 5,
                BaseDelayMs = 0,
                MaxDelayMs = 0,
                Jitter = false,
                DelayFunc = async (_, ct) =>
                {
                    await cts.CancelAsync();
                    ct.ThrowIfCancellationRequested();
                },
            };

            var act = async () => await RetryHelper.RetryAsync(
                (_, _) =>
                {
                    Interlocked.Increment(ref attempts);
                    throw new TimeoutException("transient");
                },
                options,
                cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();

            // ReSharper restore AccessToDisposedClosure
            // First attempt threw transient; DelayFunc canceled mid-backoff;
            // OCE escaped before the second attempt could begin.
            attempts.Should().Be(1);
        }
        finally
        {
            cts.Dispose();
        }
    }

    [Fact]
    public void CalculateDelay_WithJitter_StaysWithinZeroToCalculated()
    {
        // Property: jittered delay is always in [0, calculated). Run many
        // samples to reduce flakiness from a degenerate single draw.
        const int retry_index = 2;
        const int base_delay_ms = 100;
        const double backoff = 2.0;
        const int max_delay_ms = 10_000;
        const double calculated_max_ms = base_delay_ms * (backoff * backoff); // 400

        for (var i = 0; i < 200; i++)
        {
            var d = RetryHelper.CalculateDelay(
                retry_index,
                base_delay_ms,
                backoff,
                max_delay_ms,
                jitter: true);

            d.TotalMilliseconds.Should().BeInRange(0, calculated_max_ms);
        }
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static RetryOptions<T> NoDelayOptions<T>(int maxAttempts = 3)
    {
        return new RetryOptions<T>
        {
            MaxAttempts = maxAttempts,
            BaseDelayMs = 0,
            BackoffMultiplier = 1.0,
            MaxDelayMs = 0,
            Jitter = false,
            DelayFunc = (_, _) => Task.CompletedTask,
        };
    }

    private static RetryOptions<int> NoDelayOptions(int maxAttempts = 3)
        => NoDelayOptions<int>(maxAttempts);
}
