// -----------------------------------------------------------------------
// <copyright file="EndToEndCompositionSmokeTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.ServiceDefaults;

using AwesomeAssertions;
using D2.Shared.ServiceDefaults;
using D2.Shared.Tests.Integration.ServiceDefaults.Infrastructure;
using global::Microsoft.AspNetCore.Builder;
using global::Microsoft.Extensions.Configuration;
using global::Microsoft.Extensions.DependencyInjection;
using global::Microsoft.Extensions.Diagnostics.HealthChecks;
using global::Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Composition smoke tests — pin that the composed pipeline builds for
/// every supported configuration combination + that
/// <c>AddD2ServiceDefaults</c> / <c>UseD2DefaultPipeline</c> /
/// <c>MapD2DefaultEndpoints</c> reject obvious null inputs and emit the
/// remediation message on the auth fail-fast path.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class EndToEndCompositionSmokeTests
{
    [Fact]
    public async Task AddD2ServiceDefaults_AuthSkipped_HostBuilds()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            configureOptions: opts => opts.SkipAuthAutoWiring = true);

        handle.Host.Should().NotBeNull();
    }

    [Fact]
    public void AddD2ServiceDefaults_AuthWired_WithStubAuthOptions_RegistrationSucceeds()
    {
        // Auth-WIRED ServiceCollection registration succeeds when
        // AuthConfigure supplies Issuer + Audience + JWKS backplane
        // channel + valid algorithms. Full host startup additionally
        // requires ITieredCache (SessionLivenessTracker dependency)
        // which services compose themselves alongside auth wiring.
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddRouting();

        var act = () => services.AddD2ServiceDefaults(
            configuration,
            opts =>
            {
                opts.SkipAuthAutoWiring = false;
                opts.AuthConfigure = a =>
                {
                    a.Issuer = new Uri("https://edge.test");
                    a.Audience = "edge";
                };
            });

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AddD2ServiceDefaults_AuthConfigureNullAndAutoWire_ThrowsAtBuild()
    {
        // Fail-fast contract: the aggregator throws InvalidOperationException
        // when SkipAuthAutoWiring=false (the default) AND AuthConfigure is null
        // — preventing a service from accidentally shipping without auth wiring.
        var act = async () =>
            await CompositeTestHostBuilder.BuildAsync(
                captureSerilog: false,
                configureOptions: opts =>
                {
                    opts.SkipAuthAutoWiring = false;
                    opts.AuthConfigure = null;
                });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*AuthConfigure is required*");
    }

    [Fact]
    public async Task AddD2ServiceDefaults_AuthConfigureNullRemediationMessage_MentionsBothEscapes()
    {
        // Pins the remediation message text — the operator MUST see both
        // "set AuthConfigure" AND "set SkipAuthAutoWiring=true" because
        // the right answer differs per service (most: wire it; tests +
        // anonymous-only services: skip it).
        var act = async () =>
            await CompositeTestHostBuilder.BuildAsync(
                captureSerilog: false,
                configureOptions: opts =>
                {
                    opts.SkipAuthAutoWiring = false;
                    opts.AuthConfigure = null;
                });

        var ex = (await act.Should().ThrowAsync<InvalidOperationException>())
            .Subject.First();
        ex.Message.Should().Contain("AuthConfigure");
        ex.Message.Should().Contain("SkipAuthAutoWiring");
    }

    [Fact]
    public void AddD2ServiceDefaults_NullServices_ThrowsArgumentNull()
    {
        IServiceCollection services = null!;
        var configuration = new ConfigurationBuilder().Build();

        var act = () =>
            services.AddD2ServiceDefaults(configuration);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2ServiceDefaults_NullConfiguration_ThrowsArgumentNull()
    {
        var services = new ServiceCollection();

        var act = () => services.AddD2ServiceDefaults(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task AddD2ServiceDefaults_CalledTwice_DoesNotDoubleRegisterHealthCheck()
    {
        // The aggregator is documented as idempotent at the
        // IServiceCollection level. Re-registering AddD2HealthChecks must
        // NOT raise the duplicate-check-name exception (the lib uses an
        // internal marker to suppress the second registration).
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddRouting();
        services.AddD2ServiceDefaults(
            configuration,
            opts => opts.SkipAuthAutoWiring = true);
        services.AddD2ServiceDefaults(
            configuration,
            opts => opts.SkipAuthAutoWiring = true);

        // Resolution of HealthCheckService verifies the registration is
        // valid (duplicate "self" check would have raised on resolve).
        await using var sp = services.BuildServiceProvider();
        var hcService = sp.GetService<HealthCheckService>();
        hcService.Should().NotBeNull();
    }

    [Fact]
    public void UseD2DefaultPipeline_NullApp_ThrowsArgumentNull()
    {
        IApplicationBuilder app = null!;

        var act = () => app.UseD2DefaultPipeline();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MapD2DefaultEndpoints_NullEndpoints_ThrowsArgumentNull()
    {
        global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints =
            null!;

        var act = () => endpoints.MapD2DefaultEndpoints();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task AddD2ServiceDefaults_OptionsRegisteredInDi_BoundCorrectly()
    {
        // The aggregator binds D2ServiceDefaultsOptions into DI so
        // UseD2DefaultPipeline can read pass-through configures
        // (SecurityHeadersConfigure, InfrastructureBypassConfigure) at
        // pipeline-installation time. This pins the bind step.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            configureOptions: opts =>
            {
                opts.SkipAuthAutoWiring = true;
                opts.SkipLocalCacheAutoWiring = true;
            });

        var resolved = handle.Host.Services
            .GetRequiredService<IOptions<D2ServiceDefaultsOptions>>()
            .Value;
        resolved.SkipAuthAutoWiring.Should().BeTrue();
        resolved.SkipLocalCacheAutoWiring.Should().BeTrue();
    }
}
