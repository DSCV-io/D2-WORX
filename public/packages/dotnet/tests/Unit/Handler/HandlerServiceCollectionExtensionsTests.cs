// -----------------------------------------------------------------------
// <copyright file="HandlerServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Handler;

using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Handler.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class HandlerServiceCollectionExtensionsTests
{
    [Fact]
    public void AddD2Handler_RegistersOpenGenericHandlerContext()
    {
        var services = new ServiceCollection();

        services.AddD2Handler();

        services.Should().ContainSingle(d => d.ServiceType == typeof(HandlerContext<>));
    }

    [Fact]
    public void AddD2Handler_RegistrationIsTransient()
    {
        var services = new ServiceCollection();

        services.AddD2Handler();

        var descriptor = services.Single(d => d.ServiceType == typeof(HandlerContext<>));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Transient);
    }

    [Fact]
    public void AddD2Handler_ReturnsSameServicesForChaining()
    {
        var services = new ServiceCollection();

        var returned = services.AddD2Handler();

        returned.Should().BeSameAs(services);
    }

    [Fact]
    public void AddD2Handler_DoesNotRegisterIRequestContext()
    {
        // IRequestContext is transport-specific (aspnetcore for HTTP,
        // messaging/rabbitmq for AMQP). AddD2Handler must NOT pre-register it
        // with a default — otherwise the wrong implementation could win
        // over the transport-specific scoped registration.
        var services = new ServiceCollection();

        services.AddD2Handler();

        services.Should().NotContain(d => d.ServiceType == typeof(IRequestContext));
    }

    [Fact]
    public void AddD2Handler_DoesNotRegisterIHandlerContext()
    {
        // The interface is consumer-facing; HandlerContext<T> is what gets
        // injected via the BaseHandler<TSelf,...> generic constraint. A
        // bare IHandlerContext registration would be ambiguous (which T?).
        var services = new ServiceCollection();

        services.AddD2Handler();

        services.Should().NotContain(d => d.ServiceType == typeof(IHandlerContext));
    }

    [Fact]
    public void AddD2Handler_ResolvedHandlerContext_HasRequestAndLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRequestContext>(new TestRequestContext { TraceId = "t" });
        services.AddD2Handler();

        using var sp = services.BuildServiceProvider();
        var ctx = sp.GetRequiredService<HandlerContext<SampleHandler>>();

        ctx.Request.Should().NotBeNull();
        ctx.Request.TraceId.Should().Be("t");
        ctx.Logger.Should().NotBeNull();
    }

    [Fact]
    public void AddD2Handler_DifferentHandlerTypes_GetIndependentContexts()
    {
        // Open-generic registration must produce DIFFERENT closed types for
        // different T — verifying the open-generic shape works as intended.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRequestContext>(new TestRequestContext());
        services.AddD2Handler();

        using var sp = services.BuildServiceProvider();
        var ctxA = sp.GetRequiredService<HandlerContext<SampleHandler>>();
        var ctxB = sp.GetRequiredService<HandlerContext<AnotherHandler>>();

        ctxA.Should().NotBeSameAs(ctxB);
        ctxA.Should().BeOfType<HandlerContext<SampleHandler>>();
        ctxB.Should().BeOfType<HandlerContext<AnotherHandler>>();
    }

    [Fact]
    public void AddD2Handler_TransientLifetime_ReturnsFreshInstancePerResolve()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRequestContext>(new TestRequestContext());
        services.AddD2Handler();

        using var sp = services.BuildServiceProvider();
        var first = sp.GetRequiredService<HandlerContext<SampleHandler>>();
        var second = sp.GetRequiredService<HandlerContext<SampleHandler>>();

        first.Should().NotBeSameAs(second);
    }

    [Fact]
    public void AddD2Handler_CalledTwice_RegistersOnlyOnce()
    {
        // Adversarial: idempotency check. AddD2Handler uses TryAdd so two
        // calls produce a single descriptor — composition roots that compose
        // multiple AddD2X helpers can call AddD2Handler from each without
        // accumulating duplicate registrations.
        var services = new ServiceCollection();

        services.AddD2Handler();
        services.AddD2Handler();

        services.Count(d => d.ServiceType == typeof(HandlerContext<>)).Should().Be(1);
    }

    [Fact]
    public void AddD2Handler_LoggerCategoryNameIsHandlerFullTypeName()
    {
        // End-to-end check: the typed ILogger<T> the framework injects
        // produces logs categorized to the handler's full type name. This is
        // the contract that downstream Serilog enrichers / OTel exporters
        // depend on for source-context correlation.
        var services = new ServiceCollection();
        var capturing_provider = new CapturingLoggerProvider();
        services.AddLogging(b => b.AddProvider(capturing_provider));
        services.AddSingleton<IRequestContext>(new TestRequestContext());
        services.AddD2Handler();

        using var sp = services.BuildServiceProvider();
        var ctx = sp.GetRequiredService<HandlerContext<SampleHandler>>();
        ctx.Logger.Log(
            LogLevel.Information,
            new EventId(1, "Test"),
            state: "hi",
            exception: null,
            formatter: (s, _) => s);

        // Logger<T> normalizes nested-type '+' separators to '.' when computing
        // the category — assert against the normalized form, not the raw FullName.
        capturing_provider.LastCategory
            .Should().Be(typeof(SampleHandler).FullName!.Replace('+', '.'));
    }

    private sealed class SampleHandler
    {
    }

    private sealed class AnotherHandler
    {
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public string? LastCategory { get; private set; }

        public ILogger CreateLogger(string categoryName)
        {
            LastCategory = categoryName;
            return NullLogger.Instance;
        }

        public void Dispose()
        {
        }
    }
}
