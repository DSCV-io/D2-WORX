// -----------------------------------------------------------------------
// <copyright file="KeyCustodianSealIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Integration.KeyCustodian;

using System.Security.Cryptography;
using D2.Edge.KeyCustodian.App.Application;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GetOrLazyProvisionOwnSealPrivateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GetOrLazyProvisionSealPublicKey;
using D2.Edge.KeyCustodian.App.Infrastructure.Configuration;
using D2.Edge.KeyCustodian.App.Infrastructure.Messaging;
using D2.Edge.KeyCustodian.App.Infrastructure.Vault;
using D2.Edge.KeyCustodian.Client.Sealing;
using D2.Edge.KeyCustodian.Infra.Persistence.Postgres;
using D2.Edge.Tests.Unit.KeyCustodian.App.Fixtures;
using D2.Shared.Context.Abstractions;
using D2.Shared.Encryption;
using D2.Shared.EntityFrameworkCore.Postgres;
using D2.Shared.Handler;
using D2.Shared.Handler.Repo.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// The headline sealed-encryption live-DB test through the real seal handler graph against
/// PostgreSQL: lazy-provision a per-service ECDH keypair via the REAL
/// <see cref="GetOrLazyProvisionSealPublicKeyHandler"/> → build a producer <see cref="RecipientPublicKeyring"/>
/// from the served SPKI → <see cref="PayloadSealer"/> a payload → fetch the recipient's OWN
/// private key via the REAL <see cref="GetOrLazyProvisionOwnSealPrivateKeyHandler"/> → build a
/// <see cref="RecipientPrivateKeyring"/> → <see cref="PayloadOpener"/> the frame → assert the
/// plaintext round-trips. Plus: the one-Active provisioning race collapses to a single winner
/// every concurrent first-request converges on (no 409), and seal-encrypt is broad (a producer
/// fetches every target's public key) while seal-decrypt is structurally self-only (a producer
/// fetches only its OWN private key, never a target's). The [Collection] serializes these
/// against the shared container; unique / cleaned domains keep them isolated.
/// </summary>
[Trait("Category", "Integration")]
[Collection(KeyCustodianPostgresCollectionDefinition.NAME)]
public sealed class KeyCustodianSealIntegrationTests(KeyCustodianPostgresFixture fixture)
{
    private const string _PRODUCER = "edge";

