// -----------------------------------------------------------------------
// <copyright file="HealthEndpointsExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AspNetCore;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class HealthEndpointsExtensionsTests
{
    [Fact]
    public void AddD2HealthChecks_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddD2HealthChecks();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2HealthChecks_RegistersSelfCheckTaggedLive()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2HealthChecks();

        var sp = services.BuildServiceProvider();
        var hcOpts = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        hcOpts.Registrations.Should().ContainSingle(r =>
            r.Name == D2AspNetCoreConstants.SELF_HEALTH_CHECK_NAME
            && r.Tags.Contains(D2AspNetCoreConstants.LIVE_HEALTH_TAG));
    }

    [Fact]
    public void AddD2HealthChecks_DoubleCall_NoOpsOnSecondCall()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2HealthChecks();
        services.AddD2HealthChecks();

        var sp = services.BuildServiceProvider();
        var hcOpts = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        // Single self-check registration despite the duplicate AddD2HealthChecks call.
        hcOpts.Registrations.Count(r =>
            r.Name == D2AspNetCoreConstants.SELF_HEALTH_CHECK_NAME)
            .Should().Be(1);
    }

    [Fact]
    public void MapD2HealthEndpoints_NullEndpoints_ThrowsArgumentNullException()
    {
        IEndpointRouteBuilder? endpoints = null;

        var act = () => endpoints!.MapD2HealthEndpoints();

        act.Should().Throw<ArgumentNullException>();
    }
}
