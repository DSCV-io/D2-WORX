// -----------------------------------------------------------------------
// <copyright file="AnonymizationEngineGapTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.DataGovernance;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.DataGovernance.Abstractions;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Coverage gap-check integration tests for the anonymization engine.
/// Each gap targets a specific engine path not covered by the per-step tests:
/// <list type="bullet">
///   <item>
///     <strong>M-1</strong> — fail-closed runtime path: a Tier-A entity with
///     <c>SetNull</c> on a non-nullable value-type column reaches the engine via
///     <c>SkipModelValidation</c> and must return <c>UnhandledException</c> with no
///     writes.
///   </item>
///   <item>
///     <strong>M-3</strong> — concurrency retry-exhaustion: <c>MaxConcurrencyRetries = 0</c>
///     with a deterministic first-save conflict via <c>OnBeforeFirstSave</c> → engine
///     returns <c>ServiceUnavailable</c>.
///   </item>
/// </list>
/// </summary>
[Collection("Postgres")]
[Trait("Category", "Integration")]
public sealed class AnonymizationEngineGapTests : IAsyncLifetime
{
    private readonly PostgresFixture r_fixture;
    private readonly List<DbContext> r_engineContexts = [];

    private string r_connectionString = null!;
    private FailClosedDbContext r_failClosedSchemaCtx = null!;
    private GovDbContext r_schemaCtx = null!;

    /// <summary>
    /// Initializes a new instance of <see cref="AnonymizationEngineGapTests"/>.
    /// </summary>
    /// <param name="fixture">The shared Postgres Testcontainers fixture.</param>
    public AnonymizationEngineGapTests(PostgresFixture fixture)
    {
        r_fixture = fixture;
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        // Each test class gets its own isolated database so EnsureCreatedAsync builds the
        // full schema without colliding with sibling classes in the same xUnit collection.
        r_connectionString = await r_fixture.CreateIsolatedDatabaseAsync(
            nameof(AnonymizationEngineGapTests));

        // GovDbContext creates its tables first (fresh isolated DB — EnsureCreated succeeds
        // unconditionally because no other class has touched this database yet).
        r_schemaCtx = GovDbContext.Build(r_connectionString);
        await r_schemaCtx.Database.EnsureCreatedAsync();

        // FailClosedDbContext needs its own table in the same isolated database.
        // EnsureCreated cannot be used here (the DB already exists after GovDbContext above;
        // it would be a no-op and not create the FailClosedUsers table). Use a raw
        // CREATE TABLE IF NOT EXISTS — idempotent and safe.
        r_failClosedSchemaCtx = FailClosedDbContext.Build(r_connectionString);
        await r_failClosedSchemaCtx.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""FailClosedUsers"" (
                ""Id""           uuid         NOT NULL PRIMARY KEY,
                ""UserId""       uuid,
                ""Score""        integer      NOT NULL,
                ""DisplayName""  text         NOT NULL,
                ""IsAnonymized"" boolean      NOT NULL
            );");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var ctx in r_engineContexts)
            await ctx.DisposeAsync();

        await r_failClosedSchemaCtx.DisposeAsync();
        await r_schemaCtx.DisposeAsync();
        AnonymizationTierClassifier.ClearCache();
    }

    // =========================================================================
    // M-1: fail-closed runtime path — SetNull on non-nullable value-type column
    // =========================================================================

    // long identifier — cannot wrap
    [Fact]
    public async Task AnonymizeUserAsync_SetNull_on_non_nullable_int_returns_UnhandledException_and_no_writes()
    {
        var userId = Guid.NewGuid();
        await SeedFailClosedUser(userId, "Alice", 42);

        var ctx = FailClosedDbContext.Build(r_connectionString);
        r_engineContexts.Add(ctx);
        var engine = BuildFailClosedEngine(ctx);

        var result = await engine.AnonymizeUserAsync(userId);

        // The engine's BuildOneSetterCall detects SetNull on int → IsFailClosedMisconfiguration
        // → RunTierAAsync returns UnhandledException (500).
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.InternalServerError);

        // Verify no writes occurred: Score and DisplayName must be unchanged.
        await using var readCtx = FailClosedDbContext.Build(r_connectionString);
        var row = await readCtx.FailClosedUsers.FirstAsync(u => u.UserId == userId);