    [Fact]
    public async Task SealRoundTrip_ProvisionPublic_Seal_FetchPrivate_Open_RecoversPlaintext()
    {
        await fixture.EnsureMigratedAsync();

        var serviceId = NewServiceId();
        await CleanSealDomainsAsync("seal:" + serviceId);

        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0));
        var rootCrypto = BuildRootCrypto();
        var plaintext = "sealed-audit-event"u8.ToArray();

        // 1. Producer fetches the target's PUBLIC seal key (lazy-provisions on first use).
        string activeKid;
        byte[] framed;
        await using (var producer = BuildProvider(clock, rootCrypto, _PRODUCER, sealEncrypt: true))
        {
            var pub = await Handler<IGetOrLazyProvisionSealPublicKeyHandler>(producer)
                .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(serviceId), CancellationToken.None);

            pub.Success.Should().BeTrue();
            activeKid = pub.Data!.ActiveKid;
            pub.Data.Entries.Should().ContainSingle();

            var publicKeysByKid = pub.Data.Entries.ToDictionary(
                e => e.Kid, e => e.PublicSpki, StringComparer.Ordinal);

            var pubKeyring = new RecipientPublicKeyring(serviceId, activeKid, publicKeysByKid);

            framed = new PayloadSealer(pubKeyring).Seal(plaintext);
        }

        // The frame is a version-2 SEALED frame (asymmetric family), not a version-1 symmetric one.
        framed[SealedFrameLayout.VERSION_OFFSET].Should().Be(
            SealedFrameLayout.CURRENT_VERSION, "the sealed frame carries the version-2 marker");

        // 2. The recipient (that SAME service, cross-process) fetches its OWN private key.
        await using (var recipient = BuildProvider(clock, rootCrypto, serviceId, sealEncrypt: false))
        {
            var priv = await Handler<IGetOrLazyProvisionOwnSealPrivateKeyHandler>(recipient)
                .HandleAsync(new GetOrLazyProvisionOwnSealPrivateKeyInput(), CancellationToken.None);

            priv.Success.Should().BeTrue();

            priv.Data!.ActiveKid.Should().Be(
                activeKid, "the private fetch reuses the already-provisioned active key");

            priv.Data.Entries.Should().ContainSingle();

            var privateKeysByKid = priv.Data.Entries.ToDictionary(
                e => e.Kid, e => e.PrivatePkcs8, StringComparer.Ordinal);

            using var privKeyring = new RecipientPrivateKeyring(serviceId, privateKeysByKid);

            var recovered = new PayloadOpener(privKeyring).Open(framed);

            recovered.Should().Equal(
                plaintext, "the sealed frame opens with the recipient's own root-unwrapped key");
        }
    }

    [Fact]
    public async Task SealProvisioningRace_ConcurrentFirstRequests_CollapseToOneWinner_NoConflict()
    {
        await fixture.EnsureMigratedAsync();

        var serviceId = NewServiceId();
        var domain = "seal:" + serviceId;
        await CleanSealDomainsAsync(domain);

        const int concurrency = 8;
        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0));
        var rootCrypto = BuildRootCrypto();

        await using var producer = BuildProvider(clock, rootCrypto, _PRODUCER, sealEncrypt: true);

        // Fire N concurrent first-requests for the SAME new service, each on its own
        // scope (fresh scoped DbContext) so the one-Active EXCLUDE absorbs the race.
        // Scopes are held until WhenAll completes — do NOT resolve via a discarded
        // CreateScope() (Handler helper) or the scoped DbContext can be finalized
        // mid-SaveChanges under GC pressure (CI flake signature: UNIQUE_VIOLATION
        // leaking past converge because the loser's context dies mid-flight).
        var scopes = Enumerable.Range(0, concurrency)
            .Select(_ => producer.CreateScope())
            .ToArray();

        try
        {
            var handlers = scopes
                .Select(s => s.ServiceProvider
                    .GetRequiredService<IGetOrLazyProvisionSealPublicKeyHandler>())
                .ToArray();

            var tasks = handlers.Select(handler => Task.Run(async () =>
                await handler.HandleAsync(
                    new GetOrLazyProvisionSealPublicKeyInput(serviceId),
                    CancellationToken.None)));

            var results = await Task.WhenAll(tasks);

            var failures = results
                .Where(r => !r.Success)
                .Select(r => $"{r.StatusCode}/{r.ErrorCode}")
                .ToArray();

            results.Should().OnlyContain(
                r => r.Success,
                "every concurrent first-request converges on the winner (no 409); failures: [{0}]",
                string.Join(", ", failures));

            results.Select(r => r.Data!.ActiveKid).Distinct().Should().ContainSingle(
                "the race collapses to ONE active key every caller serves");
        }
        finally
        {
            foreach (var scope in scopes)
                scope.Dispose();
        }

        await using var ctx = fixture.NewContext();

        var active = await ctx.Keys.AsNoTracking()
            .Where(k => k.KeyDomain == domain && k.Status == KeyStatus.Active)
            .ToListAsync();

        active.Should().ContainSingle("exactly one Active seal key survives the race");
        active[0].KeyType.Should().Be(KeyType.EcdhSealing);
    }

    [Fact]
    public async Task SealEncryptBroad_ProducerFetchesEveryTargetPublic_ButOnlyItsOwnPrivate()
    {
        await fixture.EnsureMigratedAsync();

        string[] targets = [NewServiceId(), NewServiceId(), NewServiceId()];
        await CleanSealDomainsAsync(
            targets.Select(t => "seal:" + t).Append("seal:" + _PRODUCER).ToArray());

        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0));
        var rootCrypto = BuildRootCrypto();

        // Seal-encrypt is BROAD: one producer fetches every distinct target's public seal key,
        // each provisioning its own seal:<target> domain.
        await using (var producer = BuildProvider(clock, rootCrypto, _PRODUCER, sealEncrypt: true))
        {
            var kids = new List<string>();

            foreach (var target in targets)
            {
                var pub = await Handler<IGetOrLazyProvisionSealPublicKeyHandler>(producer)
                    .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(target), CancellationToken.None);

                pub.Success.Should().BeTrue($"the producer may fetch {target}'s public seal key");
                kids.Add(pub.Data!.ActiveKid);
            }

            kids.Distinct().Should().HaveCount(
                targets.Length, "each target has its OWN independently-provisioned seal key");
        }

        // Seal-decrypt is structurally SELF-ONLY: the producer calling getOrLazyProvisionOwnSealPrivateKey
        // gets ITS OWN key (seal:edge), never any target's — the op carries no target.
        await using (var recipient = BuildProvider(clock, rootCrypto, _PRODUCER, sealEncrypt: false))
        {
            var priv = await Handler<IGetOrLazyProvisionOwnSealPrivateKeyHandler>(recipient)
                .HandleAsync(new GetOrLazyProvisionOwnSealPrivateKeyInput(), CancellationToken.None);

            priv.Success.Should().BeTrue();

            await using var ctx = fixture.NewContext();
            var ownKid = await ctx.Keys.AsNoTracking()
                .Where(k => k.KeyDomain == "seal:" + _PRODUCER && k.Status == KeyStatus.Active)
                .Select(k => k.Kid)
                .SingleAsync();

            priv.Data!.ActiveKid.Should().Be(
                ownKid, "getOrLazyProvisionOwnSealPrivateKey serves seal:edge — the caller's own domain");
        }
    }

    [Fact]
    public async Task SealProvisioning_MaxLengthServiceId_PersistsThe69CharDomain()
    {
        await fixture.EnsureMigratedAsync();

        // The longest legal workload service id is 64 chars, so the seal domain is
        // "seal:" + 64 = 69 chars. Regression pin for the key_domain column width
        // (varchar(69)) — a 64-char service id must provision, not fail the INSERT.
        var serviceId = NewMaxLengthServiceId();
        var domain = "seal:" + serviceId;
        await CleanSealDomainsAsync(domain);

        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0));
        var rootCrypto = BuildRootCrypto();

        await using var producer = BuildProvider(clock, rootCrypto, _PRODUCER, sealEncrypt: true);

        var pub = await Handler<IGetOrLazyProvisionSealPublicKeyHandler>(producer)
            .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(serviceId), CancellationToken.None);

        pub.Success.Should().BeTrue("a maximum-length service id provisions its seal keypair");

        await using var ctx = fixture.NewContext();
        var persisted = await ctx.Keys.AsNoTracking()
            .Where(k => k.KeyDomain == domain && k.Status == KeyStatus.Active)
            .SingleAsync();

        persisted.KeyDomain.Should().HaveLength(69, "seal: (5) + a 64-char service id");
        persisted.KeyType.Should().Be(KeyType.EcdhSealing);
    }

    private static string NewServiceId() => "svc" + Guid.NewGuid().ToString("N");

    // A 64-char (maximum-length) service id, unique per run in its 35-char prefix.
    private static string NewMaxLengthServiceId() =>
        (NewServiceId() + new string('x', 64))[..64];

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
            // Cadence must be >= Grace + SmokeSoak for a valid policy.
            Cadence = TimeSpan.FromDays(30),
            Grace = TimeSpan.FromDays(7),
            SmokeSoak = TimeSpan.FromHours(1),
        },
    };

    private static THandler Handler<THandler>(ServiceProvider provider)
        where THandler : notnull =>
        provider.CreateScope().ServiceProvider.GetRequiredService<THandler>();

    private async Task CleanSealDomainsAsync(params string[] domains)
    {
        await using var ctx = fixture.NewContext();

        // Audit rows FK-reference key_record with RESTRICT, so the domains' audit children go
        // first. The kid list is materialized so no query lambda captures the disposable ctx.
        var kids = await ctx.Keys
            .Where(k => domains.Contains(k.KeyDomain))
            .Select(k => k.Kid)
            .ToListAsync();

        await ctx.Audit.Where(a => kids.Contains(a.Kid)).ExecuteDeleteAsync();

        await ctx.Keys.Where(k => domains.Contains(k.KeyDomain)).ExecuteDeleteAsync();
    }

    private ServiceProvider BuildProvider(
        TestClock clock, IPayloadCrypto rootCrypto, string caller, bool sealEncrypt)
    {
        var scope = sealEncrypt ? Scopes.Internal.Kc.Seal.Encrypt : Scopes.Internal.Kc.Seal.Open;

        var requestContext = new MutableRequestContext
        {
            Origin = RequestOrigin.CrossProcessHop,
            ImmediateCaller = caller,
            Scopes = new HashSet<string>(StringComparer.Ordinal) { scope },
        };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2Handler();

        services.AddSingleton<IRequestContext>(requestContext);
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<IKeyRotationAnnouncer>(new RecordingAnnouncer());
        services.AddSingleton(Options.Create(new SigningDomainAuthorityOptions()));
        services.AddSingleton(Options.Create(new KeyringDomainAuthorityOptions()));

        services.AddDbContext<KeyCustodianDbContext>(opts =>
            opts.ApplyD2NpgsqlDefaults(
                fixture.ConnectionString,
                commandTimeoutSeconds: 30,
                migrationsAssemblyName: typeof(KeyCustodianDbContext).Assembly.GetName().Name!));

        services.AddScoped<IKeyCustodianDbContext>(
            sp => sp.GetRequiredService<KeyCustodianDbContext>());

        services.AddD2Postgres();

        services.AddKeyedSingleton<IPayloadCrypto>(
            KeyCustodianRootKey.ROOT_SERVICE_KEY, (_, _) => rootCrypto);

        services.AddSingleton(Options.Create(BuildOptions()));

        services.AddD2KeyCustodianApp();

        return services.BuildServiceProvider();
    }
}
