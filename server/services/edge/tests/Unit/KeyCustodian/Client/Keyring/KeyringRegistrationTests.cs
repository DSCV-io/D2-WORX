// -----------------------------------------------------------------------
// <copyright file="KeyringRegistrationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Client.Keyring;

using System.Reflection;
using AwesomeAssertions;
using D2.Edge.KeyCustodian.App.Application.Keyring;
using D2.Edge.KeyCustodian.Client.Keyring;
using D2.Shared.Context.Abstractions;
using D2.Shared.Encryption;
using D2.Shared.Handler;
using D2.Shared.Logging.Destructuring;
using D2.Shared.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;
using KeyringStub = D2.Services.Protos.KeyCustodian.V2Alpha.KeyCustodianKeyring.KeyCustodianKeyringClient;

/// <summary>
/// DI-resolvability (§1.3), least-privilege visibility, provenance-marking, and
/// secret-redaction coverage for the keyring registration sources.
/// </summary>
public sealed class KeyringRegistrationTests
{
    private const string _DOMAIN = KeyringTestFixtures.FIXTURE_DOMAIN;

    [Fact]
    public async Task AddD2EncryptionFromKeyCustodian_ResolvesEverySeam()
    {
        await using var provider = BuildProvider(
            services => services.AddD2EncryptionFromKeyCustodian(_DOMAIN, "edge"));

        // Keyed capability + fetch seam.
        provider.GetRequiredKeyedService<IPayloadCrypto>(_DOMAIN).Should().NotBeNull();
        provider.GetRequiredKeyedService<IKeyringClient>(_DOMAIN).Should().NotBeNull();

        // Rotation channel (both the interface and the concrete singleton resolve).
        provider.GetRequiredService<IRotationEventChannel>().Should().NotBeNull();
        provider.GetRequiredService<RabbitMqRotationEventChannel>().Should().NotBeNull();

        // Encryption registry + provenance marker.
        provider.GetRequiredService<EncryptionRegistry>().Should().NotBeNull();
        provider.GetRequiredKeyedService<EncryptionSourceMarker>(_DOMAIN).Should().NotBeNull();

        // The [MqSub] subscriber + its subscription registration.
        provider.GetRequiredService<ISubscriberRegistration>().Should().NotBeNull();
        provider.GetRequiredService<KeyringRefreshSubscriber>().Should().NotBeNull();

        // The deny-by-default source-check hosted service is registered.
        provider.GetServices<IHostedService>()
            .Any(h => h.GetType().Name == "EncryptionSourceStartupCheck")
            .Should().BeTrue();
    }

    [Fact]
    public async Task AddD2EncryptionForViaKeyring_ResolvesKeyedCrypto()
    {
        await using var provider = BuildProvider(
            services => services.AddD2EncryptionForViaKeyring(_DOMAIN));

        provider.GetRequiredKeyedService<IPayloadCrypto>(_DOMAIN).Should().NotBeNull();
        provider.GetRequiredKeyedService<IKeyringClient>(_DOMAIN).Should().NotBeNull();
    }

    [Fact]
    public async Task AddD2EncryptionForViaKeyring_RealGrpcChain_ResolvesAndBoots()
    {
        // Resolve the REAL via-keyring chain — GrpcKeyringClient over a host-provided stub —
        // WITHOUT overriding IKeyringClient. A forgotten stub registration would surface here
        // as a boot-time resolution failure (the previously-unresolved real §1.3 seam).
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2Handler();
        services.AddSingleton<IRequestContext>(new MutableRequestContext());
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment("Development"));
        services.AddSingleton(new KeyringStub(
            FakeKeyringCallInvoker.Returns(KeyringTestFixtures.Reply(
                D2Result.Ok(), KeyringTestFixtures.WellFormedOutput()))));
        services.AddD2EncryptionForViaKeyring(_DOMAIN);

        await using var provider = services.BuildServiceProvider();

