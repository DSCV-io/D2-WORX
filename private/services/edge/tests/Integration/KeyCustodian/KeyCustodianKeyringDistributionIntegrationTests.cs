// -----------------------------------------------------------------------
// <copyright file="KeyCustodianKeyringDistributionIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Integration.KeyCustodian;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using D2.Edge.KeyCustodian.App.Application;
using D2.Edge.KeyCustodian.App.Application.CertificateAuthority;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.ActivateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.CompromiseKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GenerateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RotateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetKeyring;
using D2.Edge.KeyCustodian.App.Application.Observability;
using D2.Edge.KeyCustodian.App.Infrastructure.Configuration;
using D2.Edge.KeyCustodian.App.Infrastructure.Messaging;
using D2.Edge.KeyCustodian.App.Infrastructure.Vault;
using D2.Edge.KeyCustodian.Client.Keyring;
using D2.Edge.KeyCustodian.Infra.Persistence.Postgres;
using D2.Edge.Tests.Unit.KeyCustodian.App.Fixtures;
using D2.Private.Auth;
using D2.Shared.Context.Abstractions;
using D2.Shared.Encryption;
using D2.Shared.EntityFrameworkCore.Postgres;
using D2.Shared.Handler;
using D2.Shared.Handler.Repo.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Full keyring-distribution live-DB test through the real handler graph against PostgreSQL:
/// seed Active + Retiring <c>AesPayload</c> keys via the real GenerateKey / Activate / Rotate
/// handlers ΓåÆ fetch through the REAL <see cref="GetKeyringHandler"/> ΓåÆ assemble a
/// <see cref="PayloadCryptoKeyring"/> from the wire output (using the returned
/// <c>aadContext</c> verbatim) ΓåÆ encrypt/decrypt round-trip; the rotation-overlap decrypt
/// guarantee (an old-kid frame still decrypts via the post-rotation keyring; new frames ride
/// the new active kid); compromised exclusion; and the no-active 503 with its counter + 9513
/// log. One method on a self-cleaned domain (the shared-collection PostgreSQL is not reset
/// between tests, and the [Collection] serializes them).
/// </summary>
[Trait("Category", "Integration")]
[Collection(KeyCustodianPostgresCollectionDefinition.NAME)]
public sealed class KeyCustodianKeyringDistributionIntegrationTests(
    KeyCustodianPostgresFixture fixture) : IDisposable
{
    // audit is now SEALED (removed from the KC symmetric payload catalog);
    // exercise the preserved symmetric machinery on a registered fixture payload domain (the
    // field-initializer registration precedes any per-test host boot; Dispose unregisters).
    private const string _DOMAIN = "payload-fixture-a";
    private const string _CALLER = "edge";
    private const string _EMPTY_KEYRING = "d2.keycustodian.empty_keyring_served";

    private readonly IDisposable r_fixtureSeam =
        KeyDomain.RegisterFixturePayloadDomainForTesting(_DOMAIN);

    /// <summary>Unregisters the fixture payload domain (ref-counted, per-test-instance).</summary>
    public void Dispose() => r_fixtureSeam.Dispose();

    [Fact]
    public async Task KeyringDistribution_EndToEnd_RoundTripOverlapExclusionAndUnavailable()
    {
        await fixture.EnsureMigratedAsync();
        await CleanDomainAsync();

        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0));
        var rootCrypto = BuildRootCrypto();
        var logs = new CapturingLoggerProvider();

        // 1. No active key yet â†’ 503 + SR_EmptyKeyringServed + 9513 log.
        var emptyKeyring = new List<long>();
        await using (var fetchProvider = BuildFetchProvider(clock, rootCrypto, logs))
        {
            using var listener = BuildEmptyKeyringListener(emptyKeyring);
            listener.Start();

            var unavailable = await Fetch(fetchProvider);

            unavailable.Success.Should().BeFalse();
            unavailable.ErrorCode.Should().Be(
                KeyCustodianErrorCodes.KEYCUSTODIAN_KEYRING_KEY_UNAVAILABLE);
            unavailable.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        }

        emptyKeyring.Should().Contain(1L, "the no-active path increments SR_EmptyKeyringServed");
        logs.Entries.Should().Contain(
            e => e.EventId.Id == 9513, "the 9513 KeyringKeyUnavailable warning fired");

        // 2. Generate + activate the first payload key (System plane).
        string firstActiveKid;
        await using (var sys = BuildSystemProvider(clock, rootCrypto))
        {
            (await Handler<IGenerateKeyHandler>(sys).HandleAsync(
                new GenerateKeyInput(_DOMAIN, KeyType.AesPayload), CancellationToken.None))
                .Success.Should().BeTrue();
            firstActiveKid = await SingleKidAsync(KeyStatus.Pending);
            clock.Advance(Duration.FromHours(2));
            (await Handler<IActivateKeyHandler>(sys).HandleAsync(
                new ActivateKeyInput(firstActiveKid), CancellationToken.None))
                .Success.Should().BeTrue();
        }

        // 3. Fetch the keyring, assemble a PayloadCryptoKeyring, round-trip.
        byte[] frameUnderFirstKid;
        await using (var fetchProvider = BuildFetchProvider(clock, rootCrypto, logs))
        {
            var fetched = await Fetch(fetchProvider);

            fetched.Success.Should().BeTrue();
            fetched.Data!.ActiveKid.Should().Be(firstActiveKid);
            fetched.Data.Entries.Should().ContainSingle();

            using var keyring = ToKeyring(fetched.Data);
            var crypto = new PayloadCrypto(keyring);

            var plaintext = "sensitive-audit-event"u8.ToArray();
            frameUnderFirstKid = crypto.Encrypt(plaintext);
            crypto.Decrypt(frameUnderFirstKid).Should().Equal(
                plaintext, "the fetched keyring encrypts + decrypts round-trip");
        }

        // 4. Generate successor + rotate â†’ overlap (Active + Retiring).
        string secondActiveKid;
        await using (var sys = BuildSystemProvider(clock, rootCrypto))
        {
            (await Handler<IGenerateKeyHandler>(sys).HandleAsync(
                new GenerateKeyInput(_DOMAIN, KeyType.AesPayload), CancellationToken.None))
                .Success.Should().BeTrue();

            // Advance past the cadence so the successor has soaked and rotation is due.
            clock.Advance(Duration.FromDays(31));
            (await Handler<IRotateKeyHandler>(sys).HandleAsync(
                new RotateKeyInput(_DOMAIN), CancellationToken.None))
                .Success.Should().BeTrue();
            secondActiveKid = await SingleKidAsync(KeyStatus.Active);
        }

        secondActiveKid.Should().NotBe(firstActiveKid, "rotation activated a fresh key");

        // 5. Post-rotation keyring: new active first, old kid retiring; overlap decrypt.
        await using (var fetchProvider = BuildFetchProvider(clock, rootCrypto, logs))
        {
            var fetched = await Fetch(fetchProvider);

            fetched.Success.Should().BeTrue();
            fetched.Data!.ActiveKid.Should().Be(secondActiveKid, "the new active kid leads");
            fetched.Data.Entries.Select(e => e.Kid).Should().Contain(
                new[] { secondActiveKid, firstActiveKid },
                "both the new active and the old retiring key are served during overlap");
            fetched.Data.Entries[0].Kid.Should().Be(secondActiveKid, "active is served first");

            using var keyring = ToKeyring(fetched.Data);
            var crypto = new PayloadCrypto(keyring);

            // Overlap guarantee: the pre-rotation frame (tagged the OLD kid) still decrypts.
            crypto.Decrypt(frameUnderFirstKid).Should().Equal(
                "sensitive-audit-event"u8.ToArray(),
                "a frame encrypted under the pre-rotation kid still decrypts via the fresh keyring");

            // New frames ride the new active kid.
            var freshPlain = "post-rotation-event"u8.ToArray();
            crypto.Decrypt(crypto.Encrypt(freshPlain)).Should().Equal(freshPlain);
        }

        // 6. Compromise the retiring (old) key â†’ never served again.
        await using (var sys = BuildSystemProvider(clock, rootCrypto))
        {
            (await Handler<ICompromiseKeyHandler>(sys).HandleAsync(
                new CompromiseKeyInput
                {
                    Kid = firstActiveKid,
                    Reason = "integration",
                    GenerateReplacement = false,
                },
                CancellationToken.None))
                .Success.Should().BeTrue();
        }

        await using (var fetchProvider = BuildFetchProvider(clock, rootCrypto, logs))
        {
            var fetched = await Fetch(fetchProvider);

            fetched.Success.Should().BeTrue();
            fetched.Data!.Entries.Should().NotContain(
                e => e.Kid == firstActiveKid, "a compromised key is never served");
            fetched.Data.ActiveKid.Should().Be(secondActiveKid);
        }
    }

    private static PayloadCryptoKeyring ToKeyring(GetKeyringOutput output)
    {
        var keys = output.Entries.ToDictionary(e => e.Kid, e => e.KeyBytes, StringComparer.Ordinal);
        return new PayloadCryptoKeyring(output.ActiveKid, keys, output.AadContext);
    }

    private static async Task<D2Result<GetKeyringOutput?>> Fetch(ServiceProvider provider) =>
        await Handler<IGetKeyringHandler>(provider)
            .HandleAsync(new GetKeyringInput(_DOMAIN), CancellationToken.None);

    private static THandler Handler<THandler>(ServiceProvider provider)
        where THandler : notnull =>
        provider.CreateScope().ServiceProvider.GetRequiredService<THandler>();

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

    private static MeterListener BuildEmptyKeyringListener(List<long> emptyKeyring)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == KeyCustodianMetrics.METER_NAME
                    && instrument.Name == _EMPTY_KEYRING)
                    l.EnableMeasurementEvents(instrument);
            },
        };

        listener.SetMeasurementEventCallback<long>((_, value, _, _) => emptyKeyring.Add(value));

        return listener;
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

    private ServiceProvider BuildSystemProvider(TestClock clock, IPayloadCrypto rootCrypto) =>
        BuildProvider(
            clock,
            new MutableRequestContext { Origin = RequestOrigin.System },
            rootCrypto,
            keyringAuthority: null,
            loggerProvider: null);

    private ServiceProvider BuildFetchProvider(
        TestClock clock, IPayloadCrypto rootCrypto, ILoggerProvider loggerProvider)
    {
        var keyringAuth = new KeyringDomainAuthorityOptions();
        keyringAuth.AllowedKeyringDomainsByWorkload[_CALLER] = [_DOMAIN];

        var ctx = new MutableRequestContext
        {
            Origin = RequestOrigin.CrossProcessHop,
            ImmediateCaller = _CALLER,
            Scopes = new HashSet<string>(StringComparer.Ordinal) { ProductScopes.Internal.Kc.Keyring },
        };

        return BuildProvider(clock, ctx, rootCrypto, keyringAuth, loggerProvider);
    }

    private ServiceProvider BuildProvider(
        TestClock clock,
        IRequestContext requestContext,
        IPayloadCrypto rootCrypto,
        KeyringDomainAuthorityOptions? keyringAuthority,
        ILoggerProvider? loggerProvider)
    {
        var services = new ServiceCollection();
        services.AddLogging(b =>
        {
            if (loggerProvider is not null)
            {
                b.SetMinimumLevel(LogLevel.Trace);
                b.AddProvider(loggerProvider);
            }
        });
        services.AddD2Handler();

        services.AddSingleton(requestContext);
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<IKeyRotationAnnouncer>(new RecordingAnnouncer());
        services.AddSingleton(
            Microsoft.Extensions.Options.Options.Create(new SigningDomainAuthorityOptions()));
        services.AddSingleton(
            Microsoft.Extensions.Options.Options.Create(
                keyringAuthority ?? new KeyringDomainAuthorityOptions()));

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

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(BuildOptions()));

        services.AddD2KeyCustodianApp();

        // The dedicated Â§9.44 root-signing capability â€” the composition-root opt-in the
        // System-worker host makes; the four lifecycle-mutation handlers resolved here
        // (generate / activate / rotate / compromise) take it.
        services.AddD2CaRootSigningCapability();

        return services.BuildServiceProvider();
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<(LogLevel Level, EventId EventId)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(
            ConcurrentQueue<(LogLevel Level, EventId EventId)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => entries.Enqueue((logLevel, eventId));
        }
    }
}
