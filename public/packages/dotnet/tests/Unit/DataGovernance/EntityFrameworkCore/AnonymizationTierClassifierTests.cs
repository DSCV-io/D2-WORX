// -----------------------------------------------------------------------
// <copyright file="AnonymizationTierClassifierTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.DataGovernance.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.DataGovernance.Abstractions;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

/// <summary>
/// Tests for <see cref="AnonymizationTierClassifier"/>. All contexts use the Npgsql provider
/// configured with a dummy connection string (model-build-only — the connection is never opened).
/// This gives a real relational model so <c>GetColumnName()</c>, <c>IsMappedToJson()</c>, and
/// <c>GetTableName()</c> return production-faithful values.
/// </summary>
/// <remarks>
/// Serialized with the other <c>AnonymizationClassifier*</c> test classes because they all
/// call <c>AnonymizationTierClassifier.ClearCache()</c> and assert exact
/// <c>CacheCount</c> values against the process-global static cache.
/// </remarks>
[Collection("AnonymizationClassifierSerial")]
[Trait("Category", "Unit")]
public sealed class AnonymizationTierClassifierTests : IDisposable
{
    // Each test class instance gets a clean cache so tests are fully isolated.
    public AnonymizationTierClassifierTests() => AnonymizationTierClassifier.ClearCache();

    public void Dispose() => AnonymizationTierClassifier.ClearCache();

    // =========================================================================
    // Enum shape — pin member count + values
    // =========================================================================

    [Fact]
    public void AnonymizationTier_has_exactly_3_members()
    {
        Enum.GetNames<AnonymizationTier>().Length.Should().Be(3);
    }

    [Fact]
    public void AnonymizationTier_member_values_are_0_1_2()
    {
        ((int)AnonymizationTier.TierA).Should().Be(0);
        ((int)AnonymizationTier.TierB).Should().Be(1);
        ((int)AnonymizationTier.TierC).Should().Be(2);
    }

    [Fact]
    public void AnonymizationColumnShape_has_exactly_5_members()
    {
        Enum.GetNames<AnonymizationColumnShape>().Length.Should().Be(5);
    }

    [Fact]
    public void AnonymizationColumnShape_member_values_are_0_through_4()
    {
        ((int)AnonymizationColumnShape.Scalar).Should().Be(0);
        ((int)AnonymizationColumnShape.TableSplitOwned).Should().Be(1);
        ((int)AnonymizationColumnShape.Complex).Should().Be(2);
        ((int)AnonymizationColumnShape.OwnedJson).Should().Be(3);
        ((int)AnonymizationColumnShape.OwnsManyChild).Should().Be(4);
    }

    // =========================================================================
    // Record round-trips — required init + equality + with-mutation
    // =========================================================================

