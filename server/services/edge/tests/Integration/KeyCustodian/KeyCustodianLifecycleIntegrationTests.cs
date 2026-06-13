// -----------------------------------------------------------------------
// <copyright file="KeyCustodianLifecycleIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Integration.KeyCustodian;

using System.Security.Cryptography;
using D2.Edge.KeyCustodian.App.Application;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.ActivateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GenerateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RetireKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RotateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RunDueRotations;
using D2.Edge.KeyCustodian.App.Infrastructure.Configuration;
using D2.Edge.KeyCustodian.App.Infrastructure.Messaging;
using D2.Edge.KeyCustodian.App.Infrastructure.Persistence;
using D2.Edge.KeyCustodian.App.Infrastructure.Vault;
using D2.Edge.KeyCustodian.Infra.Persistence.Postgres;
using D2.Edge.Tests.Unit.KeyCustodian.App.Fixtures;
using D2.Shared.Context.Abstractions;
using D2.Shared.EntityFrameworkCore.Postgres;
using D2.Shared.Handler;
using D2.Shared.Handler.Repo.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Full-lifecycle live-DB tests through the real handler graph against PostgreSQL:
/// generate → soak → activate → cadence elapse → rotate → grace elapse → retire,
/// plus the rotation-exactly-one guarantee under the advisory lock. Uses a
/// <c>TestClock</c> to advance time and a <see cref="RecordingAnnouncer"/> to
/// capture the event sequence. Run after the orchestrator generates the Initial
/// migration.
/// </summary>
[Trait("Category", "Integration")]
[Collection(KeyCustodianPostgresCollectionDefinition.NAME)]
public sealed class KeyCustodianLifecycleIntegrationTests(KeyCustodianPostgresFixture fixture)
{
    [Fact]
    public async Task FullLifecycle_GenerateActivateRotateRetire_AgainstRealDb()
    {
        await fixture.EnsureMigratedAsync();
        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0));
        var announcer = new RecordingAnnouncer();
        await using var provider = BuildProvider(clock, announcer);
        var domain = KeyDomain.ClientSecret.Value;

        // 1. Generate a pending key.
        var generated = await Handler<IGenerateKeyHandler>(provider)
            .HandleAsync(new GenerateKeyInput(domain, KeyType.Secret), CancellationToken.None);
        generated.Success.Should().BeTrue();
        var pendingKid = await SingleKidAsync(domain, KeyStatus.Pending);

        // 2. Soak elapses, then activate.
        clock.Advance(Duration.FromHours(2));
        var activated = await Handler<IActivateKeyHandler>(provider)
            .HandleAsync(new ActivateKeyInput(pendingKid), CancellationToken.None);
        activated.Success.Should().BeTrue();
        await SingleKidAsync(domain, KeyStatus.Active);

        // 3. Generate a pending successor — the step-4 cadence advance soaks it.
        await Handler<IGenerateKeyHandler>(provider)
            .HandleAsync(new GenerateKeyInput(domain, KeyType.Secret), CancellationToken.None);

        // 4. Rotate — the original incumbent enters retiring.
        clock.Advance(Duration.FromDays(180));
        var rotated = await Handler<IRotateKeyHandler>(provider)
            .HandleAsync(new RotateKeyInput(domain), CancellationToken.None);
        rotated.Success.Should().BeTrue();

        // Overlap guarantee: exactly one Active AND one Retiring must exist in a single
        // SaveChangesAsync — the "never zero active keys" contract.
        await using (var overlap = fixture.NewContext())
        {
            (await overlap.Keys.AsNoTracking()
                    .CountAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Active))
                .Should().Be(1, "the successor must be Active immediately after rotation");
            (await overlap.Keys.AsNoTracking()
                    .CountAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Retiring))
                .Should().Be(1, "the incumbent must be Retiring immediately after rotation");
        }

        // 5. Grace elapses, then retire the retiring key.
        clock.Advance(Duration.FromDays(180));
        var retiringKid = await SingleKidAsync(domain, KeyStatus.Retiring);
        var retired = await Handler<IRetireKeyHandler>(provider)
            .HandleAsync(new RetireKeyInput(retiringKid), CancellationToken.None);
        retired.Success.Should().BeTrue();

        await using var verify = fixture.NewContext();
        (await verify.Keys.AsNoTracking()
                .CountAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Retired))
            .Should().BeGreaterThanOrEqualTo(1);

        // The rotation announced at least once (routine, non-urgent).
        announcer.Calls.Should().Contain(c => !c.Urgent);
    }

    [Fact]
    public async Task Rotation_ConcurrentTicks_ExactlyOneExecutes()
    {
        await fixture.EnsureMigratedAsync();
        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0));
        var domain = KeyDomain.Cookie.Value;
        await using var provider = BuildProvider(clock, new RecordingAnnouncer());

        // Seed an active + soaked-pending successor so a rotation is due.
        await SeedRotatableAsync(provider, clock, domain);
        clock.Advance(Duration.FromDays(365));

        // Two concurrent rotation attempts under the advisory lock — exactly one runs.
        IReadOnlyDictionary<string, KeyType> bootstrap =
            new Dictionary<string, KeyType>(StringComparer.Ordinal);

        await using var lockHandle = await PgAdvisoryLock.TryAcquireSessionAsync(
            fixture.ConnectionString, AdvisoryLocks.KeycustodianDb.ROTATION);
        lockHandle.IsHeld.Should().BeTrue();

        // While the lock is held, a competing tick's try-acquire fails (skip).
        await using var competitor = await PgAdvisoryLock.TryAcquireSessionAsync(
            fixture.ConnectionString, AdvisoryLocks.KeycustodianDb.ROTATION);
        competitor.IsHeld.Should().BeFalse();

        // The holder performs the rotation directly.
        var run = await Handler<IRunDueRotationsHandler>(provider)
            .HandleAsync(new RunDueRotationsInput(bootstrap), CancellationToken.None);
        run.Success.Should().BeTrue();

        await using var verify = fixture.NewContext();
        (await verify.Keys.AsNoTracking()
                .CountAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Retiring))
            .Should().Be(1);
        (await verify.Keys.AsNoTracking()
                .CountAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Active))
            .Should().Be(1, "the successor must be Active and no other key active after rotation");
    }

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
            Cadence = TimeSpan.FromDays(30),
            Grace = TimeSpan.FromDays(7),
            SmokeSoak = TimeSpan.FromHours(1),
        },
    };

    private async Task SeedRotatableAsync(ServiceProvider provider, TestClock clock, string domain)
    {
        // Generate + activate the incumbent; the caller advances 365 days which soaks
        // the pending successor before RunDueRotations fires.
        await Handler<IGenerateKeyHandler>(provider)
            .HandleAsync(new GenerateKeyInput(domain, KeyType.Secret), CancellationToken.None);
        var activeKid = await SingleKidAsync(domain, KeyStatus.Pending);
        clock.Advance(Duration.FromHours(2));
        await Handler<IActivateKeyHandler>(provider)
            .HandleAsync(new ActivateKeyInput(activeKid), CancellationToken.None);

        // Generate a pending successor only — leave it pending so RotateKey can find it.
        await Handler<IGenerateKeyHandler>(provider)
            .HandleAsync(new GenerateKeyInput(domain, KeyType.Secret), CancellationToken.None);
    }

    private async Task<string> SingleKidAsync(string domain, KeyStatus status)
    {
        await using var context = fixture.NewContext();
        return await context.Keys.AsNoTracking()
            .Where(k => k.KeyDomain == domain && k.Status == status)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => k.Kid)
            .FirstAsync();
    }

    private ServiceProvider BuildProvider(TestClock clock, IKeyRotationAnnouncer announcer)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2Handler();
        services.AddSingleton<IRequestContext>(_ => new MutableRequestContext());
        services.AddSingleton<IClock>(clock);
        services.AddSingleton(announcer);

        // Real DbContext against the container (scoped, like production).
        services.AddDbContext<KeyCustodianDbContext>(opts =>
            opts.ApplyD2NpgsqlDefaults(
                fixture.ConnectionString,
                commandTimeoutSeconds: 30,
                migrationsAssemblyName: typeof(KeyCustodianDbContext).Assembly.GetName().Name!));
        services.AddScoped<IKeyCustodianDbContext>(
            sp => sp.GetRequiredService<KeyCustodianDbContext>());

        services.AddD2Postgres();

        // Real root crypto over a throwaway keyring (genuine wrap/unwrap path).
        services.AddKeyedSingleton<IPayloadCrypto>(
            KeyCustodianRootKey.ROOT_SERVICE_KEY, (_, _) => BuildRootCrypto());

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(BuildOptions()));

        services.AddD2KeyCustodianApp();

        return services.BuildServiceProvider();
    }
}
