// -----------------------------------------------------------------------
// <copyright file="SealedEncryptionSourceStartupCheckTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption.Sealed;

using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

/// <summary>
/// Deny-by-default coverage for <see cref="SealedEncryptionSourceStartupCheck"/> — the sealed
/// provenance guard: an unmarked (static) sealed recipient crashes a non-Development host,
/// a KeyCustodian-marked recipient passes, and a Development host logs-and-proceeds.
/// </summary>
public sealed class SealedEncryptionSourceStartupCheckTests
{
    private const string _SERVICE_ID = "fixture-sealed-recipient";

    [Fact]
    public async Task UnmarkedRecipient_NonDevelopment_Throws()
    {
        var provider = BuildProvider(
            isDevelopment: false,
            services =>
            {
                services.AddD2SealedEncryptionRecipient(_SERVICE_ID);
                services.AddD2SealedEncryptionSourceCheck();
            });

        var act = () => RunCheckAsync(provider);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage($"*{_SERVICE_ID}*static*");
    }

    [Fact]
    public async Task KeyCustodianMarkedRecipient_NonDevelopment_Passes()
    {
        var provider = BuildProvider(
            isDevelopment: false,
            services =>
            {
                services.MarkD2EncryptionSource(_SERVICE_ID, EncryptionKeyringSource.KeyCustodian);
                services.AddD2SealedEncryptionRecipient(_SERVICE_ID);
                services.AddD2SealedEncryptionSourceCheck();
            });

        var act = () => RunCheckAsync(provider);

        await act.Should().NotThrowAsync("a KeyCustodian-sourced recipient passes in every env");
    }

    [Fact]
    public async Task StaticFactoryMarkedRecipient_NonDevelopment_Throws()
    {
        var provider = BuildProvider(
            isDevelopment: false,
            services =>
            {
                services.MarkD2EncryptionSource(_SERVICE_ID, EncryptionKeyringSource.StaticFactory);
                services.AddD2SealedEncryptionRecipient(_SERVICE_ID);
                services.AddD2SealedEncryptionSourceCheck();
            });

        var act = () => RunCheckAsync(provider);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UnmarkedRecipient_Development_LogsAndProceeds()
    {
        var provider = BuildProvider(
            isDevelopment: true,
            services =>
            {
                services.AddD2SealedEncryptionRecipient(_SERVICE_ID);
                services.AddD2SealedEncryptionSourceCheck();
            });

        var act = () => RunCheckAsync(provider);

        await act.Should().NotThrowAsync("a Development host tolerates a static source (loud log)");
    }

    [Fact]
    public async Task NoRecipients_NoOps()
    {
        var provider = BuildProvider(
            isDevelopment: false,
            services => services.AddD2SealedEncryptionSourceCheck());

        var act = () => RunCheckAsync(provider);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void SourceCheck_CalledTwice_RegistersHostedServiceOnce()
    {
        var provider = BuildProvider(
            isDevelopment: false,
            services =>
            {
                services.AddD2SealedEncryptionSourceCheck();
                services.AddD2SealedEncryptionSourceCheck();
            });

        provider.GetServices<IHostedService>()
            .OfType<SealedEncryptionSourceStartupCheck>()
            .Should().ContainSingle();
    }

    private static ServiceProvider BuildProvider(
        bool isDevelopment, Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new StubEnvironment(isDevelopment));
        configure(services);

        return services.BuildServiceProvider();
    }

    private static async Task RunCheckAsync(ServiceProvider provider)
    {
        var check = provider.GetServices<IHostedService>()
            .OfType<SealedEncryptionSourceStartupCheck>()
            .Single();

        await check.StartAsync(CancellationToken.None);
        await check.StopAsync(CancellationToken.None);
    }

    private sealed class StubEnvironment(bool isDevelopment) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } =
            isDevelopment ? Environments.Development : Environments.Production;

        public string ApplicationName { get; set; } = "D2.Tests";

        public string ContentRootPath { get; set; } = ".";

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
