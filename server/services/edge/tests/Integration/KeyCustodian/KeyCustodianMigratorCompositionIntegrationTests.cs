// -----------------------------------------------------------------------
// <copyright file="KeyCustodianMigratorCompositionIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Integration.KeyCustodian;

using D2.Edge.KeyCustodian.Infra.Persistence.Postgres;
using D2.Shared.EntityFrameworkCore.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

/// <summary>
/// Proves the shared <see cref="AdvisoryLockMigrator{TContext}"/> composes with the
/// real <see cref="KeyCustodianDbContext"/>: pointed at a not-yet-existent
/// <c>d2-keycustodian</c>, the migrator's ensure-db step CREATES the database and
/// applies the Initial migration (the generic exactly-one / auto-release mechanics
/// are pinned in the shared-lib suite, not re-tested here). Run after the
/// orchestrator generates the Initial migration.
/// </summary>
[Trait("Category", "Integration")]
[Collection(KeyCustodianPostgresCollectionDefinition.NAME)]
public sealed class KeyCustodianMigratorCompositionIntegrationTests(
    KeyCustodianPostgresFixture fixture)
{
    [Fact]
    public async Task Migrator_EnsureDb_CreatesKeycustodianDb_AndApplies()
    {
        // A fresh, container-unique target DB name the migrator must create.
        var targetDb = "d2-keycustodian_" + Guid.NewGuid().ToString("N")[..12];
        var connectionString = WithDatabase(fixture.ConnectionString, targetDb);

        var services = new ServiceCollection();
        services.AddDbContext<KeyCustodianDbContext>(opts =>
            opts.ApplyD2NpgsqlDefaults(
                connectionString,
                commandTimeoutSeconds: 30,
                migrationsAssemblyName: typeof(KeyCustodianDbContext).Assembly.GetName().Name!));
        await using var provider = services.BuildServiceProvider();

        var migrator = new AdvisoryLockMigrator<KeyCustodianDbContext>(
            provider.GetRequiredService<IServiceScopeFactory>(),
            connectionString,
            AdvisoryLocks.D2Keycustodian.MIGRATOR,
            NullLogger<AdvisoryLockMigrator<KeyCustodianDbContext>>.Instance);

        try
        {
            await migrator.StartAsync(CancellationToken.None);

            // The schema is present in the freshly-created database.
            await using var scope = provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<KeyCustodianDbContext>();
            (await context.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
            (await context.Keys.AsNoTracking().CountAsync()).Should().Be(0);
        }
        finally
        {
            await DropDatabaseAsync(fixture.ConnectionString, targetDb);
        }
    }

    [Fact]
    public async Task Migrator_SecondRun_IsIdempotent()
    {
        var targetDb = "d2-keycustodian_" + Guid.NewGuid().ToString("N")[..12];
        var connectionString = WithDatabase(fixture.ConnectionString, targetDb);

        var services = new ServiceCollection();
        services.AddDbContext<KeyCustodianDbContext>(opts =>
            opts.ApplyD2NpgsqlDefaults(
                connectionString,
                commandTimeoutSeconds: 30,
                migrationsAssemblyName: typeof(KeyCustodianDbContext).Assembly.GetName().Name!));
        await using var provider = services.BuildServiceProvider();

        var migrator = new AdvisoryLockMigrator<KeyCustodianDbContext>(
            provider.GetRequiredService<IServiceScopeFactory>(),
            connectionString,
            AdvisoryLocks.D2Keycustodian.MIGRATOR,
            NullLogger<AdvisoryLockMigrator<KeyCustodianDbContext>>.Instance);

        try
        {
            await migrator.StartAsync(CancellationToken.None);

            // Second run: DB exists, no pending migrations — no throw.
            var act = async () => await migrator.StartAsync(CancellationToken.None);
            await act.Should().NotThrowAsync();
        }
        finally
        {
            await DropDatabaseAsync(fixture.ConnectionString, targetDb);
        }
    }

    private static string WithDatabase(string connectionString, string database) =>
        new NpgsqlConnectionStringBuilder(connectionString) { Database = database }.ToString();

    private static async Task DropDatabaseAsync(string connectionString, string database)
    {
        var maintenance = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = "postgres",
        }.ToString();

        await using var conn = new NpgsqlConnection(maintenance);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)";
        await cmd.ExecuteNonQueryAsync();
    }
}
