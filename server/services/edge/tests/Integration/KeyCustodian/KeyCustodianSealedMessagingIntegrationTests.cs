// -----------------------------------------------------------------------
// <copyright file="KeyCustodianSealedMessagingIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Integration.KeyCustodian;

using System.Security.Cryptography;
using D2.Edge.Api.Grpc.KeyCustodian;
using D2.Edge.KeyCustodian.App.Application;
using D2.Edge.KeyCustodian.App.Application.CertificateAuthority;
using D2.Edge.KeyCustodian.App.Application.Issuance;
using D2.Edge.KeyCustodian.App.Infrastructure.Configuration;
using D2.Edge.KeyCustodian.App.Infrastructure.Messaging;
using D2.Edge.KeyCustodian.App.Infrastructure.Vault;
using D2.Edge.KeyCustodian.Client.Keyring;
using D2.Edge.KeyCustodian.Client.Sealing;
using D2.Edge.KeyCustodian.Infra.Persistence.Postgres;
using D2.Edge.Tests.Unit.KeyCustodian.App.Fixtures;
using D2.Shared.Auth.Events;
using D2.Shared.Context.Abstractions;
using D2.Shared.EntityFrameworkCore.Postgres;
using D2.Shared.Handler;
using D2.Shared.Handler.Repo.Postgres;
using D2.Shared.Messaging;
using D2.Shared.Messaging.RabbitMq;
using D2.Shared.Messaging.RabbitMq.Encryption;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.RabbitMq;
using OwnSealPrivateKeyStub =
    D2.Services.Protos.KeyCustodian.V2Alpha.KeyCustodianOwnSealPrivateKey.KeyCustodianOwnSealPrivateKeyClient;
using SealPublicKeyStub =
    D2.Services.Protos.KeyCustodian.V2Alpha.KeyCustodianSealPublicKey.KeyCustodianSealPublicKeyClient;

