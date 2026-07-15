// -----------------------------------------------------------------------
// <copyright file="SealedEncryptionStartupCheckTests.cs" company="DCSV">
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
/// The sealed startup self-check matrix: full round-trip pass, mismatched
/// keypair crash, seal-only pass, opener-only log-and-continue, broken
/// wiring crash, zero-registration no-op, registration idempotence, and DI
/// resolvability of every seam the registration extensions add.
/// </summary>
public sealed class SealedEncryptionStartupCheckTests
{
    private const string _SERVICE_ID = "fixture-sealed-recipient";

    // ---------------------------------------------------------------
    // Check arms.
    // ---------------------------------------------------------------

    [Fact]
    public async Task SealedSelfCheck_ValidRegistration_Passes()
    {
        var keypair = SealedTestKeys.GenerateKeypair();
        var provider = BuildProvider(services =>
        {
            services.AddKeyedSingleton<IPayloadSealer>(
                _SERVICE_ID,
                new PayloadSealer(
                    SealedTestKeys.PublicKeyring(_SERVICE_ID, "kid-1", keypair)));
            services.AddKeyedSingleton<IPayloadOpener>(
                _SERVICE_ID,
                new PayloadOpener(
                    SealedTestKeys.PrivateKeyring(_SERVICE_ID, "kid-1", keypair)));
            services.AddD2SealedEncryptionRecipient(_SERVICE_ID);
        });

        var act = () => RunCheckAsync(provider);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SealedSelfCheck_MismatchedKeypair_ThrowsCrashesHost()
    {
        var sealingPair = SealedTestKeys.GenerateKeypair();
        var wrongPair = SealedTestKeys.GenerateKeypair();
        var provider = BuildProvider(services =>
        {
            services.AddKeyedSingleton<IPayloadSealer>(
                _SERVICE_ID,
                new PayloadSealer(
                    SealedTestKeys.PublicKeyring(_SERVICE_ID, "kid-1", sealingPair)));
            services.AddKeyedSingleton<IPayloadOpener>(
                _SERVICE_ID,
                new PayloadOpener(
                    SealedTestKeys.PrivateKeyring(_SERVICE_ID, "kid-1", wrongPair)));
            services.AddD2SealedEncryptionRecipient(_SERVICE_ID);
        });

        var act = () => RunCheckAsync(provider);

        await act.Should().ThrowAsync<Exception>(
            "a mismatched keypair must crash the host, never serve traffic");
    }

    [Fact]
    public async Task SealedSelfCheck_SealerOnly_Passes()
    {
        var keypair = SealedTestKeys.GenerateKeypair();
        var provider = BuildProvider(services =>
        {
            services.AddKeyedSingleton<IPayloadSealer>(
                _SERVICE_ID,
                new PayloadSealer(
                    SealedTestKeys.PublicKeyring(_SERVICE_ID, "kid-1", keypair)));
            services.AddD2SealedEncryptionRecipient(_SERVICE_ID);
        });

        var act = () => RunCheckAsync(provider);

        await act.Should().NotThrowAsync(
            "a producer host holds no opener by design — the capability split");
    }

    [Fact]
    public async Task SealedSelfCheck_OpenerOnly_Passes()
    {
        var keypair = SealedTestKeys.GenerateKeypair();
        var provider = BuildProvider(services =>
        {
            services.AddKeyedSingleton<IPayloadOpener>(
                _SERVICE_ID,
                new PayloadOpener(
                    SealedTestKeys.PrivateKeyring(_SERVICE_ID, "kid-1", keypair)));
            services.AddD2SealedEncryptionRecipient(_SERVICE_ID);
        });

        var act = () => RunCheckAsync(provider);

        await act.Should().NotThrowAsync(
            "the private material was validated at keyring construction");
    }

    [Fact]
    public async Task SealedSelfCheck_NeitherSealerNorOpener_Throws()
    {
        var provider = BuildProvider(services =>
            services.AddD2SealedEncryptionRecipient(_SERVICE_ID));

        var act = () => RunCheckAsync(provider);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage($"*{_SERVICE_ID}*");
    }

    [Fact]
    public async Task SealedSelfCheck_NoRegistrations_LogsAndNoOps()
    {
        var provider = BuildProvider(services =>
            services.AddD2SealedEncryptionStartupCheck());

        var act = () => RunCheckAsync(provider);

        await act.Should().NotThrowAsync();
    }

    // ---------------------------------------------------------------
    // Registration seams (§1.3 — every seam resolved).
    // ---------------------------------------------------------------

    [Fact]
    public void AddD2SealedEncryptionRecipient_EverySeamResolvable()
    {
        var provider = BuildProvider(services =>
            services.AddD2SealedEncryptionRecipient(_SERVICE_ID));

        provider.GetRequiredService<SealedEncryptionRegistry>()
            .RecipientServiceIds.Should().ContainSingle().Which.Should().Be(_SERVICE_ID);
        provider.GetRequiredService<SealedEncryptionRegistration>()
            .RecipientServiceId.Should().Be(_SERVICE_ID);
        provider.GetServices<IHostedService>()
            .OfType<SealedEncryptionStartupCheck>()
            .Should().ContainSingle();
    }

    [Fact]
    public void AddD2SealedEncryptionStartupCheck_CalledTwice_RegistersHostedServiceOnce()
    {
        var provider = BuildProvider(services =>
        {
            services.AddD2SealedEncryptionStartupCheck();
            services.AddD2SealedEncryptionStartupCheck();
        });

        provider.GetServices<IHostedService>()
            .OfType<SealedEncryptionStartupCheck>()
            .Should().ContainSingle();
    }

    [Fact]
    public void AddD2SealedEncryptionRecipient_TwoRecipients_RegistryHoldsBoth()
    {
        var provider = BuildProvider(services =>
        {
            services.AddD2SealedEncryptionRecipient("fixture-recipient-a");
            services.AddD2SealedEncryptionRecipient("fixture-recipient-b");
        });

        provider.GetRequiredService<SealedEncryptionRegistry>()
            .RecipientServiceIds.Should().BeEquivalentTo(
                "fixture-recipient-a", "fixture-recipient-b");
    }

    [Fact]
    public void AddD2SealedEncryptionRecipient_InvalidServiceId_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddD2SealedEncryptionRecipient("Not-Valid!");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Registration_NullServiceId_Throws()
    {
        var act = () => new SealedEncryptionRegistration(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Registry_NullRegistrations_Throws()
    {
        var act = () => new SealedEncryptionRegistry(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ---------------------------------------------------------------
    // Helpers.
    // ---------------------------------------------------------------

    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configure(services);

        return services.BuildServiceProvider();
    }

    private static async Task RunCheckAsync(ServiceProvider provider)
    {
        var check = provider.GetServices<IHostedService>()
            .OfType<SealedEncryptionStartupCheck>()
            .Single();

        await check.StartAsync(CancellationToken.None);
        await check.StopAsync(CancellationToken.None);
    }
}
