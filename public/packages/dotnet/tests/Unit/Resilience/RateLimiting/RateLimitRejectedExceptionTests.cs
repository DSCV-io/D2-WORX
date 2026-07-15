// -----------------------------------------------------------------------
// <copyright file="RateLimitRejectedExceptionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.RateLimiting;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.RateLimiting;
using Xunit;

public sealed class RateLimitRejectedExceptionTests
{
    [Fact]
    public void DefaultConstructor_UsesCanonicalMessage()
    {
        var ex = new RateLimitRejectedException();

        ex.Message.Should().Contain("Rate limit exceeded");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void MessageOnlyConstructor_CarriesMessage()
    {
        var ex = new RateLimitRejectedException("custom message");

        ex.Message.Should().Be("custom message");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void MessageAndInnerConstructor_CarriesBoth()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new RateLimitRejectedException("custom", inner);

        ex.Message.Should().Be("custom");
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void IsExceptionSubtype()
    {
        var ex = new RateLimitRejectedException();

        ex.Should().BeAssignableTo<Exception>();
    }
}