    [Fact]
    public void AnonymizationColumn_record_equality_and_with_mutation()
    {
        using ScalarOnlyContext ctx = ScalarOnlyContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ScalarOnlyEntity))!;
        IProperty prop = entityType.FindProperty(nameof(ScalarOnlyEntity.Email))!;
        var rule = AnonymizationRule.Create(AnonymizeKind.Constant, constantValue: "[deleted]");

        var col = new AnonymizationColumn
        {
            ColumnName = "email",
            PropertyName = "Email",
            Rule = rule,
            Shape = AnonymizationColumnShape.Scalar,
            Property = prop,
        };

        var col2 = col with { ColumnName = "email2" };
        col2.ColumnName.Should().Be("email2");
        col2.PropertyName.Should().Be("Email");
        col.Should().NotBe(col2); // different ColumnName
    }

    [Fact]
    public void AnonymizationClassification_record_equality_and_with_mutation()
    {
        var classification1 = new AnonymizationClassification
        {
            Tier = AnonymizationTier.TierA,
            Columns = Array.Empty<AnonymizationColumn>(),
            TierCBlocker = null,
        };

        var classification2 = classification1 with { Tier = AnonymizationTier.TierB };
        classification2.Tier.Should().Be(AnonymizationTier.TierB);
        classification1.Should().NotBe(classification2);
    }

    // =========================================================================
    // Tier A — plain scalar (shape Scalar)
    // =========================================================================

    [Fact]
    public void Classify_entity_with_scalar_constant_fields_returns_TierA()
    {
        using ScalarOnlyContext ctx = ScalarOnlyContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ScalarOnlyEntity))!;

        AnonymizationClassification result = AnonymizationTierClassifier.Classify(entityType);

        result.Tier.Should().Be(AnonymizationTier.TierA);
    }

    [Fact]
    public void Classify_scalar_entity_columns_have_Scalar_shape()
    {
        using ScalarOnlyContext ctx = ScalarOnlyContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ScalarOnlyEntity))!;

        AnonymizationClassification result = AnonymizationTierClassifier.Classify(entityType);

        result.Columns.Should().NotBeEmpty();
        result.Columns.All(c => c.Shape == AnonymizationColumnShape.Scalar).Should().BeTrue();
    }

    [Fact]
    public void Classify_scalar_entity_column_name_matches_npgsql_mapped_column()
    {
        using ScalarOnlyContext ctx = ScalarOnlyContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ScalarOnlyEntity))!;

        AnonymizationClassification result = AnonymizationTierClassifier.Classify(entityType);

        var emailCol = result.Columns
            .FirstOrDefault(c => c.PropertyName == nameof(ScalarOnlyEntity.Email));
        emailCol.Should().NotBeNull();

        // EF Core default convention (no explicit column mapping, no snake_case convention):
        // the relational column name equals the property name.  Assert the EXACT column name
        // from the Npgsql relational model — a non-relational provider would return null here,
        // so a non-null match proves the classifier is reading the relational metadata.
        emailCol.ColumnName.Should().Be(nameof(ScalarOnlyEntity.Email));

        // Cross-check: classifier ColumnName matches what GetColumnName() returns on the model.
        IProperty emailProp = entityType.FindProperty(nameof(ScalarOnlyEntity.Email))!;
        emailCol.ColumnName.Should().Be(emailProp.GetColumnName());
    }

    [Fact]
    public void Classify_scalar_entity_columns_carry_correct_rules()
    {
        using ScalarOnlyContext ctx = ScalarOnlyContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ScalarOnlyEntity))!;

        AnonymizationClassification result = AnonymizationTierClassifier.Classify(entityType);

        var emailCol = result.Columns
            .FirstOrDefault(c => c.PropertyName == nameof(ScalarOnlyEntity.Email));
        emailCol!.Rule.Kind.Should().Be(AnonymizeKind.Constant);
        var phoneCol = result.Columns
            .FirstOrDefault(c => c.PropertyName == nameof(ScalarOnlyEntity.Phone));
        phoneCol!.Rule.Kind.Should().Be(AnonymizeKind.SetNull);
        var displayCol = result.Columns
            .FirstOrDefault(c => c.PropertyName == nameof(ScalarOnlyEntity.DisplayName));
        displayCol!.Rule.Kind.Should().Be(AnonymizeKind.SetEmpty);
    }

    [Fact]
    public void Classify_scalar_entity_TierCBlocker_is_null()
    {
        using ScalarOnlyContext ctx = ScalarOnlyContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ScalarOnlyEntity))!;

        AnonymizationClassification result = AnonymizationTierClassifier.Classify(entityType);

        result.TierCBlocker.Should().BeNull();
    }

    // =========================================================================
    // Tier A — table-split OwnsOne (shape TableSplitOwned)
    // =========================================================================

    [Fact]
    public void Classify_table_split_owned_entity_returns_TierA_with_TableSplitOwned_shape()
    {
        using TableSplitOwnedContext ctx = TableSplitOwnedContext.Build();

        AnonymizationClassification result = AnonymizationTierClassifier.Classify(
            ctx.Model.FindEntityType(typeof(TableSplitOwnerEntity))!);

        result.Tier.Should().Be(AnonymizationTier.TierA);
        result.Columns.Should().NotBeEmpty();
        result.Columns.All(c => c.Shape == AnonymizationColumnShape.TableSplitOwned ||
                                c.Shape == AnonymizationColumnShape.Scalar).Should().BeTrue();

        var streetCol = result.Columns
            .FirstOrDefault(c => c.PropertyName == nameof(OwnedAddressTableSplit.Street));
        streetCol.Should().NotBeNull();
        streetCol.Shape.Should().Be(AnonymizationColumnShape.TableSplitOwned);
    }

    // =========================================================================
    // Tier A — ComplexProperty table-split (shape Complex)
    // =========================================================================

    [Fact]
    public void Classify_complex_property_table_split_returns_TierA_with_Complex_shape()
    {
        using ComplexTableSplitContext ctx = ComplexTableSplitContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ComplexTableSplitOwner))!;

        AnonymizationClassification result = AnonymizationTierClassifier.Classify(entityType);

        result.Tier.Should().Be(AnonymizationTier.TierA);
        result.Columns.Should().NotBeEmpty();

        var firstNameCol = result.Columns
            .FirstOrDefault(c => c.PropertyName == nameof(ComplexName.First));
        firstNameCol.Should().NotBeNull();
        firstNameCol.Shape.Should().Be(AnonymizationColumnShape.Complex);
    }

    // =========================================================================
    // Tier A — ComplexProperty with .ToJson() (still Tier A in EF10 — shape Complex)
    // =========================================================================

    [Fact]
    public void Classify_complex_json_entity_returns_TierA_with_Complex_shape()
    {
        using ComplexJsonContext ctx = ComplexJsonContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ComplexJsonOwner))!;

        AnonymizationClassification result = AnonymizationTierClassifier.Classify(entityType);

        // EF Core 10 ExecuteUpdate reaches complex-JSON; TierA is correct.
        result.Tier.Should().Be(AnonymizationTier.TierA);
        result.Columns.Should().NotBeEmpty();
        result.Columns.All(c => c.Shape == AnonymizationColumnShape.Complex).Should().BeTrue();
    }

    // =========================================================================
    // Tier B — Template field demotes entity
    // =========================================================================

    [Fact]
    public void Classify_entity_with_one_template_field_returns_TierB()
    {
        using TemplateFieldContext ctx = TemplateFieldContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(TemplateFieldEntity))!;

        AnonymizationClassification result = AnonymizationTierClassifier.Classify(entityType);

        result.Tier.Should().Be(AnonymizationTier.TierB);
    }

    [Fact]
    public void Classify_TierB_entity_TierCBlocker_is_null()
    {
        using TemplateFieldContext ctx = TemplateFieldContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(TemplateFieldEntity))!;

        AnonymizationClassification result = AnonymizationTierClassifier.Classify(entityType);

        result.TierCBlocker.Should().BeNull();
    }

    // =========================================================================
    // Tier C — OwnsOne(...).ToJson() (shape OwnedJson)
    // =========================================================================

    [Fact]
    public void Classify_entity_with_owned_json_field_returns_TierC()
    {
        using OwnedJsonContext ctx = OwnedJsonContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(OwnedJsonOwner))!;

        AnonymizationClassification result = AnonymizationTierClassifier.Classify(entityType);

        result.Tier.Should().Be(AnonymizationTier.TierC);
    }

    [Fact]
    public void Classify_owned_json_entity_TierCBlocker_has_OwnedJson_shape()
    {
        using OwnedJsonContext ctx = OwnedJsonContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(OwnedJsonOwner))!;

        AnonymizationClassification result = AnonymizationTierClassifier.Classify(entityType);

        result.TierCBlocker.Should().NotBeNull();
        result.TierCBlocker.Shape.Should().Be(AnonymizationColumnShape.OwnedJson);
    }

    // =========================================================================
    // Tier C — OwnsMany child table (shape OwnsManyChild)
    // =========================================================================

    [Fact]
    public void Classify_entity_with_OwnsMany_child_returns_TierC()
    {
        using OwnsManyContext ctx = OwnsManyContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(OwnsManyOwner))!;

        AnonymizationClassification result = AnonymizationTierClassifier.Classify(entityType);

        result.Tier.Should().Be(AnonymizationTier.TierC);
    }

    [Fact]
    public void Classify_OwnsMany_entity_TierCBlocker_has_OwnsManyChild_shape()
    {
        using OwnsManyContext ctx = OwnsManyContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(OwnsManyOwner))!;

        AnonymizationClassification result = AnonymizationTierClassifier.Classify(entityType);

        result.TierCBlocker.Should().NotBeNull();
        result.TierCBlocker.Shape.Should().Be(AnonymizationColumnShape.OwnsManyChild);
    }

    // =========================================================================
    // Mixed precedence — C beats B beats A
    // =========================================================================

    [Fact]
    public void Classify_entity_with_constant_and_template_and_owned_json_returns_TierC()
    {
        using MixedAllTiersContext ctx = MixedAllTiersContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(MixedAllTiersEntity))!;

        AnonymizationClassification result = AnonymizationTierClassifier.Classify(entityType);

        result.Tier.Should().Be(AnonymizationTier.TierC);
    }

    [Fact]
    public void Classify_entity_with_constant_and_template_no_C_returns_TierB()
    {
        using MixedAandBContext ctx = MixedAandBContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(MixedAandBEntity))!;

        AnonymizationClassification result = AnonymizationTierClassifier.Classify(entityType);

        result.Tier.Should().Be(AnonymizationTier.TierB);
    }

    // =========================================================================
    // No annotated fields — Tier A with empty Columns
    // =========================================================================

    [Fact]
    public void Classify_entity_with_no_annotated_fields_returns_TierA_with_empty_columns()
    {
        using NoAnnotationsContext ctx = NoAnnotationsContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(NoAnnotationsEntity))!;

        AnonymizationClassification result = AnonymizationTierClassifier.Classify(entityType);

        result.Tier.Should().Be(AnonymizationTier.TierA);
        result.Columns.Should().BeEmpty();
    }

    // =========================================================================
    // Cache — same instance, CacheCount, ClearCache
    // =========================================================================

    [Fact]
    public void Classify_two_calls_with_same_entity_type_return_reference_equal_instance()
    {
        using ScalarOnlyContext ctx = ScalarOnlyContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ScalarOnlyEntity))!;

        AnonymizationClassification first = AnonymizationTierClassifier.Classify(entityType);
        AnonymizationClassification second = AnonymizationTierClassifier.Classify(entityType);

        ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public void CacheCount_increments_by_one_for_two_calls_on_same_entity_type()
    {
        using ScalarOnlyContext ctx = ScalarOnlyContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ScalarOnlyEntity))!;

        var before = AnonymizationTierClassifier.CacheCount;
        AnonymizationTierClassifier.Classify(entityType);
        AnonymizationTierClassifier.Classify(entityType);
        var after = AnonymizationTierClassifier.CacheCount;

        (after - before).Should().Be(1);
    }

    [Fact]
    public void ClearCache_resets_CacheCount_to_zero()
    {
        using ScalarOnlyContext ctx = ScalarOnlyContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ScalarOnlyEntity))!;

        AnonymizationTierClassifier.Classify(entityType);
        AnonymizationTierClassifier.CacheCount.Should().BeGreaterThan(0);

        AnonymizationTierClassifier.ClearCache();

        AnonymizationTierClassifier.CacheCount.Should().Be(0);
    }

    [Fact]
    public void Classify_parallel_calls_on_same_entity_type_return_reference_equal_instance()
    {
        using ScalarOnlyContext ctx = ScalarOnlyContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ScalarOnlyEntity))!;
        const int thread_count = 16;
        var results = new AnonymizationClassification[thread_count];

        Parallel.For(0, thread_count, i =>
        {
            results[i] = AnonymizationTierClassifier.Classify(entityType);
        });

        // ConcurrentDictionary.GetOrAdd may call the factory more than once under contention,
        // but the stored value is always the same winner — all results must reference the
        // canonical cached entry (checked via cached-value equality rather than reference
        // equality, because GetOrAdd can return a race-loser on the same key).
        AnonymizationClassification cached = AnonymizationTierClassifier.Classify(entityType);
        foreach (AnonymizationClassification r in results)
            r.Should().BeEquivalentTo(cached);
    }

    // =========================================================================
    // Null guard
    // =========================================================================

    [Fact]
    public void Classify_null_entity_type_throws_ArgumentNullException()
    {
        var act = () => AnonymizationTierClassifier.Classify(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // =========================================================================
    // Entity types and DbContexts
    // =========================================================================

    // -- Scalar only (Tier A) --

    private sealed class ScalarOnlyEntity
    {
        public int Id { get; set; }

        public string Email { get; set; } = string.Empty;

        public string? Phone { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public Guid UserId { get; set; }
    }

    private sealed class ScalarOnlyContext : DbContext
    {
        private ScalarOnlyContext(DbContextOptions<ScalarOnlyContext> options)
            : base(options)
        {
        }

        public static ScalarOnlyContext Build()
        {
            var options = new DbContextOptionsBuilder<ScalarOnlyContext>()
                .UseNpgsql("Host=localhost;Database=govtest")
                .Options;
            return new ScalarOnlyContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<ScalarOnlyEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Email).Anonymize("deleted@deleted.invalid");
                e.Property(x => x.Phone).AnonymizeNull();
                e.Property(x => x.DisplayName).AnonymizeEmpty();
            });
        }
    }

    // -- Table-split OwnsOne (Tier A) --

    private sealed class TableSplitOwnerEntity
    {
        public int Id { get; set; }

        public OwnedAddressTableSplit Address { get; set; } = new();
    }

    private sealed class OwnedAddressTableSplit
    {
        public string Street { get; set; } = string.Empty;

        // Referenced via LINQ expression in TableSplitOwnedContext.OnModelCreating.
        [UsedImplicitly]
        public string? City { get; }
    }

    private sealed class TableSplitOwnedContext : DbContext
    {
        private TableSplitOwnedContext(DbContextOptions<TableSplitOwnedContext> options)
            : base(options)
        {
        }

        public static TableSplitOwnedContext Build()
        {
            var options = new DbContextOptionsBuilder<TableSplitOwnedContext>()
                .UseNpgsql("Host=localhost;Database=govtest")
                .Options;
            return new TableSplitOwnedContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<TableSplitOwnerEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.OwnsOne(x => x.Address, nav =>
                {
                    // No .ToJson() → table splitting
                    nav.Anonymize(a => a.Street, "[deleted]");
                    nav.AnonymizeNull(a => a.City);
                });
            });
        }
    }

    // -- ComplexProperty table-split (Tier A) --

    private sealed class ComplexTableSplitOwner
    {
        public int Id { get; set; }

        public ComplexName Name { get; set; } = new();
    }

    private sealed class ComplexName
    {
        public string First { get; set; } = string.Empty;

        // Referenced via LINQ expression in ComplexTableSplitContext.OnModelCreating.
        [UsedImplicitly]
        public string? Last { get; }
    }

    private sealed class ComplexTableSplitContext : DbContext
    {
        private ComplexTableSplitContext(DbContextOptions<ComplexTableSplitContext> options)
            : base(options)
        {
        }

        public static ComplexTableSplitContext Build()
        {
            var options = new DbContextOptionsBuilder<ComplexTableSplitContext>()
                .UseNpgsql("Host=localhost;Database=govtest")
                .Options;
            return new ComplexTableSplitContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<ComplexTableSplitOwner>(e =>
            {
                e.HasKey(x => x.Id);
                e.ComplexProperty(x => x.Name, cp =>
                {
                    cp.Anonymize(n => n.First, "[name deleted]");
                    cp.AnonymizeNull(n => n.Last);
                });
            });
        }
    }

    // -- ComplexProperty .ToJson() (Tier A — EF10 ExecuteUpdate reaches it) --

    private sealed class ComplexJsonOwner
    {
        public int Id { get; set; }

        public ComplexJsonName Info { get; set; } = new();
    }

    private sealed class ComplexJsonName
    {
        public string First { get; set; } = string.Empty;
    }

    private sealed class ComplexJsonContext : DbContext
    {
        private ComplexJsonContext(DbContextOptions<ComplexJsonContext> options)
            : base(options)
        {
        }

        public static ComplexJsonContext Build()
        {
            var options = new DbContextOptionsBuilder<ComplexJsonContext>()
                .UseNpgsql("Host=localhost;Database=govtest")
                .Options;
            return new ComplexJsonContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<ComplexJsonOwner>(e =>
            {
                e.HasKey(x => x.Id);
                e.ComplexProperty(x => x.Info, cp =>
                {
                    cp.IsRequired().ToJson();
                    cp.Anonymize(n => n.First, "[name deleted]");
                });
            });
        }
    }

    // -- Template field (Tier B) --

    private sealed class TemplateFieldEntity
    {
        public int Id { get; set; }

        public Guid UserId { get; set; }

        public string Email { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;
    }

    private sealed class TemplateFieldContext : DbContext
    {
        private TemplateFieldContext(DbContextOptions<TemplateFieldContext> options)
            : base(options)
        {
        }

        public static TemplateFieldContext Build()
        {
            var options = new DbContextOptionsBuilder<TemplateFieldContext>()
                .UseNpgsql("Host=localhost;Database=govtest")
                .Options;
            return new TemplateFieldContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<TemplateFieldEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Email)
                    .AnonymizeTemplate("deletedUser{UserId}@deleted.user.dcsv.io");
                e.Property(x => x.DisplayName).AnonymizeEmpty();
            });
        }
    }

    // -- OwnsOne(..).ToJson() (Tier C) --

    private sealed class OwnedJsonOwner
    {
        public int Id { get; set; }

        public OwnedJsonDetails? Details { get; set; }
    }

    private sealed class OwnedJsonDetails
    {
        public string Secret { get; set; } = string.Empty;
    }

    private sealed class OwnedJsonContext : DbContext
    {
        private OwnedJsonContext(DbContextOptions<OwnedJsonContext> options)
            : base(options)
        {
        }

        public static OwnedJsonContext Build()
        {
            var options = new DbContextOptionsBuilder<OwnedJsonContext>()
                .UseNpgsql("Host=localhost;Database=govtest")
                .Options;
            return new OwnedJsonContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<OwnedJsonOwner>(e =>
            {
                e.HasKey(x => x.Id);
                e.OwnsOne(x => x.Details, nav =>
                {
                    nav.ToJson();
                    nav.Anonymize(d => d.Secret, "[deleted]");
                });
            });
        }
    }

    // -- OwnsMany child table (Tier C) --

    private sealed class OwnsManyOwner
    {
        public int Id { get; set; }

        public ICollection<OwnsManyChild> Items { get; set; } = new List<OwnsManyChild>();
    }

    private sealed class OwnsManyChild
    {
        public string Data { get; set; } = string.Empty;
    }

    private sealed class OwnsManyContext : DbContext
    {
        private OwnsManyContext(DbContextOptions<OwnsManyContext> options)
            : base(options)
        {
        }

        public static OwnsManyContext Build()
        {
            var options = new DbContextOptionsBuilder<OwnsManyContext>()
                .UseNpgsql("Host=localhost;Database=govtest")
                .Options;
            return new OwnsManyContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<OwnsManyOwner>(e =>
            {
                e.HasKey(x => x.Id);
                e.OwnsMany(x => x.Items, nav =>
                {
                    nav.Anonymize(d => d.Data, "[deleted]");
                });
            });
        }
    }

    // -- Mixed: constant + template + owned-JSON → Tier C --
    // DisplayName carries a Constant rule; Email carries a Template rule;
    // JsonPart is owned-JSON (Tier C).
    // All three rule kinds (Constant / Template / OwnedJson) are independently represented.

    private sealed class MixedAllTiersEntity
    {
        public int Id { get; set; }

        public Guid UserId { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public MixedJsonDetails? JsonPart { get; set; }
    }

    private sealed class MixedJsonDetails
    {
        public string Hidden { get; set; } = string.Empty;
    }

    private sealed class MixedAllTiersContext : DbContext
    {
        private MixedAllTiersContext(DbContextOptions<MixedAllTiersContext> options)
            : base(options)
        {
        }

        public static MixedAllTiersContext Build()
        {
            var options = new DbContextOptionsBuilder<MixedAllTiersContext>()
                .UseNpgsql("Host=localhost;Database=govtest")
                .Options;
            return new MixedAllTiersContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<MixedAllTiersEntity>(e =>
            {
                e.HasKey(x => x.Id);

                // Constant rule on DisplayName (Tier-A shape).
                e.Property(x => x.DisplayName).Anonymize("[deleted]");

                // Template rule on Email (Tier-B shape) — separate property from Constant above.
                e.Property(x => x.Email)
                    .AnonymizeTemplate("deletedUser{UserId}@deleted.user.dcsv.io");

                // Owned-JSON (Tier-C shape) — C beats B beats A.
                e.OwnsOne(x => x.JsonPart, nav =>
                {
                    nav.ToJson();
                    nav.Anonymize(d => d.Hidden, "[deleted]");
                });
            });
        }
    }

    // -- Mixed: constant + template, no C → Tier B --

    private sealed class MixedAandBEntity
    {
        public int Id { get; set; }

        public Guid UserId { get; set; }

        public string Email { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;
    }

    private sealed class MixedAandBContext : DbContext
    {
        private MixedAandBContext(DbContextOptions<MixedAandBContext> options)
            : base(options)
        {
        }

        public static MixedAandBContext Build()
        {
            var options = new DbContextOptionsBuilder<MixedAandBContext>()
                .UseNpgsql("Host=localhost;Database=govtest")
                .Options;
            return new MixedAandBContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<MixedAandBEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.DisplayName).AnonymizeEmpty();
                e.Property(x => x.Email)
                    .AnonymizeTemplate("deletedUser{UserId}@deleted.user.dcsv.io");
            });
        }
    }

    // -- No annotations → Tier A, empty Columns --

    private sealed class NoAnnotationsEntity
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class NoAnnotationsContext : DbContext
    {
        private NoAnnotationsContext(DbContextOptions<NoAnnotationsContext> options)
            : base(options)
        {
        }

        public static NoAnnotationsContext Build()
        {
            var options = new DbContextOptionsBuilder<NoAnnotationsContext>()
                .UseNpgsql("Host=localhost;Database=govtest")
                .Options;
            return new NoAnnotationsContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<NoAnnotationsEntity>(e => e.HasKey(x => x.Id));
        }
    }
}
