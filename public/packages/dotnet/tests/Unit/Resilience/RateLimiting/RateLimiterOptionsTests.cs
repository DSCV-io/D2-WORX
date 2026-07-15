// -----------------------------------------------------------------------
// <copyright file="RateLimiterOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.RateLimiting;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.RateLimiting;
using Xunit;

public sealed class RateLimiterOptionsTests
{
    [Fact]
    public void DefaultCtor_YieldsAllDefaults()
    {
        var opts = new RateLimiterOptions();

        opts.MaxConcurrency.Should().Be(RateLimiterOptions.DEFAULT_MAX_CONCURRENCY);
        opts.MaxConcurrency.Should().Be(100);
        opts.AcquisitionTimeout.Should().Be(RateLimiterOptions.SR_DefaultAcquisitionTimeout);
        opts.AcquisitionTimeout.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ParameterizedCtor_NullArgs_YieldsDefaults()
    {
        var opts = new RateLimiterOptions(maxConcurrency: null, acquisitionTimeout: null);

        opts.MaxConcurrency.Should().Be(RateLimiterOptions.DEFAULT_MAX_CONCURRENCY);
        opts.AcquisitionTimeout.Should().Be(RateLimiterOptions.SR_DefaultAcquisitionTimeout);
    }

    [Fact]
    public void ParameterizedCtor_ExplicitValues_PreservesValues()
    {
        var opts = new RateLimiterOptions(
            maxConcurrency: 10,
            acquisitionTimeout: TimeSpan.FromMilliseconds(500));

        opts.MaxConcurrency.Should().Be(10);
        opts.AcquisitionTimeout.Should().Be(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void ParameterizedCtor_ZeroMaxConcurrency_ThrowsArgumentOutOfRange()
    {
        // F-2 regression pin: MaxConcurrency=0 is a misconfiguration; ctor must
        // throw rather than silently pass 0 to SemaphoreSlim where the error is
        // harder to diagnose.
        var act = () => new RateLimiterOptions(maxConcurrency: 0);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*MaxConcurrency must be at least 1*");
    }

    [Fact]
    public void ParameterizedCtor_NegativeMaxConcurrency_ThrowsArgumentOutOfRange()
    {
        // Negative values are also invalid (same predicate as zero).
        var act = () => new RateLimiterOptions(maxConcurrency: -1);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*MaxConcurrency must be at least 1*");
    }

    [Fact]
    public void ParameterizedCtor_OneMaxConcurrency_IsMinimumValidValue()
    {
        // MaxConcurrency=1 is the lowest valid value and must not throw.
        var opts = new RateLimiterOptions(maxConcurrency: 1);

        opts.MaxConcurrency.Should().Be(1);
    }

    [Fact]
    public void ParameterizedCtor_ZeroAcquisitionTimeout_PreservesZero()
    {
        var opts = new RateLimiterOptions(acquisitionTimeout: TimeSpan.Zero);

        opts.AcquisitionTimeout.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void WithExpression_OverridesIndividualProps()
    {
        var original = new RateLimiterOptions();
        var modified = original with { MaxConcurrency = 5 };

        original.MaxConcurrency.Should().Be(100);
        modified.MaxConcurrency.Should().Be(5);
        modified.AcquisitionTimeout.Should().Be(TimeSpan.Zero);
    }
}
