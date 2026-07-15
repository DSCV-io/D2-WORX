// -----------------------------------------------------------------------
// <copyright file="AnonymizationModelValidatorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.DataGovernance.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.DataGovernance.Abstractions;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// DB-free unit tests for <c>AnonymizationModelValidator</c>. Builds model-only contexts
/// (Npgsql provider, dummy connection string — connection never opened) and exercises
/// each validation rule plus the aggregation and opt-out paths.
/// </summary>
/// <remarks>
/// Serialized with the other <c>AnonymizationClassifier*</c> test classes because they all
/// call <c>AnonymizationTierClassifier.ClearCache()</c> against the process-global static
/// cache.
/// </remarks>
[Collection("AnonymizationClassifierSerial")]
[Trait("Category", "Unit")]
public sealed class AnonymizationModelValidatorTests
{
    // Clear the tier classifier cache between tests so entity-type identities across
    // different DbContext subclasses don't collide.
    public AnonymizationModelValidatorTests() => AnonymizationTierClassifier.ClearCache();

    // =========================================================================
    // PASS: good model completes without throwing
    // =========================================================================

    [Fact]
    public async Task StartAsync_GoodModel_completes_without_throwing()
    {
        await using var ctx = GoodModelContext.Build();
        var validator = BuildValidator(ctx);

        // No throw — test passes.
        await validator.StartAsync(default);
    }

    // =========================================================================
    // V1: missing ownership marker
    // =========================================================================

    [Fact]
    public async Task StartAsync_V1_no_ownership_marker_throws_naming_entity()
    {
        await using var ctx = V1NoOwnerContext.Build();
        var validator = BuildValidator(ctx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(default));

        ex.Message.Should().Contain("V1", "finding code must appear");
        ex.Message.Should().Contain(
            nameof(NoOwnerEntity),
            "entity type name must be named");
        ex.Message.Should().NotContain("row", "never include row data");
    }

    // =========================================================================
    // V2: missing IAnonymizationTrackable
    // =========================================================================

    [Fact]
    public async Task StartAsync_V2_missing_IAnonymizationTrackable_throws_naming_entity()
    {
        await using var ctx = V2NoTrackableContext.Build();
        var validator = BuildValidator(ctx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(default));

        ex.Message.Should().Contain("V2");
        ex.Message.Should().Contain(nameof(NoTrackableEntity));
    }

    // =========================================================================
    // V3: Tier-C owned-JSON shape
    // =========================================================================

    [Fact]
    public async Task StartAsync_V3_TierC_ownedJson_throws_naming_entity_and_property()
    {
        await using var ctx = V3OwnedJsonContext.Build();
        var validator = BuildValidator(ctx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(default));

        ex.Message.Should().Contain("V3");
        ex.Message.Should().Contain(nameof(TierCJsonEntity));
    }

    // =========================================================================
    // V3: Tier-C OwnsMany child shape
    // =========================================================================

    [Fact]
    public async Task StartAsync_V3_TierC_OwnsManyChild_throws_naming_entity()
    {
        await using var ctx = V3OwnsManyContext.Build();
        var validator = BuildValidator(ctx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(default));

        ex.Message.Should().Contain("V3");
        ex.Message.Should().Contain(nameof(OwnsManyParentEntity));
    }

    // =========================================================================
    // V4: bad template sibling
    // =========================================================================

    [Fact]
    public async Task StartAsync_V4_bad_template_token_throws_naming_bad_token()
    {
        await using var ctx = V4BadTemplateContext.Build();
        var validator = BuildValidator(ctx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(default));

        ex.Message.Should().Contain("V4");
        ex.Message.Should().Contain("NonExistentSibling", "bad token must be named");
    }

    // =========================================================================
    // V4: malformed template — parse failure branch (M-4)
    // =========================================================================

    [Fact]
    public async Task StartAsync_V4_malformed_template_brace_throws_naming_property()
    {
        await using var ctx = V4MalformedTemplateContext.Build();
        var validator = BuildValidator(ctx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(default));

        ex.Message.Should().Contain("V4", "finding code must appear");
        ex.Message.Should().Contain(
            nameof(MalformedTemplateEntity.Alias),
            "property name must appear in the finding");
    }