        row.Score.Should().Be(42, "non-nullable int must be unchanged");
        row.DisplayName.Should().Be("Alice", "constant field must also be unchanged");
        row.IsAnonymized.Should().BeFalse("IsAnonymized must not be set when engine fails");
    }

    // =========================================================================
    // M-3: concurrency retry-exhaustion — MaxConcurrencyRetries = 0
    // =========================================================================

    // long identifier — cannot wrap
    [Fact]
    public async Task AnonymizeUserAsync_MaxConcurrencyRetries_zero_conflict_returns_ServiceUnavailable()
    {
        var userId = Guid.NewGuid();
        await SeedTierBUser(userId, "exhaust@example.com", "Exhaust User");

        // MaxConcurrencyRetries = 0: the engine fails immediately on the first concurrency
        // conflict without any retry. OnBeforeFirstSave bumps the xmin via an independent
        // context, forcing DbUpdateConcurrencyException on the engine's first save.
        var ctx = GovDbContext.Build(r_connectionString);
        r_engineContexts.Add(ctx);

        var opts = Options.Create(new AnonymizationEngineOptions
        {
            BatchSize = 500,
            MaxConcurrencyRetries = 0,
        });
        var engine = new AnonymizationEngine(
            ctx,
            opts,
            NullLogger<AnonymizationEngine>.Instance);

        engine.OnBeforeFirstSave = () =>
        {
            using var bumpCtx = GovDbContext.Build(r_connectionString);
            var row = bumpCtx.TierBUsers.First(u => u.UserId == userId);
            row.DisplayName = "BumpedToExhaustRetries";
            bumpCtx.SaveChanges();
        };

        var result = await engine.AnonymizeUserAsync(userId);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static AnonymizationEngine BuildFailClosedEngine(DbContext ctx, int batchSize = 500)
    {
        var opts = Options.Create(new AnonymizationEngineOptions
        {
            BatchSize = batchSize,
        });
        return new AnonymizationEngine(ctx, opts, NullLogger<AnonymizationEngine>.Instance);
    }

    private async Task SeedFailClosedUser(Guid userId, string displayName, int score)
    {
        await using var ctx = FailClosedDbContext.Build(r_connectionString);
        ctx.FailClosedUsers.Add(new FailClosedDbContext.FailClosedUser
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DisplayName = displayName,
            Score = score,
        });
        await ctx.SaveChangesAsync();
    }

    private async Task SeedTierBUser(Guid userId, string email, string displayName)
    {
        await using var ctx = GovDbContext.Build(r_connectionString);
        ctx.TierBUsers.Add(new GovDbContext.TierBUser
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Email = email,
            DisplayName = displayName,
        });
        await ctx.SaveChangesAsync();
    }

    // =========================================================================
    // FailClosedDbContext — isolated context for M-1
    // =========================================================================

    /// <summary>
    /// Isolated DbContext for M-1. Contains only <see cref="FailClosedUser"/> so the
    /// engine sweeps exactly one entity type. Using a separate context avoids polluting
    /// the shared <see cref="GovDbContext"/> schema with the misconfigured entity.
    /// </summary>
    private sealed class FailClosedDbContext : DbContext
    {
        private FailClosedDbContext(DbContextOptions<FailClosedDbContext> options)
            : base(options)
        {
        }

        public DbSet<FailClosedUser> FailClosedUsers => Set<FailClosedUser>();

        internal static FailClosedDbContext Build(string connectionString)
        {
            var options = new DbContextOptionsBuilder<FailClosedDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            return new FailClosedDbContext(options);
        }

        protected override void ConfigureConventions(
            ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.ApplyAnonymizationConventions();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FailClosedUser>(e =>
            {
                e.HasKey(x => x.Id);

                // SetNull on non-nullable int — V7 startup guard blocks this at model-build
                // time. This context is used only in tests that intentionally exercise the
                // engine's fail-closed runtime path by bypassing the validator.
                e.Property(x => x.Score).AnonymizeNull();
            });
        }

        /// <summary>
        /// Entity with a non-nullable <c>int</c> column decorated with <c>SetNull</c>.
        /// Misconfiguration caught by the startup guard (V7); intentionally bypassed here
        /// to exercise the engine's <c>BuildOneSetterCall</c> fail-closed path.
        /// </summary>
        internal sealed class FailClosedUser : IUserOwned, IAnonymizationTrackable
        {
            public Guid Id { get; set; }

            public Guid? UserId { get; set; }

            /// <summary>
            /// Gets or sets the score. Non-nullable int with SetNull — misconfigured.
            /// </summary>
            public int Score { get; set; }

            /// <summary>Gets or sets the display name. Constant — valid column.</summary>
            [Anonymizable("Deleted")]
            public string DisplayName { get; set; } = string.Empty;

            /// <inheritdoc />
            public bool IsAnonymized { get; set; }
        }
    }
}
