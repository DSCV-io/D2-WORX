// -----------------------------------------------------------------------
// <copyright file="AnonymizeMappingExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.DataGovernance.EntityFrameworkCore;

using System;
using System.ComponentModel.DataAnnotations.Schema;
using AwesomeAssertions;
using DcsvIo.D2.DataGovernance.Abstractions;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xunit;

/// <summary>
/// Tests for the <see cref="AnonymizeMappingExtensions"/> fluent API overloads on
/// <c>PropertyBuilder&lt;T&gt;</c>, <c>OwnedNavigationBuilder&lt;,&gt;</c>,
/// <c>ComplexPropertyBuilder&lt;T&gt;</c>, and
/// <c>ComplexTypePropertyBuilder&lt;T&gt;</c>. Exercises every overload across each builder
/// type, round-trip annotation reads, precedence over the attribute, adversarial null
/// arguments, and the chain-shape compile proof for the
/// <c>cp.Property(lambda).HasMaxLength(n).Anonymize*(…)</c> pattern.
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
    // ComplexTypePropertyBuilder<T> — no-selector overloads (cp.Property(lambda))
    // =========================================================================

    [Fact]
    public void ComplexTypePropertyBuilder_Anonymize_constant_writes_correct_annotation()
    {
        using ComplexTypePropertyBuilderTestContext ctx =
            ComplexTypePropertyBuilderTestContext.Build();
        IComplexType complexType = ctx.Model
            .FindEntityType(typeof(ComplexTypePropOwner))!
            .FindComplexProperty(nameof(ComplexTypePropOwner.Info))!
            .ComplexType;
        IProperty prop = complexType.FindProperty(nameof(ComplexTypePropInfo.Code))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be("cleared");
        rule.Template.Should().BeNull();
    }

    [Fact]
    public void ComplexTypePropertyBuilder_AnonymizeNull_writes_SetNull_annotation()
    {
        using ComplexTypePropertyBuilderTestContext ctx =
            ComplexTypePropertyBuilderTestContext.Build();
        IComplexType complexType = ctx.Model
            .FindEntityType(typeof(ComplexTypePropOwner))!
            .FindComplexProperty(nameof(ComplexTypePropOwner.Info))!
            .ComplexType;
        IProperty prop = complexType.FindProperty(nameof(ComplexTypePropInfo.Optional))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.SetNull);
        rule.ConstantValue.Should().BeNull();
        rule.Template.Should().BeNull();
    }

    [Fact]
    public void ComplexTypePropertyBuilder_AnonymizeEmpty_writes_SetEmpty_annotation()
    {
        using ComplexTypePropertyBuilderTestContext ctx =
            ComplexTypePropertyBuilderTestContext.Build();
        IComplexType complexType = ctx.Model
            .FindEntityType(typeof(ComplexTypePropOwner))!
            .FindComplexProperty(nameof(ComplexTypePropOwner.Info))!
            .ComplexType;
        IProperty prop = complexType.FindProperty(nameof(ComplexTypePropInfo.Slug))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.SetEmpty);
        rule.ConstantValue.Should().BeNull();
        rule.Template.Should().BeNull();
    }

    [Fact]
    public void ComplexTypePropertyBuilder_AnonymizeTemplate_writes_Template_annotation()
    {
        using ComplexTypePropertyBuilderTestContext ctx =
            ComplexTypePropertyBuilderTestContext.Build();
        IComplexType complexType = ctx.Model
            .FindEntityType(typeof(ComplexTypePropOwner))!
            .FindComplexProperty(nameof(ComplexTypePropOwner.Info))!
            .ComplexType;
        IProperty prop = complexType.FindProperty(nameof(ComplexTypePropInfo.Alias))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Template);
        rule.Template.Should().Be("a{B}");
        rule.ConstantValue.Should().BeNull();
    }

    /// <summary>
    /// Proves that the overload resolves on the exact
    /// <c>cp.Property(lambda).HasMaxLength(n).Anonymize(constant)</c> and
    /// <c>cp.Property(lambda).HasMaxLength(n).AnonymizeNull()</c> chain shapes that
    /// consuming EF libs use. Compilation of this test is the chain-shape compile proof:
    /// if this test does not compile, the overload shape is wrong and is a finding against
    /// this overload set.
    /// </summary>
    [Fact]
    public void ComplexTypePropertyBuilder_chain_shape_compiles_and_annotation_lands()
    {
        using ComplexTypePropertyChainContext ctx =
            ComplexTypePropertyChainContext.Build();
        IComplexType complexType = ctx.Model
            .FindEntityType(typeof(ChainOwnerEntity))!
            .FindComplexProperty(nameof(ChainOwnerEntity.Details))!
            .ComplexType;

        var codeRule = complexType.FindProperty(nameof(ChainDetails.Code))!
            .FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;
        var noteRule = complexType.FindProperty(nameof(ChainDetails.Note))!
            .FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;
        var tagRule = complexType.FindProperty(nameof(ChainDetails.Tag))!
            .FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;
        var labelRule = complexType.FindProperty(nameof(ChainDetails.Label))!
            .FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        codeRule.Should().NotBeNull();
        codeRule.Kind.Should().Be(AnonymizeKind.Constant);
        codeRule.ConstantValue.Should().Be("cleared");

        noteRule.Should().NotBeNull();
        noteRule.Kind.Should().Be(AnonymizeKind.SetNull);

        tagRule.Should().NotBeNull();
        tagRule.Kind.Should().Be(AnonymizeKind.SetEmpty);

        labelRule.Should().NotBeNull();
        labelRule.Kind.Should().Be(AnonymizeKind.Template);
        labelRule.Template.Should().Be("a{B}");
    }

    /// <summary>
    /// Proves that the fluent annotation is byte-identical to the direct
    /// <see cref="AnonymizationRule.Create"/> factory call — same kind, same constant value.
    /// </summary>
    [Fact]
    public void ComplexTypePropertyBuilder_Anonymize_annotation_byte_identical_to_direct_Create()
    {
        using ComplexTypePropertyBuilderTestContext ctx =
            ComplexTypePropertyBuilderTestContext.Build();
        IComplexType complexType = ctx.Model
            .FindEntityType(typeof(ComplexTypePropOwner))!
            .FindComplexProperty(nameof(ComplexTypePropOwner.Info))!
            .ComplexType;

        var actual = complexType.FindProperty(nameof(ComplexTypePropInfo.Code))!
            .FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;
        var expected =
            AnonymizationRule.Create(AnonymizeKind.Constant, constantValue: "cleared");

        actual.Should().Be(expected);
    }

    [Fact]
    public void ComplexTypePropertyBuilder_Anonymize_null_constant_throws_ArgumentNullException()
    {
        var act = () =>
        {
            using DbContext ctx = new NullConstantComplexTypePropContext();
            _ = ctx.Model;
        };

        act.Should().Throw<ArgumentNullException>();
    }

    // long identifier — cannot wrap
    [Fact]
    public void ComplexTypePropertyBuilder_AnonymizeTemplate_null_template_throws_ArgumentNullException()
    {
        var act = () =>
        {
            using DbContext ctx = new NullTemplateComplexTypePropContext();
            _ = ctx.Model;
        };

        act.Should().Throw<ArgumentNullException>();
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
    // T10 — ComplexTypePropertyBuilder<T> null-builder guard
    // =========================================================================

    /// <summary>
    /// Proves that passing <see langword="null!"/> as the implicit receiver (builder) to
    /// the <c>ComplexTypePropertyBuilder&lt;T&gt;</c> overloads throws
    /// <see cref="ArgumentNullException"/>. All four overloads share the same guard via
    /// <c>ArgumentNullException.ThrowIfNull(builder)</c> — one call exercises all paths.
    /// </summary>
    [Fact]
    public void ComplexTypePropertyBuilder_null_receiver_throws_ArgumentNullException()
    {
        ComplexTypePropertyBuilder<string> nullBuilder = null!;

        var act1 = () => nullBuilder.Anonymize("x");
        var act2 = () => nullBuilder.AnonymizeNull();
        var act3 = () => nullBuilder.AnonymizeEmpty();
        var act4 = () => nullBuilder.AnonymizeTemplate("t");

        act1.Should().Throw<ArgumentNullException>();
        act2.Should().Throw<ArgumentNullException>();
        act3.Should().Throw<ArgumentNullException>();
        act4.Should().Throw<ArgumentNullException>();
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
    // ComplexTypePropertyBuilder<T> — entity + complex types for new overload tests
    // =========================================================================

    private sealed class ComplexTypePropOwner
    {
        public int Id { get; set; }

        public ComplexTypePropInfo Info { get; set; } = new();
    }

    private sealed class ComplexTypePropInfo
    {
        // Required string — Anonymize("cleared") test
        public string Code { get; set; } = string.Empty;

        // Nullable string — AnonymizeNull() test
        public string? Optional { get; set; }

        // Required string — AnonymizeEmpty() test
        public string Slug { get; set; } = string.Empty;

        // Required string — AnonymizeTemplate("a{B}") test
        public string Alias { get; set; } = string.Empty;
    }

    // Chain-shape compile-proof entities — cp.Property(lambda).HasMaxLength(n).Anonymize*()
    private sealed class ChainOwnerEntity
    {
        public int Id { get; set; }

        public ChainDetails Details { get; set; } = new();
    }

    private sealed class ChainDetails
    {
        public string Code { get; set; } = string.Empty;

        public string? Note { get; set; }

        public string Tag { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;
    }

    private sealed class NullConstantComplexTypePropOwner
    {
        public int Id { get; set; }

        public NullConstantComplexTypePropInfo Info { get; set; } = new();
    }

    private sealed class NullConstantComplexTypePropInfo
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class NullTemplateComplexTypePropOwner
    {
        public int Id { get; set; }

        public NullTemplateComplexTypePropInfo Info { get; set; } = new();
    }

    private sealed class NullTemplateComplexTypePropInfo
    {
        public string Value { get; set; } = string.Empty;
    }

    // =========================================================================
    // ComplexTypePropertyBuilder<T> — DbContext types
    // =========================================================================

    private sealed class ComplexTypePropertyBuilderTestContext : DbContext
    {
        private ComplexTypePropertyBuilderTestContext(
            DbContextOptions<ComplexTypePropertyBuilderTestContext> options)
            : base(options)
        {
        }

        public static ComplexTypePropertyBuilderTestContext Build()
        {
            var options =
                new DbContextOptionsBuilder<ComplexTypePropertyBuilderTestContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString())
                    .Options;
            return new ComplexTypePropertyBuilderTestContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<ComplexTypePropOwner>(e =>
            {
                e.HasKey(x => x.Id);
                e.ComplexProperty(x => x.Info, cp =>
                {
                    // Direct cp.Property(lambda).Anonymize*() — no intermediate HasMaxLength.
                    cp.Property(p => p.Code).Anonymize("cleared");
                    cp.Property(p => p.Optional).AnonymizeNull();
                    cp.Property(p => p.Slug).AnonymizeEmpty();
                    cp.Property(p => p.Alias).AnonymizeTemplate("a{B}");
                });
            });
        }
    }

    private sealed class ComplexTypePropertyChainContext : DbContext
    {
        private ComplexTypePropertyChainContext(
            DbContextOptions<ComplexTypePropertyChainContext> options)
            : base(options)
        {
        }

        public static ComplexTypePropertyChainContext Build()
        {
            var options =
                new DbContextOptionsBuilder<ComplexTypePropertyChainContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString())
                    .Options;
            return new ComplexTypePropertyChainContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            // Chain-shape compile proof: the EXACT fluent chain
            // cp.Property(lambda).HasMaxLength(n).Anonymize*(…) that consuming EF libs
            // will use. If this does not compile, the ComplexTypePropertyBuilder<T>
            // overload shape is wrong.
            const int max = 50;
            model.Entity<ChainOwnerEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.ComplexProperty(x => x.Details, cp =>
                {
                    cp.Property(p => p.Code).HasMaxLength(max).Anonymize("cleared");
                    cp.Property(p => p.Note).HasMaxLength(max).AnonymizeNull();
                    cp.Property(p => p.Tag).HasMaxLength(max).AnonymizeEmpty();
                    cp.Property(p => p.Label).HasMaxLength(max).AnonymizeTemplate("a{B}");
                });
            });
        }
    }

    private sealed class NullConstantComplexTypePropContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<NullConstantComplexTypePropOwner>(e =>
            {
                e.HasKey(x => x.Id);
                e.ComplexProperty(x => x.Info, cp =>
                {
                    // null! forces the null-guard on the constant parameter.
                    cp.Property(p => p.Value).Anonymize(null!);
                });
            });
        }
    }

    private sealed class NullTemplateComplexTypePropContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<NullTemplateComplexTypePropOwner>(e =>
            {
                e.HasKey(x => x.Id);
                e.ComplexProperty(x => x.Info, cp =>
                {
                    // null! forces the null-guard on the template parameter.
                    cp.Property(p => p.Value).AnonymizeTemplate(null!);
                });
            });
        }
    }

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
