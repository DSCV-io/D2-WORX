// -----------------------------------------------------------------------
// <copyright file="KeyCustodianValidateOnStartTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Infra;

using D2.Edge.KeyCustodian.App.Infrastructure.Configuration;
using D2.Edge.KeyCustodian.Infra.Configuration;
using D2.Shared.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Proves the <c>AddD2KeyCustodian</c> options pipeline validates — resolving
/// <see cref="IOptions{T}"/>.<c>Value</c> over invalid <c>KEYCUSTODIAN_APP__*</c> /
/// <c>KEYCUSTODIAN_INFRA__*</c> configuration throws
/// <see cref="OptionsValidationException"/> (the same data-annotation gate
/// <c>ValidateOnStart</c> enforces at host build) rather than surfacing on first
/// use (§23.7). Covers BOTH options POCOs.
/// </summary>
public sealed class KeyCustodianValidateOnStartTests : IDisposable
{
    private readonly string r_rootKeyDir;

    public KeyCustodianValidateOnStartTests()
    {
        r_rootKeyDir = KcInfraTestKit.CreateRootKeyDir();
    }

    public void Dispose()
    {
        if (Directory.Exists(r_rootKeyDir))
            Directory.Delete(r_rootKeyDir, recursive: true);
    }

    [Fact]
    public void ValidOptions_BothResolve()
    {
        using var sp = BuildProvider(KcInfraTestKit.BuildConfiguration(r_rootKeyDir));

        sp.GetRequiredService<IOptions<KeyCustodianOptions>>().Value.Should().NotBeNull();
        sp.GetRequiredService<IOptions<KeyCustodianInfraOptions>>().Value.Should().NotBeNull();
    }

    [Fact]
    public void InvalidInfraInterval_FailsValidationOnResolve()
    {
        using var sp = BuildProvider(ConfigurationWithOverride(
            "KEYCUSTODIAN_INFRA:RotationCheckInterval", "00:00:00"));

        sp.Invoking(s => s.GetRequiredService<IOptions<KeyCustodianInfraOptions>>().Value)
            .Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void InvalidInfraRootKeyPath_FailsValidationOnResolve()
    {
        using var sp = BuildProvider(
            ConfigurationWithOverride("KEYCUSTODIAN_INFRA:RootKeyPath", string.Empty));

        sp.Invoking(s => s.GetRequiredService<IOptions<KeyCustodianInfraOptions>>().Value)
            .Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void InvalidInfraCommandTimeout_FailsValidationOnResolve()
    {
        using var sp = BuildProvider(
            ConfigurationWithOverride("KEYCUSTODIAN_INFRA:DbCommandTimeoutSeconds", "0"));

        sp.Invoking(s => s.GetRequiredService<IOptions<KeyCustodianInfraOptions>>().Value)
            .Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void InvalidAppRsaKeySize_FailsValidationOnResolve()
    {
        // Top-level [Range] on KeyCustodianOptions.RsaKeySizeBits (minimum 2048).
        // ValidateDataAnnotations validates top-level data-annotated members; a
        // below-minimum value fails the start gate.
        using var sp = BuildProvider(
            ConfigurationWithOverride("KEYCUSTODIAN_APP:RsaKeySizeBits", "512"));

        sp.Invoking(s => s.GetRequiredService<IOptions<KeyCustodianOptions>>().Value)
            .Should().Throw<OptionsValidationException>();
    }

    private static ServiceProvider BuildProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMessageBus, NoopMessageBus>();
        services.AddD2KeyCustodian(configuration, KcInfraTestKit.FAKE_CONNECTION_STRING);
        return services.BuildServiceProvider();
    }

    private IConfiguration ConfigurationWithOverride(string key, string value)
    {
        var settings = new Dictionary<string, string?>
        {
            ["KEYCUSTODIAN_APP:Default:Cadence"] = "30.00:00:00",
            ["KEYCUSTODIAN_APP:Default:Grace"] = "7.00:00:00",
            ["KEYCUSTODIAN_APP:Default:SmokeSoak"] = "01:00:00",
            ["KEYCUSTODIAN_INFRA:RootKeyPath"] = r_rootKeyDir,
            ["KEYCUSTODIAN_INFRA:RotationCheckInterval"] = "00:05:00",
            ["KEYCUSTODIAN_INFRA:DbCommandTimeoutSeconds"] = "30",
            [key] = value,
        };
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private sealed class NoopMessageBus : IMessageBus
    {
        public ValueTask<D2Result> PublishAsync<TMessage>(
            TMessage message, PublisherOptions? options = null, CancellationToken ct = default)
            where TMessage : class => ValueTask.FromResult(D2Result.Ok());

        public Task WaitForReadyAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
