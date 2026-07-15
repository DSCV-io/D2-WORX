// -----------------------------------------------------------------------
// <copyright file="AnonymizationEngineTierAConverterRegressionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.DataGovernance;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Contacts.ValueObjects;
using DcsvIo.D2.DataGovernance.Abstractions;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Regression tests: Tier-A <c>BuildOneSetterCall</c> used raw
/// <c>Convert.ChangeType</c> to build the value constant. For a value-converted
/// column (<c>EmailAddress?</c> stored as <c>string</c>) with a <c>Constant</c>
/// tombstone, the old code threw <see cref="InvalidCastException"/> inside
/// <c>Expression.Constant</c> because <c>EmailAddress</c> is not
/// <see cref="IConvertible"/>; the catch silently swallowed the error and skipped
/// the column, leaving PII unerased.
/// <para>
/// Fix: <c>BuildOneSetterCall</c> now calls
/// <c>ConvertValue(value, propertyClrType, efProperty)</c> — the same helper
/// Tier-B uses — which applies the EF value-converter's
/// <c>ConvertFromProvider</c> to turn the raw tombstone string into an
/// <c>EmailAddress</c> instance before creating the <c>Expression.Constant</c>.
/// EF's <c>ExecuteUpdateAsync</c> then calls <c>ConvertToProvider</c> on the
/// resulting <c>EmailAddress</c> to get the correct store string.
/// </para>
/// <para>
/// This test MUST fail without the fix (the old code swallows the
/// <see cref="InvalidCastException"/> and skips the setter, so the column is
/// never overwritten) and MUST pass with it.
/// </para>
/// </summary>
[Collection("Postgres")]
[Trait("Category", "Integration")]
public sealed class AnonymizationEngineTierAConverterRegressionTests : IAsyncLifetime
{
    private readonly PostgresFixture r_fixture;
    private readonly List<DbContext> r_engineContexts = [];

    private string r_connectionString = null!;
    private TierAConverterDbContext r_schemaCtx = null!;

