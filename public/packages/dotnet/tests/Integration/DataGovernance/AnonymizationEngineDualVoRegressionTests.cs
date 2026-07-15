// -----------------------------------------------------------------------
// <copyright file="AnonymizationEngineDualVoRegressionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.DataGovernance;

using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Regression tests for the dual-VO navigation bug: <c>AnonymizationEngine</c> incorrectly
/// resolved both complex-property navigations to the first matching CLR type when an entity
/// maps the same complex-VO CLR type twice (e.g.
/// <c>DualVoHost { PrimaryAddress: SimpleAddress, SecondaryAddress: SimpleAddress }</c>).
/// <para>
/// Tier-A symptom: <c>ExecuteUpdateAsync</c> emitted duplicate SET columns
/// (<c>"PrimaryAddress_City"=…, "PrimaryAddress_City"=…</c>), causing Postgres to reject
/// the statement. Tier-B symptom: <c>SetPropertyValue</c> always wrote to
/// <c>PrimaryAddress</c> for both navigations, leaving <c>SecondaryAddress</c> untouched.
/// </para>
/// <para>
/// Fix: <c>BuildNavigationChain(IProperty)</c> walks the EF model chain
/// (<c>IProperty.DeclaringType → IComplexType.ComplexProperty →
/// IComplexProperty.DeclaringType</c>) instead of CLR-reflection-by-type, so each
/// property resolves to its exact, distinct navigation regardless of how many navigations
/// share the same CLR type.
/// </para>
/// </summary>
[Collection("Postgres")]
[Trait("Category", "Integration")]
public sealed class AnonymizationEngineDualVoRegressionTests : IAsyncLifetime
{
    private readonly PostgresFixture r_fixture;
    private readonly System.Collections.Generic.List<GovDbContext> r_engineContexts = [];

    private string r_connectionString = null!;
    private GovDbContext r_schemaCtx = null!;

    /// <summary>
    /// Initializes a new instance of
    /// <see cref="AnonymizationEngineDualVoRegressionTests"/>.
    /// </summary>
    /// <param name="fixture">The shared Postgres Testcontainers fixture.</param>
    public AnonymizationEngineDualVoRegressionTests(PostgresFixture fixture)
    {
        r_fixture = fixture;
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        // Each test class gets its own isolated database so EnsureCreatedAsync builds the
        // full schema without colliding with sibling classes in the same xUnit collection.
        r_connectionString = await r_fixture.CreateIsolatedDatabaseAsync(
            nameof(AnonymizationEngineDualVoRegressionTests));
        r_schemaCtx = GovDbContext.Build(r_connectionString);

        // Fresh isolated DB — EnsureCreatedAsync succeeds unconditionally and creates all
        // GovDbContext tables (TierAUsers, TierBUsers, OrgRecords, ExemptLedgers).
        // DualVoHosts and TierBDualVoHosts are added via raw IF NOT EXISTS DDL (they are
        // not in GovDbContext's model — the DDL is idempotent as a safety measure).
        await r_schemaCtx.Database.EnsureCreatedAsync();

        await r_schemaCtx.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""DualVoHosts"" (
                ""Id""                        uuid         NOT NULL PRIMARY KEY,
                ""UserId""                    uuid,
                ""PrimaryAddress_City""       text         NOT NULL,
                ""PrimaryAddress_ZipCode""    text,
                ""SecondaryAddress_City""     text         NOT NULL,
                ""SecondaryAddress_ZipCode""  text,
                ""IsAnonymized""              boolean      NOT NULL
            );");

        await r_schemaCtx.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""TierBDualVoHosts"" (
                ""Id""                        uuid         NOT NULL PRIMARY KEY,
                ""UserId""                    uuid,
                ""Email""                     text         NOT NULL,
                ""PrimaryAddress_City""       text         NOT NULL,
                ""PrimaryAddress_ZipCode""    text,
                ""SecondaryAddress_City""     text         NOT NULL,
                ""SecondaryAddress_ZipCode""  text,
                ""IsAnonymized""              boolean      NOT NULL
            );");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var ctx in r_engineContexts)
            await ctx.DisposeAsync();

        await r_schemaCtx.DisposeAsync();
        AnonymizationTierClassifier.ClearCache();
    }

    // =========================================================================
    // Dual-VO regression — Tier-A: same complex VO mapped twice → independent tombstones
    // =========================================================================

    // long identifier — cannot wrap
    [Fact]
    public async Task Anonymize_EntityMappingSameComplexVoTwice_TombstonesBothIndependently()
    {
        var userId = Guid.NewGuid();

        await SeedDualVoHost(
            userId,
            primaryCity: "Springfield",
            primaryZip: "00001",
            secondaryCity: "Shelbyville",
            secondaryZip: "00002");

        // Pre-erasure: assert the two address slots are genuinely distinct so the test
        // is not tautological — seeds distinct values, not the same value twice.
        await using var preCtx = GovDbContext.Build(r_connectionString);
        var pre = await preCtx.DualVoHosts.FirstAsync(h => h.UserId == userId);
        pre.PrimaryAddress.City.Should().Be("Springfield", "primary city pre-erasure");
        pre.SecondaryAddress.City.Should().Be(
            "Shelbyville",
            "secondary city pre-erasure — distinct");

        // The buggy engine produced duplicate SET columns in SQL
        // ("PrimaryAddress_City"=…, "PrimaryAddress_City"=…) and Postgres rejected the
        // statement. With the fix, each navigation resolves to its own column.
        var engine = BuildEngine();
        var result = await engine.AnonymizeUserAsync(userId);

        // (a) No SQL error: the engine must succeed.
        result.Success.Should().BeTrue(
            "duplicate-column SQL error must be gone (dual-VO Tier-A fix)");
        result.Data!.RowsAnonymized.Should().Be(1);

        await using var readCtx = GovDbContext.Build(r_connectionString);
        var row = await readCtx.DualVoHosts.FirstAsync(h => h.UserId == userId);

        // (b) Both slots tombstoned independently — no cross-leak.
        row.PrimaryAddress.City.Should().Be(
            "[deleted]",
            "primary city tombstoned with its own constant");
        row.PrimaryAddress.ZipCode.Should().BeNull("primary zip tombstoned to null");

        row.SecondaryAddress.City.Should().Be(
            "[deleted-secondary]",
            "secondary city tombstoned with its OWN distinct constant — proves no cross-leak");
        row.SecondaryAddress.ZipCode.Should().BeNull("secondary zip tombstoned to null");

        // (c) IsAnonymized flagged.
        row.IsAnonymized.Should().BeTrue();
    }

