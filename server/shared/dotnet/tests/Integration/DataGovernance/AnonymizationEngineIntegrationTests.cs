// -----------------------------------------------------------------------
// <copyright file="AnonymizationEngineIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.DataGovernance;

using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using D2.Shared.DataGovernance.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// End-to-end anonymization engine integration tests. Each test seeds unique subject
/// <see cref="Guid"/> values so tests share the container schema without cross-test
/// interference. The schema is created once via <c>EnsureCreatedAsync</c> in the
/// collection fixture-shared setup call.
/// </summary>
[Collection("Postgres")]
[Trait("Category", "Integration")]
public sealed class AnonymizationEngineIntegrationTests : IAsyncLifetime
{
    private readonly PostgresFixture r_fixture;

    // Tracks engine DbContext instances created per test so they are disposed in DisposeAsync.
    private readonly System.Collections.Generic.List<GovDbContext> r_engineContexts = [];

    private GovDbContext r_schemaCtx = null!;

    /// <summary>
    /// Initializes a new instance of <see cref="AnonymizationEngineIntegrationTests"/>.
    /// </summary>
    /// <param name="fixture">The shared Postgres Testcontainers fixture.</param>
    public AnonymizationEngineIntegrationTests(PostgresFixture fixture)
    {
        r_fixture = fixture;
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        r_schemaCtx = GovDbContext.Build(r_fixture.ConnectionString);
        await r_schemaCtx.Database.EnsureCreatedAsync();
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
    // Test 1 — Tier-A overwrite: constant, null, empty, owned, complex fields
    // =========================================================================

    // long identifier — cannot wrap
    [Fact]
    public async Task AnonymizeUserAsync_TierA_overwrites_all_decorated_columns_and_sets_IsAnonymized()
    {
        var userId = Guid.NewGuid();
        await SeedTierAUser(
            userId,
            "Alice",
            "My bio",
            "Some notes",
            "premium",
            "123 Main St",
            "Apt 1",
            "Alice",
            "Smith");

        var engine = BuildEngine();
        var result = await engine.AnonymizeUserAsync(userId);

        result.Success.Should().BeTrue();
        result.Data!.RowsAnonymized.Should().Be(1);

        await using var readCtx = GovDbContext.Build(r_fixture.ConnectionString);
        var row = await readCtx.TierAUsers.FirstAsync(u => u.UserId == userId);

        row.DisplayName.Should().Be("Deleted");
        row.Bio.Should().BeNull();
        row.Notes.Should().BeEmpty();
        row.Status.Should().BeEmpty();
        row.Address!.Line1.Should().Be("[deleted]");
        row.Address.Line2.Should().BeNull();
        row.Name.First.Should().Be("[deleted]");
        row.Name.Last.Should().BeNull();
        row.IsAnonymized.Should().BeTrue();

        // CreatedAt must be unchanged (undecorated).
        row.CreatedAt.Should().NotBe(default);
    }

    // =========================================================================
    // Test 2 — Tier-B template render (guid-no-dashes) + constant
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_TierB_renders_template_with_guid_no_dashes_and_constant()
    {
        var userId = Guid.NewGuid();
        await SeedTierBUser(userId, "bob@example.com", "Bob");

        var engine = BuildEngine();
        var result = await engine.AnonymizeUserAsync(userId);

        result.Success.Should().BeTrue();
        result.Data!.RowsAnonymized.Should().Be(1);

        await using var readCtx = GovDbContext.Build(r_fixture.ConnectionString);
        var row = await readCtx.TierBUsers.FirstAsync(u => u.UserId == userId);

        var expectedEmail = $"deletedUser{userId:N}@deleted.user.dcsv.io";
        row.Email.Should().Be(expectedEmail);
        row.DisplayName.Should().Be("Deleted");
        row.IsAnonymized.Should().BeTrue();
    }

    // =========================================================================
    // Test 3 — Exempt entity: untouched, counted in EntityTypesSkippedExempt
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_exempt_entity_is_untouched_and_counted_in_skipped()
    {
        var userId = Guid.NewGuid();
        await SeedExemptLedger(userId, "Private note", 999.99m);

        var engine = BuildEngine();
        var result = await engine.AnonymizeUserAsync(userId);

        result.Success.Should().BeTrue();
        result.Data!.EntityTypesSkippedExempt.Should().BeGreaterThan(0);
        result.Data.RowsAnonymized.Should().Be(0);

        await using var readCtx = GovDbContext.Build(r_fixture.ConnectionString);
        var row = await readCtx.ExemptLedgers.FirstAsync(e => e.UserId == userId);

        row.OwnerNote.Should().Be("Private note");
        row.Amount.Should().Be(999.99m);
        row.IsAnonymized.Should().BeFalse();
    }

    // =========================================================================
    // Test 4 — Subject isolation: anonymizing S1 does not touch S2
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_does_not_touch_other_subject_rows()
    {
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        await SeedTierAUser(
            userId1, "User1", null, string.Empty, "active", "Street1", null, "First1", null);
        await SeedTierAUser(
            userId2, "User2", null, string.Empty, "active", "Street2", null, "First2", null);

        var engine = BuildEngine();
        await engine.AnonymizeUserAsync(userId1);

        await using var readCtx = GovDbContext.Build(r_fixture.ConnectionString);
        var row2 = await readCtx.TierAUsers.FirstAsync(u => u.UserId == userId2);

        row2.DisplayName.Should().Be("User2");
        row2.IsAnonymized.Should().BeFalse();
    }

    // =========================================================================
    // Test 5 — User/org isolation: AnonymizeUserAsync does not touch OrgRecord rows
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_does_not_touch_org_owned_rows()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        await SeedTierAUser(
            userId, "User", null, string.Empty, "active", "Street", null, "First", null);
        await SeedOrgRecord(orgId, "MyOrg");

        var engine = BuildEngine();
        await engine.AnonymizeUserAsync(userId);

        await using var readCtx = GovDbContext.Build(r_fixture.ConnectionString);
        var orgRow = await readCtx.OrgRecords.FirstAsync(o => o.OrgId == orgId);
        orgRow.OrgName.Should().Be("MyOrg");
        orgRow.IsAnonymized.Should().BeFalse();
    }

