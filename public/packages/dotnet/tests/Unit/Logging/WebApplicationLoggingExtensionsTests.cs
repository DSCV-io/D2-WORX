// -----------------------------------------------------------------------
// <copyright file="WebApplicationLoggingExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Logging;

using AwesomeAssertions;
using DcsvIo.D2.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

/// <summary>
/// Lightweight unit coverage on the <c>UseD2RequestLogging</c> extension —
/// argument guards + chaining + middleware registration smoke. End-to-end
/// runtime coverage (request emission, infrastructure path suppression,
/// custom enrichment, IRequestContext field projection) lives in
/// <c>Integration.Logging.RequestLoggingMiddlewareTests</c> +
/// <c>RequestContextEnricherIntegrationTests</c>.
/// </summary>
public sealed class WebApplicationLoggingExtensionsTests
{
    [Fact]
    public void UseD2RequestLogging_NullApp_Throws()
    {
        IApplicationBuilder? app = null;

        var act = () => app!.UseD2RequestLogging();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UseD2RequestLogging_ReturnsSameApp_ForChaining()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddD2Logging(new ConfigurationBuilder().Build());
        services.AddLogging();
        var sp = services.BuildServiceProvider();
        var app = new ApplicationBuilder(sp);

        var ret = app.UseD2RequestLogging();

        ret.Should().BeSameAs(app);
    }

    [Fact]
    public void UseD2RequestLogging_WithConfigureCallback_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddD2Logging(new ConfigurationBuilder().Build());
        services.AddLogging();
        var sp = services.BuildServiceProvider();
        var app = new ApplicationBuilder(sp);

        var act = () => app.UseD2RequestLogging(opts =>
        {
            opts.MessageTemplate = "Custom — {RequestPath}";
        });

        act.Should().NotThrow();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment()
        {
            ApplicationName = "ut";
            EnvironmentName = "Test";
            ContentRootPath = AppContext.BaseDirectory;
            ContentRootFileProvider =
                new Microsoft.Extensions.FileProviders.NullFileProvider();
        }

        public string ApplicationName { get; set; }

        public string EnvironmentName { get; set; }

        public string ContentRootPath { get; set; }

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider
        {
            get;
            set;
        }
    }
}
