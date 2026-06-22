// -----------------------------------------------------------------------
// <copyright file="ServiceDefaultsConfigurationE2ETests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.ServiceDefaults;

using AwesomeAssertions;
using D2.Shared.AspNetCore;
using D2.Shared.Auth.Validation;
using D2.Shared.Caching;
using D2.Shared.Logging;
using D2.Shared.ServiceDefaults;
using D2.Shared.Telemetry;
using D2.Shared.Tests.Integration.ServiceDefaults.Infrastructure;
using global::Microsoft.AspNetCore.TestHost;
using global::Microsoft.Extensions.DependencyInjection;
using global::Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Configuration knobs E2E — every <see cref="D2ServiceDefaultsOptions"/>
/// opt-out flag and every typed pass-through
/// <see cref="Action{T}"/> delegate verified end-to-end through the
/// composed pipeline.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class ServiceDefaultsConfigurationE2ETests
{
    [Fact]
    public async Task Options_SkipAuthAutoWiring_True_NoJwtValidatorInDi()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            configureOptions: opts => opts.SkipAuthAutoWiring = true);

        handle.Host.Services.GetService<JwtValidator>().Should().BeNull();
    }

    [Fact]
    public async Task Options_SkipLocalCacheAutoWiring_True_NoILocalCacheRegistered()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            configureOptions: opts =>
            {
                opts.SkipAuthAutoWiring = true;
                opts.SkipLocalCacheAutoWiring = true;
            });

        handle.Host.Services.GetService<ILocalCache>().Should().BeNull();
    }

    [Fact]
    public async Task Options_SkipLocalCacheAutoWiring_False_ILocalCacheRegistered()
    {
        // Default state — local cache IS auto-wired.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            configureOptions: opts =>
            {
                opts.SkipAuthAutoWiring = true;
                opts.SkipLocalCacheAutoWiring = false;
            });

        handle.Host.Services.GetService<ILocalCache>().Should().NotBeNull();
    }

    [Fact]
    public async Task Options_LoggingConfigure_PassThroughDelegate_InvokedAndAffectsLoggingOptions()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            configureOptions: opts =>
            {
                opts.SkipAuthAutoWiring = true;
                opts.LoggingConfigure = log => log.ServiceName = "logging-config-svc";
            });

        var resolved = handle.Host.Services
            .GetRequiredService<IOptions<D2LoggingOptions>>()
            .Value;
        resolved.ServiceName.Should().Be("logging-config-svc");
    }

    [Fact]
    public async Task Options_TelemetryConfigure_PassThrough_InvokedAndAffectsOptions()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            configureOptions: opts =>
            {
                opts.SkipAuthAutoWiring = true;
                opts.TelemetryConfigure = t => t.ServiceName = "telemetry-config-svc";
            });

        var resolved = handle.Host.Services
            .GetRequiredService<IOptions<D2TelemetryOptions>>()
            .Value;
        resolved.ServiceName.Should().Be("telemetry-config-svc");
    }

    [Fact]
    public async Task Options_CorsConfigure_PassThroughDelegate_InvokedAndAffectsCorsOptions()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            configureOptions: opts =>
            {
                opts.SkipAuthAutoWiring = true;
                opts.CorsConfigure = c => c.PreflightMaxAgeSeconds = 999;
            });

        var resolved = handle.Host.Services
            .GetRequiredService<IOptions<D2CorsOptions>>()
            .Value;
        resolved.PreflightMaxAgeSeconds.Should().Be(999);
    }

    [Fact]
    public async Task Options_ProblemDetailsConfigure_PassThroughDelegate_InvokedAndAffectsOptions()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            configureOptions: opts =>
            {
                opts.SkipAuthAutoWiring = true;
                opts.ProblemDetailsConfigure =
                    pd => pd.CorrelationIdHeaderName = "X-Custom-Correlation";
            });

        var resolved = handle.Host.Services
            .GetRequiredService<IOptions<D2ProblemDetailsOptions>>()
            .Value;
        resolved.CorrelationIdHeaderName.Should().Be("X-Custom-Correlation");
    }

    [Fact]
    public async Task Options_LocalCacheConfigure_PassThrough_InvokedAndAffectsOptions()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            configureOptions: opts =>
            {
                opts.SkipAuthAutoWiring = true;
                opts.SkipLocalCacheAutoWiring = false;
                opts.LocalCacheConfigure =
                    lc => lc.MaxEntries = 12345;
            });

        var resolved = handle.Host.Services
            .GetRequiredService<IOptions<LocalCacheOptions>>()
            .Value;
        resolved.MaxEntries.Should().Be(12345);
    }

    [Fact]
    public async Task Options_SecurityHeadersConfigure_AppliedAtPipelineInstall()
    {
        // SecurityHeadersConfigure runs at pipeline-installation time
        // (the *Use* extension reads the IOptions on first request).
        // Pin via response-header observation rather than DI lookup.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            configureOptions: opts =>
            {
                opts.SkipAuthAutoWiring = true;
                opts.SecurityHeadersConfigure =
                    sh => sh.ReferrerPolicy = "no-referrer";
            });
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync("https://localhost/probe");

        response.Headers.GetValues("Referrer-Policy")
            .Should().ContainSingle().Which.Should().Be("no-referrer");
    }

    [Fact]
    public async Task Options_InfrastructureBypassConfigure_AppliedAtPipelineInstall_TagOnlyMode()
    {
        var downstreamRan = false;
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            configureOptions: opts =>
            {
                opts.SkipAuthAutoWiring = true;
                opts.InfrastructureBypassConfigure = ib => ib.TagOnly = true;
            },
            extraConfigure: app =>
            {
                global::Microsoft.AspNetCore.Builder.UseExtensions.Use(
                    app,
                    async (_, next) =>
                    {
                        downstreamRan = true;
                        await next();
                    });
            });
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/health", UriKind.Relative));

        downstreamRan.Should().BeTrue(
            "TagOnly=true means infra-path requests do NOT short-circuit "
            + "and downstream middleware still runs.");
    }

    [Fact]
    public void Options_AuthConfigure_PassThroughDelegate_InvokedAndAffectsAuthOptions()
    {
        // Verify AuthConfigure flows into AuthOptions via the
        // service-collection-level resolution (full host startup with
        // auth wired additionally requires ITieredCache, which services
        // compose alongside auth).
        var services = new ServiceCollection();
        var configuration = new global::Microsoft.Extensions.Configuration
            .ConfigurationBuilder().Build();
        services.AddRouting();
        services.AddD2ServiceDefaults(
            configuration,
            opts =>
            {
                opts.SkipAuthAutoWiring = false;
                opts.AuthConfigure = a =>
                {
                    a.Issuer = new Uri("https://my-edge.test");
                    a.Audience = "audience-from-test";
                };
            });

        // Build provider just to materialize AuthOptions via IOptions
        // — does NOT call IHost.StartAsync, so hosted services don't try
        // to resolve their dependencies.
        using var sp = services.BuildServiceProvider();
        var resolved = sp
            .GetRequiredService<IOptions<D2.Shared.Auth.AuthOptions>>()
            .Value;
        resolved.Issuer.Should().Be(new Uri("https://my-edge.test"));
        resolved.Audience.Should().Be("audience-from-test");
    }

    [Fact]
    public async Task Options_IOptions_BoundAtPipelineInstallTime_ReadableFromMiddlewareScope()
    {
        // The aggregator binds D2ServiceDefaultsOptions into DI so
        // UseD2DefaultPipeline can read pass-throughs at pipeline-install
        // time. Verify the IOptions<D2ServiceDefaultsOptions> is
        // resolvable from a per-request scope.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            configureOptions: opts =>
            {
                opts.SkipAuthAutoWiring = true;
            });

        using var scope = handle.Host.Services.CreateScope();
        var resolved = scope.ServiceProvider
            .GetRequiredService<IOptions<D2ServiceDefaultsOptions>>()
            .Value;
        resolved.SkipAuthAutoWiring.Should().BeTrue();
    }
}
