// -----------------------------------------------------------------------
// <copyright file="AnonymizationEngineUnitTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.DataGovernance.EntityFrameworkCore;

using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.DataGovernance.Abstractions;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// DB-free unit tests for <c>AnonymizationEngine</c>. Uses model-build-only contexts
/// (Npgsql provider with a dummy connection string — connection never opened).
/// Covers the Guid.Empty guard, Tier-C defensive rejection, and option validation.
/// </summary>
/// <remarks>
/// Serialized with the other <c>AnonymizationClassifier*</c> test classes because they all
/// call <c>AnonymizationTierClassifier.ClearCache()</c> against the process-global static
/// cache.
/// </remarks>
[Collection("AnonymizationClassifierSerial")]
[Trait("Category", "Unit")]
public sealed class AnonymizationEngineUnitTests
{
    // Clear the tier classifier cache between tests so entity-type model identities
    // across different DbContext subclasses don't collide.
    public AnonymizationEngineUnitTests() => AnonymizationTierClassifier.ClearCache();

    // =========================================================================
    // Guid.Empty guard — no DB writes
    // =========================================================================

    // long identifier — cannot wrap
    [Fact]
    public async Task AnonymizeUserAsync_GuidEmpty_returns_ValidationFailed_without_connecting_to_DB()
    {
        await using var ctx = EmptyModelContext.Build();
        var engine = BuildEngine(ctx);

        var result = await engine.AnonymizeUserAsync(Guid.Empty);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    // long identifier — cannot wrap
    [Fact]
    public async Task AnonymizeOrgAsync_GuidEmpty_returns_ValidationFailed_without_connecting_to_DB()
    {
        await using var ctx = EmptyModelContext.Build();
        var engine = BuildEngine(ctx);

        var result = await engine.AnonymizeOrgAsync(Guid.Empty);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    // =========================================================================
    // Tier-C defensive rejection — entity reaching engine at runtime
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_TierC_entity_returns_UnhandledException_without_writes()
    {
        await using var ctx = TierCModelContext.Build();
        var engine = BuildEngine(ctx);

        var result = await engine.AnonymizeUserAsync(Guid.NewGuid());

        // Tier-C at runtime is a deploy-integrity failure (500)
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.InternalServerError);
    }

    // =========================================================================
    // Empty model — zero entity types → Ok with all-zero outcome
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_no_owned_entities_in_model_returns_Ok_with_zero_counts()
    {
        await using var ctx = EmptyModelContext.Build();
        var engine = BuildEngine(ctx);

        var result = await engine.AnonymizeUserAsync(Guid.NewGuid());

        result.Success.Should().BeTrue();
        result.Data!.EntityTypesProcessed.Should().Be(0);
        result.Data.RowsAnonymized.Should().Be(0);
        result.Data.EntityTypesSkippedExempt.Should().Be(0);
        result.Data.AlreadyAnonymizedRows.Should().Be(0);
    }

    // =========================================================================
    // Options — BatchSize < 1 → ServiceUnavailable
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_BatchSize_zero_returns_ServiceUnavailable()
    {
        await using var ctx = UserModelContext.Build();
        var engine = BuildEngine(ctx, batchSize: 0);

        var result = await engine.AnonymizeUserAsync(Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
    }

    // =========================================================================
    // Options — BatchSize negative → ServiceUnavailable (M-2)
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_BatchSize_negative_returns_ServiceUnavailable()
    {
        await using var ctx = UserModelContext.Build();
        var engine = BuildEngine(ctx, batchSize: -1);

        var result = await engine.AnonymizeUserAsync(Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
    }

    // =========================================================================
    // AnonymizationEngineOptions — shape validation
    // =========================================================================

    // =========================================================================
    // Options — MaxConcurrencyRetries negative → clamped to 0
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_MaxConcurrencyRetries_negative_treated_as_zero_no_crash()
    {
        await using var ctx = EmptyModelContext.Build();

        // Negative MaxConcurrencyRetries is silently clamped to 0 — no exception.
        var engine = BuildEngine(ctx, maxRetries: -1);

        var result = await engine.AnonymizeUserAsync(Guid.NewGuid());

        // Empty model → Ok with zero counts — proves clamping, not a crash.
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void AnonymizationEngineOptions_DefaultValues_are_500_and_3()
    {
        var opts = new AnonymizationEngineOptions();

        opts.BatchSize.Should().Be(500);
        opts.MaxConcurrencyRetries.Should().Be(3);
    }

    [Fact]
    public void AnonymizationEngineOptions_SECTION_NAME_is_DATA_GOVERNANCE()
    {
        AnonymizationEngineOptions.SECTION_NAME.Should().Be("DATA_GOVERNANCE");
    }

    // =========================================================================
    // Exempt enumeration — entity with IExemptFromAnonymization is counted, not processed
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_exempt_entity_counted_in_skipped_not_processed()
    {
        await using var ctx = ExemptModelContext.Build();
        var engine = BuildEngine(ctx);

        // No DB connection — the model has an exempt entity but no processable ones
        var result = await engine.AnonymizeUserAsync(Guid.NewGuid());

        result.Success.Should().BeTrue();
        result.Data!.EntityTypesSkippedExempt.Should().Be(1);
        result.Data.EntityTypesProcessed.Should().Be(0);
    }

    // =========================================================================
    // Constructor null-guards
    // =========================================================================

    [Fact]
    public void Ctor_null_db_throws_ArgumentNullException()
    {
        var opts = Options.Create(new AnonymizationEngineOptions());
        var act = () => new AnonymizationEngine(
            null!,
            opts,
            NullLogger<AnonymizationEngine>.Instance);

        act.Should().Throw<ArgumentNullException>().WithParameterName("db");
    }

    [Fact]
    public void Ctor_null_options_throws_ArgumentNullException()
    {
        // Allocate the context inside the lambda so it is never captured across a dispose.
        var act = () =>
        {
            using var c = EmptyModelContext.Build();
            return new AnonymizationEngine(
                c,
                null!,
                NullLogger<AnonymizationEngine>.Instance);
        };

        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void Ctor_null_logger_throws_ArgumentNullException()
    {
        var opts = Options.Create(new AnonymizationEngineOptions());
        var act = () =>
        {
            using var c = EmptyModelContext.Build();
            return new AnonymizationEngine(c, opts, null!);
        };

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static AnonymizationEngine BuildEngine(
        DbContext ctx,
        int batchSize = 500,
        int maxRetries = 3)
    {
        var opts = Options.Create(new AnonymizationEngineOptions
        {
            BatchSize = batchSize,
            MaxConcurrencyRetries = maxRetries,
        });
        return new AnonymizationEngine(ctx, opts, NullLogger<AnonymizationEngine>.Instance);
    }

    // =========================================================================
    // Minimal model contexts
    // =========================================================================

    // -- Empty model (no ownership-marked entities) --

    private sealed class EmptyEntity
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class EmptyModelContext : DbContext
    {
        private EmptyModelContext(DbContextOptions<EmptyModelContext> options)
            : base(options)
        {
        }

        internal static EmptyModelContext Build()
        {
            var opts = new DbContextOptionsBuilder<EmptyModelContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new EmptyModelContext(opts);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<EmptyEntity>(e => e.HasKey(x => x.Id));
        }
    }

    // -- Tier-C model (user-owned entity with owned-JSON decorated field) --

    private sealed class TierCEntity : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        public TierCOwnedJson? JsonPart { get; set; }

        public bool IsAnonymized { get; set; }
    }

    private sealed class TierCOwnedJson
    {
        public string Secret { get; set; } = string.Empty;
    }

    private sealed class TierCModelContext : DbContext
    {
        private TierCModelContext(DbContextOptions<TierCModelContext> options)
            : base(options)
        {
        }

        internal static TierCModelContext Build()
        {
            var opts = new DbContextOptionsBuilder<TierCModelContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new TierCModelContext(opts);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<TierCEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.OwnsOne(x => x.JsonPart, nav =>
                {
                    nav.ToJson();
                    nav.Anonymize(d => d.Secret, "[deleted]");
                });
            });
        }
    }

    // -- User-owned model (for BatchSize=0 test) --

    private sealed class SimpleUserEntity : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        [Anonymizable("Deleted")]
        public string Name { get; set; } = string.Empty;

        public bool IsAnonymized { get; set; }
    }

    private sealed class UserModelContext : DbContext
    {
        private UserModelContext(DbContextOptions<UserModelContext> options)
            : base(options)
        {
        }

        internal static UserModelContext Build()
        {
            var opts = new DbContextOptionsBuilder<UserModelContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new UserModelContext(opts);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.ApplyAnonymizationConventions();
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<SimpleUserEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).Anonymize("Deleted");
            });
        }
    }

    // -- Exempt model (one exempt user-owned entity, no processable entities) --

    private sealed class ExemptOnlyEntity :
        IUserOwned,
        IExemptFromAnonymization,
        IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        [Anonymizable("Deleted")]
        public string Data { get; set; } = string.Empty;

        public bool IsAnonymized { get; set; }
    }

    private sealed class ExemptModelContext : DbContext
    {
        private ExemptModelContext(DbContextOptions<ExemptModelContext> options)
            : base(options)
        {
        }

        internal static ExemptModelContext Build()
        {
            var opts = new DbContextOptionsBuilder<ExemptModelContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new ExemptModelContext(opts);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.ApplyAnonymizationConventions();
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<ExemptOnlyEntity>(e => e.HasKey(x => x.Id));
        }
    }
}
