// -----------------------------------------------------------------------
// <copyright file="NpgsqlContextDefaults.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EntityFrameworkCore.Postgres;

using DcsvIo.D2.Time.EfCore;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Canonical D2 Npgsql defaults applier for <see cref="DbContextOptionsBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// Apply from both the runtime DI registration (<c>AddDbContext</c> lambda) AND the
/// design-time <see cref="DesignTimeDbContextFactoryBase{TContext}"/> so the two
/// paths can never drift.
/// </para>
/// <para>
/// <strong>No <c>EnableRetryOnFailure</c>.</strong> An execution strategy's silent
/// reconnect drops a session advisory lock mid-critical-section, turning a hard
/// multi-replica correctness guarantee into a silent data race. The deliberate
/// absence of a retry strategy is a correctness decision, not an omission — services
/// that need advisory locks must handle transient failures at the application level
/// (restart the host, skip the tick, etc.).
/// </para>
/// </remarks>
public static class NpgsqlContextDefaults
{
    extension(DbContextOptionsBuilder builder)
    {
        /// <summary>
        /// Applies the canonical D2 Npgsql settings: <c>UseNpgsql</c> with
        /// <c>AddD2NodaTime()</c>, <c>CommandTimeout</c>, and
        /// <c>MigrationsAssembly</c>. No <c>EnableRetryOnFailure</c> — see
        /// remarks on <see cref="NpgsqlContextDefaults"/>.
        /// </summary>
        /// <param name="connectionString">The Npgsql connection string.</param>
        /// <param name="commandTimeoutSeconds">
        /// Per-command timeout in seconds.
        /// </param>
        /// <param name="migrationsAssemblyName">
        /// Name of the assembly that owns the EF Core migrations. Required for
        /// module-within-host services whose migrations live in a different
        /// assembly than the <see cref="DbContext"/>.
        /// </param>
        /// <returns>The same builder for chaining.</returns>
        public DbContextOptionsBuilder ApplyD2NpgsqlDefaults(
            string connectionString,
            int commandTimeoutSeconds,
            string migrationsAssemblyName)
        {
            builder.UseNpgsql(
                connectionString,
                npgsql =>
                {
                    npgsql.AddD2NodaTime();
                    npgsql.CommandTimeout(commandTimeoutSeconds);
                    npgsql.MigrationsAssembly(migrationsAssemblyName);

                    // EnableRetryOnFailure is intentionally ABSENT — see class remarks.
                });

            return builder;
        }
    }
}
