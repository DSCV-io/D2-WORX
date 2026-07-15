// -----------------------------------------------------------------------
// <copyright file="EncryptionSourceStartupCheckTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using DcsvIo.D2.Tests.Unit.Handler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Deny-by-default matrix for the encryption-source provenance guard: a
/// static-factory (or unmarked) keyring crashes a non-Development host; a
/// KeyCustodian-sourced keyring passes everywhere; a static source only warns
/// in Development.
/// </summary>
public sealed class EncryptionSourceStartupCheckTests
{
    private const string _AUDIT = "audit";
    private const string _COURIER = "courier";

    [Fact]
    public async Task UnmarkedRegistration_ProductionEnvironment_TreatedAsStaticAndThrows()
    {
        using var sp = BuildProvider(
            Environments.Production,
            services => services.AddD2EncryptionFor(_AUDIT, _ => TestKeyrings.AuditSingleKey()));

        var check = CheckOver(sp);
        var act = () => check.StartAsync(default);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage($"*{_AUDIT}*");
    }

    [Fact]
    public async Task StaticFactoryMarkedSource_ProductionEnvironment_Throws()
    {
        using var sp = BuildProvider(
            Environments.Production,
            services =>
            {
                services.AddD2EncryptionFor(_AUDIT, _ => TestKeyrings.AuditSingleKey());
                services.MarkD2EncryptionSource(_AUDIT, EncryptionKeyringSource.StaticFactory);
            });

        var check = CheckOver(sp);
        var act = () => check.StartAsync(default);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task StaticFactorySource_DevelopmentEnvironment_WarnsAndStarts()
    {
        using var sp = BuildProvider(
            Environments.Development,
            services => services.AddD2EncryptionFor(_AUDIT, _ => TestKeyrings.AuditSingleKey()));

        var logger = new TestLogger<EncryptionSourceStartupCheck>();

        var check = CheckOver(sp, logger);
        var act = () => check.StartAsync(default);

        await act.Should().NotThrowAsync();
        logger.Entries.Should().Contain(
            e => e.Level == LogLevel.Warning && e.Message.Contains(_AUDIT, StringComparison.Ordinal),
            "the Development warning must name the offending domain");
    }

    [Fact]
    public async Task KeyCustodianSource_ProductionEnvironment_Passes()
    {
        using var sp = BuildProvider(
            Environments.Production,
            services =>
            {
                services.AddD2EncryptionFor(_AUDIT, _ => TestKeyrings.AuditSingleKey());
                services.MarkD2EncryptionSource(_AUDIT, EncryptionKeyringSource.KeyCustodian);
            });

        var check = CheckOver(sp);
        var act = () => check.StartAsync(default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task KeyCustodianSource_DevelopmentEnvironment_Passes()
    {
        using var sp = BuildProvider(
            Environments.Development,
            services =>
            {
                services.AddD2EncryptionFor(_AUDIT, _ => TestKeyrings.AuditSingleKey());
                services.MarkD2EncryptionSource(_AUDIT, EncryptionKeyringSource.KeyCustodian);
            });

        var check = CheckOver(sp);
        var act = () => check.StartAsync(default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MixedDomains_ProductionEnvironment_ThrowsNamingTheStaticDomain()
    {
        using var sp = BuildProvider(
            Environments.Production,
            services =>
            {
                services.AddD2EncryptionFor(_AUDIT, _ => TestKeyrings.SingleKey("audit-kid", _AUDIT));
                services.MarkD2EncryptionSource(_AUDIT, EncryptionKeyringSource.KeyCustodian);

                // courier is left unmarked → deny-by-default static.
                services.AddD2EncryptionFor(
                    _COURIER, _ => TestKeyrings.SingleKey("courier-kid", _COURIER));
            });

        var check = CheckOver(sp);
        var act = () => check.StartAsync(default);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage($"*{_COURIER}*")
            .Which.Message.Should().NotContain(
                _AUDIT, "the KeyCustodian-sourced domain must not be the one flagged");
    }

    [Fact]
    public async Task MultipleMarkersForOneDomain_AnyStatic_ProductionEnvironment_FailsClosed()
    {
        using var sp = BuildProvider(
            Environments.Production,
            services =>
            {
                services.AddD2EncryptionFor(_AUDIT, _ => TestKeyrings.AuditSingleKey());
                services.MarkD2EncryptionSource(_AUDIT, EncryptionKeyringSource.KeyCustodian);
                services.MarkD2EncryptionSource(_AUDIT, EncryptionKeyringSource.StaticFactory);
            });

        var check = CheckOver(sp);
        var act = () => check.StartAsync(default);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task NoRegistrations_LogsAndPasses()
    {
        using var sp = BuildProvider(Environments.Production, _ => { });
        var logger = new TestLogger<EncryptionSourceStartupCheck>();

        var check = new EncryptionSourceStartupCheck(
            sp, new EncryptionRegistry([]), logger);

        await check.StartAsync(default);

        logger.Entries.Should().Contain(e => e.EventId.Id == 1);
    }

    [Fact]
    public async Task MissingHostEnvironment_TreatedAsProduction_FailsClosed()
    {
        // No IHostEnvironment registered — the fail-closed default is non-Development.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2EncryptionFor(_AUDIT, _ => TestKeyrings.AuditSingleKey());

        using var sp = services.BuildServiceProvider();

        var check = CheckOver(sp);
        var act = () => check.StartAsync(default);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task StartAsync_CancellationRequested_Throws()
    {
        using var sp = BuildProvider(
            Environments.Development,
            services => services.AddD2EncryptionFor(_AUDIT, _ => TestKeyrings.AuditSingleKey()));

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var check = CheckOver(sp);
        var token = cts.Token;
        var act = () => check.StartAsync(token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task StopAsync_IsNoOp()
    {
        using var sp = BuildProvider(Environments.Production, _ => { });
        var check = new EncryptionSourceStartupCheck(
            sp, new EncryptionRegistry([]), NullLogger<EncryptionSourceStartupCheck>.Instance);

        await check.StopAsync(default);
    }

    [Fact]
    public void AddD2EncryptionFor_HooksTheSourceCheck()
    {
        var services = new ServiceCollection();
        services.AddD2EncryptionFor(_AUDIT, _ => TestKeyrings.AuditSingleKey());

        HostedCheckDescriptorCount(services).Should().Be(1);
    }

    [Fact]
    public void AddD2EncryptionSourceCheck_CalledTwice_RegistersOnce()
    {
        var services = new ServiceCollection();
        services.AddD2EncryptionSourceCheck();
        services.AddD2EncryptionSourceCheck();

        HostedCheckDescriptorCount(services).Should().Be(1);
    }

    [Fact]
    public void SourceCheckSeams_ResolveFromContainer()
    {
        using var sp = BuildProvider(
            Environments.Production,
            services =>
            {
                services.AddD2EncryptionFor(_AUDIT, _ => TestKeyrings.AuditSingleKey());
                services.MarkD2EncryptionSource(_AUDIT, EncryptionKeyringSource.KeyCustodian);
            });

        // Every new DI seam resolves (rules.md §1.3 — descriptor presence is not resolvability).
        sp.GetRequiredService<EncryptionRegistry>().Should().NotBeNull();
        sp.GetKeyedService<EncryptionSourceMarker>(_AUDIT).Should().NotBeNull();
        sp.GetServices<IHostedService>()
            .OfType<EncryptionSourceStartupCheck>()
            .Should().ContainSingle();
    }

    [Fact]
    public void MarkD2EncryptionSource_NullServices_Throws()
    {
        var act = () => EncryptionSourceServiceCollectionExtensions.MarkD2EncryptionSource(
            null!, _AUDIT, EncryptionKeyringSource.KeyCustodian);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MarkD2EncryptionSource_NullServiceKey_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.MarkD2EncryptionSource(
            null!, EncryptionKeyringSource.KeyCustodian);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MarkD2EncryptionSource_WhitespaceServiceKey_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.MarkD2EncryptionSource(
            "   ", EncryptionKeyringSource.KeyCustodian);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddD2EncryptionSourceCheck_NullServices_Throws()
    {
        var act = () =>
            EncryptionSourceServiceCollectionExtensions.AddD2EncryptionSourceCheck(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EncryptionSourceMarker_NullServiceKey_Throws()
    {
        var act = () => new EncryptionSourceMarker(null!, EncryptionKeyringSource.KeyCustodian);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EncryptionSourceMarker_RetainsServiceKeyAndSource()
    {
        var marker = new EncryptionSourceMarker(_AUDIT, EncryptionKeyringSource.KeyCustodian);

        marker.ServiceKey.Should().Be(_AUDIT);
        marker.Source.Should().Be(EncryptionKeyringSource.KeyCustodian);
    }

    private static ServiceProvider BuildProvider(
        string environmentName,
        Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment(environmentName));
        configure(services);

        return services.BuildServiceProvider();
    }

    private static EncryptionSourceStartupCheck CheckOver(
        ServiceProvider sp,
        ILogger<EncryptionSourceStartupCheck>? logger = null)
        => new(
            sp,
            sp.GetRequiredService<EncryptionRegistry>(),
            logger ?? NullLogger<EncryptionSourceStartupCheck>.Instance);

    private static int HostedCheckDescriptorCount(IServiceCollection services)
        => services.Count(d =>
            d.ServiceType == typeof(IHostedService)
            && d.ImplementationType == typeof(EncryptionSourceStartupCheck));

    /// <summary>Minimal <see cref="IHostEnvironment"/> stub for driving the dev/prod gate.</summary>
    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "test-app";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
