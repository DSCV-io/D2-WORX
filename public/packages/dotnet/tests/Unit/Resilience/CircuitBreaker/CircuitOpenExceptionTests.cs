// -----------------------------------------------------------------------
// <copyright file="CircuitOpenExceptionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.CircuitBreaker;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.CircuitBreaker;
using Xunit;

public sealed class CircuitOpenExceptionTests
{
    [Fact]
    public void DefaultConstructor_UsesCanonicalMessage()
    {
        var ex = new CircuitOpenException();

        ex.Message.Should().Be("Circuit breaker is open");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void MessageOnlyConstructor_CarriesMessage()
    {
        var ex = new CircuitOpenException("custom message");

        ex.Message.Should().Be("custom message");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void MessageAndInnerConstructor_CarriesBoth()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new CircuitOpenException("custom", inner);

        ex.Message.Should().Be("custom");
        ex.InnerException.Should().BeSameAs(inner);
    }
}
