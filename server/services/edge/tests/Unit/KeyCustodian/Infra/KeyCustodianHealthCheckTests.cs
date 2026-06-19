// -----------------------------------------------------------------------
// <copyright file="KeyCustodianHealthCheckTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Infra;

using D2.Edge.KeyCustodian.App.Infrastructure.Vault;
using D2.Edge.KeyCustodian.Infra.Observability;
using D2.Shared.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Tests for <see cref="KeyCustodianHealthCheck"/> readiness semantics: Unhealthy
/// when the root key is unloadable OR the database is unreachable; Degraded when a
/// configured domain has no Active key (empty DB included); Healthy when every
/// configured domain has an Active key. The READY tag is asserted at registration.
/// </summary>
public sealed class KeyCustodianHealthCheckTests
{
    [Fact]
    public async Task CheckHealth_RootKeyUnloadable_ReturnsUnhealthy()
    {
        var check = new KeyCustodianHealthCheck(
            BuildScopeFactory(SeededContext()),
            new ThrowingRootKeyProvider());

        var result = await check.CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealth_DatabaseUnreachable_ReturnsUnhealthy()
    {
        var check = new KeyCustodianHealthCheck(
            BuildScopeFactory(new ThrowingDbContext()),
            new StubRootKeyProvider());

        var result = await check.CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealth_EmptyDatabase_ReturnsDegraded()
    {
        var check = new KeyCustodianHealthCheck(
            BuildScopeFactory(SeededContext()),
            new StubRootKeyProvider());

        var result = await check.CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealth_SomeDomainsMissingActive_ReturnsDegraded()
    {
        var db = SeededContext();

        // Active key for only ONE of the configured domains.
        AddActiveKey(db, KeyDomain.JwksSigning.Value);
        await db.SaveChangesAsync();

        var check = new KeyCustodianHealthCheck(
            BuildScopeFactory(db), new StubRootKeyProvider());

        var result = await check.CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealth_AllDomainsHaveActive_ReturnsHealthy()
    {
        var db = SeededContext();

        foreach (var domain in KeyDomain.All)
            AddActiveKey(db, domain.Value);

        await db.SaveChangesAsync();

        var check = new KeyCustodianHealthCheck(
            BuildScopeFactory(db), new StubRootKeyProvider());

        var result = await check.CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    private static HealthCheckContext Context() => new()
    {
        Registration = new HealthCheckRegistration(
            "keycustodian", new AlwaysHealthy(), failureStatus: null, tags: null),
    };

    private static App.KeyCustodianTestDbContext SeededContext() =>
        App.KeyCustodianTestDbContext.CreateEmpty();

    private static void AddActiveKey(App.KeyCustodianTestDbContext db, string domain)
    {
        db.Keys.Add(new KeyRecord
        {
            Kid = "kid-" + Guid.NewGuid().ToString("N"),
            KeyDomain = domain,
            KeyType = KeyType.AesPayload,
            KeyMaterialEncrypted = [1, 2, 3],
            CreatedAt = Instant.FromUtc(2026, 1, 1, 0, 0),
            Status = KeyStatus.Active,
            ActivatedAt = Instant.FromUtc(2026, 1, 1, 0, 0),
        });
    }

    private static IServiceScopeFactory BuildScopeFactory(IKeyCustodianDbContext db)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => db);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private sealed class StubRootKeyProvider : IRootKeyProvider
    {
        public PayloadCryptoKeyring GetRootKeyring()
        {
            var key = new byte[PayloadCryptoKeyring.KEY_SIZE_BYTES];
            return new PayloadCryptoKeyring(
                "root",
                new Dictionary<string, byte[]> { ["root"] = key },
                "keycustodian-root"u8.ToArray());
        }
    }

    private sealed class ThrowingRootKeyProvider : IRootKeyProvider
    {
        public PayloadCryptoKeyring GetRootKeyring() =>
            throw new InvalidOperationException("root key unloadable");
    }

    private sealed class ThrowingDbContext : IKeyCustodianDbContext
    {
        public DbSet<KeyRecord> Keys =>
            throw new InvalidOperationException("database unreachable");

        public DbSet<KeyAuditRecord> Audit =>
            throw new InvalidOperationException("database unreachable");

        public DbSet<LeafIssuanceAuditRecord> LeafIssuanceAudit =>
            throw new InvalidOperationException("database unreachable");

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("database unreachable");
    }

    private sealed class AlwaysHealthy : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Healthy());
    }
}