    // =========================================================================
    // V5: [Anonymizable] without ApplyAnonymizationConventions()
    // =========================================================================

    [Fact]
    public async Task StartAsync_V5_attribute_without_convention_throws_naming_property()
    {
        await using var ctx = V5NoConventionContext.Build();
        var validator = BuildValidator(ctx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(default));

        ex.Message.Should().Contain("V5");
        ex.Message.Should().Contain(nameof(AttributeOnlyEntity.Email));
    }

    // =========================================================================
    // V6: divergent attribute + fluent double-declaration
    // =========================================================================

    [Fact]
    public async Task StartAsync_V6_divergent_attribute_fluent_throws_naming_property()
    {
        await using var ctx = V6DivergentContext.Build();
        var validator = BuildValidator(ctx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(default));

        ex.Message.Should().Contain("V6");
        ex.Message.Should().Contain(nameof(DivergentEntity.Email));
    }

    // =========================================================================
    // V7: SetNull on non-nullable value type
    // =========================================================================

    [Fact]
    public async Task StartAsync_V7_SetNull_on_non_nullable_int_throws_naming_property()
    {
        await using var ctx = V7SetNullIntContext.Build();
        var validator = BuildValidator(ctx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(default));

        ex.Message.Should().Contain("V7");
        ex.Message.Should().Contain(nameof(BadNullableIntEntity.Count));
    }

    [Fact]
    public async Task StartAsync_V7_SetNull_on_non_nullable_Guid_throws_naming_property()
    {
        await using var ctx = V7SetNullGuidContext.Build();
        var validator = BuildValidator(ctx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(default));

        ex.Message.Should().Contain("V7");
        ex.Message.Should().Contain(nameof(BadNullableGuidEntity.TrackingCode));
    }

    [Fact]
    public async Task StartAsync_V7_SetNull_on_required_string_throws_naming_property()
    {
        await using var ctx = V7SetNullRequiredStringContext.Build();
        var validator = BuildValidator(ctx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(default));

        ex.Message.Should().Contain("V7");
        ex.Message.Should().Contain(nameof(BadNullableStringEntity.Name));
    }

    // =========================================================================
    // H-1 regression: non-exempt root that OwnsOne .ToJson() with [Anonymizable]
    // on the child → V3 on the ROOT, NOT V1/V2 on the child entity type
    // =========================================================================

    [Fact]
    public async Task StartAsync_NonExemptRootWithOwnedJsonChild_fires_V3_on_root_not_child()
    {
        await using var ctx = NonExemptOwnedJsonRootContext.Build();
        var validator = BuildValidator(ctx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(default));

        // V3 must appear (root is Tier-C due to the owned-JSON child's annotation).
        ex.Message.Should().Contain("V3", "root must be Tier-C");

        // V1 and V2 must NOT appear for the owned child sub-entity CLR type.
        ex.Message.Should().NotContain(
            $"V1 [{nameof(OwnedJsonPart)}]",
            "owned sub-entity must not get V1");
        ex.Message.Should().NotContain(
            $"V2 [{nameof(OwnedJsonPart)}]",
            "owned sub-entity must not get V2");
    }

    // =========================================================================
    // Exempt: IExemptFromAnonymization skips V2 and V3
    // =========================================================================

    [Fact]
    public async Task StartAsync_ExemptEntity_skips_V2_and_V3_checks_and_passes()
    {
        await using var ctx = ExemptContext.Build();
        var validator = BuildValidator(ctx);

        // Should NOT throw even though the exempt entity lacks IAnonymizationTrackable
        // and carries a Tier-C shape.
        await validator.StartAsync(default);
    }

    // =========================================================================
    // Multi-finding aggregation
    // =========================================================================

    [Fact]
    public async Task StartAsync_MultipleFindings_throws_with_all_findings_in_message()
    {
        await using var ctx = MultiViolationContext.Build();
        var validator = BuildValidator(ctx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(default));

        // Both V1 (un-owned) and V2 (no trackable) should appear in the single message.
        ex.Message.Should().Contain("V1");
        ex.Message.Should().Contain("V2");
        ex.Message.Should().Contain("misconfiguration(s)", "count must be present");
    }

