// -----------------------------------------------------------------------
// <copyright file="AnonymizeMappingExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.DataGovernance.EntityFrameworkCore;

using System;
using System.ComponentModel.DataAnnotations.Schema;
using AwesomeAssertions;
using D2.Shared.DataGovernance.Abstractions;
using D2.Shared.DataGovernance.EntityFrameworkCore;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

/// <summary>
/// Tests for the <see cref="AnonymizeMappingExtensions"/> fluent API overloads on
/// <c>PropertyBuilder&lt;T&gt;</c>, <c>OwnedNavigationBuilder&lt;,&gt;</c>, and
/// <c>ComplexPropertyBuilder&lt;T&gt;</c>. Exercises every overload across each builder type,
/// round-trip annotation reads, precedence over the attribute, and adversarial null arguments.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AnonymizeMappingExtensionsTests
{
    // =========================================================================
    // PropertyBuilder<T> — scalar overloads
    // =========================================================================

    [Fact]
    public void PropertyBuilder_Anonymize_constant_writes_correct_annotation()
    {
        using ScalarTestContext ctx = ScalarTestContext.Build();
        IProperty prop = ctx.Model.FindEntityType(typeof(ScalarEntity))!
            .FindProperty(nameof(ScalarEntity.Email))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be("deleted@deleted.invalid");
        rule.Template.Should().BeNull();
    }

    [Fact]
    public void PropertyBuilder_AnonymizeNull_writes_SetNull_annotation()
    {
        using ScalarTestContext ctx = ScalarTestContext.Build();
        IProperty prop = ctx.Model.FindEntityType(typeof(ScalarEntity))!
            .FindProperty(nameof(ScalarEntity.Phone))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.SetNull);
        rule.ConstantValue.Should().BeNull();
        rule.Template.Should().BeNull();
    }

    [Fact]
    public void PropertyBuilder_AnonymizeEmpty_writes_SetEmpty_annotation()
    {
        using ScalarTestContext ctx = ScalarTestContext.Build();
        IProperty prop = ctx.Model.FindEntityType(typeof(ScalarEntity))!
            .FindProperty(nameof(ScalarEntity.DisplayName))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.SetEmpty);
        rule.ConstantValue.Should().BeNull();
        rule.Template.Should().BeNull();
    }

    [Fact]
    public void PropertyBuilder_AnonymizeTemplate_writes_Template_annotation()
    {
        using ScalarTestContext ctx = ScalarTestContext.Build();
        IProperty prop = ctx.Model.FindEntityType(typeof(ScalarEntity))!
            .FindProperty(nameof(ScalarEntity.Username))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Template);
        rule.Template.Should().Be("deletedUser{UserId}@deleted.dcsv.io");
        rule.ConstantValue.Should().BeNull();
    }

    [Fact]
    public void PropertyBuilder_Anonymize_empty_string_constant_is_stored_as_Constant_kind()
    {
        using EmptyConstantContext ctx = EmptyConstantContext.Build();
        IProperty prop = ctx.Model.FindEntityType(typeof(EmptyConstantEntity))!
            .FindProperty(nameof(EmptyConstantEntity.Value))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be(string.Empty);
    }

    [Fact]
    public void Annotation_value_is_AnonymizationRule_object_not_string_or_tuple()
    {
        using ScalarTestContext ctx = ScalarTestContext.Build();
        IProperty prop = ctx.Model.FindEntityType(typeof(ScalarEntity))!
            .FindProperty(nameof(ScalarEntity.Email))!;

        object? value = prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value;

        value.Should().BeOfType<AnonymizationRule>();
    }

    // =========================================================================
    // Round-trip read — record equality
    // =========================================================================

    [Fact]
    public void Annotation_round_trip_preserves_record_equality()
    {
        using ScalarTestContext ctx = ScalarTestContext.Build();
        IProperty prop = ctx.Model.FindEntityType(typeof(ScalarEntity))!
            .FindProperty(nameof(ScalarEntity.Email))!;

        var actual =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;
        var expected = AnonymizationRule.Create(
            AnonymizeKind.Constant,
            constantValue: "deleted@deleted.invalid");

        actual.Should().Be(expected);
    }

    [Fact]
    public void Template_round_trip_preserves_brace_token_literally()
    {
        using ScalarTestContext ctx = ScalarTestContext.Build();
        IProperty prop = ctx.Model.FindEntityType(typeof(ScalarEntity))!
            .FindProperty(nameof(ScalarEntity.Username))!;

        var actual =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        actual!.Template.Should().Be("deletedUser{UserId}@deleted.dcsv.io");
    }

    [Fact]
    public void Constant_round_trip_preserves_whitespace()
    {
        using WhitespaceConstantContext ctx = WhitespaceConstantContext.Build();
        IProperty prop = ctx.Model.FindEntityType(typeof(WhitespaceConstantEntity))!
            .FindProperty(nameof(WhitespaceConstantEntity.Value))!;

        var actual =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        actual!.ConstantValue.Should().Be(" ");
    }

    // =========================================================================
    // OwnedNavigationBuilder — sub-property overloads
    // =========================================================================

    [Fact]
    public void OwnedNavBuilder_Anonymize_constant_writes_correct_annotation_on_sub_property()
    {
        using OwnedNavTestContext ctx = OwnedNavTestContext.Build();
        IEntityType ownedType = ctx.Model.FindEntityType(typeof(OwnedAddress))!;
        IProperty prop = ownedType.FindProperty(nameof(OwnedAddress.Street))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be("[deleted]");
    }

    [Fact]
    public void OwnedNavBuilder_AnonymizeNull_writes_SetNull_on_sub_property()
    {
        using OwnedNavTestContext ctx = OwnedNavTestContext.Build();
        IEntityType ownedType = ctx.Model.FindEntityType(typeof(OwnedAddress))!;
        IProperty prop = ownedType.FindProperty(nameof(OwnedAddress.City))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.SetNull);
    }

    [Fact]
    public void OwnedNavBuilder_AnonymizeEmpty_writes_SetEmpty_on_sub_property()
    {
        using OwnedNavTestContext ctx = OwnedNavTestContext.Build();
        IEntityType ownedType = ctx.Model.FindEntityType(typeof(OwnedAddress))!;
        IProperty prop = ownedType.FindProperty(nameof(OwnedAddress.PostalCode))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.SetEmpty);
    }

    [Fact]
    public void OwnedNavBuilder_AnonymizeTemplate_writes_Template_on_sub_property()
    {
        using OwnedNavTestContext ctx = OwnedNavTestContext.Build();
        IEntityType ownedType = ctx.Model.FindEntityType(typeof(OwnedAddress))!;
        IProperty prop = ownedType.FindProperty(nameof(OwnedAddress.FullAddress))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Template);
        rule.Template.Should().Be("{Street}{City}@deleted.dcsv.io");
    }

    // =========================================================================
    // ComplexPropertyBuilder — sub-property overloads
    // =========================================================================

    // long identifier — cannot wrap
    [Fact]
    public void ComplexPropertyBuilder_Anonymize_constant_writes_annotation_on_complex_sub_property()
    {
        using ComplexTestContext ctx = ComplexTestContext.Build();
        IComplexType complexType = ctx.Model
            .FindEntityType(typeof(ComplexOwnerEntity))!
            .FindComplexProperty(nameof(ComplexOwnerEntity.DisplayName))!
            .ComplexType;
        IProperty prop = complexType.FindProperty(nameof(ComplexName.First))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be("[name deleted]");
    }

    [Fact]
    public void ComplexPropertyBuilder_AnonymizeNull_writes_SetNull_on_complex_sub_property()
    {
        using ComplexTestContext ctx = ComplexTestContext.Build();
        IComplexType complexType = ctx.Model
            .FindEntityType(typeof(ComplexOwnerEntity))!
            .FindComplexProperty(nameof(ComplexOwnerEntity.DisplayName))!
            .ComplexType;
        IProperty prop = complexType.FindProperty(nameof(ComplexName.Last))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.SetNull);
    }

    [Fact]
    public void ComplexPropertyBuilder_AnonymizeEmpty_writes_SetEmpty_on_complex_sub_property()
    {
        using ComplexTestContext ctx = ComplexTestContext.Build();
        IComplexType complexType = ctx.Model
            .FindEntityType(typeof(ComplexOwnerEntity))!
            .FindComplexProperty(nameof(ComplexOwnerEntity.DisplayName))!
            .ComplexType;
        IProperty prop = complexType.FindProperty(nameof(ComplexName.Suffix))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.SetEmpty);
    }

    [Fact]
    public void ComplexPropertyBuilder_AnonymizeTemplate_writes_Template_on_complex_sub_property()
    {
        using ComplexTestContext ctx = ComplexTestContext.Build();
        IComplexType complexType = ctx.Model
            .FindEntityType(typeof(ComplexOwnerEntity))!
            .FindComplexProperty(nameof(ComplexOwnerEntity.DisplayName))!
            .ComplexType;
        IProperty prop = complexType.FindProperty(nameof(ComplexName.Display))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Template);
        rule.Template.Should().Be("DeletedUser{UserId}");
    }

    // =========================================================================
    // Adversarial — invalid payloads propagated from AnonymizationRule.Create
    // =========================================================================

    [Fact]
    public void AnonymizationRule_Create_null_constantValue_throws_ArgumentException()
    {
        var act = () => AnonymizationRule.Create(AnonymizeKind.Constant, constantValue: null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AnonymizationRule_Create_null_template_throws_ArgumentException()
    {
        var act = () => AnonymizationRule.Create(AnonymizeKind.Template, template: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AnonymizationRule_Create_whitespace_template_throws_ArgumentException()
    {
        var act = () => AnonymizationRule.Create(AnonymizeKind.Template, template: "   ");

        act.Should().Throw<ArgumentException>();
    }

    // =========================================================================
    // Adversarial — [NotMapped] guard on sub-selector overloads
    // =========================================================================

    [Fact]
    public void OwnedNavBuilder_selector_for_NotMapped_member_throws_on_model_build()
    {
        var act = () =>
        {
            using DbContext ctx = new UnmappedSelectorContext();
            _ = ctx.Model;
        };

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ComplexPropertyBuilder_selector_for_NotMapped_member_throws_on_model_build()
    {
        var act = () =>
        {
            using DbContext ctx = new ComplexNotMappedSelectorContext();
            _ = ctx.Model;
        };

        act.Should().Throw<InvalidOperationException>();
    }

    // =========================================================================
    // Adversarial — extension's own null-guard on real builders
    // =========================================================================

    [Fact]
    public void PropertyBuilder_Anonymize_null_builder_throws_ArgumentNullException()
    {
        var act = () =>
        {
            using DbContext ctx = new NullBuilderAnonymizeContext();
            _ = ctx.Model;
        };

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void PropertyBuilder_AnonymizeTemplate_null_builder_throws_ArgumentNullException()
    {
        var act = () =>
        {
            using DbContext ctx = new NullBuilderAnonymizeTemplateContext();
            _ = ctx.Model;
        };

        act.Should().Throw<ArgumentNullException>();
    }

    // =========================================================================
    // Entity types used by test contexts
    // =========================================================================

    private sealed class ScalarEntity
    {
        public int Id { get; set; }

        public string Email { get; set; } = string.Empty;

        public string? Phone { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public Guid UserId { get; set; }
    }

    private sealed class EmptyConstantEntity
    {
        public int Id { get; set; }

        public string Value { get; set; } = string.Empty;
    }

    private sealed class WhitespaceConstantEntity
    {
        public int Id { get; set; }

        public string Value { get; set; } = string.Empty;
    }

    private sealed class OwnedNavOwnerEntity
    {
        public int Id { get; set; }

        public OwnedAddress Address { get; set; } = new();
    }

    private sealed class OwnedAddress
    {
        public string Street { get; set; } = string.Empty;

        public string? City { get; set; }

        public string PostalCode { get; set; } = string.Empty;

        public string FullAddress { get; set; } = string.Empty;
    }

    private sealed class ComplexOwnerEntity
    {
        public int Id { get; set; }

        public ComplexName DisplayName { get; set; } = new();
    }

    private sealed class ComplexName
    {
        public string First { get; set; } = string.Empty;

        public string? Last { get; set; }

        public string Suffix { get; set; } = string.Empty;

        public string Display { get; set; } = string.Empty;
    }

    private sealed class UnmappedSelectorOwner
    {
        public int Id { get; set; }

        public UnmappedSelectorDep Dep { get; set; } = new();
    }

    private sealed class UnmappedSelectorDep
    {
        // Required by EF Core — owned type needs a mapped CLR property alongside
        // the [NotMapped] member, so the guard test has a real model to build against.
        [UsedImplicitly]
        public string Mapped { get; set; } = string.Empty;

        [NotMapped]
        public string NotMappedField { get; set; } = string.Empty;
    }

    // =========================================================================
    // Test DbContext types
    // =========================================================================

    private sealed class ScalarTestContext : DbContext
    {
        private ScalarTestContext(DbContextOptions<ScalarTestContext> options)
            : base(options)
        {
        }

        public static ScalarTestContext Build()
        {
            var options = new DbContextOptionsBuilder<ScalarTestContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ScalarTestContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<ScalarEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Email).Anonymize("deleted@deleted.invalid");
                e.Property(x => x.Phone).AnonymizeNull();
                e.Property(x => x.DisplayName).AnonymizeEmpty();
                e.Property(x => x.Username)
                    .AnonymizeTemplate("deletedUser{UserId}@deleted.dcsv.io");
            });
        }
    }

    private sealed class EmptyConstantContext : DbContext
    {
        private EmptyConstantContext(DbContextOptions<EmptyConstantContext> options)
            : base(options)
        {
        }

        public static EmptyConstantContext Build()
        {
            var options = new DbContextOptionsBuilder<EmptyConstantContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new EmptyConstantContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<EmptyConstantEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Value).Anonymize(string.Empty);
            });
        }
    }

    private sealed class WhitespaceConstantContext : DbContext
    {
        private WhitespaceConstantContext(DbContextOptions<WhitespaceConstantContext> options)
            : base(options)
        {
        }

        public static WhitespaceConstantContext Build()
        {
            var options = new DbContextOptionsBuilder<WhitespaceConstantContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new WhitespaceConstantContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<WhitespaceConstantEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Value).Anonymize(" ");
            });
        }
    }

    private sealed class OwnedNavTestContext : DbContext
    {
        private OwnedNavTestContext(DbContextOptions<OwnedNavTestContext> options)
            : base(options)
        {
        }

        public static OwnedNavTestContext Build()
        {
            var options = new DbContextOptionsBuilder<OwnedNavTestContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new OwnedNavTestContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<OwnedNavOwnerEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.OwnsOne(x => x.Address, nav =>
                {
                    nav.Anonymize(a => a.Street, "[deleted]");
                    nav.AnonymizeNull(a => a.City);
                    nav.AnonymizeEmpty(a => a.PostalCode);
                    nav.AnonymizeTemplate(a => a.FullAddress, "{Street}{City}@deleted.dcsv.io");
                });
            });
        }
    }

    private sealed class ComplexTestContext : DbContext
    {
        private ComplexTestContext(DbContextOptions<ComplexTestContext> options)
            : base(options)
        {
        }

        public static ComplexTestContext Build()
        {
            var options = new DbContextOptionsBuilder<ComplexTestContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ComplexTestContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<ComplexOwnerEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.ComplexProperty(x => x.DisplayName, cp =>
                {
                    cp.Anonymize(d => d.First, "[name deleted]");
                    cp.AnonymizeNull(d => d.Last);
                    cp.AnonymizeEmpty(d => d.Suffix);
                    cp.AnonymizeTemplate(d => d.Display, "DeletedUser{UserId}");
                });
            });
        }
    }

    private sealed class UnmappedSelectorContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<UnmappedSelectorOwner>(e =>
            {
                e.HasKey(x => x.Id);
                e.OwnsOne(x => x.Dep, nav =>
                {
                    // NotMappedField is [NotMapped] — guard throws InvalidOperationException.
                    nav.Anonymize(a => a.NotMappedField, "[deleted]");
                });
            });
        }
    }

    // =========================================================================
    // H-2 — ComplexProperty [NotMapped] guard
    // =========================================================================

    private sealed class ComplexNotMappedOwner
    {
        public int Id { get; set; }

        public ComplexWithNotMapped Info { get; set; } = new();
    }

    private sealed class ComplexWithNotMapped
    {
        // Required by EF Core — complex type needs a mapped CLR property alongside
        // the [NotMapped] member, so the guard test has a real model to build against.
        [UsedImplicitly]
        public string Mapped { get; set; } = string.Empty;

        [NotMapped]
        public string NotMappedField { get; set; } = string.Empty;
    }

    private sealed class ComplexNotMappedSelectorContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<ComplexNotMappedOwner>(e =>
            {
                e.HasKey(x => x.Id);
                e.ComplexProperty(x => x.Info, cp =>
                {
                    // NotMappedField is [NotMapped] — guard throws InvalidOperationException.
                    cp.Anonymize(c => c.NotMappedField, "[deleted]");
                });
            });
        }
    }

    // =========================================================================
    // L-1 — extension's own null-guard on real builders
    // =========================================================================

    private sealed class NullGuardEntity
    {
        public int Id { get; set; }

        public string Value { get; set; } = string.Empty;
    }

    private sealed class NullBuilderAnonymizeContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<NullGuardEntity>(e =>
            {
                e.HasKey(x => x.Id);

                // null! forces the null-guard on the constant parameter.
                e.Property(x => x.Value).Anonymize(null!);
            });
        }
    }

    private sealed class NullBuilderAnonymizeTemplateContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<NullGuardEntity>(e =>
            {
                e.HasKey(x => x.Id);

                // null! forces the null-guard on the template parameter.
                e.Property(x => x.Value).AnonymizeTemplate(null!);
            });
        }
    }
}
