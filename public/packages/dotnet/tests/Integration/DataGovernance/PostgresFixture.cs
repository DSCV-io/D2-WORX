// -----------------------------------------------------------------------
// <copyright file="PostgresFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.DataGovernance;

using System;
using System.Threading.Tasks;
using DcsvIo.D2.Utilities.Extensions;
using JetBrains.Annotations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

/// <summary>
/// Shared Testcontainers PostgreSQL fixture. One container per xUnit collection.
/// <para>
/// Test classes that share a schema (advisory locks, index probes) use the shared
/// <see cref="ConnectionString"/>. Test classes that each call
/// <c>EnsureCreatedAsync</c> on a distinct <c>DbContext</c> type MUST call
/// <see cref="CreateIsolatedDatabaseAsync"/> in <c>InitializeAsync</c> to obtain a
/// per-class database, preventing schema-collision 42P01 errors caused by
/// <c>EnsureCreated</c> being a no-op on an already-existing database.
/// </para>
/// </summary>
[MustDisposeResource(false)]
public sealed class PostgresFixture : IAsyncLifetime
{
    // TEST-INFRA: up to 3 startup attempts, 5 s backoff — guards against slow image
    // pulls and transient Docker hiccups on CI without retrying actual test logic.
    private const int _STARTUP_ATTEMPTS = 3;
    private const int _STARTUP_BACKOFF_MS = 5_000;

    private PostgreSqlContainer _container = BuildContainer();

    /// <summary>Gets the Npgsql connection string for the running container.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        for (var attempt = 1; attempt <= _STARTUP_ATTEMPTS; attempt++)
        {
            try
            {
                await _container.StartAsync();
                return;
            }
            catch (Exception) when (attempt < _STARTUP_ATTEMPTS)
            {
                await _container.DisposeAsync();
                await Task.Delay(_STARTUP_BACKOFF_MS);
                _container = BuildContainer();
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// Provisions a new, uniquely-named database on the running container and returns
    /// its connection string. Each call with a distinct <paramref name="label"/> creates
    /// an isolated database, so concurrent test classes that each call
    /// <c>EnsureCreatedAsync</c> on their own <c>DbContext</c> build their full schema
    /// in separate databases with zero cross-class interference.
    /// </summary>
    /// <param name="label">
    /// A short human-readable label (e.g., the test class name). Combined with a
    /// short random suffix to form the database name. Must contain only ASCII letters,
    /// digits, and underscores; any other character is replaced with <c>_</c>.
    /// The combined name is truncated to 63 characters (Postgres identifier limit).
    /// </param>
    /// <returns>
    /// A Npgsql connection string that targets the newly created isolated database.
    /// </returns>
    public async Task<string> CreateIsolatedDatabaseAsync(string label)
    {
        var safe = MakeSafeDatabaseName(label);
        var dbName = safe + "_" + Guid.NewGuid().ToString("N")[..8];

        // Trim to 63 chars — Postgres max identifier length.
        if (dbName.Length > 63)
            dbName = dbName[..63];

        var adminCs = _container.GetConnectionString();

        await using var conn = new NpgsqlConnection(adminCs);
        await conn.OpenAsync();

        // CREATE DATABASE cannot run inside a transaction; NpgsqlConnection defaults to
        // AutoCommit mode so no explicit transaction management is needed here.
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE DATABASE \"" + dbName + "\"";
        await cmd.ExecuteNonQueryAsync();

        // Swap the database name in the container connection string.
        var builder = new NpgsqlConnectionStringBuilder(adminCs)
        {
            Database = dbName,
        };

        return builder.ConnectionString;
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    private static PostgreSqlContainer BuildContainer() =>
        new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .Build();

    private static string MakeSafeDatabaseName(string label)
    {
        if (label.Falsey())
            return "isolated";

        var chars = new char[label.Length];

        for (var i = 0; i < label.Length; i++)
        {
            var c = label[i];
            chars[i] = char.IsAsciiLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_';
        }

        return new string(chars);
    }
}
