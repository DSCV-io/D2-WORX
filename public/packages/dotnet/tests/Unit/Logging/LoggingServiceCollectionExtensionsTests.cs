// -----------------------------------------------------------------------
// <copyright file="LoggingServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Logging;

using AwesomeAssertions;
using DcsvIo.D2.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Xunit;

/// <summary>
/// <c>Log.Logger</c> is a process-global static; the
/// <c>LogLoggerIsSetAfterAddD2Logging</c> test mutates it. We pin it into a
/// dedicated xUnit collection so its execution is serialized against any
/// other test that might also touch the static.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class LoggingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddD2Logging_NullServices_Throws()
    {
        IServiceCollection? services = null;
        IConfiguration cfg = new ConfigurationBuilder().Build();

        var act = () => services!.AddD2Logging(cfg);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2Logging_NullConfiguration_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddD2Logging(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2Logging_ReturnsSameServicesForChaining()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        var ret = services.AddD2Logging(cfg);

        ret.Should().BeSameAs(services);
    }

    [Fact]
    public void AddD2Logging_RegistersDiagnosticContext()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        services.AddD2Logging(cfg);
        using var sp = services.BuildServiceProvider();

        sp.GetService<IDiagnosticContext>().Should().NotBeNull();
    }

    [Fact]
    public void AddD2Logging_RegistersIloggerOfT()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        services.AddD2Logging(cfg);
        using var sp = services.BuildServiceProvider();

        sp.GetService<ILogger<LoggingServiceCollectionExtensionsTests>>()
            .Should().NotBeNull();
    }

    [Fact]
    public void AddD2Logging_CalledTwice_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        var act = () =>
        {
            services.AddD2Logging(cfg);
            services.AddD2Logging(cfg);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void AddD2Logging_ServiceNameFromConfig_IsApplied()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment("default-app"));
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_SERVICE_NAME"] = "from-config",
            })
            .Build();

        services.AddD2Logging(cfg);
        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<D2LoggingOptions>>().Value;

        opts.ServiceName.Should().Be("from-config");
    }

    [Fact]
    public void AddD2Logging_ServiceNameFromHostEnvironment_AppliedWhenConfigAbsent()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment("env-app"));
        IConfiguration cfg = new ConfigurationBuilder().Build();

        services.AddD2Logging(cfg);
        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<D2LoggingOptions>>().Value;

        opts.ServiceName.Should().Be("env-app");
    }

    [Fact]
    public void AddD2Logging_ConfigureCallback_Wins()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment("env-app"));
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_SERVICE_NAME"] = "from-config",
            })
            .Build();

        services.AddD2Logging(cfg, opts => opts.ServiceName = "explicit-override");
        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IOptions<D2LoggingOptions>>().Value;

        resolved.ServiceName.Should().Be("explicit-override");
    }

    [Fact]
    public void AddD2Logging_EnvironmentFromHostEnvironment_Applied()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(
            new TestHostEnvironment(applicationName: "x", environmentName: "Staging"));
        IConfiguration cfg = new ConfigurationBuilder().Build();

        services.AddD2Logging(cfg);
        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<D2LoggingOptions>>().Value;

        opts.Environment.Should().Be("Staging");
    }

    [Fact]
    public void AddD2Logging_NoServiceNameAvailable_ValidationFailsOnResolve()
    {
        var services = new ServiceCollection();

        // No IHostEnvironment registered, no config, no configure callback.
        IConfiguration cfg = new ConfigurationBuilder().Build();

        services.AddD2Logging(cfg);

        var act = () =>
        {
            using var sp = services.BuildServiceProvider();
            return sp.GetRequiredService<IOptions<D2LoggingOptions>>().Value;
        };

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddD2Logging_EmptyInfrastructurePaths_ValidationFails()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        services.AddD2Logging(cfg, opts =>
        {
            opts.ServiceName = "x";
            opts.Environment = "x";
        });
        services.PostConfigure<D2LoggingOptions>(opts =>
        {
            // Force the post-bind validation to see an empty list.
            var t = opts.GetType();
            var field = t.GetProperty(nameof(D2LoggingOptions.InfrastructurePaths))!;
            field.SetValue(opts, Array.Empty<string>());
        });

        var act = () =>
        {
            using var sp = services.BuildServiceProvider();
            return sp.GetRequiredService<IOptions<D2LoggingOptions>>().Value;
        };

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddD2Logging_InfrastructurePathsContainsWhitespace_ValidationFails()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        services.AddD2Logging(cfg, opts =>
        {
            opts.ServiceName = "x";
            opts.Environment = "x";
        });
        services.PostConfigure<D2LoggingOptions>(opts =>
        {
            var t = opts.GetType();
            var field = t.GetProperty(nameof(D2LoggingOptions.InfrastructurePaths))!;
            field.SetValue(opts, new[] { "/health", "   " });
        });

        var act = () =>
        {
            using var sp = services.BuildServiceProvider();
            return sp.GetRequiredService<IOptions<D2LoggingOptions>>().Value;
        };

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddD2Logging_LogLoggerIsSetAfterAddD2Logging()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment("explicit-app"));
        IConfiguration cfg = new ConfigurationBuilder().Build();

        services.AddD2Logging(cfg);

        Log.Logger.Should().NotBeNull();
    }

    [Fact]
    public void OtelServiceNameConfigKey_IsExpectedConstant()
    {
        D2LoggingConstants.OTEL_SERVICE_NAME_CONFIG_KEY.Should().Be("OTEL_SERVICE_NAME");
    }

    /// <summary>
    /// Minimal <see cref="IHostEnvironment"/> stub used to drive the
    /// env-derived defaults inside <c>AddD2Logging</c>.
    /// </summary>
    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(
            string applicationName = "test-app",
            string environmentName = "Test")
        {
            ApplicationName = applicationName;
            EnvironmentName = environmentName;
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