    // =========================================================================
    // H-2 regression: V5/V6 must recurse into complex sub-properties
    // =========================================================================

    [Fact]
    public async Task StartAsync_V5_complex_sub_property_Anonymizable_without_convention_fires()
    {
        await using var ctx = V5ComplexNoConventionContext.Build();
        var validator = BuildValidator(ctx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(default));

        ex.Message.Should().Contain("V5", "finding code must appear");
        ex.Message.Should().Contain(
            nameof(ComplexNamePart.Code),
            "complex sub-property name must be named");
    }

    [Fact]
    public async Task StartAsync_V6_complex_sub_property_divergent_attribute_fluent_fires()
    {
        await using var ctx = V6ComplexDivergentContext.Build();
        var validator = BuildValidator(ctx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(default));

        ex.Message.Should().Contain("V6", "finding code must appear");
        ex.Message.Should().Contain(
            nameof(ComplexNamePart.Code),
            "complex sub-property name must be named");
    }

    // =========================================================================
    // SkipModelValidation = true: bad model does NOT throw
    // =========================================================================

    [Fact]
    public async Task StartAsync_SkipModelValidation_true_bad_model_does_not_throw()
    {
        await using var ctx = V1NoOwnerContext.Build();
        var validator = BuildValidator(ctx, skipValidation: true);

        // Should complete silently despite the V1 violation.
        await validator.StartAsync(default);
    }

    // =========================================================================
    // SkipModelValidation default is false
    // =========================================================================

    [Fact]
    public void AnonymizationEngineOptions_SkipModelValidation_default_is_false()
    {
        var opts = new AnonymizationEngineOptions();
        opts.SkipModelValidation.Should().BeFalse();
    }

    // =========================================================================
    // StopAsync: no-op returns completed task
    // =========================================================================

    [Fact]
    public async Task StopAsync_returns_completed_task()
    {
        await using var ctx = GoodModelContext.Build();
        var validator = BuildValidator(ctx);

        await validator.StopAsync(default);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static AnonymizationModelValidator BuildValidator(
        DbContext ctx,
        bool skipValidation = false)
    {
        var opts = Options.Create(new AnonymizationEngineOptions
        {
            SkipModelValidation = skipValidation,
        });
        var services = new TestServiceProvider(ctx);

        return new AnonymizationModelValidator(
            services,
            opts,
            NullLogger<AnonymizationModelValidator>.Instance);
    }

    private sealed class TestServiceProvider : IServiceProvider
    {
        private readonly DbContext r_ctx;

        public TestServiceProvider(DbContext ctx) => r_ctx = ctx;

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IServiceScopeFactory))
                return new TestScopeFactory(r_ctx);

