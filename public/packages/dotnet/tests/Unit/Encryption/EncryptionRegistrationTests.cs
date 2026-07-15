// -----------------------------------------------------------------------
// <copyright file="EncryptionRegistrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption;

using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Coverage for the DI registration helpers and the keyed-services pattern
/// they produce.
/// </summary>
public sealed class EncryptionRegistrationTests
{
    [Fact]
    public void AddD2EncryptionFor_RegistersKeyedSingleton()
    {
        var services = new ServiceCollection();
        var ring = TestKeyrings.AuditSingleKey();
        services.AddD2EncryptionFor("audit", _ => ring);

        using var sp = services.BuildServiceProvider();
        var crypto = sp.GetRequiredKeyedService<IPayloadCrypto>("audit");
        var also = sp.GetRequiredKeyedService<IPayloadCrypto>("audit");

        crypto.Should().BeSameAs(also, "keyed singleton must return the same instance");
    }

    [Fact]
    public void AddD2EncryptionFor_KeyringFactoryRunsOnce()
    {
        var calls = 0;
        var services = new ServiceCollection();
        services.AddD2EncryptionFor("audit", _ =>
        {
            calls++;
            return TestKeyrings.AuditSingleKey();
        });

        using var sp = services.BuildServiceProvider();
        sp.GetRequiredKeyedService<IPayloadCrypto>("audit");
        sp.GetRequiredKeyedService<IPayloadCrypto>("audit");
        sp.GetRequiredKeyedService<PayloadCryptoKeyring>("audit");

        calls.Should().Be(1);
    }

    [Fact]
    public void AddD2EncryptionFor_MultipleDomains_IsolatedCryptos()
    {
        var services = new ServiceCollection();
        services.AddD2EncryptionFor("audit", _ => TestKeyrings.SingleKey("audit-2026q2", "audit"));
        services.AddD2EncryptionFor(
            "courier", _ => TestKeyrings.SingleKey("courier-2026q2", "courier"));

        using var sp = services.BuildServiceProvider();
        var auditCrypto = sp.GetRequiredKeyedService<IPayloadCrypto>("audit");
        var courierCrypto = sp.GetRequiredKeyedService<IPayloadCrypto>("courier");

        auditCrypto.Should().NotBeSameAs(courierCrypto);

        // Cross-decrypt must fail — different keys + different AAD.
        var auditFrame = auditCrypto.Encrypt("for audit"u8);
        var act = () => courierCrypto.Decrypt(auditFrame);
        act.Should().Throw<KidNotInKeyringException>();
    }

    [Fact]
    public void AddD2EncryptionFor_NullServices_Throws()
    {
        var act = () => EncryptionServiceCollectionExtensions.AddD2EncryptionFor(
            null!,
            "audit",
            _ => TestKeyrings.AuditSingleKey());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2EncryptionFor_NullServiceKey_Throws()
    {
        var services = new ServiceCollection();
        var act = () => services.AddD2EncryptionFor(null!, _ => TestKeyrings.AuditSingleKey());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2EncryptionFor_WhitespaceServiceKey_Throws()
    {
        var services = new ServiceCollection();
        var act = () => services.AddD2EncryptionFor("   ", _ => TestKeyrings.AuditSingleKey());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddD2EncryptionFor_NullKeyringFactory_Throws()
    {
        var services = new ServiceCollection();
        var act = () => services.AddD2EncryptionFor("audit", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task EncryptionStartupCheck_RoundTripsEveryRegisteredDomain()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2EncryptionFor("audit", _ => TestKeyrings.SingleKey("audit-2026q2", "audit"));
        services.AddD2EncryptionFor(
            "courier", _ => TestKeyrings.SingleKey("courier-2026q2", "courier"));
        services.AddD2EncryptionStartupCheck();

        using var sp = services.BuildServiceProvider();
        var hosted = sp.GetServices<IHostedService>().OfType<EncryptionStartupCheck>().Single();

        await hosted.StartAsync(default);

        // Reaching here without exception = self-test passed for both domains.
    }

    [Fact]
    public async Task EncryptionStartupCheck_NoRegistrations_LogsAndPasses()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new EncryptionRegistry([]));
        var check = new EncryptionStartupCheck(
            services.BuildServiceProvider(),
            new EncryptionRegistry([]),
            NullLogger<EncryptionStartupCheck>.Instance);

        await check.StartAsync(default);

        // No keys registered → no-op pass.
    }

    [Fact]
    public async Task EncryptionStartupCheck_MissingKeyedRegistration_Throws()
    {
        // Registry says "audit" was registered but no keyed IPayloadCrypto exists.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new EncryptionRegistry([new EncryptionRegistration("audit")]));

        var check = new EncryptionStartupCheck(
            services.BuildServiceProvider(),
            new EncryptionRegistry([new EncryptionRegistration("audit")]),
            NullLogger<EncryptionStartupCheck>.Instance);

        var act = async () => await check.StartAsync(default);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task EncryptionStartupCheck_StopAsync_IsNoOp()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2EncryptionFor("audit", _ => TestKeyrings.AuditSingleKey());
        services.AddD2EncryptionStartupCheck();

        using var sp = services.BuildServiceProvider();
        var hosted = sp.GetServices<IHostedService>().OfType<EncryptionStartupCheck>().Single();

        await hosted.StopAsync(default);
    }

    [Fact]
    public void EncryptionRegistration_NullServiceKey_Throws()
    {
        var act = () => new EncryptionRegistration(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EncryptionRegistry_DistinctServiceKeys()
    {
        var registry = new EncryptionRegistry(
        [
            new EncryptionRegistration("audit"),
            new EncryptionRegistration("audit"),
            new EncryptionRegistration("courier"),
        ]);

        registry.ServiceKeys.Should().BeEquivalentTo("audit", "courier");
    }

    [Fact]
    public void EncryptionRegistry_NullRegistrations_Throws()
    {
        var act = () => new EncryptionRegistry(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
