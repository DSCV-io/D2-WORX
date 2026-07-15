// -----------------------------------------------------------------------
// <copyright file="AnonymizableAttributeConventionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.DataGovernance.EntityFrameworkCore;

using System;
using System.ComponentModel.DataAnnotations.Schema;
using AwesomeAssertions;
using DcsvIo.D2.DataGovernance.Abstractions;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

/// <summary>
/// Tests for <see cref="AnonymizableAttributeConvention"/> and
/// <see cref="AnonymizationModelBuilderExtensions.ApplyAnonymizationConventions"/>.
/// </summary>
/// <remarks>
/// All tests build the EF model in-memory (no database, no migrations). Each
/// assertion reads the <c>D2:Anonymize</c> annotation via <c>IProperty.FindAnnotation</c>.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class AnonymizableAttributeConventionTests
{
    // =========================================================================
    // Activation — ApplyAnonymizationConventions present vs absent
    // =========================================================================

    // long identifier — cannot wrap
    [Fact]
    public void Without_ApplyAnonymizationConventions_Anonymizable_attribute_produces_no_annotation()
    {
        using DbContext ctx = new WithoutConventionContext();
        IProperty prop = ctx.Model.FindEntityType(typeof(AttributedEntity))!
            .FindProperty(nameof(AttributedEntity.Email))!;

        object? value = prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value;

        value.Should().BeNull();
    }

    [Fact]
    public void With_ApplyAnonymizationConventions_Anonymizable_attribute_produces_annotation()
    {
        using DbContext ctx = new WithConventionContext();
        IProperty prop = ctx.Model.FindEntityType(typeof(AttributedEntity))!
            .FindProperty(nameof(AttributedEntity.Email))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
    }

    // =========================================================================
    // Each attribute form → correct annotation
    // =========================================================================

    [Fact]
    public void Attribute_SetNull_form_produces_SetNull_rule()
    {
        using DbContext ctx = new WithConventionContext();
        IProperty prop = ctx.Model.FindEntityType(typeof(AttributedEntity))!
            .FindProperty(nameof(AttributedEntity.Phone))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.SetNull);
        rule.ConstantValue.Should().BeNull();
        rule.Template.Should().BeNull();
    }

    [Fact]
    public void Attribute_SetEmpty_form_produces_SetEmpty_rule()
    {
        using DbContext ctx = new WithConventionContext();
        IProperty prop = ctx.Model.FindEntityType(typeof(AttributedEntity))!
            .FindProperty(nameof(AttributedEntity.DisplayName))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.SetEmpty);
    }

    [Fact]
    public void Attribute_Constant_form_produces_Constant_rule_with_correct_value()
    {
        using DbContext ctx = new WithConventionContext();
        IProperty prop = ctx.Model.FindEntityType(typeof(AttributedEntity))!
            .FindProperty(nameof(AttributedEntity.Email))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be("[deleted]");
    }

    [Fact]
    public void Attribute_Template_form_produces_Template_rule_with_correct_template()
    {
        using DbContext ctx = new WithConventionContext();
        IProperty prop = ctx.Model.FindEntityType(typeof(AttributedEntity))!
            .FindProperty(nameof(AttributedEntity.Username))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Template);
        rule.Template.Should().Be("deletedUser{UserId}@deleted.dcsv.io");
    }

    // =========================================================================
    // Precedence — fluent Explicit wins over attribute DataAnnotation
    // =========================================================================

    [Fact]
    public void Fluent_overrides_attribute_when_both_target_same_property()
    {
        using DbContext ctx = new PrecedenceTestContext();
        IProperty prop = ctx.Model.FindEntityType(typeof(PrecedenceEntity))!
            .FindProperty(nameof(PrecedenceEntity.Email))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        // Attribute says Constant("[attr]"); fluent says Constant("[fluent]").
        // Fluent (Explicit source) > attribute (DataAnnotation source) → fluent wins.
        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be("[fluent]");
    }

    [Fact]
    public void Fluent_and_attribute_identical_value_produces_no_error()
    {
        using DbContext ctx = new IdenticalDoubleDeclarationContext();
        IProperty prop = ctx.Model.FindEntityType(typeof(IdenticalDoubleEntity))!
            .FindProperty(nameof(IdenticalDoubleEntity.Value))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be("x");
    }

    // =========================================================================
    // Convention walks owned-entity sub-properties via their IEntityType
    // =========================================================================

    [Fact]
    public void Convention_walks_owned_entity_CLR_properties_for_Anonymizable()
    {
        using DbContext ctx = new OwnedAttributeConventionContext();
        IEntityType ownedType = ctx.Model.FindEntityType(typeof(OwnedAttributedAddress))!;
        IProperty prop = ownedType.FindProperty(nameof(OwnedAttributedAddress.Street))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be("[address deleted]");
    }

    // =========================================================================
    // Convention walks complex-type sub-properties for [Anonymizable]
    // =========================================================================

    [Fact]
    public void Convention_walks_complex_type_CLR_properties_for_Anonymizable()
    {
        using DbContext ctx = new ComplexAttributeConventionContext();
        IComplexType complexType = ctx.Model
            .FindEntityType(typeof(ComplexAttributedOwner))!
            .FindComplexProperty(nameof(ComplexAttributedOwner.Info))!
            .ComplexType;
        IProperty prop = complexType.FindProperty(nameof(ComplexAttributedInfo.Code))!;

        var rule =
            prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be("[code deleted]");
    }

    // =========================================================================
    // Convention does not crash on NotMapped properties (invisible to EF)
    // =========================================================================

    [Fact]
    public void NotMapped_property_with_Anonymizable_produces_no_annotation_and_no_exception()
    {
        var act = () =>
        {
            using DbContext ctx = new NotMappedAttributeContext();
            _ = ctx.Model;
        };

        act.Should().NotThrow();
    }

    // =========================================================================
    // Attributed entity type
    // =========================================================================

    private sealed class AttributedEntity
    {
        public int Id { get; set; }

        [Anonymizable("[deleted]")]
        public string Email { get; set; } = string.Empty;

        [Anonymizable(AnonymizeKind.SetNull)]
        public string? Phone { get; set; }

        [Anonymizable(AnonymizeKind.SetEmpty)]
        public string DisplayName { get; set; } = string.Empty;

        [Anonymizable(template: "deletedUser{UserId}@deleted.dcsv.io")]
        public string Username { get; set; } = string.Empty;

        public Guid UserId { get; set; }
    }

    private sealed class PrecedenceEntity
    {
        public int Id { get; set; }

        [Anonymizable("[attr]")]
        public string Email { get; set; } = string.Empty;
    }

    private sealed class IdenticalDoubleEntity
    {
        public int Id { get; set; }

        [Anonymizable("x")]
        public string Value { get; set; } = string.Empty;
    }

    private sealed class OwnedAttributeOwnerEntity
    {
        public int Id { get; set; }

        public OwnedAttributedAddress Address { get; set; } = new();
    }

    private sealed class OwnedAttributedAddress
    {
        [Anonymizable("[address deleted]")]
        public string Street { get; set; } = string.Empty;
    }

    private sealed class NotMappedEntity
    {
        public int Id { get; set; }

        public string Mapped { get; set; } = string.Empty;

        [NotMapped]
        [Anonymizable("[should be invisible]")]
        public string Ignored { get; set; } = string.Empty;
    }

    private sealed class ComplexAttributedOwner
    {
        public int Id { get; set; }

        public ComplexAttributedInfo Info { get; set; } = new();
    }

    private sealed class ComplexAttributedInfo
    {
        [Anonymizable("[code deleted]")]
        public string Code { get; set; } = string.Empty;
    }

    // =========================================================================
    // Test DbContext types
    // =========================================================================

    private sealed class WithConventionContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.ApplyAnonymizationConventions();
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<AttributedEntity>(e => e.HasKey(x => x.Id));
        }
    }

    private sealed class WithoutConventionContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            // No ConfigureConventions override — convention NOT activated.
            model.Entity<AttributedEntity>(e => e.HasKey(x => x.Id));
        }
    }

    private sealed class PrecedenceTestContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.ApplyAnonymizationConventions();
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<PrecedenceEntity>(e =>
            {
                e.HasKey(x => x.Id);

                // Fluent (Explicit source) overrides attribute (DataAnnotation source).
                e.Property(x => x.Email).Anonymize("[fluent]");
            });
        }
    }

    private sealed class IdenticalDoubleDeclarationContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.ApplyAnonymizationConventions();
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<IdenticalDoubleEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Value).Anonymize("x"); // Same value as attribute.
            });
        }
    }

    private sealed class OwnedAttributeConventionContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.ApplyAnonymizationConventions();
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<OwnedAttributeOwnerEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.OwnsOne(x => x.Address);
            });
        }
    }

    private sealed class NotMappedAttributeContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.ApplyAnonymizationConventions();
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<NotMappedEntity>(e => e.HasKey(x => x.Id));
        }
    }

    private sealed class ComplexAttributeConventionContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.ApplyAnonymizationConventions();
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<ComplexAttributedOwner>(e =>
            {
                e.HasKey(x => x.Id);
                e.ComplexProperty(x => x.Info);
            });
        }
    }
}