    /// <summary>
    /// Initializes a new instance of
    /// <see cref="AnonymizationEngineTierAConverterRegressionTests"/>.
    /// </summary>
    /// <param name="fixture">The shared Postgres Testcontainers fixture.</param>
    public AnonymizationEngineTierAConverterRegressionTests(PostgresFixture fixture)
    {
        r_fixture = fixture;
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        // Each test class gets its own isolated database so EnsureCreatedAsync builds the
        // full schema without colliding with sibling classes in the same xUnit collection.
        r_connectionString = await r_fixture.CreateIsolatedDatabaseAsync(
            nameof(AnonymizationEngineTierAConverterRegressionTests));
        r_schemaCtx = TierAConverterDbContext.Build(r_connectionString);

        // Fresh isolated DB — EnsureCreatedAsync succeeds unconditionally.
        // TierAConverterHosts is added via IF NOT EXISTS DDL as a belt-and-suspenders
        // measure (TierAConverterDbContext only contains that one entity type, so
        // EnsureCreated would create it anyway — but the explicit DDL is idempotent).
        await r_schemaCtx.Database.EnsureCreatedAsync();
        await r_schemaCtx.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""TierAConverterHosts"" (
                ""Id""           uuid  NOT NULL PRIMARY KEY,
                ""UserId""       uuid,
                ""Email""        text,
                ""IsAnonymized"" boolean NOT NULL
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
    // Regression: Tier-A value-converted Constant tombstone is written to DB
    // =========================================================================

    /// <summary>
    /// Seeds a row with a real <c>EmailAddress?</c> value, runs <c>AnonymizeUserAsync</c>,
    /// and then reads back the raw column via parameterized SQL to confirm the constant
    /// tombstone string was written. Without the fix, <c>BuildOneSetterCall</c>
    /// catches the <see cref="InvalidCastException"/> from <c>Convert.ChangeType</c> and
    /// calls <c>SetterCallResult.Skip()</c>, meaning the Email column is omitted from the
    /// <c>ExecuteUpdateAsync</c> SET clause and the original value remains in the DB —
    /// the assertion <c>raw.Should().Be("deleted@deleted.user.dcsv.io")</c> would fail.
    /// </summary>
    [Fact]
    public async Task
        AnonymizeUserAsync_TierA_value_converted_Constant_column_is_erased_to_tombstone()
    {
        var userId = Guid.NewGuid();
        const string seed_email = "alice@example.com";
        const string expected_tombstone = "deleted@deleted.user.dcsv.io";

        // Seed via the DbContext so the value converter writes the correct store string.
        await using var writeCtx = TierAConverterDbContext.Build(r_connectionString);
        writeCtx.TierAConverterHosts.Add(new TierAConverterDbContext.TierAConverterHost
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Email = EmailAddress.FromTrusted(seed_email),
        });
        await writeCtx.SaveChangesAsync();

        // Confirm the seed value is present before erasure (non-tautology guard).
        await using var preCtx = TierAConverterDbContext.Build(r_connectionString);
        var pre = await preCtx.TierAConverterHosts.FirstAsync(h => h.UserId == userId);
        pre.Email!.Value.Should().Be(seed_email, "seed must be present before erasure");

        // Run the engine — the entity has NO Template rule → classified Tier-A.
        var engine = BuildEngine();
        var result = await engine.AnonymizeUserAsync(userId);

        result.Success.Should().BeTrue("Tier-A sweep must succeed");
        result.Data!.RowsAnonymized.Should().Be(1);

        // Read back the raw DB column via SQL to bypass any CLR value-converter
        // re-hydration that might mask a failure where the store string was not updated.
        await using var rawCtx = TierAConverterDbContext.Build(r_connectionString);
        string? raw = await rawCtx.Database
            .SqlQueryRaw<string>(
                @"SELECT ""Email"" AS ""Value""
                  FROM ""TierAConverterHosts""
                  WHERE ""UserId"" = {0}",
                userId)
            .FirstOrDefaultAsync();

        raw.Should().Be(
            expected_tombstone,
            "Tier-A ExecuteUpdate must have SET the Email column to the constant tombstone "
            + "via ConvertValue — old Convert.ChangeType path would skip the column silently");

        // Also verify IsAnonymized is set (belt-and-braces: engine set ALL columns).
        await using var flagCtx = TierAConverterDbContext.Build(r_connectionString);
        var row = await flagCtx.TierAConverterHosts.FirstAsync(h => h.UserId == userId);
        row.IsAnonymized.Should().BeTrue();
        row.Email!.Value.Should().Be(expected_tombstone);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private AnonymizationEngine BuildEngine(int batchSize = 500)
    {
        var opts = Options.Create(new AnonymizationEngineOptions { BatchSize = batchSize });
        var ctx = TierAConverterDbContext.Build(r_connectionString);
        r_engineContexts.Add(ctx);
        return new AnonymizationEngine(ctx, opts, NullLogger<AnonymizationEngine>.Instance);
    }

    // =========================================================================
    // TierAConverterDbContext — isolated context for the value-converted regression
    // =========================================================================

    /// <summary>
    /// Minimal DbContext for the value-converted Constant tombstone regression.
    /// Contains ONLY <see cref="TierAConverterHost"/> — a single entity with ONE
    /// value-converted PII column (<c>EmailAddress?</c> stored as <c>string</c>)
    /// decorated with a <see cref="AnonymizeKind.Constant"/> tombstone and NO
    /// Template rule. The absence of any Template rule forces the engine to
    /// classify it Tier-A and use <c>ExecuteUpdateAsync</c> — the exact path where
    /// the old <c>Convert.ChangeType</c> code silently skipped the column.
    /// </summary>
    private sealed class TierAConverterDbContext : DbContext
    {
        private TierAConverterDbContext(DbContextOptions<TierAConverterDbContext> options)
            : base(options)
        {
        }

        public DbSet<TierAConverterHost> TierAConverterHosts => Set<TierAConverterHost>();

        internal static TierAConverterDbContext Build(string connectionString)
        {
            var options = new DbContextOptionsBuilder<TierAConverterDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            return new TierAConverterDbContext(options);
        }

        protected override void ConfigureConventions(
            ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.ApplyAnonymizationConventions();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TierAConverterHost>(e =>
            {
                e.HasKey(x => x.Id);

                // Wire the EmailAddress ↔ string value converter explicitly.
                // Write the Constant tombstone annotation directly (avoids the
                // PropertyBuilder<T> where T:notnull constraint on the .Anonymize()
                // extension when T is EmailAddress? — same approach as EmailMapping).
                // Constant tombstone — forces Tier-A (no Template rule on this entity).
                var emailProp = e.Property(x => x.Email);
                emailProp
                 .HasConversion(
                     v => v == null ? null : v.Value,
                     s => s == null ? null : EmailAddress.FromTrusted(s))
                 .HasMaxLength(254)
                 .HasAnnotation(
                     AnonymizationAnnotations.ANONYMIZE,
                     AnonymizationRule.Create(
                         AnonymizeKind.Constant,
                         constantValue: "deleted@deleted.user.dcsv.io"));
            });
        }

        /// <summary>
        /// Minimal Tier-A entity: one value-converted <c>EmailAddress?</c> column
        /// decorated with a Constant tombstone. No Template field — forces Tier-A.
        /// </summary>
        internal sealed class TierAConverterHost : IUserOwned, IAnonymizationTrackable
        {
            public Guid Id { get; set; }

            public Guid? UserId { get; set; }

            /// <summary>
            /// Gets or sets the email address. Value-converted (CLR: <c>EmailAddress?</c>;
            /// store: <c>text</c>). Decorated with a Constant tombstone to exercise the
            /// value-converted column path in <c>BuildOneSetterCall</c>.
            /// </summary>
            public EmailAddress? Email { get; set; }

            /// <inheritdoc />
            public bool IsAnonymized { get; set; }
        }
    }
}
