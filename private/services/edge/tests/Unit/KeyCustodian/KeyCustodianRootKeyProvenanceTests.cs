// -----------------------------------------------------------------------
// <copyright file="KeyCustodianRootKeyProvenanceTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian;

using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Pins that the KeyCustodian composition marks its root-key encryption registration
/// <see cref="EncryptionKeyringSource.KeyCustodian"/>, so the deny-by-default encryption
/// source guard passes even in a non-Development host (an unmarked static factory would
/// crash the host).
/// </summary>
public sealed class KeyCustodianRootKeyProvenanceTests
{
    [Fact]
    public async Task KeyCustodianComposition_RootKeyMarkedKeyCustodian_SourceCheckPassesInProduction()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment("Production"));
        services.AddD2KeyCustodian(
            new ConfigurationBuilder().Build(),
            "Host=localhost;Database=keycustodian;Username=kc;Password=fixture");

        using var provider = services.BuildServiceProvider();

        // Construct the source check directly (avoids booting the DB-dependent hosted
        // services) and drive it against the real KeyCustodian registration graph.
        var check = new EncryptionSourceStartupCheck(
            provider,
            provider.GetRequiredService<EncryptionRegistry>(),
            NullLogger<EncryptionSourceStartupCheck>.Instance);

        var act = async () => await check.StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync(
            "the KC root key is marked KeyCustodian, so the deny-by-default guard passes in production");
    }

    private sealed class FakeHostEnvironment(string environment) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environment;

        public string ApplicationName { get; set; } = "keycustodian-tests";

        public string ContentRootPath { get; set; } = ".";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
