// -----------------------------------------------------------------------
// <copyright file="AdvisoryLockMigrator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EntityFrameworkCore.Postgres;

using System;
using System.Threading;
using System.Threading.Tasks;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

/// <summary>
/// <see cref="IHostedService"/> that ensures the target database exists, acquires a
/// blocking PostgreSQL session advisory lock, applies pending EF Core migrations, and
/// releases the lock. Generic over the consuming <see cref="DbContext"/> type.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Multi-replica safety.</strong> Multiple instances starting simultaneously all
/// try to acquire the same lock. The first acquires it and migrates; the others block.
/// When the first releases the lock, each waiter acquires it in turn and finds no
/// pending migrations — idempotent.
/// </para>
/// <para>
/// <strong>Fail-fast.</strong> Any migration failure throws, crash-looping the host. A
/// bad migration must be fixed before the service starts.
/// </para>
/// <para>
/// <strong>Ensure-database.</strong> Connects to the <c>postgres</c> maintenance
/// database (derived from the target connection string) and issues a
/// <c>CREATE DATABASE IF NOT EXISTS</c>-equivalent before acquiring the migration lock.
/// Idempotent when the database already exists.
/// </para>
/// </remarks>
/// <typeparam name="TContext">The EF Core <see cref="DbContext"/> to migrate.</typeparam>
public sealed partial class AdvisoryLockMigrator<TContext> : IHostedService
    where TContext : DbContext
{
    private readonly IServiceScopeFactory r_scopeFactory;
    private readonly string r_connectionString;
    private readonly long r_migratorLockKey;
    private readonly ILogger<AdvisoryLockMigrator<TContext>> r_logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AdvisoryLockMigrator{TContext}"/>.
    /// </summary>
    /// <param name="scopeFactory">DI scope factory — migrator creates its own scope.</param>
    /// <param name="connectionString">Target database connection string.</param>
    /// <param name="migratorLockKey">
    /// Advisory lock key used during migration. Must be unique within the database
    /// (pass the domain-owned generated <c>AdvisoryLocks.{Db}.MIGRATOR</c> constant).
    /// </param>
    /// <param name="logger">Logger instance.</param>
    public AdvisoryLockMigrator(
        IServiceScopeFactory scopeFactory,
        string connectionString,
        long migratorLockKey,
        ILogger<AdvisoryLockMigrator<TContext>> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        connectionString.ThrowIfFalsey();
        ArgumentNullException.ThrowIfNull(logger);

        r_scopeFactory = scopeFactory;
        r_connectionString = connectionString;
        r_migratorLockKey = migratorLockKey;
        r_logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        MigratorLog.Starting(r_logger, typeof(TContext).Name);

        // Step 1: ensure the target database exists (connect to maintenance DB).
        await EnsureDatabaseExistsAsync(cancellationToken).ConfigureAwait(false);

        // Step 2: acquire the blocking migration lock; migrate; release.
        await using var lockHandle = await PgAdvisoryLock
            .AcquireSessionBlockingAsync(r_connectionString, r_migratorLockKey, cancellationToken)
            .ConfigureAwait(false);

        MigratorLog.LockAcquired(r_logger, r_migratorLockKey);

        await using var scope = r_scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        var pending = await context.Database
            .GetPendingMigrationsAsync(cancellationToken)
            .ConfigureAwait(false);

        using var enumerator = pending.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            MigratorLog.NoPendingMigrations(r_logger, typeof(TContext).Name);
            return;
        }

        MigratorLog.ApplyingMigrations(r_logger, typeof(TContext).Name);
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        MigratorLog.MigrationsApplied(r_logger, typeof(TContext).Name);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // =========================================================================
    // Internal helpers (test seam — InternalsVisibleTo DcsvIo.D2.Tests)
    // =========================================================================

    /// <summary>
    /// Returns <see langword="true"/> when a <see cref="PostgresException"/> thrown
    /// by <c>CREATE DATABASE</c> indicates that the database already exists, making
    /// the error benign (idempotent). Two distinct SqlState codes cover the race:
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>42P04</c> (duplicate_database) — the common outcome when another
    ///     migrator finishes <c>CREATE DATABASE</c> before this one's statement runs.
    ///   </description></item>
    ///   <item><description>
    ///     <c>23505</c> (unique_violation) — the narrow interleave where both
    ///     migrators read "absent", both attempt <c>CREATE DATABASE</c>, and the
    ///     second hits a primary-key conflict on <c>pg_database</c>.
    ///   </description></item>
    /// </list>
    /// </summary>
    /// <param name="ex">The <see cref="PostgresException"/> to classify.</param>
    /// <returns>
    /// <see langword="true"/> when the exception's <c>SqlState</c> is <c>23505</c>
    /// or <c>42P04</c>; <see langword="false"/> for all other codes.
    /// </returns>
    internal static bool IsBenignDuplicateDatabase(PostgresException ex)
        => ex.SqlState is "23505" or "42P04";

    // =========================================================================
    // Private helpers
    // =========================================================================

    /// <summary>
    /// Ensures the target database exists by connecting to the <c>postgres</c>
    /// maintenance database and issuing a CREATE DATABASE statement. Idempotent.
    /// </summary>
    private async Task EnsureDatabaseExistsAsync(CancellationToken ct)
    {
        var builder = new NpgsqlConnectionStringBuilder(r_connectionString);
        var targetDbNullable = builder.Database;
        if (targetDbNullable.Falsey())
        {
            throw new InvalidOperationException(
                "AdvisoryLockMigrator: connection string has no database name.");
        }

        var targetDb = targetDbNullable!;
        builder.Database = "postgres";
        var maintenanceConnStr = builder.ToString();

        await using var conn = new NpgsqlConnection(maintenanceConnStr);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        // Check existence first; CREATE DATABASE cannot be run in a transaction.
        await using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText =
            "SELECT 1 FROM pg_database WHERE datname = @db";
        checkCmd.Parameters.AddWithValue("db", targetDb);
        var exists = await checkCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);

        if (exists is null)
        {
            MigratorLog.CreatingDatabase(r_logger, targetDb);

            // Database name is from the connection string, not user input.
            // Use NpgsqlConnection.CreateCommand — identifier quoting applied manually
            // because parameterized DDL is not supported in PostgreSQL.
            await using var createCmd = conn.CreateCommand();
            createCmd.CommandText = $"CREATE DATABASE \"{targetDb.Replace("\"", "\"\"")}\"";

            try
            {
                await createCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                MigratorLog.DatabaseCreated(r_logger, targetDb);
            }
            catch (PostgresException pgEx) when (IsBenignDuplicateDatabase(pgEx))
            {
                // Another concurrent migrator created the database between our
                // existence check and this CREATE DATABASE statement.
                // 23505 = unique_violation (narrow interleave: both read "absent",
                //   both try INSERT into pg_database, second one hits PK conflict).
                // 42P04 = duplicate_database (common case: migrator A finishes
                //   CREATE DATABASE before migrator B's statement runs).
                // Either code means the database now exists — idempotent.
                MigratorLog.DatabaseAlreadyExists(r_logger, targetDb);
            }
        }
        else
        {
            MigratorLog.DatabaseAlreadyExists(r_logger, targetDb);
        }
    }

    // =========================================================================
    // [LoggerMessage] delegates — no Exception params (§3.1 PII safety)
    // =========================================================================
    private static partial class MigratorLog
    {
        [LoggerMessage(
            EventId = 1001,
            Level = LogLevel.Information,
            Message = "AdvisoryLockMigrator starting for context {ContextName}.")]
        public static partial void Starting(
            ILogger logger, string contextName);

        [LoggerMessage(
            EventId = 1002,
            Level = LogLevel.Debug,
            Message = "Migration advisory lock {LockKey} acquired.")]
        public static partial void LockAcquired(
            ILogger logger, long lockKey);

        [LoggerMessage(
            EventId = 1003,
            Level = LogLevel.Information,
            Message = "No pending migrations for {ContextName}.")]
        public static partial void NoPendingMigrations(
            ILogger logger, string contextName);

        [LoggerMessage(
            EventId = 1004,
            Level = LogLevel.Information,
            Message = "Applying pending migrations for {ContextName}.")]
        public static partial void ApplyingMigrations(
            ILogger logger, string contextName);

        [LoggerMessage(
            EventId = 1005,
            Level = LogLevel.Information,
            Message = "Migrations applied for {ContextName}.")]
        public static partial void MigrationsApplied(
            ILogger logger, string contextName);

        [LoggerMessage(
            EventId = 1006,
            Level = LogLevel.Information,
            Message = "Creating database '{DatabaseName}'.")]
        public static partial void CreatingDatabase(
            ILogger logger, string databaseName);

        [LoggerMessage(
            EventId = 1007,
            Level = LogLevel.Information,
            Message = "Database '{DatabaseName}' created.")]
        public static partial void DatabaseCreated(
            ILogger logger, string databaseName);

        [LoggerMessage(
            EventId = 1008,
            Level = LogLevel.Debug,
            Message = "Database '{DatabaseName}' already exists.")]
        public static partial void DatabaseAlreadyExists(
            ILogger logger, string databaseName);
    }
}
