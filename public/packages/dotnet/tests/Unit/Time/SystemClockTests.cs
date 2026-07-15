// -----------------------------------------------------------------------
// <copyright file="SystemClockTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Time;

using AwesomeAssertions;
using NodaTime;
using Xunit;
using IClock = D2.Shared.Time.IClock;
using SystemClock = D2.Shared.Time.SystemClock;

public sealed class SystemClockTests
{
    [Fact]
    public void GetCurrentInstant_ReturnsInstantGreaterThanEpoch()
    {
        var clock = new SystemClock();

        var now = clock.GetCurrentInstant();

        now.Should().BeGreaterThan(Instant.FromUnixTimeTicks(0));
    }

    [Fact]
    public void GetCurrentInstant_CalledTwiceSequentially_SecondIsGreaterThanOrEqualToFirst()
    {
        var clock = new SystemClock();

        var first = clock.GetCurrentInstant();
        var second = clock.GetCurrentInstant();

        second.Should().BeGreaterThanOrEqualTo(first);
    }

    [Fact]
    public void SystemClock_ImplementsIClock()
    {
        typeof(SystemClock).IsAssignableTo(typeof(IClock)).Should().BeTrue();
    }

    [Fact]
    public void SystemClock_IsSealed()
    {
        typeof(SystemClock).IsSealed.Should().BeTrue();
    }
}