            return null;
        }
    }

    private sealed class TestScopeFactory : IServiceScopeFactory
    {
        private readonly DbContext r_ctx;

        public TestScopeFactory(DbContext ctx) => r_ctx = ctx;

        public IServiceScope CreateScope() => new TestScope(r_ctx);
    }

    private sealed class TestScope : IServiceScope
    {
        public TestScope(DbContext ctx) => ServiceProvider = new TestScopedProvider(ctx);

        public IServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
            // Scope disposal — context lifetime managed by the caller.
        }
    }

    private sealed class TestScopedProvider : IServiceProvider
    {
        private readonly DbContext r_ctx;

        public TestScopedProvider(DbContext ctx) => r_ctx = ctx;

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(DbContext))
                return r_ctx;

            return null;
        }
    }

    // =========================================================================
    // Model entity shapes
    // =========================================================================

    private sealed class GoodEntity : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        [Anonymizable(AnonymizeKind.SetNull)]
        public string? Email { get; set; }

        [Anonymizable("[deleted]")]
        public string Name { get; set; } = string.Empty;

        public bool IsAnonymized { get; set; }
    }

    private sealed class NoOwnerEntity
    {
        public Guid Id { get; set; }

        [Anonymizable("[deleted]")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class NoTrackableEntity : IUserOwned
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        [Anonymizable("[deleted]")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TierCJsonEntity : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        public TierCJsonPart? JsonPart { get; set; }

        public bool IsAnonymized { get; set; }
    }

    private sealed class TierCJsonPart
    {
        public string Secret { get; set; } = string.Empty;
    }

    private sealed class OwnsManyParentEntity : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        public bool IsAnonymized { get; set; }

        public List<OwnsManyChildItem> Tags { get; set; } = [];
    }

    private sealed class OwnsManyChildItem
    {
        public string Label { get; set; } = string.Empty;
    }

    private sealed class BadTemplateEntity : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsAnonymized { get; set; }
    }

    private sealed class AttributeOnlyEntity : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        [Anonymizable("[deleted]")]
        public string Email { get; set; } = string.Empty;

        public bool IsAnonymized { get; set; }
    }

    private sealed class DivergentEntity : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        [Anonymizable(AnonymizeKind.SetEmpty)]
        public string Email { get; set; } = string.Empty;

        public bool IsAnonymized { get; set; }
    }

    private sealed class BadNullableIntEntity : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        public int Count { get; set; }

        public bool IsAnonymized { get; set; }
    }

    private sealed class BadNullableGuidEntity : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        public Guid TrackingCode { get; set; }

        public bool IsAnonymized { get; set; }
    }

    private sealed class BadNullableStringEntity : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsAnonymized { get; set; }
    }

    private sealed class NonExemptRootWithOwnedJson : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        public OwnedJsonPart? JsonPart { get; set; }

        public bool IsAnonymized { get; set; }
    }

    private sealed class OwnedJsonPart
    {
        public string Secret { get; set; } = string.Empty;
    }

    private sealed class ExemptedEntity : IUserOwned, IExemptFromAnonymization
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        public ExemptOwnedJson? JsonData { get; set; }
    }

    private sealed class ExemptOwnedJson
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class MultiViolationEntity
    {
        public Guid Id { get; set; }

        [Anonymizable("[deleted]")]
        public string Data { get; set; } = string.Empty;
    }

    private sealed class MalformedTemplateEntity : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        public string Alias { get; set; } = string.Empty;

        public bool IsAnonymized { get; set; }
    }

    // =========================================================================
    // Model contexts
    // =========================================================================

    private sealed class GoodModelContext : DbContext
    {
        private GoodModelContext(DbContextOptions<GoodModelContext> options)
            : base(options)
        {
        }

        internal static GoodModelContext Build()
        {
            var opts = new DbContextOptionsBuilder<GoodModelContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new GoodModelContext(opts);
        }

        protected override void ConfigureConventions(
            ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.ApplyAnonymizationConventions();
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<GoodEntity>(e => e.HasKey(x => x.Id));
        }
    }

    private sealed class V1NoOwnerContext : DbContext
    {
        private V1NoOwnerContext(DbContextOptions<V1NoOwnerContext> options)
            : base(options)
        {
        }

        internal static V1NoOwnerContext Build()
        {
            var opts = new DbContextOptionsBuilder<V1NoOwnerContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new V1NoOwnerContext(opts);
        }

        protected override void ConfigureConventions(
            ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.ApplyAnonymizationConventions();
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<NoOwnerEntity>(e => e.HasKey(x => x.Id));
        }
    }

    private sealed class V2NoTrackableContext : DbContext
    {
        private V2NoTrackableContext(DbContextOptions<V2NoTrackableContext> options)
            : base(options)
        {
        }

        internal static V2NoTrackableContext Build()
        {
            var opts = new DbContextOptionsBuilder<V2NoTrackableContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new V2NoTrackableContext(opts);
        }

        protected override void ConfigureConventions(
            ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.ApplyAnonymizationConventions();
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<NoTrackableEntity>(e => e.HasKey(x => x.Id));
        }
    }

    private sealed class V3OwnedJsonContext : DbContext
    {
        private V3OwnedJsonContext(DbContextOptions<V3OwnedJsonContext> options)
            : base(options)
        {
        }

        internal static V3OwnedJsonContext Build()
        {
            var opts = new DbContextOptionsBuilder<V3OwnedJsonContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new V3OwnedJsonContext(opts);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<TierCJsonEntity>(e =>
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

    private sealed class V3OwnsManyContext : DbContext
    {
        private V3OwnsManyContext(DbContextOptions<V3OwnsManyContext> options)
            : base(options)
        {
        }

        internal static V3OwnsManyContext Build()
        {
            var opts = new DbContextOptionsBuilder<V3OwnsManyContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new V3OwnsManyContext(opts);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<OwnsManyParentEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.OwnsMany(x => x.Tags, nav =>
                    nav.Anonymize(d => d.Label, "[deleted]"));
            });
        }
    }

    private sealed class V4BadTemplateContext : DbContext
    {
        private V4BadTemplateContext(DbContextOptions<V4BadTemplateContext> options)
            : base(options)
        {
        }

        internal static V4BadTemplateContext Build()
        {
            var opts = new DbContextOptionsBuilder<V4BadTemplateContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new V4BadTemplateContext(opts);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<BadTemplateEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Name)
                    .AnonymizeTemplate("deleted_{NonExistentSibling}@deleted.invalid");
            });
        }
    }

    private sealed class V5NoConventionContext : DbContext
    {
        private V5NoConventionContext(DbContextOptions<V5NoConventionContext> options)
            : base(options)
        {
        }

        internal static V5NoConventionContext Build()
        {
            var opts = new DbContextOptionsBuilder<V5NoConventionContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new V5NoConventionContext(opts);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<AttributeOnlyEntity>(e => e.HasKey(x => x.Id));
        }
    }

    private sealed class V6DivergentContext : DbContext
    {
        private V6DivergentContext(DbContextOptions<V6DivergentContext> options)
            : base(options)
        {
        }

        internal static V6DivergentContext Build()
        {
            var opts = new DbContextOptionsBuilder<V6DivergentContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new V6DivergentContext(opts);
        }

        protected override void ConfigureConventions(
            ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.ApplyAnonymizationConventions();
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<DivergentEntity>(e =>
            {
                e.HasKey(x => x.Id);

                // Attribute says SetEmpty; fluent writes Constant("[diverged]") — divergent.
                e.Property(x => x.Email).Anonymize("[diverged]");
            });
        }
    }

    private sealed class V7SetNullIntContext : DbContext
    {
        private V7SetNullIntContext(DbContextOptions<V7SetNullIntContext> options)
            : base(options)
        {
        }

        internal static V7SetNullIntContext Build()
        {
            var opts = new DbContextOptionsBuilder<V7SetNullIntContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new V7SetNullIntContext(opts);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<BadNullableIntEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Count).AnonymizeNull();
            });
        }
    }

    private sealed class V7SetNullGuidContext : DbContext
    {
        private V7SetNullGuidContext(DbContextOptions<V7SetNullGuidContext> options)
            : base(options)
        {
        }

        internal static V7SetNullGuidContext Build()
        {
            var opts = new DbContextOptionsBuilder<V7SetNullGuidContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new V7SetNullGuidContext(opts);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<BadNullableGuidEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.TrackingCode).AnonymizeNull();
            });
        }
    }

    private sealed class V7SetNullRequiredStringContext : DbContext
    {
        private V7SetNullRequiredStringContext(
            DbContextOptions<V7SetNullRequiredStringContext> options)
            : base(options)
        {
        }

        internal static V7SetNullRequiredStringContext Build()
        {
            var opts = new DbContextOptionsBuilder<V7SetNullRequiredStringContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new V7SetNullRequiredStringContext(opts);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<BadNullableStringEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).IsRequired().AnonymizeNull();
            });
        }
    }

    private sealed class NonExemptOwnedJsonRootContext : DbContext
    {
        private NonExemptOwnedJsonRootContext(
            DbContextOptions<NonExemptOwnedJsonRootContext> options)
            : base(options)
        {
        }

        internal static NonExemptOwnedJsonRootContext Build()
        {
            var opts = new DbContextOptionsBuilder<NonExemptOwnedJsonRootContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new NonExemptOwnedJsonRootContext(opts);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<NonExemptRootWithOwnedJson>(e =>
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

    private sealed class ExemptContext : DbContext
    {
        private ExemptContext(DbContextOptions<ExemptContext> options)
            : base(options)
        {
        }

        internal static ExemptContext Build()
        {
            var opts = new DbContextOptionsBuilder<ExemptContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new ExemptContext(opts);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<ExemptedEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.OwnsOne(x => x.JsonData, nav =>
                {
                    nav.ToJson();
                    nav.Anonymize(d => d.Value, "[exempt-tombstone]");
                });
            });
        }
    }

    private sealed class V4MalformedTemplateContext : DbContext
    {
        private V4MalformedTemplateContext(
            DbContextOptions<V4MalformedTemplateContext> options)
            : base(options)
        {
        }

        internal static V4MalformedTemplateContext Build()
        {
            var opts = new DbContextOptionsBuilder<V4MalformedTemplateContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new V4MalformedTemplateContext(opts);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<MalformedTemplateEntity>(e =>
            {
                e.HasKey(x => x.Id);

                // Unmatched opening brace — triggers the ArgumentException catch in
                // ValidateV4Templates, firing the parse-failure branch of V4.
                e.Property(x => x.Alias)
                    .AnonymizeTemplate("deleted_{malformed");
            });
        }
    }

    private sealed class MultiViolationContext : DbContext
    {
        private MultiViolationContext(DbContextOptions<MultiViolationContext> options)
            : base(options)
        {
        }

        internal static MultiViolationContext Build()
        {
            var opts = new DbContextOptionsBuilder<MultiViolationContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new MultiViolationContext(opts);
        }

        protected override void ConfigureConventions(
            ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.ApplyAnonymizationConventions();
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<MultiViolationEntity>(e => e.HasKey(x => x.Id));
        }
    }

    private sealed class ComplexOwnerForV5 : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        public ComplexNamePart Info { get; set; } = new();

        public bool IsAnonymized { get; set; }
    }

    private sealed class ComplexNamePart
    {
        [Anonymizable("[deleted-code]")]
        public string Code { get; set; } = string.Empty;
    }

    private sealed class ComplexOwnerForV6 : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        public ComplexNamePart Info { get; set; } = new();

        public bool IsAnonymized { get; set; }
    }

    // V5: [Anonymizable] on complex sub-property, no ApplyAnonymizationConventions()
    private sealed class V5ComplexNoConventionContext : DbContext
    {
        private V5ComplexNoConventionContext(
            DbContextOptions<V5ComplexNoConventionContext> options)
            : base(options)
        {
        }

        internal static V5ComplexNoConventionContext Build()
        {
            var opts = new DbContextOptionsBuilder<V5ComplexNoConventionContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new V5ComplexNoConventionContext(opts);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            // No ApplyAnonymizationConventions() → [Anonymizable] on ComplexNamePart.Code
            // produces no annotation → V5 must fire.
            model.Entity<ComplexOwnerForV5>(e =>
            {
                e.HasKey(x => x.Id);
                e.ComplexProperty(x => x.Info);
            });
        }
    }

    // V6: [Anonymizable] on complex sub-property + divergent fluent override
    private sealed class V6ComplexDivergentContext : DbContext
    {
        private V6ComplexDivergentContext(
            DbContextOptions<V6ComplexDivergentContext> options)
            : base(options)
        {
        }

        internal static V6ComplexDivergentContext Build()
        {
            var opts = new DbContextOptionsBuilder<V6ComplexDivergentContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new V6ComplexDivergentContext(opts);
        }

        protected override void ConfigureConventions(
            ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.ApplyAnonymizationConventions();
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            // Attribute says Constant("[deleted-code]"); fluent writes SetEmpty — divergent.
            model.Entity<ComplexOwnerForV6>(e =>
            {
                e.HasKey(x => x.Id);
                e.ComplexProperty(x => x.Info, cp =>
                    cp.AnonymizeEmpty(c => c.Code));
            });
        }
    }
}
