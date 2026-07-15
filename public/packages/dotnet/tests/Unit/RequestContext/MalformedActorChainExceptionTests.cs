// -----------------------------------------------------------------------
// <copyright file="MalformedActorChainExceptionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.RequestContext;

using System;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Context.Abstractions;
using Xunit;

public sealed class MalformedActorChainExceptionTests
{
    [Fact]
    public void Ctor_MessageOnly_StoresMessage()
    {
        const string expected_message = "specific malformation reason";

        var ex = new MalformedActorChainException(expected_message);

        ex.Message.Should().Be(expected_message);
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void Ctor_MessageAndInnerException_StoresBoth()
    {
        const string expected_message = "wrapped malformation";
        var inner = new JsonException("invalid token at position 12");

        var ex = new MalformedActorChainException(expected_message, inner);

        ex.Message.Should().Be(expected_message);
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void Class_DerivesFromException()
    {
        var ex = new MalformedActorChainException("x");

        ex.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void Class_IsSealed()
    {
        // Adversarial: exceptions are sealed by default. Subclassing the
        // parser's exception would let middleware accidentally catch / unwrap
        // a narrower type and miss the fail-closed contract.
        typeof(MalformedActorChainException).IsSealed.Should().BeTrue();
    }
}
