// -----------------------------------------------------------------------
// <copyright file="CorsExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AspNetCore;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class CorsExtensionsTests
{
    [Fact]
    public void AddD2Cors_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;
        IConfiguration cfg = new ConfigurationBuilder().Build();

        var act = () => services!.AddD2Cors(cfg);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2Cors_NullConfiguration_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        var act = () => services.AddD2Cors(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2Cors_RegistersOptions_Resolvable()
    {
        var services = new ServiceCollection();
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["D2_CORS_ORIGINS:0"] = "https://app.example.com",
            })
            .Build();

        services.AddD2Cors(cfg);

        var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IOptions<D2CorsOptions>>().Value;
        resolved.Origins.Should().Contain("https://app.example.com");
    }

    [Fact]
    public void AddD2Cors_ConfigureCallback_RunsAfterEnvDerivedDefaults()
    {
        var services = new ServiceCollection();
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["D2_CORS_ORIGINS:0"] = "https://from-env.example.com",
            })
            .Build();

        services.AddD2Cors(cfg, opts =>
        {
            // Override env-derived value.
            opts.Origins = ["https://from-callback.example.com"];
            opts.PreflightMaxAgeSeconds = 1200;
        });

        var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IOptions<D2CorsOptions>>().Value;
        resolved.Origins.Should().Equal("https://from-callback.example.com");
        resolved.PreflightMaxAgeSeconds.Should().Be(1200);
    }

    [Fact]
    public void AddD2Cors_EmptyOriginsAndNoConfigure_ValidationFailsOnResolve()
    {
        var services = new ServiceCollection();
        var cfg = new ConfigurationBuilder().Build();

        services.AddD2Cors(cfg);

        var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredService<IOptions<D2CorsOptions>>().Value;
        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddD2Cors_AllowCredentialsAndWildcardOrigin_ValidationFailsOnResolve()
    {
        var services = new ServiceCollection();
        var cfg = new ConfigurationBuilder().Build();

        services.AddD2Cors(cfg, opts =>
        {
            opts.Origins = ["*"];
            opts.AllowCredentials = true;
        });

        var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredService<IOptions<D2CorsOptions>>().Value;
        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void UseD2Cors_NullApp_ThrowsArgumentNullException()
    {
        IApplicationBuilder? app = null;

        var act = () => app!.UseD2Cors();

        act.Should().Throw<ArgumentNullException>();
    }
}
