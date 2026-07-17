// -----------------------------------------------------------------------
// <copyright file="ProblemDetailsServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AspNetCore;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class ProblemDetailsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddD2ProblemDetails_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddD2ProblemDetails();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2ProblemDetails_NoConfigure_DoesNotThrow()
    {
        var services = new ServiceCollection();

        var act = () => services.AddD2ProblemDetails();

        act.Should().NotThrow();
    }

    [Fact]
    public void AddD2ProblemDetails_RegistersProblemDetailsService_Resolvable()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2ProblemDetails();

        var sp = services.BuildServiceProvider();
        var resolved = sp.GetService<IProblemDetailsService>();

        resolved.Should().NotBeNull();
    }

    [Fact]
    public void AddD2ProblemDetails_ConfigureCallback_OverridesOptions()
    {
        var services = new ServiceCollection();
        services.AddD2ProblemDetails(opts =>
        {
            opts.CorrelationIdHeaderName = "X-Request-Id";
            opts.EchoCorrelationIdInResponse = false;
        });

        var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IOptions<D2ProblemDetailsOptions>>().Value;

        resolved.CorrelationIdHeaderName.Should().Be("X-Request-Id");
        resolved.EchoCorrelationIdInResponse.Should().BeFalse();
    }

    [Fact]
    public void AddD2ProblemDetails_EmptyCorrelationIdHeaderName_FailsValidationOnResolve()
    {
        var services = new ServiceCollection();
        services.AddD2ProblemDetails(opts =>
        {
            opts.CorrelationIdHeaderName = string.Empty;
        });

        var sp = services.BuildServiceProvider();
        var act = () => sp.GetRequiredService<IOptions<D2ProblemDetailsOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }
}
