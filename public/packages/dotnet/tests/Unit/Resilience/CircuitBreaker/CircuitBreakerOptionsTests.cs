// -----------------------------------------------------------------------
// <copyright file="CircuitBreakerOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.CircuitBreaker;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.CircuitBreaker;
using Xunit;

public sealed class CircuitBreakerOptionsTests
{
    [Fact]
    public void Defaults_MatchInternalConstants()
    {
        // The Options class IS the single source of truth for defaults
        // (CircuitBreaker.cs defers to it). Verify every property defaults
        // to the corresponding internal constant rather than a re-stated
        // literal — would fail if the two ever drift.
        var options = new CircuitBreakerOptions();

        options.FailureThreshold.Should().Be(CircuitBreakerOptions.DEFAULT_FAILURE_THRESHOLD);
        options.CooldownDuration.Should().Be(CircuitBreakerOptions.SR_DefaultCooldownDuration);
        options.NowFunc.Should().BeSameAs(CircuitBreakerOptions.SR_DefaultNowFunc);
    }

    [Fact]
    public void DefaultNowFunc_DelegatesToTickCount64()
    {
        // Sanity check that the cached default delegate actually does what
        // we documented — a monotonic millisecond reading from TickCount64.
        var before = Environment.TickCount64;
        var observed = CircuitBreakerOptions.SR_DefaultNowFunc();
        var after = Environment.TickCount64;

        observed.Should().BeInRange(before, after);
    }

    [Fact]
    public void InitOnly_OverridesAreApplied()
    {
        long Clock() => 12345L;

        var options = new CircuitBreakerOptions(3, TimeSpan.FromSeconds(5), Clock);

        options.FailureThreshold.Should().Be(3);
        options.CooldownDuration.Should().Be(TimeSpan.FromSeconds(5));
        options.NowFunc().Should().Be(12345L);
    }
}
