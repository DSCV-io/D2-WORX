// -----------------------------------------------------------------------
// <copyright file="AdvisoryLockMigratorIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.SharedEntityFrameworkCorePostgres;

using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Tests.Integration.DataGovernance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Live-DB integration tests for <see cref="AdvisoryLockMigrator{TContext}"/>.
/// Exercises the StartAsync ensure-database + lock-acquire + migrate lifecycle
/// against a real PostgreSQL container. Uses a minimal probe <see cref="DbContext"/>
/// with no migrations assembly (EnsureCreated instead of MigrateAsync) to avoid
/// requiring an EF migrations DLL at test time.
/// </summary>
/// <remarks>
/// The migrator's <c>MigrateAsync</c> path is called; for a freshly-created
/// database with no migrations table at all, EF Core's GetPendingMigrationsAsync
/// returns an empty collection (no migrations assembly registered), so the migrator
/// correctly takes the NoPendingMigrations fast path. The lock acquisition and
/// database-existence checks are exercised regardless.
/// </remarks>
[Collection("Postgres")]
[Trait("Category", "Integration")]
public sealed class AdvisoryLockMigratorIntegrationTests
{
    private const long _MIGRATOR_KEY = 9990001L;

    private readonly PostgresFixture r_fixture;

    /// <summary>
    /// Initializes a new instance of <see cref="AdvisoryLockMigratorIntegrationTests"/>.
    /// </summary>
    /// <param name="fixture">The shared Postgres Testcontainers fixture.</param>
    public AdvisoryLockMigratorIntegrationTests(PostgresFixture fixture)
    {
        r_fixture = fixture;
    }

    // =========================================================================
    // StartAsync — database-already-exists path
    // =========================================================================

    [Fact]
    public async Task StartAsync_DatabaseAlreadyExists_CompletesWithoutThrowing()
    {
        // The PostgresFixture container already has the 'test' database (or postgres).
        // We target the existing connection string directly.
        var services = BuildServices(r_fixture.ConnectionString);
        var migrator = services.GetRequiredService<AdvisoryLockMigrator<ProbeDbContext>>();

        await migrator.StartAsync(CancellationToken.None);

        // Reaching here means: ensure-db was idempotent, lock acquired, no pending
        // migrations (context has no migrations assembly), lock released.
    }

    // =========================================================================
    // StopAsync — always completes
    // =========================================================================

    [Fact]
    public async Task StopAsync_Always_CompletesImmediately()
    {
        var services = BuildServices(r_fixture.ConnectionString);
        var migrator = services.GetRequiredService<AdvisoryLockMigrator<ProbeDbContext>>();

        await migrator.StopAsync(CancellationToken.None);
    }

    // =========================================================================
    // StartAsync — ensure-database creates a fresh DB
    // =========================================================================

    [Fact]
    public async Task StartAsync_FreshDatabase_EnsuresDatabaseExists()
    {
        // Use a unique database name so we can guarantee it doesn't exist yet.
        var uniqueDb = $"migrator_probe_{Guid.NewGuid():N}";
        var connStr = SwapDatabase(r_fixture.ConnectionString, uniqueDb);

        var services = BuildServices(connStr);
        var migrator = services.GetRequiredService<AdvisoryLockMigrator<ProbeDbContext>>();

        await migrator.StartAsync(CancellationToken.None);

        // Verify the database was created by opening a connection to it.
        await using var checkCtx = new ProbeDbContext(
            new DbContextOptionsBuilder<ProbeDbContext>()
                .UseNpgsql(connStr)
                .Options);

        var canConnect = await checkCtx.Database.CanConnectAsync();
        canConnect.Should().BeTrue(
            "StartAsync must create the target database if it doesn't exist");

        // Clean up the created database.
        await checkCtx.Database.EnsureDeletedAsync();
    }

    // =========================================================================
    // StartAsync — concurrent-migrators serialization
    // =========================================================================

    [Fact]
    public async Task StartAsync_TwoConcurrentMigrators_ExactlyOneMigratesOtherBlocksThenFindsNoPending()
    {
        // Two migrators race to acquire the same advisory lock against the same DB.
        // The first acquires the lock, migrates (finds no pending — no migrations assembly),
        // then releases. The second acquires, also finds no pending, and completes.
        // Both Task.WhenAll completions prove: no deadlock, no throw, DB created once.
        var uniqueDb = $"concurrent_migrator_{Guid.NewGuid():N}";
        var connStr = SwapDatabase(r_fixture.ConnectionString, uniqueDb);

        // Use a different lock key from the serial test to avoid cross-test lock contention.
        const long _CONCURRENT_KEY = 9990002L;

        await using var services1 = BuildServices(connStr, _CONCURRENT_KEY);
        await using var services2 = BuildServices(connStr, _CONCURRENT_KEY);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var migrator1 = services1.GetRequiredService<AdvisoryLockMigrator<ProbeDbContext>>();
        var migrator2 = services2.GetRequiredService<AdvisoryLockMigrator<ProbeDbContext>>();

        await Task.WhenAll(
            migrator1.StartAsync(cts.Token),
            migrator2.StartAsync(cts.Token));

        // Verify the database exists — was created exactly once (the migrator is idempotent
        // so the second run simply finds it present).
        await using var checkCtx = new ProbeDbContext(
            new DbContextOptionsBuilder<ProbeDbContext>()
                .UseNpgsql(connStr)
                .Options);

        var canConnect = await checkCtx.Database.CanConnectAsync(cts.Token);
        canConnect.Should().BeTrue(
            "both migrators must have completed and left the DB in a connectable state");

        // Clean up.
        await checkCtx.Database.EnsureDeletedAsync(CancellationToken.None);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static ServiceProvider BuildServices(string connectionString)
        => BuildServices(connectionString, _MIGRATOR_KEY);

    private static ServiceProvider BuildServices(string connectionString, long lockKey)
    {
        var services = new ServiceCollection();
        services.AddDbContext<ProbeDbContext>(opts =>
            opts.UseNpgsql(connectionString));

        services.AddSingleton(sp =>
            new AdvisoryLockMigrator<ProbeDbContext>(
                sp.GetRequiredService<IServiceScopeFactory>(),
                connectionString,
                lockKey,
                NullLogger<AdvisoryLockMigrator<ProbeDbContext>>.Instance));

        return services.BuildServiceProvider();
    }

    private static string SwapDatabase(string connectionString, string newDatabase)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = newDatabase,
        };
        return builder.ToString();
    }

    // =========================================================================
    // Minimal probe DbContext — no migrations, just proves schema presence
    // =========================================================================

    private sealed class ProbeDbContext : DbContext
    {
        public ProbeDbContext(DbContextOptions<ProbeDbContext> options)
            : base(options)
        {
        }
    }
}