    [Fact]
    public async Task AnonymizeOrgAsync_anonymizes_org_row_and_leaves_user_rows_untouched()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        await SeedTierAUser(
            userId, "User", null, string.Empty, "active", "Street", null, "First", null);
        await SeedOrgRecord(orgId, "OrgToAnonymize");

        var engine = BuildEngine();
        var result = await engine.AnonymizeOrgAsync(orgId);

        result.Success.Should().BeTrue();
        result.Data!.RowsAnonymized.Should().Be(1);

        await using var readCtx = GovDbContext.Build(r_fixture.ConnectionString);
        var orgRow = await readCtx.OrgRecords.FirstAsync(o => o.OrgId == orgId);
        orgRow.OrgName.Should().Be("Deleted");
        orgRow.IsAnonymized.Should().BeTrue();

        var userRow = await readCtx.TierAUsers.FirstAsync(u => u.UserId == userId);
        userRow.DisplayName.Should().Be("User");
        userRow.IsAnonymized.Should().BeFalse();
    }

    // =========================================================================
    // Test 6 — Idempotent re-run: second call → RowsAnonymized==0, AlreadyAnonymizedRows>0
    // =========================================================================

    // long identifier — cannot wrap
    [Fact]
    public async Task AnonymizeUserAsync_idempotent_rerun_reports_already_anonymized_and_skips_writes()
    {
        var userId = Guid.NewGuid();
        await SeedTierAUser(userId, "Alice", "bio", "notes", "active", "St", null, "F", null);

        var engine = BuildEngine();
        var first = await engine.AnonymizeUserAsync(userId);
        var second = await engine.AnonymizeUserAsync(userId);

        first.Success.Should().BeTrue();
        first.Data!.RowsAnonymized.Should().BeGreaterThan(0);

        second.Success.Should().BeTrue();
        second.Data!.RowsAnonymized.Should().Be(0);
        second.Data.AlreadyAnonymizedRows.Should().BeGreaterThan(0);

        // Values must be byte-stable (same as first run).
        await using var readCtx = GovDbContext.Build(r_fixture.ConnectionString);
        var row = await readCtx.TierAUsers.FirstAsync(u => u.UserId == userId);
        row.DisplayName.Should().Be("Deleted");
        row.IsAnonymized.Should().BeTrue();
    }

    // =========================================================================
    // Test 7 — Batch > BatchSize: all rows anonymized across multiple chunks
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_batch_larger_than_BatchSize_anonymizes_all_rows()
    {
        const int row_count = 7;
        const int batch_size = 2;

        var userId = Guid.NewGuid();
        for (var i = 0; i < row_count; i++)
            await SeedTierBUser(userId, $"user{i}@example.com", $"User {i}");

        var engine = BuildEngine(batchSize: batch_size);
        var result = await engine.AnonymizeUserAsync(userId);

        result.Success.Should().BeTrue();
        result.Data!.RowsAnonymized.Should().Be(row_count);

        await using var readCtx = GovDbContext.Build(r_fixture.ConnectionString);
        var rows = await readCtx.TierBUsers
            .Where(u => u.UserId == userId)
            .ToListAsync();

        rows.Should().HaveCount(row_count);
        rows.All(r => r.IsAnonymized).Should().BeTrue();
    }

    // =========================================================================
    // Test 8 — Concurrency: engine materializes chunk, external writer bumps xmin,
    //           engine's first SaveChanges fires DbUpdateConcurrencyException,
    //           retry converges and the row ends up anonymized.
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_TierB_concurrency_conflict_retries_and_converges()
    {
        var userId = Guid.NewGuid();
        await SeedTierBUser(userId, "concurrent@example.com", "Concurrent User");

        // Build engine with a test hook: after the engine materializes the chunk (rows are
        // now tracked in the engine's DbContext with their current xmin), but before it
        // calls SaveChanges, we bump the row via an independent context. This changes the
        // DB-side xmin so the engine's pending save conflicts, forcing DbUpdateConcurrencyException
        // and triggering the reload-retry path.
        var engine = BuildEngine();
        engine.OnBeforeFirstSave = () =>
        {
            // Synchronous bump via a fresh blocking context — runs on the thread-pool
            // continuation before the engine's SaveChangesAsync actually sends bytes.
            using var bumpCtx = GovDbContext.Build(r_fixture.ConnectionString);
            var bumpRow = bumpCtx.TierBUsers.First(u => u.UserId == userId);
            bumpRow.DisplayName = "BumpedByConcurrentWriter";
            bumpCtx.SaveChanges();
        };

        var result = await engine.AnonymizeUserAsync(userId);

        result.Success.Should().BeTrue();

        await using var readCtx = GovDbContext.Build(r_fixture.ConnectionString);
        var row = await readCtx.TierBUsers.FirstAsync(u => u.UserId == userId);

        // The retry must have run: row is anonymized after convergence.
        row.IsAnonymized.Should().BeTrue();
        row.Email.Should().StartWith("deletedUser");
    }

    // =========================================================================
    // Test 9 — Guid.Empty: no writes, ValidationFailed
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_GuidEmpty_returns_ValidationFailed_and_no_rows_written()
    {
        var userId = Guid.NewGuid();
        await SeedTierAUser(userId, "Alice", null, string.Empty, "active", "St", null, "F", null);

        var engine = BuildEngine();
        var result = await engine.AnonymizeUserAsync(Guid.Empty);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);

        await using var readCtx = GovDbContext.Build(r_fixture.ConnectionString);
        var row = await readCtx.TierAUsers.FirstAsync(u => u.UserId == userId);
        row.IsAnonymized.Should().BeFalse();
        row.DisplayName.Should().Be("Alice");
    }

    // =========================================================================
    // Test 10 — Zero-match subject: Ok with all-zero row counts
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_no_rows_for_subject_returns_Ok_with_zero_row_counts()
    {
        var engine = BuildEngine();
        var result = await engine.AnonymizeUserAsync(Guid.NewGuid());

        result.Success.Should().BeTrue();
        result.Data!.RowsAnonymized.Should().Be(0);
        result.Data.AlreadyAnonymizedRows.Should().Be(0);
    }

    // =========================================================================
    // Test 11 — Mixed tiers in one sweep: TierAUser + TierBUser for same subject
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_mixed_tiers_in_one_sweep_anonymizes_both_entities()
    {
        var userId = Guid.NewGuid();
        await SeedTierAUser(userId, "Alice", "bio", "notes", "active", "St", null, "F", null);
        await SeedTierBUser(userId, "alice@example.com", "Alice");

        var engine = BuildEngine();
        var result = await engine.AnonymizeUserAsync(userId);

        result.Success.Should().BeTrue();

        // 1 TierA row + 1 TierB row.
        result.Data!.RowsAnonymized.Should().Be(2);
        result.Data.EntityTypesProcessed.Should().BeGreaterThanOrEqualTo(2);

        await using var readCtx = GovDbContext.Build(r_fixture.ConnectionString);
        var tierA = await readCtx.TierAUsers.FirstAsync(u => u.UserId == userId);
        var tierB = await readCtx.TierBUsers.FirstAsync(u => u.UserId == userId);

        tierA.IsAnonymized.Should().BeTrue();
        tierB.IsAnonymized.Should().BeTrue();
        tierA.DisplayName.Should().Be("Deleted");
        tierB.Email.Should().StartWith("deletedUser");
    }

    // =========================================================================
    // Test 12 — Converter field survives: Status round-trips to empty string in DB
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_converter_backed_field_round_trips_empty_string_correctly()
    {
        var userId = Guid.NewGuid();
        await SeedTierAUser(userId, "Alice", null, string.Empty, "premium", "St", null, "F", null);

        var engine = BuildEngine();
        var result = await engine.AnonymizeUserAsync(userId);

        result.Success.Should().BeTrue();

        await using var readCtx = GovDbContext.Build(r_fixture.ConnectionString);
        var row = await readCtx.TierAUsers.FirstAsync(u => u.UserId == userId);

        // Status decorated with SetEmpty — should be empty string in DB.
        row.Status.Should().BeEmpty();
        row.IsAnonymized.Should().BeTrue();
    }

    // =========================================================================
    // Seed helpers
    // =========================================================================

    private async Task SeedTierAUser(
        Guid userId,
        string displayName,
        string? bio,
        string notes,
        string status,
        string addressLine1,
        string? addressLine2,
        string nameFirst,
        string? nameLast)
    {
        await using var ctx = GovDbContext.Build(r_fixture.ConnectionString);
        ctx.TierAUsers.Add(new GovDbContext.TierAUser
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DisplayName = displayName,
            Bio = bio,
            Notes = notes,
            Status = status,
            Address = new GovDbContext.TierAAddress { Line1 = addressLine1, Line2 = addressLine2 },
            Name = new GovDbContext.TierAName { First = nameFirst, Last = nameLast },
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();
    }

    private async Task SeedTierBUser(Guid userId, string email, string displayName)
    {
        await using var ctx = GovDbContext.Build(r_fixture.ConnectionString);
        ctx.TierBUsers.Add(new GovDbContext.TierBUser
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Email = email,
            DisplayName = displayName,
        });
        await ctx.SaveChangesAsync();
    }

    private async Task SeedOrgRecord(Guid orgId, string orgName)
    {
        await using var ctx = GovDbContext.Build(r_fixture.ConnectionString);
        ctx.OrgRecords.Add(new GovDbContext.OrgRecord
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            OrgName = orgName,
        });
        await ctx.SaveChangesAsync();
    }

    private async Task SeedExemptLedger(Guid userId, string ownerNote, decimal amount)
    {
        await using var ctx = GovDbContext.Build(r_fixture.ConnectionString);
        ctx.ExemptLedgers.Add(new GovDbContext.ExemptLedger
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OwnerNote = ownerNote,
            Amount = amount,
        });
        await ctx.SaveChangesAsync();
    }

    private AnonymizationEngine BuildEngine(int batchSize = 500, int maxRetries = 3)
    {
        var opts = Options.Create(new AnonymizationEngineOptions
        {
            BatchSize = batchSize,
            MaxConcurrencyRetries = maxRetries,
        });
        var ctx = GovDbContext.Build(r_fixture.ConnectionString);
        r_engineContexts.Add(ctx);
        return new AnonymizationEngine(ctx, opts, NullLogger<AnonymizationEngine>.Instance);
    }
}
