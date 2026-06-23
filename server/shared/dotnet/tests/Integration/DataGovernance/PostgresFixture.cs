// -----------------------------------------------------------------------
// <copyright file="PostgresFixture.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.DataGovernance;

using System;
using System.Threading.Tasks;
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
    private readonly PostgreSqlContainer r_container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    /// <summary>Gets the Npgsql connection string for the running container.</summary>
    public string ConnectionString => r_container.GetConnectionString();

    /// <inheritdoc />
    public async ValueTask InitializeAsync() => await r_container.StartAsync();

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await r_container.DisposeAsync();

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

        var adminCs = r_container.GetConnectionString();

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

    private static string MakeSafeDatabaseName(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
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
