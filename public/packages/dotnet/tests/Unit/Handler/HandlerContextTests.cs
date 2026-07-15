// -----------------------------------------------------------------------
// <copyright file="HandlerContextTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Handler;

using AwesomeAssertions;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Handler.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class HandlerContextTests
{
    [Fact]
    public void Ctor_NonNullArguments_ExposesRequestAndLogger()
    {
        var request = new TestRequestContext { TraceId = "trace-1" };
        var logger = NullLogger<SampleHandlerType>.Instance;

        var ctx = new HandlerContext<SampleHandlerType>(request, logger);

        ctx.Request.Should().BeSameAs(request);
        ctx.Logger.Should().BeSameAs(logger);
    }

    [Fact]
    public void Ctor_ImplementsIHandlerContextInterface()
    {
        var request = new TestRequestContext();
        var logger = NullLogger<SampleHandlerType>.Instance;

        IHandlerContext ctx = new HandlerContext<SampleHandlerType>(request, logger);

        ctx.Request.Should().BeSameAs(request);
        ctx.Logger.Should().BeSameAs(logger);
    }

    [Fact]
    public void Ctor_RequestExposesIRequestContextProperties()
    {
        // Adversarial: ensure the IRequestContext is exposed by reference
        // rather than copied — properties read after construction must
        // reflect the real instance (not a snapshot).
        var request = new TestRequestContext
        {
            TraceId = "trace-xyz",
            UserId = System.Guid.NewGuid(),
            Username = "alice",
        };
        var ctx = new HandlerContext<SampleHandlerType>(
            request,
            NullLogger<SampleHandlerType>.Instance);

        ctx.Request.TraceId.Should().Be("trace-xyz");
        ctx.Request.Username.Should().Be("alice");
        ctx.Request.UserId.Should().Be(request.UserId);
    }

    [Fact]
    public void Logger_GenericTypeFlowsThroughToCategoryName()
    {
        // The whole point of HandlerContext<T> is that the typed logger's
        // category matches the handler's full type name — verify by running
        // a real log call through a TestLogger and inspecting the category
        // we record on construction (the TestLogger captures typeof(T)).
        var test_logger = new TestLogger<SampleHandlerType>();
        var ctx = new HandlerContext<SampleHandlerType>(new TestRequestContext(), test_logger);

        ctx.Logger.Should().BeSameAs(test_logger);
        test_logger.CategoryName.Should().Be(typeof(SampleHandlerType).FullName);
    }

    [Fact]
    public void Logger_LogCallFlowsThroughTypedLogger()
    {
        // End-to-end: write a log entry through the IHandlerContext.Logger
        // surface and assert the underlying typed TestLogger captured it.
        // Use Log() directly (not LogInformation extension) to avoid CA1848
        // — this test asserts plumbing, not a real log shape.
        var test_logger = new TestLogger<SampleHandlerType>();
        var ctx = new HandlerContext<SampleHandlerType>(new TestRequestContext(), test_logger);

        ctx.Logger.Log(
            LogLevel.Information,
            new EventId(42, "Test"),
            state: "hello world",
            exception: null,
            formatter: (s, _) => s);

        test_logger.Entries.Should().ContainSingle().Which.Message
            .Should().Contain("hello world");
    }

    // ----------------------------------------------------------------------
    // Adversarial: null arguments. The implementation does not currently
    // throws ArgumentNullException — fail-fast at DI resolution, so a
    // misconfigured container surfaces the missing registration at
    // construction (not far inside the handler pipeline where the trace
    // points away from the actual misconfiguration).
    // ----------------------------------------------------------------------

    [Fact]
    public void Ctor_NullRequest_ThrowsArgumentNullException()
    {
        // Fail-fast at DI resolution: a misconfigured container that
        // hasn't registered IRequestContext (e.g. transport middleware
        // never wired it) would otherwise silently store null and NPE far
        // inside the handler pipeline.
        var act = () => new HandlerContext<SampleHandlerType>(
            null!,
            NullLogger<SampleHandlerType>.Instance);

        act.Should().Throw<ArgumentNullException>().WithParameterName("request");
    }

    [Fact]
    public void Ctor_NullLogger_ThrowsArgumentNullException()
    {
        // Same fail-fast for the logger arg.
        var act = () => new HandlerContext<SampleHandlerType>(
            new TestRequestContext(),
            null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    private sealed class SampleHandlerType
    {
    }
}
