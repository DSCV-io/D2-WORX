// -----------------------------------------------------------------------
// <copyright file="SealingServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Client.Sealing;

using System.Linq;
using AwesomeAssertions;
using D2.Edge.KeyCustodian.App.Application.Sealing;
using D2.Edge.KeyCustodian.Client.Sealing;
using D2.Private.Encryption;
using D2.Shared.Encryption;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// DI-shape coverage for the single spec-driven sealed-encryption registration call
/// (<c>AddD2SealedEncryptionViaKeyCustodian</c>) and the in-process twin
/// (<c>AddD2SealedEncryptionFromKeyCustodian</c>): a keyed sealer per DISTINCT generated
/// consumer service, the self-only opener wired IFF the registering service is a sealed
/// consumer, KeyCustodian provenance on every registration, and the structural pin that NO
/// in-process opener source exists.
/// </summary>
/// <remarks>
/// Presence is probed via <see cref="IServiceProviderIsKeyedService"/> -- never resolving the
/// sealer/opener (that would trigger the KC fetch). The hard enforcement of who may open a
/// sealed frame is KeyCustodian-side; this asserts only the DI hygiene shape.
/// </remarks>
public sealed class SealingServiceCollectionExtensionsTests
{
    [Fact]
    public void ViaKeyCustodian_WiresKeyedSealer_PerDistinctConsumerService()
    {
        var sp = new ServiceCollection()
            .AddD2SealedEncryptionViaKeyCustodian("some-producer")
            .BuildServiceProvider();
        var isKeyed = sp.GetRequiredService<IServiceProviderIsKeyedService>();
        var consumers = DistinctConsumers();

        foreach (var consumer in consumers)
            isKeyed.IsKeyedService(typeof(IPayloadSealer), consumer).Should().BeTrue();
    }

    [Fact]
    public void ViaKeyCustodian_NonConsumerHost_WiresNoOpenerAtAll()
    {
        // A pure producer (not any sealed domain's consumer) gets NO opener registration --
        // structural least-privilege; a producer-only host can never open a sealed frame.
        var sp = new ServiceCollection()
            .AddD2SealedEncryptionViaKeyCustodian("some-producer")
            .BuildServiceProvider();
        var isKeyed = sp.GetRequiredService<IServiceProviderIsKeyedService>();
        var consumers = DistinctConsumers();

        isKeyed.IsKeyedService(typeof(IPayloadOpener), "some-producer").Should().BeFalse();

        foreach (var consumer in consumers)
            isKeyed.IsKeyedService(typeof(IPayloadOpener), consumer).Should().BeFalse();
    }

    [Fact]
    public void ViaKeyCustodian_ConsumerHost_WiresSelfOpener()
    {
        // The registering service IS a sealed domain's consumer -> its own private-key opener
        // is wired (and no other opener).
        var ownConsumer = ProductEncryptionDomainModes.ConsumerServiceByDomain.Values.First();
        var sp = new ServiceCollection()
            .AddD2SealedEncryptionViaKeyCustodian(ownConsumer)
            .BuildServiceProvider();
        var isKeyed = sp.GetRequiredService<IServiceProviderIsKeyedService>();

        isKeyed.IsKeyedService(typeof(IPayloadOpener), ownConsumer).Should().BeTrue();

        foreach (var other in DistinctConsumers().Where(c => c != ownConsumer))
        {
            isKeyed.IsKeyedService(typeof(IPayloadOpener), other).Should().BeFalse();
        }
    }

    [Fact]
    public void ViaKeyCustodian_MarksEveryRegistrationKeyCustodianSourced()
    {
        var ownConsumer = ProductEncryptionDomainModes.ConsumerServiceByDomain.Values.First();
        var sp = new ServiceCollection()
            .AddD2SealedEncryptionViaKeyCustodian(ownConsumer)
            .BuildServiceProvider();
        var consumers = DistinctConsumers();

        foreach (var consumer in consumers)
        {
            var markers = sp.GetKeyedServices<EncryptionSourceMarker>(consumer).ToArray();
            markers.Should().NotBeEmpty();
            markers.Should().OnlyContain(m => m.Source == EncryptionKeyringSource.KeyCustodian);
        }
    }

    [Fact]
    public void ViaKeyCustodian_BlankOwnServiceId_ThrowsAtRegistration()
    {
        var act = () => new ServiceCollection().AddD2SealedEncryptionViaKeyCustodian("   ");

        act.Should().Throw<Exception>("fail-loud at registration on an invalid service id");
    }

    [Fact]
    public void ViaKeyCustodian_NonGrammarOwnServiceId_ThrowsAtRegistration()
    {
        var act = () => new ServiceCollection().AddD2SealedEncryptionViaKeyCustodian("Bad_Id!");

        act.Should().Throw<ArgumentException>("service id must match [a-z0-9-]");
    }

    [Fact]
    public void FromKeyCustodian_InProcess_WiresSealerArmsOnly_NeverOpener()
    {
        // The in-process twin registers sealers only -- NO in-process opener source exists
        // anywhere, even when own IS a sealed consumer (decrypt is CrossProcessHop-only).
        var ownConsumer = ProductEncryptionDomainModes.ConsumerServiceByDomain.Values.First();
        var sp = new ServiceCollection()
            .AddD2SealedEncryptionFromKeyCustodian(ownConsumer, callingModuleId: "edge")
            .BuildServiceProvider();
        var isKeyed = sp.GetRequiredService<IServiceProviderIsKeyedService>();
        var consumers = DistinctConsumers();

        foreach (var consumer in consumers)
        {
            isKeyed.IsKeyedService(typeof(IPayloadSealer), consumer).Should().BeTrue();
            isKeyed.IsKeyedService(typeof(IPayloadOpener), consumer).Should().BeFalse();
        }
    }

    [Fact]
    public void FromKeyCustodian_RegistersProductSealedOverlay_OnPublicModeCatalog()
    {
        // Fail-without-fix: From must compose the shared sealed wiring that boots
        // ProductEncryptionDomainBootstrap -- otherwise ModeFor(audit) stays Symmetric and
        // MqMessageDescriptor.IsSealed / SealedConsumerStartupCheck take the wrong path.
        _ = new ServiceCollection()
            .AddD2SealedEncryptionFromKeyCustodian("audit", callingModuleId: "edge");

        EncryptionDomainModeCatalog.ModeFor("audit").Should().Be(EncryptionDomainMode.Sealed);
        var found = EncryptionDomainModeCatalog.TryGetConsumerService(
            "audit", out var consumer);
        found.Should().BeTrue();
        consumer.Should().Be("audit");
    }

    [Fact]
    public void ViaKeyCustodian_RegistersProductSealedOverlay_OnPublicModeCatalog()
    {
        _ = new ServiceCollection()
            .AddD2SealedEncryptionViaKeyCustodian("some-producer");

        EncryptionDomainModeCatalog.ModeFor("notifications")
            .Should().Be(EncryptionDomainMode.Sealed);
        EncryptionDomainModeCatalog.ModeFor("courier")
            .Should().Be(EncryptionDomainMode.Sealed);
    }

    private static IEnumerable<string> DistinctConsumers() =>
        ProductEncryptionDomainModes.ConsumerServiceByDomain.Values.Distinct();
}
