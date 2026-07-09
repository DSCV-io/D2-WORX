// -----------------------------------------------------------------------
// <copyright file="SealingRegistrationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Client.Sealing;

using System.Reflection;
using System.Text;
using AwesomeAssertions;
using D2.Edge.KeyCustodian.App.Application.Sealing;
using D2.Edge.KeyCustodian.Client.Sealing;
using D2.Shared.Encryption;
using D2.Shared.Logging.Destructuring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog.Core;
using Serilog.Events;
using Xunit;

/// <summary>
/// DI-matrix coverage for the single spec-driven sealed-encryption registration call
/// (<c>AddD2SealedEncryptionViaKeyCustodian</c>) and its in-process sealer-only twin: a keyed
/// sealer per DISTINCT generated consumer service, the private-key opener IFF the registering
/// service is itself a sealed consumer, KeyCustodian provenance markers, the
/// populated sealed startup self-check, and the round-trip proving the wired instances work.
/// </summary>
public sealed class SealingRegistrationTests
{
    // The three generated sealed-domain consumer services (spec-derived).
    private static readonly string[] sr_consumers = ["audit", "notifications", "courier"];

    [Fact]
    public void ViaKeyCustodian_WiresAKeyedSealerPerDistinctConsumerService()
    {
        var sp = BuildViaProvider(ownServiceId: "audit");
        var isKeyed = sp.GetRequiredService<IServiceProviderIsKeyedService>();

        foreach (var consumer in sr_consumers)
        {
            isKeyed.IsKeyedService(typeof(IPayloadSealer), consumer)
                .Should().BeTrue($"a keyed sealer is wired for consumer '{consumer}'");
        }
    }

    [Fact]
    public void ViaKeyCustodian_ResolvesAKeyedSealerPerDistinctConsumerService()
    {
        // §1.3: descriptor-presence (IsKeyedService) is not resolvability — RESOLVE every
        // generated keyed sealer from a real composition root. Resolution is lazy (the
        // sealer performs no KC fetch until first Seal), so a coherent fake ISealingClient
        // is enough and no gRPC stub is needed.
        var sp = BuildViaProvider(ownServiceId: "audit", CoherentFake());

        foreach (var consumer in sr_consumers)
        {
            var sealer = sp.GetRequiredKeyedService<IPayloadSealer>(consumer);
            sealer.Should().NotBeNull($"the keyed sealer for consumer '{consumer}' resolves");
        }
    }

    [Fact]
    public void ViaKeyCustodian_ConsumerHost_WiresOpenerForOwnService()
    {
        // own == a sealed consumer → the self-only opener IS registered under ownServiceId.
        var sp = BuildViaProvider(ownServiceId: "audit");
        var isKeyed = sp.GetRequiredService<IServiceProviderIsKeyedService>();

        isKeyed.IsKeyedService(typeof(IPayloadOpener), "audit").Should().BeTrue();
    }

    [Fact]
    public void ViaKeyCustodian_NonConsumerHost_WiresNoOpener()
    {
        // own is NOT a sealed consumer → NO opener registration at all — the DI shape is
        // structural least-privilege; a producer-only host can never open a sealed frame.
        var sp = BuildViaProvider(ownServiceId: "files");
        var isKeyed = sp.GetRequiredService<IServiceProviderIsKeyedService>();

        isKeyed.IsKeyedService(typeof(IPayloadOpener), "files").Should().BeFalse();

        // ...but it still wires sealers for every consumer (it can publish to any of them).
        foreach (var consumer in sr_consumers)
            isKeyed.IsKeyedService(typeof(IPayloadSealer), consumer).Should().BeTrue();
    }

    [Fact]
    public void FromKeyCustodian_InProcessTwin_WiresSealerArmsOnly_NeverAnOpener()
    {
        // The in-process twin registers SEALER arms only — no in-process opener source exists
        // anywhere (decrypt is CrossProcessHop-only). Even when own IS a consumer, no opener.
        var services = NewServices();
        services.AddD2SealedEncryptionFromKeyCustodian(ownServiceId: "audit", callingModuleId: "edge");
        var sp = services.BuildServiceProvider();
        var isKeyed = sp.GetRequiredService<IServiceProviderIsKeyedService>();

        foreach (var consumer in sr_consumers)
            isKeyed.IsKeyedService(typeof(IPayloadSealer), consumer).Should().BeTrue();

        isKeyed.IsKeyedService(typeof(IPayloadOpener), "audit")
            .Should().BeFalse("no in-process opener source exists");
    }

    [Fact]
    public void ViaKeyCustodian_MarksEveryWiredIdKeyCustodianProvenance()
    {
        var sp = BuildViaProvider(ownServiceId: "audit");

        foreach (var id in sr_consumers)
        {
            var markers = sp.GetKeyedServices<EncryptionSourceMarker>(id).ToArray();
            markers.Should().NotBeEmpty($"provenance is marked for '{id}'");
            markers.Should().OnlyContain(m => m.Source == EncryptionKeyringSource.KeyCustodian);
        }
    }