        // Resolving the keyed crypto runs the real GrpcKeyringClient factory + the blocking
        // boot fetch through the fake call invoker.
        provider.GetRequiredKeyedService<IPayloadCrypto>(_DOMAIN).Should().NotBeNull();
        provider.GetRequiredKeyedService<IKeyringClient>(_DOMAIN)
            .Should().BeOfType<GrpcKeyringClient>();
    }

    [Fact]
    public async Task Registrations_MarkKeyCustodianSource_StartupCheckPassesInProductionEnv()
    {
        // BOTH registration sources, one host: each marks its domain KeyCustodian, so the
        // deny-by-default guard passes for every registered domain even outside dev.
        const string via_keyring_domain = "fixture-via-keyring-domain";

        await using var provider = BuildProvider(
            services =>
            {
                services.AddD2EncryptionFromKeyCustodian(_DOMAIN, "edge");
                services.AddD2EncryptionForViaKeyring(via_keyring_domain);
            },
            environment: "Production",
            extraFakeClientDomain: via_keyring_domain);

        var check = provider.GetServices<IHostedService>()
            .Single(h => h.GetType().Name == "EncryptionSourceStartupCheck");

        var act = async () => await check.StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ResolveKeyedCrypto_UnwiredDomain_Throws()
    {
        await using var provider = BuildProvider(
            services => services.AddD2EncryptionFromKeyCustodian(_DOMAIN, "edge"));

        // ReSharper disable once AccessToDisposedClosure -- act is invoked
        // synchronously inside Should().Throw(), before provider disposes.
        var act = () => provider.GetRequiredKeyedService<IPayloadCrypto>("never-wired-domain");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void KeyringFetchSeam_IsNotPublic()
    {
        typeof(IKeyringClient).IsPublic.Should().BeFalse();
        typeof(GrpcKeyringClient).IsPublic.Should().BeFalse();
        typeof(InProcessKeyringClient).IsPublic.Should().BeFalse();
    }

    [Fact]
    public void LeafKeyringDto_KeyBytes_AreRedactedSecretInformation()
    {
        var property = typeof(KeyringEntry).GetProperty(nameof(KeyringEntry.KeyBytes));

        property.Should().NotBeNull();
        var attribute = property.GetCustomAttribute<RedactDataAttribute>();
        attribute.Should().NotBeNull();
        attribute.Reason.Should().Be(RedactReason.SecretInformation);
    }

    [Fact]
    public void LeafKeyringDto_DestructuredEntry_MasksKeyBytes()
    {
        var secretBytes = new byte[PayloadCryptoKeyring.KEY_SIZE_BYTES];
        for (var i = 0; i < secretBytes.Length; i++)
            secretBytes[i] = 0x7A;
        var entry = new KeyringEntry("kid", secretBytes);

        var policy = new RedactDataDestructuringPolicy();
        policy.TryDestructure(entry, new ScalarPropertyValueFactory(), out var result)
            .Should().BeTrue();

        var rendered = result!.ToString();
        rendered.Should().Contain("[REDACTED: SecretInformation]");
        rendered.Should().NotContain("122"); // no raw 0x7A byte value leaked
    }

    [Fact]
    public void PromotedKeyringProto_RenderedOrDestructuredLog_NeverEmitsKeyBytes()
    {
        // The wire proto cannot carry [RedactData] in its generated file; the shipping
        // Client assembly declares it via a hand-authored partial, so a destructured
        // capture of the entry — or of a whole reply, through Entries — masks everything.
        var secretBytes = new byte[PayloadCryptoKeyring.KEY_SIZE_BYTES];
        for (var i = 0; i < secretBytes.Length; i++)
            secretBytes[i] = 0x7A;
        var secretBase64 = Convert.ToBase64String(secretBytes);

        var protoEntry = new D2.Services.Protos.KeyCustodian.V2Alpha.KeyringEntry
        {
            Kid = "fixture-kid-1",
            KeyBytes = Google.Protobuf.ByteString.CopyFrom(secretBytes),
        };

        // Type-level attribute pin (the hand-authored partial in the Client assembly).
        typeof(D2.Services.Protos.KeyCustodian.V2Alpha.KeyringEntry)
            .GetCustomAttribute<RedactDataAttribute>().Should().NotBeNull();

        // A destructured capture masks the ENTIRE entry — no key bytes render.
        var policy = new RedactDataDestructuringPolicy();
        policy.TryDestructure(protoEntry, new ScalarPropertyValueFactory(), out var destructured)
            .Should().BeTrue();

        var rendered = destructured!.ToString();
        rendered.Should().Contain("[REDACTED: SecretInformation]");
        rendered.Should().NotContain(secretBase64);
        rendered.Should().NotContain("122");

        // Full-reply recursion: the promoted GetKeyringOutput carries no [RedactData] of its
        // own, so a real Serilog pipeline default-destructures it and recurses into Entries,
        // where each KeyringEntry's type-level attribute masks the raw key bytes. Pin that the
        // WHOLE reply — not just a bare entry — renders no key material through Entries.
        var fullOutput = new D2.Services.Protos.KeyCustodian.V2Alpha.GetKeyringOutput
        {
            ActiveKid = "fixture-kid-1",
            AadContext = Google.Protobuf.ByteString.CopyFrom("d2/audit"u8.ToArray()),
        };
        fullOutput.Entries.Add(protoEntry);

        var sink = new CapturingSink();
        using (var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Destructure.With<RedactDataDestructuringPolicy>()
            .WriteTo.Sink(sink, restrictedToMinimumLevel: LogEventLevel.Verbose)
            .CreateLogger())
        {
            logger.Information("keyring reply {@Reply}", fullOutput);
        }

        var replyRendered = sink.Events.Single().Properties["Reply"].ToString();
        replyRendered.Should().Contain("[REDACTED: SecretInformation]");
        replyRendered.Should().NotContain(secretBase64);
        replyRendered.Should().NotContain("122");

        // The plain-{Reply} (no @) render path is OUT OF SCOPE here: it bypasses the
        // destructuring policy by design (a logging-call-site discipline concern, not an
        // attribute gap), pinned generically by D2.Shared's SerilogPipelineRedactionTests.
    }

    [Fact]
    public void KeyringLog_Delegates_NeverTakeException()
    {
        var offenders = typeof(KeyringLog)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetParameters()
                .Any(p => typeof(Exception).IsAssignableFrom(p.ParameterType)))
            .Select(m => m.Name)
            .ToArray();

        offenders.Should().BeEmpty(
            "a [LoggerMessage] delegate must never accept an Exception (§3.1)");
    }

    private static ServiceProvider BuildProvider(
        Action<IServiceCollection> register,
        string environment = "Development",
        string? extraFakeClientDomain = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2Handler();
        services.AddSingleton<IRequestContext>(new MutableRequestContext());
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment(environment));

        register(services);

        // Override the (host-provided) fetch seam with a fake so resolving the keyed
        // IPayloadCrypto's blocking boot fetch succeeds — this is a wiring-resolvability
        // test, not a fetch-behavior test (last keyed registration wins on resolution).
        services.AddKeyedSingleton<IKeyringClient>(
            _DOMAIN,
            FakeKeyringClient.AlwaysReturns(() => KeyringTestFixtures.SingleKidKeyring()));

        if (extraFakeClientDomain is not null)
        {
            services.AddKeyedSingleton<IKeyringClient>(
                extraFakeClientDomain,
                FakeKeyringClient.AlwaysReturns(
                    () => KeyringTestFixtures.SingleKidKeyring(extraFakeClientDomain)));
        }

        return services.BuildServiceProvider();
    }

    private sealed class FakeHostEnvironment(string environment) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environment;

        public string ApplicationName { get; set; } = "keyring-tests";

        public string ContentRootPath { get; set; } = ".";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class ScalarPropertyValueFactory : ILogEventPropertyValueFactory
    {
        public LogEventPropertyValue CreatePropertyValue(object? value, bool destructureObjects)
            => new ScalarValue(value);
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
