// -----------------------------------------------------------------------
// <copyright file="CreateD2IndexIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.SharedEntityFrameworkCore;

using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.EntityFrameworkCore;
using DcsvIo.D2.Location.ValueObjects;
using DcsvIo.D2.Tests.Integration.DataGovernance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

/// <summary>
/// Live-DB integration tests for <see cref="CreateD2IndexExtensions.CreateD2Index{TEntity}"/>.
/// Shares the <see cref="PostgresFixture"/> container
/// (see <c>PostgresCollectionDefinition</c>).
/// <para>
/// These tests prove the full production-relevant chain:
/// typed member expression → <c>DeriveColumnName</c> →
/// <c>CreateIndexOperation</c> → Npgsql <c>IMigrationsSqlGenerator</c> → real Postgres →
/// <c>pg_indexes</c> catalog metadata. They close the gap that the unit suite
/// (<c>Unit/SharedEntityFrameworkCore/CreateD2IndexExtensionsTests</c>) cannot cover:
/// that the emitted operation actually produces valid DDL and creates a real index on the
/// real EF-derived column.
/// </para>
/// </summary>
[Collection("Postgres")]
[Trait("Category", "Integration")]
public sealed class CreateD2IndexIntegrationTests : IAsyncLifetime
{
    private const string _TABLE = "CreateD2IndexProbe";
    private const string _IDX_DEFAULT = "IX_CreateD2IndexProbe_Geo_Latitude";
    private const string _IDX_CUSTOM = "UX_probe_geo_lat_custom";
    private const string _IDX_UNIQUE = "UX_probe_geo_lat_unique";
    private const string _IDX_LON = "IX_probe_geo_lon_nonuniq";

    private readonly PostgresFixture r_fixture;
    private IndexHostDbContext r_ctx = null!;

    /// <summary>
    /// Initializes a new instance of <see cref="CreateD2IndexIntegrationTests"/>.
    /// </summary>
    /// <param name="fixture">The shared Postgres Testcontainers fixture.</param>
    public CreateD2IndexIntegrationTests(PostgresFixture fixture)
    {
        r_fixture = fixture;
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        // GovDbContext.EnsureCreatedAsync creates the database when no other test in the
        // collection has done so yet; idempotent when the DB already exists. Same ordering
        // pattern as ContactHostIntegrationTests.
        await using var govCtx = GovDbContext.Build(r_fixture.ConnectionString);
        await govCtx.Database.EnsureCreatedAsync();

        await IndexHostDbContext.EnsureSchemaAsync(r_fixture.ConnectionString);
        r_ctx = IndexHostDbContext.Build(r_fixture.ConnectionString);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Drop all four test indexes (idempotent — safe on re-run).
        // Explicit per-constant DROP avoids SQL injection warnings from interpolation.
        await r_ctx.Database.ExecuteSqlRawAsync(
            "DROP INDEX IF EXISTS \"" + _IDX_DEFAULT + "\"");

        await r_ctx.Database.ExecuteSqlRawAsync(
            "DROP INDEX IF EXISTS \"" + _IDX_CUSTOM + "\"");

        await r_ctx.Database.ExecuteSqlRawAsync(
            "DROP INDEX IF EXISTS \"" + _IDX_UNIQUE + "\"");

        await r_ctx.Database.ExecuteSqlRawAsync(
            "DROP INDEX IF EXISTS \"" + _IDX_LON + "\"");

        await r_ctx.DisposeAsync();
    }

    // =========================================================================
    // Case 1 — derived default name, non-unique
    // =========================================================================

