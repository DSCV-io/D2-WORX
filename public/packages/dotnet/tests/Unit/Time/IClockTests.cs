// -----------------------------------------------------------------------
// <copyright file="IClockTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Time;

using System.Linq;
using AwesomeAssertions;
using NodaTime;
using Xunit;
using IClock = DcsvIo.D2.Time.IClock;

public sealed class IClockTests
{
    [Fact]
    public void Interface_HasSingleMethod_GetCurrentInstant()
    {
        var methods = typeof(IClock).GetMethods();
        methods.Should().HaveCount(1);

        var method = methods.Single();
        method.Name.Should().Be("GetCurrentInstant");
        method.ReturnType.Should().Be<Instant>();
        method.GetParameters().Should().BeEmpty();
    }

    [Fact]
    public void Interface_IsAnInterface_NotASealedClass()
    {
        typeof(IClock).IsInterface.Should().BeTrue();
    }
}
