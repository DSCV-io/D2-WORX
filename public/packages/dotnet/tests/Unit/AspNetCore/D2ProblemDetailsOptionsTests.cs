// -----------------------------------------------------------------------
// <copyright file="D2ProblemDetailsOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AspNetCore;

using AwesomeAssertions;
using D2.Shared.AspNetCore;
using D2.Shared.Headers.Http;
using Xunit;

public sealed class D2ProblemDetailsOptionsTests
{
    [Fact]
    public void Defaults_CorrelationIdHeaderName_IsCanonical()
    {
        new D2ProblemDetailsOptions().CorrelationIdHeaderName
            .Should().Be(HttpHeaders.CORRELATION_ID);
    }

    [Fact]
    public void Defaults_EchoCorrelationIdInResponse_IsTrue()
    {
        new D2ProblemDetailsOptions().EchoCorrelationIdInResponse.Should().BeTrue();
    }

    [Fact]
    public void Defaults_IncludeRequestPath_IsTrue()
    {
        new D2ProblemDetailsOptions().IncludeRequestPath.Should().BeTrue();
    }

    [Fact]
    public void WithExpression_OverridesProperties()
    {
        var opts = new D2ProblemDetailsOptions
        {
            CorrelationIdHeaderName = "X-Request-Id",
            EchoCorrelationIdInResponse = false,
            IncludeRequestPath = false,
        };

        opts.CorrelationIdHeaderName.Should().Be("X-Request-Id");
        opts.EchoCorrelationIdInResponse.Should().BeFalse();
        opts.IncludeRequestPath.Should().BeFalse();
    }
}
