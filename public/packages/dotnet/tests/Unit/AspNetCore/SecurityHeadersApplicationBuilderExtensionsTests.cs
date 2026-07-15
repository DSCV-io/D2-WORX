// -----------------------------------------------------------------------
// <copyright file="SecurityHeadersApplicationBuilderExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AspNetCore;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class SecurityHeadersApplicationBuilderExtensionsTests
{
    [Fact]
    public void UseD2SecurityHeaders_NullApp_ThrowsArgumentNullException()
    {
        IApplicationBuilder? app = null;

        var act = () => app!.UseD2SecurityHeaders();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UseD2SecurityHeaders_ReturnsSameAppForChaining()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var app = new ApplicationBuilder(sp);

        var result = app.UseD2SecurityHeaders();

        result.Should().BeSameAs(app);
    }

    [Fact]
    public void UseD2SecurityHeaders_ConfigureCallback_InvokedWithDefaultsInstance()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var app = new ApplicationBuilder(sp);

        D2SecurityHeadersOptions? captured = null;
        app.UseD2SecurityHeaders(opts =>
        {
            captured = opts;
            opts.XFrameOptions = "SAMEORIGIN";
        });

        captured.Should().NotBeNull();
        captured!.XFrameOptions.Should().Be("SAMEORIGIN");
    }

    [Fact]
    public void UseD2SecurityHeaders_DoubleCall_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var app = new ApplicationBuilder(sp);

        var act = () =>
        {
            app.UseD2SecurityHeaders();
            app.UseD2SecurityHeaders();
        };

        act.Should().NotThrow();
    }
}
