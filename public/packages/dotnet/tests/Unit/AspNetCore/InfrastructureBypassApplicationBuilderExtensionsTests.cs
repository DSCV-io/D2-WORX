// -----------------------------------------------------------------------
// <copyright file="InfrastructureBypassApplicationBuilderExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AspNetCore;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class InfrastructureBypassApplicationBuilderExtensionsTests
{
    [Fact]
    public void UseD2InfrastructureBypass_NullApp_ThrowsArgumentNullException()
    {
        IApplicationBuilder? app = null;

        var act = () => app!.UseD2InfrastructureBypass();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UseD2InfrastructureBypass_ReturnsSameAppForChaining()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var app = new ApplicationBuilder(sp);

        var result = app.UseD2InfrastructureBypass();

        result.Should().BeSameAs(app);
    }

    [Fact]
    public void UseD2InfrastructureBypass_ConfigureCallback_AppliesOptions()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var app = new ApplicationBuilder(sp);

        D2InfrastructureBypassOptions? captured = null;
        app.UseD2InfrastructureBypass(opts =>
        {
            captured = opts;
            opts.TagOnly = true;
        });

        captured.Should().NotBeNull();
        captured!.TagOnly.Should().BeTrue();
    }
}
