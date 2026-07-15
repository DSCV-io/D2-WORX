// -----------------------------------------------------------------------
// <copyright file="KeyCustodianKeyringRotationHotSwapIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Integration.KeyCustodian;

using System.Security.Cryptography;
using D2.Edge.Api.Grpc.KeyCustodian;
using D2.Edge.KeyCustodian.App.Application;
using D2.Edge.KeyCustodian.App.Application.CertificateAuthority;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.ActivateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GenerateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RotateKey;
using D2.Edge.KeyCustodian.App.Application.Issuance;
using D2.Edge.KeyCustodian.App.Infrastructure.Configuration;
using D2.Edge.KeyCustodian.App.Infrastructure.Messaging;
using D2.Edge.KeyCustodian.App.Infrastructure.Vault;
using D2.Edge.KeyCustodian.Client.Keyring;
using D2.Edge.KeyCustodian.Infra.Messaging.RabbitMq;
using D2.Edge.KeyCustodian.Infra.Persistence.Postgres;
using D2.Edge.Tests.Unit.KeyCustodian.Client.Keyring;
using D2.Private.Auth;
using D2.Shared.Auth.Events;
using D2.Shared.Context.Abstractions;
using D2.Shared.Encryption;
using D2.Shared.EntityFrameworkCore.Postgres;
using D2.Shared.Handler;
using D2.Shared.Handler.Repo.Postgres;
using D2.Shared.Messaging;
using D2.Shared.Messaging.RabbitMq;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.RabbitMq;
using Xunit;
using KeyringClientStub = D2.Services.Protos.KeyCustodian.V2Alpha.KeyCustodianKeyring.KeyCustodianKeyringClient;

