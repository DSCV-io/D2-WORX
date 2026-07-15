// -----------------------------------------------------------------------
// <copyright file="TestClockTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Time;

using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using NodaTime;
using Xunit;
using IClock = DcsvIo.D2.Time.IClock;
using TestClock = DcsvIo.D2.Time.TestClock;

public sealed class TestClockTests
{
    [Fact]
    public void Constructor_WithInitialInstant_NowEqualsInitial()
    {
        var initial = Instant.FromUnixTimeSeconds(1000);

        var clock = new TestClock(initial);

        clock.Now.Should().Be(initial);
    }

    [Fact]
    public void GetCurrentInstant_MatchesNow()
    {
        var clock = new TestClock(Instant.FromUnixTimeSeconds(42));

        clock.GetCurrentInstant().Should().Be(clock.Now);
    }

    [Fact]
    public void Advance_PositiveDuration_ShiftsNowForward()
    {
        var clock = new TestClock(Instant.FromUnixTimeSeconds(0));

        clock.Advance(Duration.FromSeconds(10));

        clock.Now.Should().Be(Instant.FromUnixTimeSeconds(10));
    }

    [Fact]
    public void Advance_NegativeDuration_ShiftsNowBackward()
    {
        var clock = new TestClock(Instant.FromUnixTimeSeconds(100));

        clock.Advance(Duration.FromSeconds(-30));

        clock.Now.Should().Be(Instant.FromUnixTimeSeconds(70));
    }

    [Fact]
    public void Advance_ZeroDuration_NowUnchanged()
    {
        var initial = Instant.FromUnixTimeSeconds(500);
        var clock = new TestClock(initial);

        clock.Advance(Duration.Zero);

        clock.Now.Should().Be(initial);
    }

    [Fact]
    public void SetTo_ExplicitInstant_OverridesNow()
    {
        var clock = new TestClock(Instant.FromUnixTimeSeconds(0));

        clock.SetTo(Instant.FromUnixTimeSeconds(9999));

        clock.Now.Should().Be(Instant.FromUnixTimeSeconds(9999));
    }

    [Fact]
    public void SetTo_CalledTwice_UsesSecondValue()
    {
        var clock = new TestClock(Instant.FromUnixTimeSeconds(0));

        clock.SetTo(Instant.FromUnixTimeSeconds(100));
        clock.SetTo(Instant.FromUnixTimeSeconds(200));

        clock.Now.Should().Be(Instant.FromUnixTimeSeconds(200));
    }

    [Fact]
    public void SetTo_SameValueAsNow_IsNoOp()
    {
        var initial = Instant.FromUnixTimeSeconds(1234);
        var clock = new TestClock(initial);

        clock.SetTo(initial);
        clock.SetTo(initial);

        clock.Now.Should().Be(initial);
    }

    [Fact]
    public void Advance_AfterSetTo_AppliesFromNewBase()
    {
        var clock = new TestClock(Instant.FromUnixTimeSeconds(0));

        clock.SetTo(Instant.FromUnixTimeSeconds(500));
        clock.Advance(Duration.FromSeconds(50));

        clock.Now.Should().Be(Instant.FromUnixTimeSeconds(550));
    }

    [Fact]
    public async Task ConcurrentReads_AllReturnValidInstant_NoRace()
    {
        var clock = new TestClock(Instant.FromUnixTimeSeconds(0));
        const int reader_count = 50;
        const int reads_per_reader = 200;
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var writer = Task.Run(
            async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    clock.Advance(Duration.FromSeconds(1));
                    await Task.Yield();
                }
            },
            CancellationToken.None);

        var readers = new Task[reader_count];
        for (var i = 0; i < reader_count; i++)
        {
            readers[i] = Task.Run(
                () =>
                {
                    for (var j = 0; j < reads_per_reader; j++)
                    {
                        var read = clock.GetCurrentInstant();
                        read.Should().BeGreaterThanOrEqualTo(Instant.FromUnixTimeSeconds(0));
                    }
                },
                CancellationToken.None);
        }

        await Task.WhenAll(readers);
        cts.Cancel();
        await writer;
    }

    [Fact]
    public void TestClock_ImplementsIClock()
    {
        typeof(TestClock).IsAssignableTo(typeof(IClock)).Should().BeTrue();
    }

    [Fact]
    public void TestClock_IsSealed()
    {
        typeof(TestClock).IsSealed.Should().BeTrue();
    }

    // --- Adversarial boundary + concurrency ---

    [Fact]
    public void Advance_NegativeDurationCrossingEpochBoundary_NoOverflow()
    {
        var clock = new TestClock(Instant.FromUnixTimeSeconds(100));

        clock.Advance(Duration.FromSeconds(-200));

        clock.Now.Should().Be(Instant.FromUnixTimeSeconds(-100));
    }

    [Fact]
    public void Advance_TowardMinInstant_LargeNegativeDuration_HandledOrThrowsCleanly()
    {
        // Document NodaTime's overflow behavior at the lower boundary. The
        // observed behavior is that Plus throws OverflowException when the
        // resulting instant would underflow Instant.MinValue. Test pins the
        // contract — if NodaTime ever changes to a saturating semantic, this
        // test will surface the difference.
        var clock = new TestClock(Instant.MinValue + Duration.FromHours(1));

        var act = () => clock.Advance(Duration.FromHours(-48));

        act.Should().Throw<OverflowException>();
    }

    [Fact]
    public void SetTo_MaxInstant_StoredCorrectly()
    {
        var clock = new TestClock(Instant.FromUnixTimeSeconds(0));

        clock.SetTo(Instant.MaxValue);

        clock.Now.Should().Be(Instant.MaxValue);
    }

    [Fact]
    public void SetTo_MinInstant_StoredCorrectly()
    {
        var clock = new TestClock(Instant.FromUnixTimeSeconds(0));

        clock.SetTo(Instant.MinValue);

        clock.Now.Should().Be(Instant.MinValue);
    }

    [Fact]
    public void Advance_LargePositiveDurationFromMaxInstant_HandledOrThrowsCleanly()
    {
        var clock = new TestClock(Instant.MaxValue - Duration.FromHours(1));

        var act = () => clock.Advance(Duration.FromHours(48));

        act.Should().Throw<OverflowException>();
    }

    [Fact]
    public async Task ConcurrentSetToAndAdvance_NoTorn_NoDeadlock()
    {
        var clock = new TestClock(Instant.FromUnixTimeSeconds(1000));
        const int writer_count = 50;
        const int ops_per_writer = 100;
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var setters = new Task[writer_count];
        var advancers = new Task[writer_count];
        for (var i = 0; i < writer_count; i++)
        {
            var seed = i;
            setters[i] = Task.Run(
                () =>
                {
                    var rng = new Random(seed);
                    for (var j = 0; j < ops_per_writer; j++)
                        clock.SetTo(Instant.FromUnixTimeSeconds(rng.Next(0, 1_000_000)));
                },
                CancellationToken.None);
            advancers[i] = Task.Run(
                () =>
                {
                    for (var j = 0; j < ops_per_writer; j++)
                        clock.Advance(Duration.FromSeconds(1));
                },
                CancellationToken.None);
        }

        var reader = Task.Run(
            () =>
            {
                while (!token.IsCancellationRequested)
                {
                    var read = clock.GetCurrentInstant();
                    read.Should().BeGreaterThanOrEqualTo(Instant.MinValue);
                }
            },
            CancellationToken.None);

        await Task.WhenAll(setters);
        await Task.WhenAll(advancers);
        cts.Cancel();
        await reader;
    }
}