    [Fact]
    public void ViaKeyCustodian_ResolvedSealerAndOpener_RoundTrip()
    {
        // own == "audit" (a sealed consumer) so both a keyed sealer AND the self opener wire.
        // Pre-register a coherent fake ISealingClient (TryAdd in the call is skipped) so the
        // resolved KC-backed sealer + opener use the same fixture keypair and round-trip.
        var sp = BuildViaProvider(ownServiceId: "audit", CoherentFake());
        var sealer = sp.GetRequiredKeyedService<IPayloadSealer>("audit");
        var opener = sp.GetRequiredKeyedService<IPayloadOpener>("audit");

        var frame = sealer.Seal("round-trip"u8);
        Encoding.UTF8.GetString(opener.Open(frame)).Should().Be("round-trip");
    }

    [Fact]
    public async Task ViaKeyCustodian_SealedSelfCheck_PassesWithCoherentKeyCustodianPair()
    {
        var sp = BuildViaProvider(ownServiceId: "audit", CoherentFake());

        // The sealed startup self-check (encryption core, internal) is registered as a hosted
        // service by the single call — running it seals→opens the sentinel under the wired pair.
        var act = () => RunHostedServicesAsync(sp);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void ViaKeyCustodian_InternalBuildingBlocks_AreNotPublicSurface()
    {
        // The single call is the ONLY public method on the extensions type; the fine-grained
        // sealer/opener building blocks stay internal (the AddKeyringBackedPayloadCrypto
        // precedent). Public API tracking pins the exact surface; this is the reflection twin.
        var publicMethods = typeof(SealingServiceCollectionExtensions)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(m => m.Name.StartsWith("AddD2", StringComparison.Ordinal))
            .Select(m => m.Name)
            .ToArray();

        publicMethods.Should().OnlyContain(n => n == "AddD2SealedEncryptionViaKeyCustodian");
        publicMethods.Should().NotContain(["AddSealerViaKeyCustodian", "AddOpenerViaKeyCustodian"]);
    }

    [Fact]
    public void PromotedSealPrivateProto_CarriesTypeLevelRedact_AndMasksPrivateKeyBytes()
    {
        // The seal-private-keyring wire proto cannot carry [RedactData] in its generated
        // file; the shipping Client assembly declares it via the hand-authored partial
        // SealPrivateEntry.Redaction.cs, so a destructured capture of the entry masks the
        // raw PKCS#8 private key. Mirrors KeyringRegistrationTests' proto-attribute pin
        // (defense-in-depth: nothing logs this proto today).
        var secretBytes = new byte[48];
        for (var i = 0; i < secretBytes.Length; i++)
            secretBytes[i] = 0x7A;

        var secretBase64 = Convert.ToBase64String(secretBytes);

        // Type-level attribute pin (the hand-authored partial in the Client assembly).
        typeof(D2.Services.Protos.KeyCustodian.V2Alpha.SealPrivateEntry)
            .GetCustomAttribute<RedactDataAttribute>().Should().NotBeNull(
                "the promoted seal-private wire proto must carry a type-level "
                + "[RedactData(SecretInformation)] partial so no PKCS#8 bytes ever render");

        var protoEntry = new D2.Services.Protos.KeyCustodian.V2Alpha.SealPrivateEntry
        {
            Kid = "fixture-kid-1",
            PrivatePkcs8 = Google.Protobuf.ByteString.CopyFrom(secretBytes),
        };

        var policy = new RedactDataDestructuringPolicy();
        policy.TryDestructure(protoEntry, new ScalarPropertyValueFactory(), out var destructured)
            .Should().BeTrue();

        var rendered = destructured!.ToString();
        rendered.Should().Contain("[REDACTED: SecretInformation]");
        rendered.Should().NotContain(secretBase64, "the raw PKCS#8 private key must never render");
    }

    private static ServiceCollection NewServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    private static ServiceProvider BuildViaProvider(string ownServiceId, ISealingClient? fake = null)
    {
        var services = NewServices();

        if (fake is not null)
            services.AddSingleton(fake);

        services.AddD2SealedEncryptionViaKeyCustodian(ownServiceId);
        return services.BuildServiceProvider();
    }

    // A coherent fake: same fixture keypair on both sides, so seal (public kid1) → open
    // (private kid1) round-trips and the sealed self-check passes.
    private static FakeSealingClient CoherentFake()
        => new(
            privateResponder: _ => D2Result<RecipientPrivateKeyring>.Ok(
                SealingTestFixtures.SingleKidPrivateKeyring()),
            publicResponder: _ => D2Result<RecipientPublicKeyring>.Ok(
                SealingTestFixtures.SingleKidPublicKeyring()));

    private static async Task RunHostedServicesAsync(IServiceProvider sp)
    {
        foreach (var hosted in sp.GetServices<IHostedService>())
            await hosted.StartAsync(CancellationToken.None);
    }

    private sealed class ScalarPropertyValueFactory : ILogEventPropertyValueFactory
    {
        public LogEventPropertyValue CreatePropertyValue(object? value, bool destructureObjects)
            => new ScalarValue(value);
    }
}