    [Fact]
    public async Task
        CreateD2Index_derived_default_name_creates_non_unique_index_on_Geo_Latitude()
    {
        await DropAndCreateIndexAsync(
            _IDX_DEFAULT,
            mb => mb.CreateD2Index<IndexHostDbContext.CreateD2IndexProbe>(
                _TABLE, x => x.Geo.Latitude));

        var row = await QueryIndexAsync(_IDX_DEFAULT);

        row.Should().NotBeNull("index must exist in pg_indexes");
        row.IndexDef.Should().Contain(
            "\"Geo_Latitude\"",
            "indexdef must reference the EF-derived Geo_Latitude column");
        row.IndexDef.Should().Contain(
            "CREATE INDEX",
            "non-unique index must start with CREATE INDEX, not CREATE UNIQUE INDEX");
        row.IndexDef.Should().NotContain(
            "UNIQUE",
            "unique:false must not produce a UNIQUE index");

        // Pin the EF-derived column name ↔ DeriveColumnName contract end-to-end.
        // If EF ever changes its complex-column naming convention, this assertion catches it.
        var efColumnName = GetEfColumnName();
        efColumnName.Should().Be(
            "Geo_Latitude",
            "EF complex-column default naming must produce Geo_Latitude for"
            + " a host property 'Geo' with member 'Latitude'");
    }

    // =========================================================================
    // Case 2 — explicit name override, non-unique
    // =========================================================================

    [Fact]
    public async Task
        CreateD2Index_explicit_name_override_creates_index_with_supplied_name()
    {
        await DropAndCreateIndexAsync(
            _IDX_CUSTOM,
            mb => mb.CreateD2Index<IndexHostDbContext.CreateD2IndexProbe>(
                _TABLE,
                x => x.Geo.Latitude,
                name: _IDX_CUSTOM));

        var row = await QueryIndexAsync(_IDX_CUSTOM);
        var absent = await QueryIndexAsync(_IDX_DEFAULT);

        row.Should().NotBeNull("explicitly-named index must exist in pg_indexes");
        row.IndexDef.Should().Contain("\"Geo_Latitude\"");
        row.IndexDef.Should().NotContain(
            "UNIQUE",
            "unique:false must not produce a UNIQUE index");

        absent.Should().BeNull(
            "default-named index must NOT be created when name: is supplied");
    }

    // =========================================================================
    // Case 3 — unique:true
    // =========================================================================

    [Fact]
    public async Task CreateD2Index_unique_true_creates_unique_index()
    {
        // Seed two rows with distinct latitudes so the UNIQUE index build succeeds on
        // real data (proves the index is functional, not just declared).
        await using var seedCtx = IndexHostDbContext.Build(r_fixture.ConnectionString);
        seedCtx.Set<IndexHostDbContext.CreateD2IndexProbe>().AddRange(
            new IndexHostDbContext.CreateD2IndexProbe
            {
                Id = Guid.NewGuid(),
                Geo = Coordinates.Create(0.0, 0.0).Data!,
            },
            new IndexHostDbContext.CreateD2IndexProbe
            {
                Id = Guid.NewGuid(),
                Geo = Coordinates.Create(1.0, 1.0).Data!,
            });
        await seedCtx.SaveChangesAsync();

        await DropAndCreateIndexAsync(
            _IDX_UNIQUE,
            mb => mb.CreateD2Index<IndexHostDbContext.CreateD2IndexProbe>(
                _TABLE,
                x => x.Geo.Latitude,
                name: _IDX_UNIQUE,
                unique: true));

        var row = await QueryIndexAsync(_IDX_UNIQUE);

        row.Should().NotBeNull("unique index must exist in pg_indexes");
        row.IndexDef.Should().Contain(
            "CREATE UNIQUE INDEX",
            "unique:true must produce CREATE UNIQUE INDEX DDL");
        row.IndexDef.Should().Contain("\"Geo_Latitude\"");
    }

    // =========================================================================
    // Case 4 — distinct column Geo.Longitude, unique:false
    // =========================================================================

