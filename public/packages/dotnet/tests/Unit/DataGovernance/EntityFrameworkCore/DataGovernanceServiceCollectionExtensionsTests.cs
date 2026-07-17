// -----------------------------------------------------------------------
// <copyright file="DataGovernanceServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.DataGovernance.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.DataGovernance.Abstractions;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Tests for <c>DataGovernanceServiceCollectionExtensions</c>: verifies registration of the
/// engine, options binding, hosted-service wiring, idempotency, and the configure-callback
/// overload.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DataGovernanceServiceCollectionExtensionsTests
{
    // =========================================================================
    // Registration: engine + options + hosted service
    // =========================================================================

    [Fact]
    public void AddD2DataGovernance_registers_IAnonymizationEngine_as_scoped()
    {
        var services = new ServiceCollection();
        var config = BuildEmptyConfig();

        services.AddD2DataGovernance(config);

        var descriptor = services.Single(
            d => d.ServiceType == typeof(IAnonymizationEngine));

        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddD2DataGovernance_registers_AnonymizationModelValidator_as_IHostedService()
    {
        var services = new ServiceCollection();
        var config = BuildEmptyConfig();

        services.AddD2DataGovernance(config);

        var hosted = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .ToList();

        hosted.Should().ContainSingle(
            d => d.ImplementationType == typeof(AnonymizationModelValidator));
    }

    [Fact]
    public void AddD2DataGovernance_registers_AnonymizationEngineOptions_binding()
    {
        const int expected_batch_size = 250;

        var services = new ServiceCollection();
        var config = BuildConfig(new Dictionary<string, string?>
        {
            [
                "DATA_GOVERNANCE:BatchSize"
            ] = expected_batch_size.ToString(CultureInfo.InvariantCulture),
        });

        services.AddD2DataGovernance(config);
        services.AddLogging();

        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<AnonymizationEngineOptions>>().Value;

        opts.BatchSize.Should().Be(expected_batch_size, "bound from config section");
    }

    // =========================================================================
    // Idempotent: double-call does not duplicate engine or hosted service
    // =========================================================================

    [Fact]
    public void AddD2DataGovernance_idempotent_does_not_duplicate_engine_registration()
    {
        var services = new ServiceCollection();
        var config = BuildEmptyConfig();

        services.AddD2DataGovernance(config);
        services.AddD2DataGovernance(config); // second call — must be no-op

        var engineDescriptors = services
            .Where(d => d.ServiceType == typeof(IAnonymizationEngine))
            .ToList();

        engineDescriptors.Should().ContainSingle("TryAddScoped is idempotent");
    }

    [Fact]
    public void AddD2DataGovernance_idempotent_does_not_duplicate_hosted_service()
    {
        var services = new ServiceCollection();
        var config = BuildEmptyConfig();

        services.AddD2DataGovernance(config);
        services.AddD2DataGovernance(config);

        var validatorDescriptors = services
            .Where(d =>
                d.ServiceType == typeof(IHostedService)
                && d.ImplementationType == typeof(AnonymizationModelValidator))
            .ToList();

        validatorDescriptors.Should().ContainSingle("TryAddEnumerable is idempotent");
    }

    // =========================================================================
    // MaxConcurrencyRetries binding (L-2)
    // =========================================================================

    [Fact]
    public void AddD2DataGovernance_MaxConcurrencyRetries_bound_from_config_section()
    {
        const int expected_retries = 7;

        var services = new ServiceCollection();
        var config = BuildConfig(new Dictionary<string, string?>
        {
            [
                "DATA_GOVERNANCE:MaxConcurrencyRetries"
            ] = expected_retries.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });

        services.AddD2DataGovernance(config);
        services.AddLogging();

        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<AnonymizationEngineOptions>>().Value;

        opts.MaxConcurrencyRetries.Should().Be(expected_retries, "bound from config section");
    }

    // =========================================================================
    // Configure-callback overload: callback wins over section binding
    // =========================================================================

    [Fact]
    public void AddD2DataGovernance_configure_callback_overrides_bound_config()
    {
        var services = new ServiceCollection();
        var config = BuildConfig(new Dictionary<string, string?>
        {
            { "DATA_GOVERNANCE:BatchSize", "100" },
        });

        const int expected_batch_size = 999;

        services.AddD2DataGovernance(config, opts => opts.BatchSize = expected_batch_size);
        services.AddLogging();

        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<AnonymizationEngineOptions>>().Value;

        opts.BatchSize.Should().Be(
            expected_batch_size,
            "PostConfigure callback must win over the section-bound value");
    }

    // =========================================================================
    // SkipModelValidation: bound from config
    // =========================================================================

    [Fact]
    public void AddD2DataGovernance_SkipModelValidation_bound_from_config_section()
    {
        var services = new ServiceCollection();
        var config = BuildConfig(new Dictionary<string, string?>
        {
            { "DATA_GOVERNANCE:SkipModelValidation", "true" },
        });

        services.AddD2DataGovernance(config);
        services.AddLogging();

        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<AnonymizationEngineOptions>>().Value;

        opts.SkipModelValidation.Should().BeTrue("bound from config section");
    }

    [Fact]
    public void AddD2DataGovernance_configure_callback_can_set_SkipModelValidation_true()
    {
        var services = new ServiceCollection();
        var config = BuildEmptyConfig();

        services.AddD2DataGovernance(config, opts => opts.SkipModelValidation = true);
        services.AddLogging();

        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<AnonymizationEngineOptions>>().Value;

        opts.SkipModelValidation.Should().BeTrue();
    }

    // =========================================================================
    // Engine resolution: resolves when a DbContext is registered
    // =========================================================================

    [Fact]
    public void AddD2DataGovernance_engine_resolves_when_DbContext_registered()
    {
        var services = new ServiceCollection();
        var config = BuildEmptyConfig();

        services.AddD2DataGovernance(config);
        services.AddLogging();
        services.AddScoped<DbContext>(_ => DummyContext.Build());

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IAnonymizationEngine>();

        engine.Should().NotBeNull().And.BeOfType<AnonymizationEngine>();
    }

    // =========================================================================
    // ArgumentNullException guards
    // =========================================================================

    [Fact]
    public void AddD2DataGovernance_null_services_throws()
    {
        IServiceCollection? nullSvc = null;
        var config = BuildEmptyConfig();

        var act = () => nullSvc!.AddD2DataGovernance(config);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2DataGovernance_null_configuration_throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddD2DataGovernance(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2DataGovernance_configure_overload_null_configure_throws()
    {
        var services = new ServiceCollection();
        var config = BuildEmptyConfig();

        var act = () => services.AddD2DataGovernance(config, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static IConfiguration BuildEmptyConfig() =>
        new ConfigurationBuilder().Build();

    private static IConfiguration BuildConfig(Dictionary<string, string?> data) =>
        new ConfigurationBuilder().AddInMemoryCollection(data).Build();

    // Minimal DbContext for engine resolution tests — no entity types needed.
    private sealed class DummyContext : DbContext
    {
        private DummyContext(DbContextOptions<DummyContext> options)
            : base(options)
        {
        }

        internal static DummyContext Build()
        {
            var opts = new DbContextOptionsBuilder<DummyContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new DummyContext(opts);
        }
    }
}
