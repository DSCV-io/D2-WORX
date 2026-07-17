// -----------------------------------------------------------------------
// <copyright file="TelemetryServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Telemetry;

using AwesomeAssertions;
using DcsvIo.D2.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

/// <summary>
/// OTel SDK builds singleton <c>MeterProvider</c> / <c>TracerProvider</c>
/// per process, and the OTEL_SDK_DISABLED env var influences what
/// AddD2Telemetry registers — pin into a dedicated collection that
/// serializes against integration tests touching the same surface.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class TelemetryServiceCollectionExtensionsTests
{
    [Fact]
    public void AddD2Telemetry_NullServices_Throws()
    {
        IServiceCollection? services = null;
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            var act = () => services!.AddD2Telemetry(cfg);

            act.Should().Throw<ArgumentNullException>();
        });
    }

    [Fact]
    public void AddD2Telemetry_NullConfiguration_Throws()
    {
        var services = new ServiceCollection();

        WithCleanEnv(() =>
        {
            var act = () => services.AddD2Telemetry(null!);

            act.Should().Throw<ArgumentNullException>();
        });
    }

    [Fact]
    public void AddD2Telemetry_ReturnsSameServicesForChaining()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            var ret = services.AddD2Telemetry(cfg);

            ret.Should().BeSameAs(services);
        });
    }

    [Fact]
    public void AddD2Telemetry_RegistersOptions_Resolvable()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg);
            using var sp = services.BuildServiceProvider();

            sp.GetService<IOptions<D2TelemetryOptions>>().Should().NotBeNull();
        });
    }

    [Fact]
    public void AddD2Telemetry_RegistersMeterProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg);
            using var sp = services.BuildServiceProvider();

            sp.GetService<MeterProvider>().Should().NotBeNull();
        });
    }

    [Fact]
    public void AddD2Telemetry_RegistersTracerProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg);
            using var sp = services.BuildServiceProvider();

            sp.GetService<TracerProvider>().Should().NotBeNull();
        });
    }

    [Fact]
    public void AddD2Telemetry_CalledTwice_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            var act = () =>
            {
                services.AddD2Telemetry(cfg);
                services.AddD2Telemetry(cfg);
            };

            act.Should().NotThrow();
        });
    }

    [Fact]
    public void AddD2Telemetry_ServiceNameFromConfig_IsApplied()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment("default-app"));
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [D2TelemetryConstants.OTEL_SERVICE_NAME_CONFIG_KEY] = "from-config",
            })
            .Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg);
            using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;

            opts.ServiceName.Should().Be("from-config");
        });
    }

    [Fact]
    public void AddD2Telemetry_ServiceNameFallsBackToHostEnvironment_WhenConfigAbsent()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment("env-app"));
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg);
            using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;

            opts.ServiceName.Should().Be("env-app");
        });
    }

    [Fact]
    public void AddD2Telemetry_ConfigureCallback_Wins()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment("env-app"));
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [D2TelemetryConstants.OTEL_SERVICE_NAME_CONFIG_KEY] = "from-config",
            })
            .Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg, opts => opts.ServiceName = "explicit-override");
            using var sp = services.BuildServiceProvider();
            var resolved = sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;

            resolved.ServiceName.Should().Be("explicit-override");
        });
    }

    [Fact]
    public void AddD2Telemetry_OtlpTracesEndpointFromConfig_IsApplied()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [D2TelemetryConstants.OTLP_TRACES_ENDPOINT_CONFIG_KEY] =
                    "https://otlp.example.com/v1/traces",
            })
            .Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg);
            using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;

            opts.OtlpTracesEndpoint.Should().Be("https://otlp.example.com/v1/traces");
        });
    }

    [Fact]
    public void AddD2Telemetry_OtlpMetricsEndpointFromConfig_IsApplied()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [D2TelemetryConstants.OTLP_METRICS_ENDPOINT_CONFIG_KEY] =
                    "https://otlp.example.com/v1/metrics",
            })
            .Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg);
            using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;

            opts.OtlpMetricsEndpoint.Should().Be("https://otlp.example.com/v1/metrics");
        });
    }

    [Fact]
    public void AddD2Telemetry_OtlpLogsEndpointFromConfig_IsApplied()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [D2TelemetryConstants.OTLP_LOGS_ENDPOINT_CONFIG_KEY] =
                    "https://otlp.example.com/v1/logs",
            })
            .Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg);
            using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;

            opts.OtlpLogsEndpoint.Should().Be("https://otlp.example.com/v1/logs");
        });
    }

    [Fact]
    public void AddD2Telemetry_NoServiceNameAvailable_ValidationFailsOnResolve()
    {
        var services = new ServiceCollection();

        // No IHostEnvironment registered, no config, no configure callback.
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg);

            var act = () =>
            {
                using var sp = services.BuildServiceProvider();
                return sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;
            };

            act.Should().Throw<OptionsValidationException>();
        });
    }

    [Fact]
    public void AddD2Telemetry_EmptyExcludedPaths_ValidationFails()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg, opts => opts.ServiceName = "x");
            services.PostConfigure<D2TelemetryOptions>(opts =>
            {
                var t = opts.GetType();
                var prop = t.GetProperty(
                    nameof(D2TelemetryOptions.InstrumentationExcludedPaths))!;
                prop.SetValue(opts, Array.Empty<string>());
            });

            var act = () =>
            {
                using var sp = services.BuildServiceProvider();
                return sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;
            };

            act.Should().Throw<OptionsValidationException>();
        });
    }

    [Fact]
    public void AddD2Telemetry_ExcludedPathsContainsWhitespace_ValidationFails()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg, opts => opts.ServiceName = "x");
            services.PostConfigure<D2TelemetryOptions>(opts =>
            {
                var t = opts.GetType();
                var prop = t.GetProperty(
                    nameof(D2TelemetryOptions.InstrumentationExcludedPaths))!;
                prop.SetValue(opts, new[] { "/health", "   " });
            });

            var act = () =>
            {
                using var sp = services.BuildServiceProvider();
                return sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;
            };

            act.Should().Throw<OptionsValidationException>();
        });
    }

    [Fact]
    public void AddD2Telemetry_AdditionalActivitySourcesContainsWhitespace_ValidationFails()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg, opts => opts.ServiceName = "x");
            services.PostConfigure<D2TelemetryOptions>(opts =>
            {
                var t = opts.GetType();
                var prop = t.GetProperty(
                    nameof(D2TelemetryOptions.AdditionalActivitySources))!;
                prop.SetValue(opts, new[] { "DcsvIo.D2.Private.Edge", "  " });
            });

            var act = () =>
            {
                using var sp = services.BuildServiceProvider();
                return sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;
            };

            act.Should().Throw<OptionsValidationException>();
        });
    }

    [Fact]
    public void AddD2Telemetry_AdditionalMetersContainsWhitespace_ValidationFails()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg, opts => opts.ServiceName = "x");
            services.PostConfigure<D2TelemetryOptions>(opts =>
            {
                var t = opts.GetType();
                var prop = t.GetProperty(
                    nameof(D2TelemetryOptions.AdditionalMeters))!;
                prop.SetValue(opts, new[] { "DcsvIo.D2.Private.Edge", string.Empty });
            });

            var act = () =>
            {
                using var sp = services.BuildServiceProvider();
                return sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;
            };

            act.Should().Throw<OptionsValidationException>();
        });
    }

    [Fact]
    public void AddD2Telemetry_InvalidOtlpTracesEndpoint_ValidationFails()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg, opts =>
            {
                opts.ServiceName = "x";
                opts.OtlpTracesEndpoint = "not a uri";
            });

            var act = () =>
            {
                using var sp = services.BuildServiceProvider();
                return sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;
            };

            act.Should().Throw<OptionsValidationException>();
        });
    }

    [Fact]
    public void AddD2Telemetry_OtelSdkDisabled_ShortCircuits_NoProviders()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithEnvVar(D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR, "true", () =>
        {
            services.AddD2Telemetry(cfg);
            using var sp = services.BuildServiceProvider();

            sp.GetService<MeterProvider>().Should().BeNull();
            sp.GetService<TracerProvider>().Should().BeNull();
            sp.GetService<IOptions<D2TelemetryOptions>>().Should().BeNull();
        });
    }

    [Fact]
    public void AddD2Telemetry_OtelSdkDisabled_ReturnsSameServicesForChaining()
    {
        var services = new ServiceCollection();
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithEnvVar(D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR, "true", () =>
        {
            var ret = services.AddD2Telemetry(cfg);

            ret.Should().BeSameAs(services);
        });
    }

    [Fact]
    public void AddD2Telemetry_DisableEverySwitch_StillBuilds()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg, opts =>
            {
                opts.ServiceName = "x";
            });
            services.PostConfigure<D2TelemetryOptions>(opts =>
            {
                var t = opts.GetType();
                t.GetProperty(nameof(D2TelemetryOptions.EnableAspNetCoreInstrumentation))!
                    .SetValue(opts, false);
                t.GetProperty(nameof(D2TelemetryOptions.EnableHttpClientInstrumentation))!
                    .SetValue(opts, false);
                t.GetProperty(
                    nameof(D2TelemetryOptions.EnableGrpcNetClientInstrumentation))!
                    .SetValue(opts, false);
                t.GetProperty(nameof(D2TelemetryOptions.EnableProcessInstrumentation))!
                    .SetValue(opts, false);
                t.GetProperty(nameof(D2TelemetryOptions.EnableRuntimeInstrumentation))!
                    .SetValue(opts, false);
                t.GetProperty(nameof(D2TelemetryOptions.EnablePrometheusExporter))!
                    .SetValue(opts, false);
            });

            var act = () =>
            {
                using var sp = services.BuildServiceProvider();
                _ = sp.GetService<MeterProvider>();
                _ = sp.GetService<TracerProvider>();
            };

            act.Should().NotThrow();
        });
    }

    private static void WithCleanEnv(Action body) =>
        WithEnvVar(D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR, null, body);

    private static void WithEnvVar(string name, string? value, Action body)
    {
        var prior = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, value);
            body();
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, prior);
        }
    }

    /// <summary>
    /// Minimal <see cref="IHostEnvironment"/> stub.
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