    [Fact]
    public async Task
        CreateD2Index_Geo_Longitude_creates_distinct_column_non_unique_index()
    {
        await DropAndCreateIndexAsync(
            _IDX_LON,
            mb => mb.CreateD2Index<IndexHostDbContext.CreateD2IndexProbe>(
                _TABLE,
                x => x.Geo.Longitude,
                name: _IDX_LON,
                unique: false));

        var row = await QueryIndexAsync(_IDX_LON);

        row.Should().NotBeNull("Geo_Longitude index must exist in pg_indexes");
        row.IndexDef.Should().Contain(
            "\"Geo_Longitude\"",
            "member-chain Geo.Longitude must produce a Geo_Longitude column,"
            + " not Geo_Latitude — proves derivation is not hard-wired to Latitude");
        row.IndexDef.Should().NotContain(
            "UNIQUE",
            "unique:false must not produce a UNIQUE index");
        row.IndexDef.Should().Contain("CREATE INDEX");
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    /// <summary>
    /// Drops the named index (if it exists) then runs the supplied
    /// <paramref name="configure"/> callback against a fresh <see cref="MigrationBuilder"/>,
    /// converts the resulting operations to real Npgsql DDL, and executes each statement
    /// against the live Postgres instance.
    /// </summary>
    private async Task DropAndCreateIndexAsync(
        string indexName,
        Action<MigrationBuilder> configure)
    {
        // DDL cannot use SQL parameters (Postgres does not parameterize DDL identifiers).
        // Index names are our own constants — not user input. Using a local variable so
        // the EF SQL-injection analyzers (EF1002/EF1003) do not fire on concatenation.
        string dropSql = "DROP INDEX IF EXISTS \"" + indexName + "\"";
        await r_ctx.Database.ExecuteSqlRawAsync(dropSql);

        var mb = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        configure(mb);

        var sqlGen = r_ctx.Database.GetService<IMigrationsSqlGenerator>();
        var commands = sqlGen.Generate(mb.Operations, model: null);

        foreach (var cmd in commands)
        {
            string ddl = cmd.CommandText;
            await r_ctx.Database.ExecuteSqlRawAsync(ddl);
        }
    }

    /// <summary>
    /// Queries <c>pg_indexes</c> for a single row matching
    /// <paramref name="indexName"/> on the probe table.
    /// Returns <see langword="null"/> when no matching row exists.
    /// </summary>
    private async Task<IndexRow?> QueryIndexAsync(string indexName)
    {
        // SqlQuery<T> uses FormattableString holes as SQL parameters — the {expr} values
        // are passed as $1/$2 bind parameters, not as string-interpolated SQL text.
        // Do NOT wrap the holes in SQL quotes: EF handles the parameterization.
        // A single $"..." expression is required (concatenation produces string, not
        // FormattableString). Raw string literal used to avoid escaped inner-quote noise.
        var tableName = _TABLE;
        var rows = await r_ctx.Database
            .SqlQuery<IndexRow>($"""
                SELECT indexdef AS "IndexDef"
                FROM pg_indexes
                WHERE tablename = {tableName}
                AND indexname = {indexName}
                """)
            .ToListAsync();

        return rows.Count == 0 ? null : rows[0];
    }

    /// <summary>
    /// Reads back the EF-derived column name for <c>Geo.Latitude</c> from the
    /// built <see cref="IndexHostDbContext"/> model so the derivation contract is
    /// pinned end-to-end: if EF Core ever changes its complex-column naming convention,
    /// this assertion catches the divergence immediately.
    /// </summary>
    private string GetEfColumnName()
    {
        var entityType = r_ctx.Model.FindEntityType(
            typeof(IndexHostDbContext.CreateD2IndexProbe))!;

        var complexProp = entityType.FindComplexProperty(
            nameof(IndexHostDbContext.CreateD2IndexProbe.Geo))!;

        var latProp = complexProp.ComplexType.FindProperty(
            nameof(Coordinates.Latitude))!;

        // GetColumnName(StoreObjectIdentifier) returns the full physical column name
        // including the EF complex-type prefix (e.g. "Geo_Latitude"), not just the
        // property name within the complex type ("Latitude"). Use the entity's mapped
        // table as the store-object context.
        var tableIdentifier = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier
            .Table(entityType.GetTableName()!, entityType.GetSchema());

        return latProp.GetColumnName(tableIdentifier)!;
    }

    // =========================================================================
    // Read DTO for pg_indexes catalog query
    // =========================================================================

    // ReSharper disable once ClassNeverInstantiated.Local
    private sealed class IndexRow
    {
        // Property populated by EF SqlQuery via column-name matching.
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        public string IndexDef { get; init; } = string.Empty;
    }
}