/// <summary>
/// The full keyring rotation hot-swap, every seam REAL: the real KeyCustodian handler graph
/// over PostgreSQL serves <c>GetKeyring</c> through the generated gRPC service on an
/// in-memory <see cref="TestServer"/>; <see cref="GrpcKeyringClient"/> dials it over the
/// wire; the real <c>RotateKeyHandler</c> commits a rotation and its post-commit
/// <see cref="RabbitMqKeyRotationAnnouncer"/> publishes the real <see cref="KeyRotatedEvent"/>
/// over a real RabbitMQ broker; the real <see cref="KeyringRefreshSubscriber"/> +
/// <see cref="RabbitMqRotationEventChannel"/> drive <see cref="KeyringBackedPayloadCrypto"/>
/// to refetch over the same gRPC path and atomically swap to the new active kid ΓÇö while a
/// frame encrypted before the rotation still decrypts (the retiring-key overlap guarantee).
/// Only the transport socket + peer certificate are in-memory (the committed replace trigger
/// is the live Edge host's mutual-TLS channel).
/// </summary>
[Trait("Category", "Integration")]
[Collection(KeyCustodianPostgresCollectionDefinition.NAME)]
public sealed class KeyCustodianKeyringRotationHotSwapIntegrationTests(
    KeyCustodianPostgresFixture fixture) : IDisposable
{
    // audit is now a SEALED domain (removed from the KC symmetric payload catalog);
    // exercise the preserved symmetric machinery on a registered fixture payload
    // domain. The field-initializer registration runs before any per-test host boot (so the
    // keyring-authority boot validator accepts the grant on the fixture domain); Dispose
    // unregisters (ref-counted, safe under the sequential postgres collection).
    private const string _DOMAIN = "payload-fixture-a";
    private const string _CALLER = "edge";
    private const string _WARM_UP_KID = "consumer-ready-probe";

    // Genuine-stuck guard for the async waits below: a poll-ATTEMPT budget, never a
    // wall-clock deadline. Each iteration awaits the delay interval, so under load the
    // effective wait GROWS with the slowdown instead of expiring. 2400 attempts is a
    // multi-minute ceiling â€” far above any healthy broker / keyring round-trip, yet a
    // permanently-stuck test still terminates (no xunit.runner.json / test timeout here).
    private const int _POLL_ATTEMPT_BUDGET = 2400;

    private readonly IDisposable r_fixtureSeam =
        KeyDomain.RegisterFixturePayloadDomainForTesting(_DOMAIN);

    /// <summary>Unregisters the fixture payload domain (ref-counted, per-test-instance).</summary>
    public void Dispose() => r_fixtureSeam.Dispose();

    [Fact]
    public async Task Rotation_RealHandlerToRealBroker_SwapsWrapperToNewActiveKid_AndDecryptsOldFrame()
    {
        await fixture.EnsureMigratedAsync();
        await CleanDomainAsync();

        await using var broker = new RabbitMqBuilder()
            .WithImage("rabbitmq:3.13-management-alpine")
            .Build();
        await broker.StartAsync();

        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0));

        using var host = BuildHost(clock, broker.GetConnectionString(), fixture.ConnectionString);
        await host.StartAsync();

        try
        {
            // Seed + activate the first payload key through the real System-plane handlers.
            var firstKid = await SeedActiveKeyAsync(host.Services, clock);

            // Prove the broker consumer is live before rotating: a warm-up event must reach
            // the rotation channel (fanout queues bind when the consumer starts; an event
            // published earlier would be dropped, making the real rotation racy).
            await WaitForConsumerAsync(host.Services);

            // The wrapper, backed by the REAL gRPC wire path into the REAL handler graph.
            var httpClient = host.GetTestServer().CreateClient();
            using var channel = GrpcChannel.ForAddress(
                httpClient.BaseAddress!, new GrpcChannelOptions { HttpClient = httpClient });
            var grpcClient = new GrpcKeyringClient(new KeyringClientStub(channel));
            var rotationChannel = host.Services.GetRequiredService<RabbitMqRotationEventChannel>();

            await using var crypto = KeyringBackedPayloadCrypto.CreateForTesting(
                _DOMAIN,
                grpcClient,
                rotationChannel,
                NullLogger<KeyringBackedPayloadCrypto>.Instance,
                TimeSpan.FromSeconds(30),
                3,
                TimeSpan.FromMilliseconds(200));

            var oldFrame = crypto.Encrypt("pre-rotation-audit-event"u8);
            KeyringTestFixtures.ReadFrameKid(oldFrame).Should().Be(firstKid);

            // Rotate through the REAL handler â€” its post-commit announce publishes the real
            // KeyRotatedEvent via the real RabbitMqKeyRotationAnnouncer over the real broker.
            var secondKid = await RotateAsync(host.Services, clock);
            secondKid.Should().NotBe(firstKid);

            // The event flows broker â†’ subscriber â†’ channel â†’ wrapper refetch (real gRPC) â†’ swap.
            await WaitUntilAsync(
                () => KeyringTestFixtures.ReadFrameKid(crypto.Encrypt("probe"u8)) == secondKid,
                "the rotation event should hot-swap the wrapper to the new active kid");

            KeyringTestFixtures.ReadFrameKid(crypto.Encrypt("post"u8)).Should().Be(secondKid);

            // Overlap guarantee: the pre-rotation frame still decrypts via the swapped keyring.
            crypto.Decrypt(oldFrame).Should().Equal(
                "pre-rotation-audit-event"u8.ToArray(),
                "the retiring key remains served for decryption during the overlap window");
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static IHost BuildHost(
        TestClock clock, string brokerConnectionString, string pgConnectionString)
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

                    // Every DI scope starts as an established cross-process Edge caller
                    // carrying the keyring scope (the live host's interceptors do this);
                    // the System-plane arms overwrite Origin on their own scope.
                    services.AddScoped(_ => new MutableRequestContext
                    {
                        Origin = RequestOrigin.CrossProcessHop,
                        ImmediateCaller = _CALLER,
                        Scopes = new HashSet<string>(StringComparer.Ordinal)
                        {
                            ProductScopes.Internal.Kc.Keyring,
                        },
                    });
                    services.AddScoped<IRequestContext>(
                        sp => sp.GetRequiredService<MutableRequestContext>());

                    services.AddSingleton<IClock>(clock);
                    services.AddSingleton(Options.Create(new SigningDomainAuthorityOptions()));

                    var keyringAuthority = new KeyringDomainAuthorityOptions();
                    keyringAuthority.AllowedKeyringDomainsByWorkload[_CALLER] = [_DOMAIN];
                    services.AddSingleton(Options.Create(keyringAuthority));

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

                    // The dedicated Â§9.44 root-signing capability â€” the lifecycle-mutation
                    // handlers driven by this hot-swap flow (generate / activate / rotate)
                    // take it; registered from its own composition seam.
                    services.AddD2CaRootSigningCapability();

                    // The REAL post-commit announcer over the REAL broker.
                    services.AddSingleton<IKeyRotationAnnouncer, RabbitMqKeyRotationAnnouncer>();
                    services.AddD2MessagingRabbitMq(
                        configureConnection: o => o.ConnectionUri = brokerConnectionString);

                    // The consumer side under test: the real subscriber + rotation channel.
                    services.AddSingleton<RabbitMqRotationEventChannel>();
                    services.AddSingleton<IRotationEventChannel>(
                        sp => sp.GetRequiredService<RabbitMqRotationEventChannel>());
                    services.AddD2Subscriber<KeyringRefreshSubscriber, KeyRotatedEvent>(
                        MqSubscriptionsRegistry.ByConstant[MqSubscriptions.KeyringRefresh]);
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                        endpoints.MapGrpcService<KeyCustodianKeyringService>());
                });
            })
            .Build();

        return host;
    }

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

    // Publishes warm-up rotation events for the domain until one lands on the channel â€”
    // deterministic proof the broker consumer is consuming before the real rotation fires.
    private static async Task WaitForConsumerAsync(IServiceProvider services)
    {
        var rotationChannel = services.GetRequiredService<RabbitMqRotationEventChannel>();
        var landed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var probe = rotationChannel.Subscribe(_DOMAIN, _ =>
        {
            landed.TrySetResult();
            return Task.CompletedTask;
        });

        for (var attempt = 0; attempt < _POLL_ATTEMPT_BUDGET; attempt++)
        {
            if (landed.Task.IsCompleted) return;

            await using (var scope = services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope())
            {
                var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
                await bus.PublishAsync(new KeyRotatedEvent
                {
                    Domain = _DOMAIN,
                    Kid = _WARM_UP_KID,
                    NewStatus = "Active",
                });
            }

            await Task.WhenAny(landed.Task, Task.Delay(500));
        }

        if (!landed.Task.IsCompleted)
            throw new TimeoutException("The rotation-event consumer never became ready.");
    }

    private static async Task RunSystemAsync(IServiceProvider services, Func<IServiceProvider, Task> body)
    {
        await using var scope = services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MutableRequestContext>();
        context.Origin = RequestOrigin.System;
        context.ImmediateCaller = null;

        await body(scope.ServiceProvider);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string because)
    {
        for (var attempt = 0; attempt < _POLL_ATTEMPT_BUDGET; attempt++)
        {
            if (condition())
                return;

            await Task.Delay(250);
        }

        condition().Should().BeTrue(because);
    }

    private async Task<string> SeedActiveKeyAsync(IServiceProvider services, TestClock clock)
    {
        await RunSystemAsync(services, async sp =>
            (await sp.GetRequiredService<IGenerateKeyHandler>().HandleAsync(
                new GenerateKeyInput(_DOMAIN, KeyType.AesPayload), CancellationToken.None))
                .Success.Should().BeTrue());

        var pendingKid = await SingleKidAsync(KeyStatus.Pending);
        clock.Advance(Duration.FromHours(2));

        await RunSystemAsync(services, async sp =>
            (await sp.GetRequiredService<IActivateKeyHandler>().HandleAsync(
                new ActivateKeyInput(pendingKid), CancellationToken.None))
                .Success.Should().BeTrue());

        return pendingKid;
    }

    private async Task<string> RotateAsync(IServiceProvider services, TestClock clock)
    {
        await RunSystemAsync(services, async sp =>
            (await sp.GetRequiredService<IGenerateKeyHandler>().HandleAsync(
                new GenerateKeyInput(_DOMAIN, KeyType.AesPayload), CancellationToken.None))
                .Success.Should().BeTrue());

        clock.Advance(Duration.FromDays(31));

        await RunSystemAsync(services, async sp =>
            (await sp.GetRequiredService<IRotateKeyHandler>().HandleAsync(
                new RotateKeyInput(_DOMAIN), CancellationToken.None))
                .Success.Should().BeTrue());

        return await SingleKidAsync(KeyStatus.Active);
    }

    private async Task CleanDomainAsync()
    {
        await using var ctx = fixture.NewContext();

        // Audit rows FK-reference key_record with RESTRICT (same-transaction audit
        // writes are never orphaned), so the domain's audit children go first. The
        // kid list is materialized so no query lambda captures the disposable ctx.
        var kids = await ctx.Keys
            .Where(k => k.KeyDomain == _DOMAIN)
            .Select(k => k.Kid)
            .ToListAsync();

        await ctx.Audit.Where(a => kids.Contains(a.Kid)).ExecuteDeleteAsync();

        await ctx.Keys.Where(k => k.KeyDomain == _DOMAIN).ExecuteDeleteAsync();
    }

    private async Task<string> SingleKidAsync(KeyStatus status)
    {
        await using var context = fixture.NewContext();

        return await context.Keys.AsNoTracking()
            .Where(k => k.KeyDomain == _DOMAIN && k.Status == status)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => k.Kid)
            .FirstAsync();
    }
}
