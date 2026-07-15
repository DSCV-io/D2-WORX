// -----------------------------------------------------------------------
// <copyright file="IndexHostDbContext.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.SharedEntityFrameworkCore;

using DcsvIo.D2.Location.EntityFrameworkCore;
using DcsvIo.D2.Location.ValueObjects;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Minimal synthetic host <c>DbContext</c> for <c>CreateD2Index</c> live-DB
/// integration tests. Holds one entity (<see cref="CreateD2IndexProbe"/>) whose
/// <c>Geo</c> complex property produces <c>Geo_Latitude</c> and <c>Geo_Longitude</c>
/// columns via <c>MapCoordinates()</c> — the target columns for the four index cases.
/// Schema created via <c>EnsureSchemaAsync</c> (IF NOT EXISTS DDL) — no migrations.
/// </summary>
internal sealed class IndexHostDbContext : DbContext
{
    internal IndexHostDbContext(DbContextOptions<IndexHostDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Builds and returns an <see cref="IndexHostDbContext"/> for the given connection
    /// string.
    /// </summary>
    /// <param name="connectionString">
    /// Npgsql connection string for the Testcontainers PostgreSQL instance.
    /// </param>
    internal static IndexHostDbContext Build(string connectionString)
    {
        var options = new DbContextOptionsBuilder<IndexHostDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new IndexHostDbContext(options);
    }

    /// <summary>
    /// Creates the <c>CreateD2IndexProbe</c> table in the shared Postgres database using
    /// <c>CREATE TABLE IF NOT EXISTS</c> DDL generated from the EF model. The EF model
    /// declares NO index on <c>Geo_Latitude</c> or <c>Geo_Longitude</c> (the EF Core 10
    /// complex-member-index limitation is the entire reason <c>CreateD2Index</c> exists);
    /// indexes are created exclusively by the test suite via <c>CreateD2Index</c>.
    /// Must be called AFTER the database has been created (e.g. via
    /// <c>GovDbContext.EnsureCreatedAsync</c>), because this method does not create the
    /// database itself.
    /// </summary>
    /// <param name="connectionString">
    /// Npgsql connection string for the Testcontainers PostgreSQL instance.
    /// </param>
    internal static async Task EnsureSchemaAsync(string connectionString)
    {
        await using var ctx = Build(connectionString);

        // Generate the full create script from the EF model, then transform each
        // CREATE TABLE / CREATE INDEX / CREATE UNIQUE INDEX into the IF NOT EXISTS
        // forms so repeated calls are idempotent. The EF Npgsql script emits these
        // tokens verbatim without extra whitespace, so literal replacements are safe.
        var script = ctx.Database.GenerateCreateScript();
        var transformed = script
            .Replace(
                "CREATE UNIQUE INDEX ",
                "CREATE UNIQUE INDEX IF NOT EXISTS ",
                System.StringComparison.Ordinal)
            .Replace(
                "CREATE INDEX ",
                "CREATE INDEX IF NOT EXISTS ",
                System.StringComparison.Ordinal)
            .Replace(
                "CREATE TABLE ",
                "CREATE TABLE IF NOT EXISTS ",
                System.StringComparison.Ordinal);

        await ctx.Database.ExecuteSqlRawAsync(transformed);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CreateD2IndexProbe>(b =>
        {
            b.HasKey(x => x.Id);
            b.ToTable("CreateD2IndexProbe");
            b.ComplexProperty(x => x.Geo, cp => cp.MapCoordinates());
        });
    }

    // =========================================================================
    // Entity definition (nested; test-infrastructure-only)
    // =========================================================================

    /// <summary>
    /// Minimal probe entity for <c>CreateD2Index</c> live-DB assertions.
    /// The <c>Geo</c> complex property maps to <c>Geo_Latitude</c>, <c>Geo_Longitude</c>,
    /// and other <c>Geo_*</c> columns via <c>MapCoordinates()</c> — the columns targeted
    /// by the integration-test index cases.
    /// </summary>
    internal sealed class CreateD2IndexProbe
    {
        public Guid Id { get; set; }

        public Coordinates Geo { get; set; } = null!;
    }
}