    // =========================================================================
    // Dual-VO regression — Tier-B: same complex VO mapped twice → independent in-memory writes
    // =========================================================================

    // long identifier — cannot wrap
    [Fact]
    public async Task Anonymize_EntityMappingSameComplexVoTwice_TierB_TombstonesBothIndependently()
    {
        // Tier-B is triggered by a Template rule. We map the DualVoHost via a
        // DualVoTierBHost that extends the scenario with a Template field, forcing the
        // engine to materialize and mutate in CLR (SetPropertyValue path). Because
        // GovDbContext only has DualVoHost (Tier-A), we drive Tier-B via TierBUser with
        // a TierBDualVoHost extension below. Instead of adding a whole new entity, we
        // exercise Tier-B through the existing TierBUser and separately pin the
        // SetPropertyValue path via a unit-observable side effect.
        //
        // Simplest approach: seed a DualVoHost row, run Tier-A (already tested above),
        // and then verify SetPropertyValue by directly invoking the engine on an
        // in-memory DualVoHost instance via the AnonymizationEngine's Tier-B code path.
        // Because DualVoHost has no Template field it goes Tier-A; to test Tier-B CLR
        // mutation we build a TierBDualVoHost that wraps SimpleAddress twice + adds a
        // Template field so the engine materializes it.

        var userId = Guid.NewGuid();

        await using var writeCtx = GovDbContext.Build(r_connectionString);
        writeCtx.TierBDualVoHosts.Add(new GovDbContext.TierBDualVoHost
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Email = "original@example.test",
            PrimaryAddress = new GovDbContext.SimpleAddress
            {
                City = "PrimaryCity",
                ZipCode = "11111",
            },
            SecondaryAddress = new GovDbContext.SimpleAddress
            {
                City = "SecondaryCity",
                ZipCode = "22222",
            },
        });
        await writeCtx.SaveChangesAsync();

        // Pre-erasure: confirm distinct values to rule out tautology.
        await using var preCtx = GovDbContext.Build(r_connectionString);
        var pre = await preCtx.TierBDualVoHosts.FirstAsync(h => h.UserId == userId);
        pre.PrimaryAddress.City.Should().Be("PrimaryCity");
        pre.SecondaryAddress.City.Should().Be("SecondaryCity");

        var engine = BuildEngine();
        var result = await engine.AnonymizeUserAsync(userId);

        // (a) No error on Tier-B materialize-mutate path.
        result.Success.Should().BeTrue(
            "Tier-B SetPropertyValue must not cross-write (dual-VO fix)");
        result.Data!.RowsAnonymized.Should().Be(1);

        await using var readCtx = GovDbContext.Build(r_connectionString);
        var row = await readCtx.TierBDualVoHosts.FirstAsync(h => h.UserId == userId);

        // (b) Both slots tombstoned to their own values — no cross-leak.
        row.PrimaryAddress.City.Should().Be(
            "[deleted]",
            "primary city tombstoned independently");
        row.PrimaryAddress.ZipCode.Should().BeNull("primary zip nulled");

        row.SecondaryAddress.City.Should().Be(
            "[deleted-secondary]",
            "secondary city tombstoned independently (no dual-VO CLR cross-write)");
        row.SecondaryAddress.ZipCode.Should().BeNull("secondary zip nulled");

        // Email was template-resolved.
        var expectedEmail = $"deletedUser{userId:N}@deleted.user.dcsv.io";
        row.Email.Should().Be(expectedEmail);

        // (c) IsAnonymized flagged.
        row.IsAnonymized.Should().BeTrue();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task SeedDualVoHost(
        Guid userId,
        string primaryCity,
        string primaryZip,
        string secondaryCity,
        string secondaryZip)
    {
        await using var ctx = GovDbContext.Build(r_connectionString);
        ctx.DualVoHosts.Add(new GovDbContext.DualVoHost
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PrimaryAddress = new GovDbContext.SimpleAddress
            {
                City = primaryCity,
                ZipCode = primaryZip,
            },
            SecondaryAddress = new GovDbContext.SimpleAddress
            {
                City = secondaryCity,
                ZipCode = secondaryZip,
            },
        });
        await ctx.SaveChangesAsync();
    }

    private AnonymizationEngine BuildEngine(int batchSize = 500)
    {
        var opts = Options.Create(new AnonymizationEngineOptions { BatchSize = batchSize });
        var ctx = GovDbContext.Build(r_connectionString);
        r_engineContexts.Add(ctx);
        return new AnonymizationEngine(ctx, opts, NullLogger<AnonymizationEngine>.Instance);
    }
}
