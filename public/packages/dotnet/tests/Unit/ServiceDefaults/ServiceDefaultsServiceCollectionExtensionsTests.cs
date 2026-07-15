// -----------------------------------------------------------------------
// <copyright file="ServiceDefaultsServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.ServiceDefaults;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using DcsvIo.D2.Auth.Startup;
using DcsvIo.D2.Auth.Validation;
using DcsvIo.D2.Caching;
using DcsvIo.D2.Caching.Local.Default;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Logging;
using DcsvIo.D2.ServiceDefaults;
using DcsvIo.D2.Telemetry;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Smoke tests for the <c>AddD2ServiceDefaults</c> aggregator — verifies the
/// composition surface (every expected DI registration appears, opt-out
/// flags actually opt out, fail-fast contracts hold) by inspecting the
/// <see cref="IServiceCollection"/> directly rather than resolving services
/// (resolution requires a full set of cross-lib dependencies that the
/// per-step Integration tests cover via a test host fixture).
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class ServiceDefaultsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddD2ServiceDefaults_NullServices_Throws()
    {
        IServiceCollection? services = null;
        var configuration = new ConfigurationBuilder().Build();

        var act = () => services!.AddD2ServiceDefaults(
            configuration,
            opts => opts.SkipAuthAutoWiring = true);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2ServiceDefaults_NullConfiguration_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddD2ServiceDefaults(
            null!,
            opts => opts.SkipAuthAutoWiring = true);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2ServiceDefaults_ReturnsSameServicesForChaining()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var returned = services.AddD2ServiceDefaults(
            configuration,
            opts => opts.SkipAuthAutoWiring = true);

        returned.Should().BeSameAs(services);
    }

    [Fact]
    public void AddD2ServiceDefaults_AuthConfigureNullAndNotSkipped_Throws()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Default: no opts callback at all → SkipAuthAutoWiring = false
        // AND AuthConfigure = null. Aggregator MUST throw with a
        // remediation message rather than silently skipping auth.
        var act = () => services.AddD2ServiceDefaults(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AuthConfigure is required*SkipAuthAutoWiring*");
    }

    [Fact]
    public void AddD2ServiceDefaults_AuthConfigureNullAndExplicitlyNotSkipped_Throws()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var act = () => services.AddD2ServiceDefaults(
            configuration,
            opts => opts.SkipAuthAutoWiring = false);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AuthConfigure is required*SkipAuthAutoWiring*");
    }

    [Fact]
    public void AddD2ServiceDefaults_AuthSkipped_DoesNotThrow_AndDoesNotRegisterJwtValidator()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddD2ServiceDefaults(
            configuration,
            opts => opts.SkipAuthAutoWiring = true);

        services.Any(d => d.ServiceType == typeof(JwtValidator))
            .Should().BeFalse();
    }

    [Fact]
    public void AddD2ServiceDefaults_AuthConfigureProvided_RegistersJwtValidator()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddD2ServiceDefaults(
            configuration,
            opts => opts.AuthConfigure = auth =>
            {
                auth.Issuer = new Uri("https://edge.internal");
                auth.Audience = "files";
            });

        services.Any(d => d.ServiceType == typeof(JwtValidator))
            .Should().BeTrue();
    }

    [Fact]
    public void AddD2ServiceDefaults_LocalCacheSkipped_DoesNotRegisterILocalCache()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddD2ServiceDefaults(
            configuration,
            opts =>
            {
                opts.SkipAuthAutoWiring = true;
                opts.SkipLocalCacheAutoWiring = true;
            });

        services.Any(d => d.ServiceType == typeof(ILocalCache))
            .Should().BeFalse();
    }

    [Fact]
    public void AddD2ServiceDefaults_LocalCacheNotSkipped_RegistersILocalCache()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddD2ServiceDefaults(
            configuration,
            opts => opts.SkipAuthAutoWiring = true);

        services.Any(d =>
            d.ServiceType == typeof(ILocalCache)
            && d.ImplementationType == typeof(DefaultLocalCache))
            .Should().BeTrue();
    }

    [Fact]
    public void AddD2ServiceDefaults_RegistersHandlerContextOpenGeneric()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddD2ServiceDefaults(
            configuration,
            opts => opts.SkipAuthAutoWiring = true);

        services.Any(d => d.ServiceType == typeof(HandlerContext<>))
            .Should().BeTrue();
    }

    [Fact]
    public void AddD2ServiceDefaults_AlwaysRegistersSystemWorkPlane()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddD2ServiceDefaults(
            configuration,
            opts => opts.SkipAuthAutoWiring = true);

        services.Any(d => d.ServiceType == typeof(ISystemWorkScopeFactory))
            .Should().BeTrue();
        services.Any(d => d.ServiceType == typeof(MutableRequestContext))
            .Should().BeTrue();
        services.Any(d => d.ServiceType == typeof(IRequestContext))
            .Should().BeTrue();
    }

    [Fact]
    public void AddD2ServiceDefaults_RegistersI18nSingletons()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddD2ServiceDefaults(
            configuration,
            opts => opts.SkipAuthAutoWiring = true);

        // I18n registers SupportedLocales + ITranslator as singletons; we
        // verify presence at the IServiceCollection level (resolution needs
        // a real messages directory + locale env vars, covered in
        // integration tests).
        services.Any(d => d.ServiceType == typeof(DcsvIo.D2.I18n.SupportedLocales))
            .Should().BeTrue();
        services.Any(d => d.ServiceType == typeof(DcsvIo.D2.I18n.ITranslator))
            .Should().BeTrue();
    }

    [Fact]
    public void AddD2ServiceDefaults_RegistersOptionsBindings_ForEveryComponent()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddD2ServiceDefaults(
            configuration,
            opts => opts.SkipAuthAutoWiring = true);

        // Each AddD2X call wires AddOptions<TOptions>().Configure(...) so
        // IOptions<TOptions> is resolvable. Verify presence of the
        // bindings via the IConfigureOptions<T> registration pattern
        // (AddOptions installs a ConfigureOptions<T> behind the scenes).
        services.Any(d =>
            d.ServiceType == typeof(IConfigureOptions<D2LoggingOptions>))
            .Should().BeTrue();
        services.Any(d =>
            d.ServiceType == typeof(IConfigureOptions<D2TelemetryOptions>))
            .Should().BeTrue();
        services.Any(d =>
            d.ServiceType == typeof(IConfigureOptions<D2CorsOptions>))
            .Should().BeTrue();
        services.Any(d =>
            d.ServiceType == typeof(IConfigureOptions<D2ProblemDetailsOptions>))
            .Should().BeTrue();
    }

    [Fact]
    public void AddD2ServiceDefaults_CalledTwice_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddD2ServiceDefaults(
            configuration,
            opts => opts.SkipAuthAutoWiring = true);
        var act = () => services.AddD2ServiceDefaults(
            configuration,
            opts => opts.SkipAuthAutoWiring = true);

        act.Should().NotThrow();
    }

    [Fact]
    public void AddD2ServiceDefaults_OptsCallbackInvokedExactlyOnce()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var invocations = 0;

        services.AddD2ServiceDefaults(
            configuration,
            opts =>
            {
                invocations++;
                opts.SkipAuthAutoWiring = true;
            });

        invocations.Should().Be(1);
    }

    // ── Auth endpoint guard wiring ────────────────────────────────────────

    [Fact]
    public void AddD2ServiceDefaults_AuthWired_GuardRegisteredByDefault()
    {
        // When auth is wired (SkipAuthAutoWiring=false, default) and
        // SkipAuthEndpointGuard is false (default), the guard IStartupFilter
        // must be present in the collection.
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddD2ServiceDefaults(
            configuration,
            opts => opts.AuthConfigure = auth =>
            {
                auth.Issuer = new Uri("https://edge.internal");
                auth.Audience = "files";
            });

        services.Any(d =>
            d.ServiceType == typeof(IStartupFilter) &&
            d.ImplementationType == typeof(AuthEndpointGuardStartupFilter))
            .Should().BeTrue("guard is ON by default when auth is wired");
    }

    [Fact]
    public void AddD2ServiceDefaults_AuthWired_SkipGuardTrue_GuardNotRegistered()
    {
        // Explicit opt-out: SkipAuthEndpointGuard=true must suppress the guard.
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddD2ServiceDefaults(
            configuration,
            opts =>
            {
                opts.SkipAuthEndpointGuard = true;
                opts.AuthConfigure = auth =>
                {
                    auth.Issuer = new Uri("https://edge.internal");
                    auth.Audience = "files";
                };
            });

        services.Any(d =>
            d.ServiceType == typeof(IStartupFilter) &&
            d.ImplementationType == typeof(AuthEndpointGuardStartupFilter))
            .Should().BeFalse("SkipAuthEndpointGuard=true must suppress guard registration");
    }

    [Fact]
    public void AddD2ServiceDefaults_AuthSkipped_GuardNotRegistered()
    {
        // SkipAuthAutoWiring=true implies the guard is not wired —
        // anonymous-only services don't declare scopes.
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddD2ServiceDefaults(
            configuration,
            opts => opts.SkipAuthAutoWiring = true);

        services.Any(d =>
            d.ServiceType == typeof(IStartupFilter) &&
            d.ImplementationType == typeof(AuthEndpointGuardStartupFilter))
            .Should().BeFalse("guard must not register when SkipAuthAutoWiring=true");
    }
}
