// -----------------------------------------------------------------------
// <copyright file="WebApplicationServiceDefaultsExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.ServiceDefaults;

using AwesomeAssertions;
using D2.Shared.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Xunit;

/// <summary>
/// Null-arg adversarials for the three
/// <see cref="WebApplicationServiceDefaultsExtensions"/> entry points. The
/// composed-pipeline behavior is exercised end-to-end in
/// <c>Integration/ServiceDefaults/AggregatorWiringTests</c>.
/// </summary>
public sealed class WebApplicationServiceDefaultsExtensionsTests
{
    [Fact]
    public void UseD2DefaultPipeline_NullApp_Throws()
    {
        IApplicationBuilder? app = null;

        var act = () => app!.UseD2DefaultPipeline();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MapD2DefaultEndpoints_NullEndpoints_Throws()
    {
        IEndpointRouteBuilder? endpoints = null;

        var act = () => endpoints!.MapD2DefaultEndpoints();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task RunD2ServiceAsync_NullApp_Throws()
    {
        WebApplication? app = null;

        var act = async () => await app!.RunD2ServiceAsync();

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