/// <summary>
/// THE HEADLINE sealed-messaging isolation proof, end-to-end over real infrastructure: a
/// PRODUCER host seals a fixture message on the REAL sealed domain (<c>audit</c>) through the
/// REAL bus path (<c>RabbitMqMessageBus</c> → Testcontainer RabbitMQ); the CONSUMER host —
/// whose sealer/opener keys come from the REAL KeyCustodian seal ops served over
/// loopback gRPC + Testcontainer PostgreSQL (lazy provisioning included) — consumes and OPENS
/// it (payload equality). The capability split is proven at the messaging layer: the producer
/// host structurally holds NO opener registration (it cannot decompose any sealed delivery,
/// including its own output), and a host that subscribes to the sealed domain WITHOUT calling
/// <c>AddD2SealedEncryptionViaKeyCustodian</c> crashes at boot BEFORE any consumer channel
/// opens (the forgotten-call net).
/// </summary>
/// <remarks>
/// The KC host's scoped request context (CrossProcessHop, caller <c>audit</c>, seal scopes)
/// is the faithful stand-in for the interceptor-established mTLS peer identity — the loopback
/// TestServer has no live mutual-TLS channel. Replace-trigger: the live Edge-host wiring.
/// </remarks>
[Trait("Category", "Integration")]
[Collection(KeyCustodianPostgresCollectionDefinition.NAME)]
public sealed class KeyCustodianSealedMessagingIntegrationTests(
    KeyCustodianPostgresFixture fixture)
{
    private const string _CONSUMER_SERVICE = "audit";
    private const string _PRODUCER_SERVICE = "sealed-producer-fixture";
    private const string _EXCHANGE = "d2.test.sealed-messaging-fixture";
    private const string _QUEUE = "d2.test.sealed-messaging-fixture-consumer";

    // Warm-up rotation domain/kid for the consumer-ready probe (never a real seal domain — a
    // §7.23 fixture marker in the value). A KeyRotatedEvent on this domain that round-trips
    // through the host's own KeyringRefreshSubscriber → RabbitMqRotationEventChannel proves
    // the consumer host has finished declaring ALL its topology and is consuming.
    private const string _WARM_UP_DOMAIN = "sealed-messaging-consumer-ready-probe";
    private const string _WARM_UP_KID = "consumer-ready-probe";

    // Genuine-stuck guard for WaitForConsumerReadyAsync: a poll-ATTEMPT budget,
    // never a wall-clock deadline. Each iteration awaits the delay interval, so
    // under load the effective wait GROWS instead of expiring. 2400 attempts is a
    // multi-minute ceiling — far above any healthy broker round-trip, yet a
    // permanently-stuck test still terminates.
    private const int _POLL_ATTEMPT_BUDGET = 2400;

    private static readonly TimeSpan sr_overall = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task
        SealedPublish_RealBusRealKeys_ConsumerOpens_ProducerCannot_ForgottenCallCrashes()
    {
        await fixture.EnsureMigratedAsync();
        await CleanSealDomainsAsync();
        RegisterSealedFixtureMessage();

        await using var broker = new RabbitMqBuilder()
            .WithImage("rabbitmq:3.13-management-alpine")
            .Build();
        await broker.StartAsync();

        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0));

        // The KC host serving the REAL seal ops (lazy provisioning over real PG) via loopback.
        using var kcHost = BuildKeyCustodianHost(clock, fixture.ConnectionString);
        await kcHost.StartAsync();

        var httpClient = kcHost.GetTestServer().CreateClient();
        using var channel = GrpcChannel.ForAddress(
            httpClient.BaseAddress!, new GrpcChannelOptions { HttpClient = httpClient });

        try
        {
            // The messaging hosts run SEQUENTIALLY (never two at once): the rotation-refresh
            // subscriber's exclusive per-process queue is shared by every messaging host in
            // this test PROCESS, so concurrent hosts would contend on it — a test-environment
            // artifact (production runs one host per process). The sequencing also proves
            // durable-queue delivery across a consumer cold boot.
            const string content = "sealed-fixture-payload: pii-looking-content 42";
            var recorder = new SealedDeliveryFixtureRecorder();

            // 1) CONSUMER host boots FIRST: own id = the sealed domain's consumer service →
            //    the single call wires its private-key opener (fail-loud boot fetch through
            //    the REAL own-private op, which lazily provisions seal:audit over real PG) +
            //    sealers for every sealed consumer; its sealed startup self-check round-trips
            //    a sentinel through the REAL fetched keys; topology (durable queue) declared.
            using (var consumerBoot = BuildConsumerHost(broker, channel, recorder))
            {
                await consumerBoot.StartAsync();

                // The durable fixture topology (exchange + queue + binding) is declared by the
                // consumer host's ConsumerHostedService in the SAME pass that starts its
                // subscribers, but that declaration runs on a BACKGROUND task (StartAsync does
                // not block on it). Stopping the host immediately would cancel that in-flight
                // declaration under load, leaving the producer's step-2 publish targeting a
                // not-yet-declared exchange (a 404 → AlreadyClosed → ServiceUnavailable). Wait
                // for the consumer to be genuinely ready first — a warm-up rotation event that
                // round-trips proves DeclareAsync completed AND the consumer is attached.
                await WaitForConsumerReadyAsync(consumerBoot.Services);

                await consumerBoot.StopAsync();
            }

            // 2) PRODUCER host: NOT a sealed consumer → sealers only, structurally NO opener.
            using (var producerHost = BuildMessagingHost(
                broker.GetConnectionString(),
                channel,
                services => services.AddD2SealedEncryptionViaKeyCustodian(_PRODUCER_SERVICE)))
            {
                await producerHost.StartAsync();

                try
                {
                    // Structural capability split at the messaging layer: the producer host
                    // holds NO opener registration — for the consumer service or itself.
                    var producerKeyed = producerHost.Services
                        .GetRequiredService<IServiceProviderIsKeyedService>();
                    producerKeyed.IsKeyedService(typeof(IPayloadOpener), _CONSUMER_SERVICE)
                        .Should().BeFalse("a producer can never open the consumer's sealed frames");
                    producerKeyed.IsKeyedService(typeof(IPayloadOpener), _PRODUCER_SERVICE)
                        .Should().BeFalse("a non-consumer host gets no opener at all");

                    // The REAL sealed publish through the real bus into the durable queue.
                    await using var scope = producerHost.Services
                        .GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
                    var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
                    var published = await bus.PublishAsync(
                        new SealedMessagingFixtureEvent { Content = content });
                    published.Success.Should().BeTrue(
                        "the sealed publish must succeed through the real bus");
                }
                finally
                {
                    await producerHost.StopAsync();
                }
            }

            // 3) CONSUMER host cold-boots again and consumes the queued sealed delivery.
            //    Payload equality at the consumer: the delivery could only have reached the
            //    handler through the opener (a sealed-domain descriptor decomposes ONLY via
            //    the keyed IPayloadOpener; the opener accepts ONLY v2 sealed frames, so a
            //    plaintext or v1 body on the wire could never have arrived here).
            using (var consumerHost = BuildConsumerHost(broker, channel, recorder))
            {
                await consumerHost.StartAsync();

                try
                {
                    var received = await recorder.Received.WaitAsync(sr_overall);
                    received.Should().Be(content);
                }
                finally
                {
                    await consumerHost.StopAsync();
                }
            }

            // FORGOTTEN-CALL net, end-to-end: a host subscribing to the sealed domain WITHOUT
            // AddD2SealedEncryptionViaKeyCustodian must crash at boot, BEFORE any consumer
            // channel opens — the SealedConsumerStartupCheck is registered by
            // AddD2MessagingRabbitMq itself, so the forgotten call cannot disable its own net.
            using var forgottenHost = BuildMessagingHost(
                broker.GetConnectionString(),
                channel,
                services =>
                {
                    services.AddSingleton(new SealedDeliveryFixtureRecorder());
                    services.AddD2Subscriber<
                        SealedMessagingFixtureSubscriber, SealedMessagingFixtureEvent>(
                        FixtureSubscription());
                });

            // ReSharper disable AccessToDisposedClosure -- the boot lambda is awaited to
            // completion below, before the using var disposes forgottenHost; R# cannot
            // prove the ordering statically.
            var boot = async () => await forgottenHost.StartAsync();

            // ReSharper restore AccessToDisposedClosure
            (await boot.Should().ThrowAsync<InvalidOperationException>())
                .WithMessage("*AddD2SealedEncryptionViaKeyCustodian*");
        }
        finally
        {
            await kcHost.StopAsync();
        }
    }

    // The KC leaf host: real app graph + real PG; the scoped request context is the faithful
    // stand-in for the interceptor-established mTLS peer (caller = the consumer service, so
    // the targetless own-private op selects seal:audit — exactly the peer-selection shape).
    private static IHost BuildKeyCustodianHost(TestClock clock, string pgConnectionString)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddLogging();
                    services.AddD2Handler();
                    services.AddRouting();
                    services.AddGrpc();

                    services.AddScoped(_ => new MutableRequestContext
                    {
                        Origin = RequestOrigin.CrossProcessHop,
                        ImmediateCaller = _CONSUMER_SERVICE,
                        Scopes = new HashSet<string>(StringComparer.Ordinal)
                        {
                            Scopes.Internal.Kc.Seal.Encrypt,
                            Scopes.Internal.Kc.Seal.Open,
                        },
                    });
                    services.AddScoped<IRequestContext>(
                        sp => sp.GetRequiredService<MutableRequestContext>());

                    services.AddSingleton<IClock>(clock);
                    services.AddSingleton(Options.Create(new SigningDomainAuthorityOptions()));
                    services.AddSingleton(Options.Create(new KeyringDomainAuthorityOptions()));

                    services.AddDbContext<KeyCustodianDbContext>(opts =>
                        opts.ApplyD2NpgsqlDefaults(
                            pgConnectionString,
                            commandTimeoutSeconds: 30,
                            migrationsAssemblyName:
                                typeof(KeyCustodianDbContext).Assembly.GetName().Name!));
                    services.AddScoped<IKeyCustodianDbContext>(
                        sp => sp.GetRequiredService<KeyCustodianDbContext>());
                    services.AddD2Postgres();

                    services.AddKeyedSingleton(
                        KeyCustodianRootKey.ROOT_SERVICE_KEY, (_, _) => BuildRootCrypto());
                    services.AddSingleton(Options.Create(BuildOptions()));
                    services.AddD2KeyCustodianApp();
                    services.AddD2CaLeafSigningCapability();
                    services.AddD2CaRootSigningCapability();

                    // The seal ops' activation announce is not under test — recording fake.
                    services.AddSingleton<IKeyRotationAnnouncer>(new RecordingAnnouncer());
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGrpcService<KeyCustodianSealPublicKeyService>();
                        endpoints.MapGrpcService<KeyCustodianOwnSealPrivateKeyService>();
                    });
                });
            })
            .Build();

        return host;
    }

    // The consumer host: the single sealed registration call under its OWN consumer-service
    // id + the fixture subscriber capturing into the shared recorder.
    private static IHost BuildConsumerHost(
        RabbitMqContainer broker, GrpcChannel kcChannel, SealedDeliveryFixtureRecorder recorder)
        => BuildMessagingHost(
            broker.GetConnectionString(),
            kcChannel,
            services =>
            {
                services.AddSingleton(recorder);
                services.AddD2SealedEncryptionViaKeyCustodian(_CONSUMER_SERVICE);
                services.AddD2Subscriber<
                    SealedMessagingFixtureSubscriber, SealedMessagingFixtureEvent>(
                    FixtureSubscription());
            });

    // A messaging host over the real broker with the two seal gRPC stubs dialing the KC
    // loopback channel (the host-provided-stub production shape).
    private static IHost BuildMessagingHost(
        string brokerConnectionString,
        GrpcChannel kcChannel,
        Action<IServiceCollection> configure)
        => new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddD2Handler();
                services.AddSingleton(new SealPublicKeyStub(kcChannel));
                services.AddSingleton(new OwnSealPrivateKeyStub(kcChannel));
                services.AddD2MessagingRabbitMq(
                    configureConnection: o => o.ConnectionUri = brokerConnectionString);
                configure(services);
            })
            .Build();

    // Publishes warm-up KeyRotatedEvents on the probe domain until one round-trips through the
    // host's own KeyringRefreshSubscriber → RabbitMqRotationEventChannel — deterministic proof
    // that the consumer host declared ALL its topology (a fanout binds only once the consumer
    // attaches) and is consuming. The loop tolerates the early publishes that fail while the
    // exchange is still being declared. Mirrors the rotation hot-swap test's readiness gate.
    private static async Task WaitForConsumerReadyAsync(IServiceProvider services)
    {
        var rotationChannel = services.GetRequiredService<RabbitMqRotationEventChannel>();
        var landed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var probe = rotationChannel.Subscribe(_WARM_UP_DOMAIN, _ =>
        {
            landed.TrySetResult();
            return Task.CompletedTask;
        });

        for (var attempt = 0; attempt < _POLL_ATTEMPT_BUDGET; attempt++)
        {
            if (landed.Task.IsCompleted) return;

            await using (var scope = services
                .GetRequiredService<IServiceScopeFactory>().CreateAsyncScope())
            {
                var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
                await bus.PublishAsync(new KeyRotatedEvent
                {
                    Domain = _WARM_UP_DOMAIN,
                    Kid = _WARM_UP_KID,
                    NewStatus = "Active",
                });
            }

            await Task.WhenAny(landed.Task, Task.Delay(500));
        }

        if (!landed.Task.IsCompleted)
            throw new TimeoutException("The sealed consumer host never became ready.");
    }

    private static void RegisterSealedFixtureMessage()
        => MessageWireResolver.RegisterForTesting(
            typeof(SealedMessagingFixtureEvent),
            new MqMessageDescriptor(
                Constant: "SealedMessagingFixture",
                MessageTypeName: typeof(SealedMessagingFixtureEvent).FullName!,
                Exchange: _EXCHANGE,
                ExchangeType: "fanout",
                Encryption: EncryptionDomains.AUDIT,
                EncryptionReason: null,
                DefaultRoutingKey: string.Empty));

    private static MqSubscriptionDescriptor FixtureSubscription() => new(
        Constant: "SealedMessagingFixtureSub",
        MessageTypeName: typeof(SealedMessagingFixtureEvent).FullName!,
        QueueName: _QUEUE,
        Pattern: QueuePattern.CompetingConsumer,
        RoutingKeyBinding: string.Empty,
        Prefetch: 10,
        Idempotency: false,
        TieredRetry: null);

    private static IPayloadCrypto BuildRootCrypto()
    {
        var key = RandomNumberGenerator.GetBytes(PayloadCryptoKeyring.KEY_SIZE_BYTES);
        var keyring = new PayloadCryptoKeyring(
            "root",
            new Dictionary<string, byte[]> { ["root"] = key },
            "keycustodian-root"u8.ToArray());

        return new PayloadCrypto(keyring);
    }

    private static KeyCustodianOptions BuildOptions() => new()
    {
        RsaKeySizeBits = 2048,
        SecretLengthBytes = 64,
        Default = new RotationPolicyOptions
        {
            Cadence = TimeSpan.FromDays(30),
            Grace = TimeSpan.FromDays(7),
            SmokeSoak = TimeSpan.FromHours(1),
        },
    };

    // The seal keypairs are wrapped under THIS run's random root key — stale rows from a
    // previous run (wrapped under a different root) would fail the unwrap, so every seal
    // domain this test lazily provisions is wiped first (audit children before keys — the
    // FK is RESTRICT).
    private async Task CleanSealDomainsAsync()
    {
        string[] sealDomains =
            ["seal:audit", "seal:notifications", "seal:courier", "seal:" + _PRODUCER_SERVICE];

        await using var ctx = fixture.NewContext();

        var kids = await ctx.Keys
            .Where(k => sealDomains.Contains(k.KeyDomain))
            .Select(k => k.Kid)
            .ToListAsync();

        await ctx.Audit.Where(a => kids.Contains(a.Kid)).ExecuteDeleteAsync();

        await ctx.Keys.Where(k => sealDomains.Contains(k.KeyDomain)).ExecuteDeleteAsync();
    }
}
