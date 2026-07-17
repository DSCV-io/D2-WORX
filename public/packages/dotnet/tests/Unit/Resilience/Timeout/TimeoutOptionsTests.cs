// -----------------------------------------------------------------------
// <copyright file="TimeoutOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.Timeout;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.Timeout;
using Xunit;

public sealed class TimeoutOptionsTests
{
    [Fact]
    public void DefaultCtor_YieldsDefaultDuration()
    {
        var opts = new TimeoutOptions();

        opts.Duration.Should().Be(TimeoutOptions.SR_DefaultDuration);
        opts.Duration.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void ParameterizedCtor_NullDuration_YieldsDefault()
    {
        var opts = new TimeoutOptions(duration: null);

        opts.Duration.Should().Be(TimeoutOptions.SR_DefaultDuration);
    }

    [Fact]
    public void ParameterizedCtor_ExplicitDuration_PreservesValue()
    {
        var duration = TimeSpan.FromSeconds(42);
        var opts = new TimeoutOptions(duration);

        opts.Duration.Should().Be(duration);
    }

    [Fact]
    public void ParameterizedCtor_ZeroDuration_PreservesZero()
    {
        // Explicit Zero is preserved as-is — zero disables the timeout.
        var opts = new TimeoutOptions(TimeSpan.Zero);

        opts.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ParameterizedCtor_NegativeDuration_PreservesNegative()
    {
        // Negative values are preserved (treated as ≤ zero = pass-through).
        var opts = new TimeoutOptions(TimeSpan.FromSeconds(-1));

        opts.Duration.Should().Be(TimeSpan.FromSeconds(-1));
    }
}
