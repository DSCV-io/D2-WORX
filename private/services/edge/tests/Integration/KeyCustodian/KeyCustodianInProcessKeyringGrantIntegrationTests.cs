// -----------------------------------------------------------------------
// <copyright file="KeyCustodianInProcessKeyringGrantIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Integration.KeyCustodian;

using System.Security.Cryptography;
using System.Text;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Encryption;
using DcsvIo.D2.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Handler.Repo.Postgres;
using DcsvIo.D2.Private.Auth;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.CertificateAuthority;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.ActivateKey;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.GenerateKey;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Issuance;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Keyring;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Configuration;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Messaging;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Vault;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Facade;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Persistence.Postgres;
using DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.App.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Proves the in-process Edge keyring consumer end-to-end THROUGH THE REAL LEAF + REAL
/// <c>AuthorizeKeyringFetch</c> over live PostgreSQL:
/// <list type="bullet">
///   <item>ALLOW ΓÇö <c>AddD2EncryptionFromKeyCustodian</c> establishes the in-process plane,
///   the real leaf authorizes the granted <c>(edge ΓåÆ audit)</c> fetch, and the resolved
///   keyed <see cref="IPayloadCrypto"/> encrypts + decrypts round-trip.</item>
///   <item>EMPTY-GRANT DENY ΓÇö the same registration with a deny-all grant map fails the boot
///   fetch loud (403), so the capability is never constructed.</item>
///   <item>DIRECT-CLIENT DENY ΓÇö a direct holder of the internal <c>InProcessKeyringClient</c>
///   fetching an ungranted domain is still denied by the real fail-closed leaf.</item>
///   <item>UNESTABLISHED DENY ΓÇö a context no boundary established fails closed at
///   the leaf.</item>
/// </list>
/// The deploy grant map stays EMPTY; the allow-arm grant rides the REAL options binding in
/// its deployed env-var shape through an isolated in-memory ConfigurationBuilder (never the
/// process env). The domain is the real catalog payload domain <c>audit</c> (the leaf's
/// <c>KeyDomain.Create</c> requires catalog membership; a synthetic fixture name cannot pass
/// it), with the fixture semantics carried by the isolated test-only grant.
/// </summary>
[Trait("Category", "Integration")]
[Collection(KeyCustodianPostgresCollectionDefinition.NAME)]
public sealed class KeyCustodianInProcessKeyringGrantIntegrationTests(
    KeyCustodianPostgresFixture fixture) : IDisposable
{
    // audit is now SEALED (removed from the KC symmetric payload catalog);
    // exercise the preserved symmetric machinery on a registered fixture payload domain (the
    // field-initializer registration precedes any per-test host boot; Dispose unregisters).
    private const string _DOMAIN = "payload-fixture-a";
    private const string _CALLER = "edge";

    private readonly IDisposable r_fixtureSeam =
        KeyDomain.RegisterFixturePayloadDomainForTesting(_DOMAIN);

    /// <summary>Unregisters the fixture payload domain (ref-counted, per-test-instance).</summary>
    public void Dispose() => r_fixtureSeam.Dispose();

    [Fact]
    public async Task InProcessGrant_AllowAndDenyArms_ThroughRealLeaf()
    {
        await fixture.EnsureMigratedAsync();
        await CleanDomainAsync();

        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0));
        var rootCrypto = BuildRootCrypto();

        // Seed + activate an AesPayload key for the domain (System plane; no grant needed).
        await using (var seed = BuildProvider(clock, rootCrypto))
        {
            await SeedActiveKeyAsync(seed, clock);
        }

        // ALLOW â€” the granted in-process consumer resolves + round-trips through the real
        // leaf. The grant rides the REAL options binding in its deployed env-var shape
        // (KEYCUSTODIAN_KEYRING_AUTHORITY__ALLOWEDKEYRINGDOMAINSBYWORKLOAD__edge__0=audit),
        // bound through an isolated in-memory ConfigurationBuilder â€” never the process env.
        await using (var granted = BuildProvider(clock, rootCrypto, grantBoundViaConfig: true))
        {
            var crypto = granted.GetRequiredKeyedService<IPayloadCrypto>(_DOMAIN);

            var plaintext = Encoding.UTF8.GetBytes("in-process-audit-event");
            var frame = crypto.Encrypt(plaintext);
            crypto.Decrypt(frame).Should().Equal(
                plaintext, "the granted in-process consumer fetches + uses the real keyring");
        }

        // EMPTY-GRANT DENY â€” deny-all grant map â†’ the boot fetch surfaces 403 â†’
        // construction fails loud.
        await using (var denied = BuildProvider(clock, rootCrypto))
        {
            // ReSharper disable once AccessToDisposedClosure -- act is invoked
            // synchronously inside Should().Throw(), before denied disposes.
            var act = () => denied.GetRequiredKeyedService<IPayloadCrypto>(_DOMAIN);

            act.Should().Throw<InvalidOperationException>(
                "an ungranted in-process fetch fails the real fail-closed authority at boot");
        }

        // DIRECT-CLIENT DENY â€” a direct holder of the internal client, ungranted, is
        // still denied.
        await using (var denied = BuildProvider(clock, rootCrypto))
        {
            var client = new InProcessKeyringClient(
                denied.GetRequiredService<IServiceScopeFactory>(),
                clock,
                _CALLER);

            var result = await client.GetKeyringAsync(_DOMAIN, CancellationToken.None);

            result.Failed.Should().BeTrue(
                "the internal client held directly still hits the real leaf deny");

            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // UNESTABLISHED DENY â€” a context no boundary established fails closed at the leaf.
        await using (var provider = BuildProvider(clock, rootCrypto, GrantFor(_CALLER, _DOMAIN)))
        {
            await using var scope =
                provider.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();

            // Deliberately DO NOT establish any plane â€” Origin stays Unestablished.
            scope.ServiceProvider.GetRequiredService<MutableRequestContext>().Scopes =
                new HashSet<string>(StringComparer.Ordinal) { ProductScopes.Internal.Kc.Keyring };

            var api = scope.ServiceProvider.GetRequiredService<IKeyCustodianApi>();

            var result = await api.GetKeyringAsync(
                new GetKeyringInput(_DOMAIN), CancellationToken.None);

            result.Failed.Should().BeTrue("an unestablished origin is a fail-closed deny");
            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    private static KeyringDomainAuthorityOptions GrantFor(string caller, string domain)
    {
        var options = new KeyringDomainAuthorityOptions();
        options.AllowedKeyringDomainsByWorkload[caller] = [domain];

        return options;
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

    private static async Task RunSystemAsync(
        ServiceProvider provider,
        Func<IServiceProvider, Task> body)
    {
        await using var scope =
            provider.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<MutableRequestContext>().Origin =
            RequestOrigin.System;

        await body(scope.ServiceProvider);
    }

    private async Task SeedActiveKeyAsync(ServiceProvider provider, TestClock clock)
    {
        await RunSystemAsync(provider, async sp =>
            (await sp.GetRequiredService<IGenerateKeyHandler>().HandleAsync(
                new GenerateKeyInput(_DOMAIN, KeyType.AesPayload), CancellationToken.None))
                .Success.Should().BeTrue());

        var pendingKid = await SingleKidAsync(KeyStatus.Pending);
        clock.Advance(Duration.FromHours(2));

        await RunSystemAsync(provider, async sp =>
            (await sp.GetRequiredService<IActivateKeyHandler>().HandleAsync(
                new ActivateKeyInput(pendingKid), CancellationToken.None))
                .Success.Should().BeTrue());
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

    private ServiceProvider BuildProvider(
        TestClock clock,
        IPayloadCrypto rootCrypto,
        KeyringDomainAuthorityOptions? keyringAuthority = null,
        bool grantBoundViaConfig = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2Handler();

        // Scoped establishment context â€” the in-process client establishes a fresh plane on
        // each fetch scope; System seeding establishes its own scope.
        services.TryAddScoped<MutableRequestContext>();

        services.TryAddScoped<IRequestContext>(
            sp => sp.GetRequiredService<MutableRequestContext>());

        services.AddSingleton<IClock>(clock);
        services.AddSingleton<IKeyRotationAnnouncer>(new RecordingAnnouncer());
        services.AddSingleton(Options.Create(new SigningDomainAuthorityOptions()));

        if (grantBoundViaConfig)
        {
            // The deployed env-var shape
            // KEYCUSTODIAN_KEYRING_AUTHORITY__ALLOWEDKEYRINGDOMAINSBYWORKLOAD__edge__0=audit
            // (":"-separated here â€” IConfiguration translates "__" to ":") bound through
            // an isolated in-memory ConfigurationBuilder, never the process env.
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{KeyringDomainAuthorityOptions.SECTION}"
                     + $":{nameof(KeyringDomainAuthorityOptions.AllowedKeyringDomainsByWorkload)}"
                     + $":{_CALLER}:0"] = _DOMAIN,
                })
                .Build();

            services.AddOptions<KeyringDomainAuthorityOptions>()
                .Bind(config.GetSection(KeyringDomainAuthorityOptions.SECTION));
        }
        else
        {
            services.AddSingleton(
                Options.Create(keyringAuthority ?? new KeyringDomainAuthorityOptions()));
        }

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

        // Resolving the full IKeyCustodianApi facade (as the in-process client does)
        // activates every handler, including the issuance handler that depends on the
        // Â§9.44-isolated CA leaf-signing capability â€” registered from its own dedicated
        // composition seam, never AddD2KeyCustodianApp.
        services.AddD2CaLeafSigningCapability();

        // The dedicated Â§9.44 root-signing capability â€” resolving the full facade also
        // activates the lifecycle-mutation handlers, which take it; registered from its
        // own composition seam, never AddD2KeyCustodianApp.
        services.AddD2CaRootSigningCapability();

        // The in-process keyring consumer under test.
        services.AddD2EncryptionFromKeyCustodian(_DOMAIN, _CALLER);

        return services.BuildServiceProvider();
    }
}
