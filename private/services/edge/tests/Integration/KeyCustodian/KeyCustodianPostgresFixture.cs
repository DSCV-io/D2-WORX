// -----------------------------------------------------------------------
// <copyright file="KeyCustodianPostgresFixture.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Integration.KeyCustodian;

using System;
using System.Threading.Tasks;
using DcsvIo.D2.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Persistence.Postgres;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

/// <summary>
/// Shared Testcontainers PostgreSQL fixture for the KeyCustodian live-DB suite.
/// One container per <see cref="KeyCustodianPostgresCollectionDefinition"/>; pinned
/// to the compose image tag via <see cref="POSTGRES_IMAGE"/>. The migration is
/// applied once on first use through <see cref="EnsureMigratedAsync"/>; tests use
/// unique kids to stay isolated without a schema reset.
/// </summary>
[MustDisposeResource(false)]
public sealed class KeyCustodianPostgresFixture : IAsyncLifetime
{
    /// <summary>
    /// The Testcontainers PostgreSQL image tag — pinned to the same tag the compose
    /// stack runs so the integration gate exercises the production engine version.
    /// </summary>
    public const string POSTGRES_IMAGE = "postgres:18.3-trixie";

    // TEST-INFRA: up to 3 startup attempts, 5 s backoff — guards against slow image
    // pulls and transient Docker hiccups on CI without retrying actual test logic.
    private const int _STARTUP_ATTEMPTS = 3;
    private const int _STARTUP_BACKOFF_MS = 5_000;

    private PostgreSqlContainer _container = BuildContainer();
    private volatile bool _migrated;

    /// <summary>Gets the Npgsql connection string for the running container.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// Builds a <see cref="KeyCustodianDbContext"/> over the running container using
    /// the same canonical Npgsql defaults the production DI seam applies.
    /// </summary>
    /// <returns>A new context (caller disposes).</returns>
    public KeyCustodianDbContext NewContext() => NewContextWithTimeout(commandTimeoutSeconds: 30);

    /// <summary>
    /// Builds a context with an explicit command timeout (used by the timeout
    /// classification test).
    /// </summary>
    /// <param name="commandTimeoutSeconds">The per-command timeout in seconds.</param>
    /// <returns>A new context (caller disposes).</returns>
    public KeyCustodianDbContext NewContextWithTimeout(int commandTimeoutSeconds)
    {
        var builder = new DbContextOptionsBuilder<KeyCustodianDbContext>();
        builder.ApplyD2NpgsqlDefaults(
            ConnectionString,
            commandTimeoutSeconds,
            migrationsAssemblyName: typeof(KeyCustodianDbContext).Assembly.GetName().Name!);
        return new KeyCustodianDbContext(builder.Options);
    }

    /// <summary>
    /// Applies the EF Core migration once for the whole collection (idempotent).
    /// Tests call this in their constructor / first step.
    /// </summary>
    /// <returns>A task that completes when the schema is present.</returns>
    public async Task EnsureMigratedAsync()
    {
        if (_migrated)
            return;

        await using var context = NewContext();
        await context.Database.MigrateAsync();
        _migrated = true;
    }

    private static PostgreSqlContainer BuildContainer() =>
        new PostgreSqlBuilder()
            .WithImage(POSTGRES_IMAGE)
            .Build();
}
