// -----------------------------------------------------------------------
// <copyright file="ContactMappingExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Contacts.EntityFrameworkCore;

using System;
using System.Linq;
using System.Security.Cryptography;
using AwesomeAssertions;
using DcsvIo.D2.Contacts.EntityFrameworkCore;
using DcsvIo.D2.Contacts.ValueObjects;
using DcsvIo.D2.DataGovernance.Abstractions;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using DcsvIo.D2.Time.EfCore;
using DcsvIo.D2.Validation.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

/// <summary>
/// Unit tests for <see cref="ContactMappingExtensions"/>,
/// <see cref="EmailPhoneMappingExtensions"/>, <see cref="EmailMapping"/>,
/// and <see cref="PhoneMapping"/>.
/// Exercises every public helper via <c>ModelBuilder</c> + built-<c>IModel</c>
/// introspection (model-build-only; no live DB).
/// </summary>
[Trait("Category", "Unit")]
public sealed class ContactMappingExtensionsTests
{
    private static readonly int sr_hashIdMax = 3 + (SHA256.HashSizeInBytes * 2);

    private static readonly string sr_hashIdCleared =
        "v1." + new string('0', SHA256.HashSizeInBytes * 2);

    // =========================================================================
    // MapPersonal
    // =========================================================================

    [Fact]
    public void MapPersonal_FirstName_has_correct_max_length()
    {
        using var ctx = PersonalContext.Build();
        ComplexProp<PersonalEntity>(ctx.Model, "Name", nameof(Personal.FirstName))
            .GetMaxLength().Should().Be(FieldConstraints.FIRST_NAME_MAX);
    }

    [Fact]
    public void MapPersonal_MiddleName_has_correct_max_length()
    {
        using var ctx = PersonalContext.Build();
        ComplexProp<PersonalEntity>(ctx.Model, "Name", nameof(Personal.MiddleName))
            .GetMaxLength().Should().Be(FieldConstraints.MIDDLE_NAME_MAX);
    }

    [Fact]
    public void MapPersonal_LastName_has_correct_max_length()
    {
        using var ctx = PersonalContext.Build();
        ComplexProp<PersonalEntity>(ctx.Model, "Name", nameof(Personal.LastName))
            .GetMaxLength().Should().Be(FieldConstraints.LAST_NAME_MAX);
    }

    [Fact]
    public void MapPersonal_PreferredName_has_correct_max_length()
    {
        using var ctx = PersonalContext.Build();
        ComplexProp<PersonalEntity>(ctx.Model, "Name", nameof(Personal.PreferredName))
            .GetMaxLength().Should().Be(FieldConstraints.PREFERRED_NAME_MAX);
    }

    [Fact]
    public void MapPersonal_HashId_has_correct_max_length()
    {
        using var ctx = PersonalContext.Build();
        ComplexProp<PersonalEntity>(ctx.Model, "Name", nameof(Personal.HashId))
            .GetMaxLength().Should().Be(sr_hashIdMax);
    }

    [Fact]
    public void MapPersonal_FirstName_has_Constant_Deleted_anonymize_rule()
    {
        using var ctx = PersonalContext.Build();
        var rule = AnonRule(
            ComplexProp<PersonalEntity>(ctx.Model, "Name", nameof(Personal.FirstName)));
        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be("Deleted");
    }

    [Fact]
    public void MapPersonal_MiddleName_has_SetNull_anonymize_rule()
    {
        using var ctx = PersonalContext.Build();
        AnonRule(ComplexProp<PersonalEntity>(ctx.Model, "Name", nameof(Personal.MiddleName)))!
            .Kind.Should().Be(AnonymizeKind.SetNull);
    }

    [Fact]
    public void MapPersonal_LastName_has_SetNull_anonymize_rule()
    {
        using var ctx = PersonalContext.Build();
        AnonRule(ComplexProp<PersonalEntity>(ctx.Model, "Name", nameof(Personal.LastName)))!
            .Kind.Should().Be(AnonymizeKind.SetNull);
    }

    [Fact]
    public void MapPersonal_PreferredName_has_SetNull_anonymize_rule()
    {
        using var ctx = PersonalContext.Build();
        AnonRule(ComplexProp<PersonalEntity>(ctx.Model, "Name", nameof(Personal.PreferredName)))!
            .Kind.Should().Be(AnonymizeKind.SetNull);
    }

    [Fact]
    public void MapPersonal_HashId_has_cleared_sentinel_anonymize_rule()
    {
        using var ctx = PersonalContext.Build();
        var rule = AnonRule(
            ComplexProp<PersonalEntity>(ctx.Model, "Name", nameof(Personal.HashId)));
        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be(sr_hashIdCleared);
    }

    [Fact]
    public void MapPersonal_maps_as_complex_property_on_entity()
    {
        using var ctx = PersonalContext.Build();
        ctx.Model.FindEntityType(typeof(PersonalEntity))!
            .FindComplexProperty("Name")
            .Should().NotBeNull("MapPersonal should map Personal as a ComplexProperty");
    }

    // =========================================================================
    // MapNameAffixes
    // =========================================================================

    [Fact]
    public void MapNameAffixes_PrefixCustom_has_correct_max_length()
    {
        using var ctx = AffixContext.Build();
        ComplexProp<AffixEntity>(ctx.Model, "Affixes", nameof(NameAffixes.PrefixCustom))
            .GetMaxLength().Should().Be(FieldConstraints.AFFIX_CUSTOM_MAX);
    }

    [Fact]
    public void MapNameAffixes_SuffixCustom_has_correct_max_length()
    {
        using var ctx = AffixContext.Build();
        ComplexProp<AffixEntity>(ctx.Model, "Affixes", nameof(NameAffixes.SuffixCustom))
            .GetMaxLength().Should().Be(FieldConstraints.AFFIX_CUSTOM_MAX);
    }

    [Fact]
    public void MapNameAffixes_all_fields_have_SetNull_anonymize_rule()
    {
        using var ctx = AffixContext.Build();
        foreach (var fieldName in new[]
        {
            nameof(NameAffixes.Prefix),
            nameof(NameAffixes.PrefixCustom),
            nameof(NameAffixes.Suffix),
            nameof(NameAffixes.SuffixCustom),
        })
        {
            AnonRule(ComplexProp<AffixEntity>(ctx.Model, "Affixes", fieldName))!
                .Kind.Should().Be(AnonymizeKind.SetNull, because: $"{fieldName} should be SetNull");
        }
    }

    // =========================================================================
    // MapDemographics
    // =========================================================================

    [Fact]
    public void MapDemographics_DateOfBirth_has_SetNull_anonymize_rule()
    {
        using var ctx = DemoContext.Build();
        AnonRule(ComplexProp<DemoEntity>(ctx.Model, "Bio", nameof(Demographics.DateOfBirth)))!
            .Kind.Should().Be(AnonymizeKind.SetNull);
    }

    [Fact]
    public void MapDemographics_BiologicalSex_has_SetNull_anonymize_rule()
    {
        using var ctx = DemoContext.Build();
        AnonRule(ComplexProp<DemoEntity>(ctx.Model, "Bio", nameof(Demographics.BiologicalSex)))!
            .Kind.Should().Be(AnonymizeKind.SetNull);
    }

    // =========================================================================
    // MapProfessional
    // =========================================================================

    [Fact]
    public void MapProfessional_CompanyName_has_correct_max_length()
    {
        using var ctx = ProfessionalContext.Build();
        ComplexProp<ProfessionalEntity>(ctx.Model, "Work", nameof(Professional.CompanyName))
            .GetMaxLength().Should().Be(FieldConstraints.COMPANY_NAME_MAX);
    }

    [Fact]
    public void MapProfessional_JobTitle_has_correct_max_length()
    {
        using var ctx = ProfessionalContext.Build();
        ComplexProp<ProfessionalEntity>(ctx.Model, "Work", nameof(Professional.JobTitle))
            .GetMaxLength().Should().Be(FieldConstraints.JOB_TITLE_MAX);
    }

    [Fact]
    public void MapProfessional_Department_has_correct_max_length()
    {
        using var ctx = ProfessionalContext.Build();
        ComplexProp<ProfessionalEntity>(ctx.Model, "Work", nameof(Professional.Department))
            .GetMaxLength().Should().Be(FieldConstraints.DEPARTMENT_MAX);
    }

    [Fact]
    public void MapProfessional_CompanyWebsite_has_correct_max_length()
    {
        using var ctx = ProfessionalContext.Build();
        ComplexProp<ProfessionalEntity>(ctx.Model, "Work", nameof(Professional.CompanyWebsite))
            .GetMaxLength().Should().Be(FieldConstraints.COMPANY_WEBSITE_MAX);
    }

    [Fact]
    public void MapProfessional_CompanyWebsite_has_Uri_value_converter()
    {
        using var ctx = ProfessionalContext.Build();
        ComplexProp<ProfessionalEntity>(ctx.Model, "Work", nameof(Professional.CompanyWebsite))
            .GetValueConverter()
            .Should().NotBeNull(
                because: "CompanyWebsite should have a Uri<->string value converter");
    }

    [Fact]
    public void MapProfessional_CompanyWebsite_converter_round_trips_and_maps_to_string()
    {
        using var ctx = ProfessionalContext.Build();
        var conv = ComplexProp<ProfessionalEntity>(
            ctx.Model, "Work", nameof(Professional.CompanyWebsite)).GetValueConverter()!;

        conv.ProviderClrType.Should().Be<string>();

        var uri = new Uri("https://x.com");
        conv.ConvertToProvider(uri).Should().Be("https://x.com/");
        conv.ConvertFromProvider("https://x.com/").Should().Be(uri);

        conv.ConvertToProvider(null).Should().BeNull();
        conv.ConvertFromProvider(null).Should().BeNull();
    }

    [Fact]
    public void MapProfessional_CompanyName_has_Constant_Deleted_anonymize_rule()
    {
        using var ctx = ProfessionalContext.Build();
        var rule = AnonRule(
            ComplexProp<ProfessionalEntity>(ctx.Model, "Work", nameof(Professional.CompanyName)));
        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be("Deleted");
    }

    [Fact]
    public void MapProfessional_nullable_fields_have_SetNull_anonymize_rule()
    {
        using var ctx = ProfessionalContext.Build();
        foreach (var fieldName in new[]
        {
            nameof(Professional.JobTitle),
            nameof(Professional.Department),
            nameof(Professional.CompanyWebsite),
        })
        {
            AnonRule(ComplexProp<ProfessionalEntity>(ctx.Model, "Work", fieldName))!
                .Kind.Should().Be(AnonymizeKind.SetNull, because: $"{fieldName} should be SetNull");
        }
    }

    // =========================================================================
    // Same-VO-type-twice column distinctness (EF Core 10 full-path uniquification)
    // =========================================================================

    [Fact]
    public void MapPersonal_same_CLR_type_twice_produces_distinct_column_sets()
    {
        using var ctx = TwoPersonalContext.Build();
        var entityType = ctx.Model.FindEntityType(typeof(TwoPersonalEntity))!;

        var legalName = entityType.FindComplexProperty("LegalName")!;
        var maidenName = entityType.FindComplexProperty("MaidenName")!;

        var legalFirst = legalName.ComplexType.FindProperty(nameof(Personal.FirstName))!;
        var maidenFirst = maidenName.ComplexType.FindProperty(nameof(Personal.FirstName))!;

        var storeObject = StoreObjectIdentifier.Table(entityType.GetTableName()!);
        var legalCol = legalFirst.GetColumnName(storeObject);
        var maidenCol = maidenFirst.GetColumnName(storeObject);

        legalCol.Should().NotBe(
            maidenCol,
            because: "EF 10 full-path uniquification must produce distinct columns");
        legalCol.Should().Be("LegalName_FirstName");
        maidenCol.Should().Be("MaidenName_FirstName");
        legalFirst.FindAnnotation(AnonymizationAnnotations.ANONYMIZE).Should().NotBeNull();
        maidenFirst.FindAnnotation(AnonymizationAnnotations.ANONYMIZE).Should().NotBeNull();
    }

    // =========================================================================
    // Optional all-nullable complex types throw at model build (EF Core 10)
    // =========================================================================

    [Fact]
    public void Optional_NameAffixes_complex_throws_at_model_build()
    {
        // NameAffixes has no required scalar — EF Core 10 rejects an OPTIONAL complex type
        // without at least one required property.
        var ex = Record.Exception(() =>
        {
            using var ctx = OptionalAffixContext.Build();
            _ = ctx.Model;
        });
        ex.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void Optional_Demographics_complex_throws_at_model_build()
    {
        var ex = Record.Exception(() =>
        {
            using var ctx = OptionalDemoContext.Build();
            _ = ctx.Model;
        });
        ex.Should().BeOfType<InvalidOperationException>();
    }

    // =========================================================================
    // MapEmailAddress — value converter + coupling
    // =========================================================================

    [Fact]
    public void MapEmailAddress_property_has_EmailAddress_to_string_converter_and_max_length()
    {
        using var ctx = EmailConstantContext.Build();
        var prop = ctx.Model.FindEntityType(typeof(EmailEntity))!
            .FindProperty(nameof(EmailEntity.Email))!;
        var conv = prop.GetValueConverter();
        conv.Should().NotBeNull(because: "MapEmailAddress should apply a value converter");
        conv.ProviderClrType.Should().Be<string>();
        prop.GetMaxLength().Should().Be(FieldConstraints.EMAIL_MAX);
    }

    [Fact]
    public void MapEmailAddress_Anonymize_writes_Constant_rule()
    {
        using var ctx = EmailConstantContext.Build();
        var rule = AnonRule(ctx.Model
            .FindEntityType(typeof(EmailEntity))!
            .FindProperty(nameof(EmailEntity.Email))!);
        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be("deleted@deleted.user.dcsv.io");
    }

    [Fact]
    public void MapEmailAddress_Anonymize_adds_no_unique_index()
    {
        using var ctx = EmailConstantContext.Build();
        var entityType = ctx.Model.FindEntityType(typeof(EmailEntity))!;
        entityType.GetIndexes()
            .Where(i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(EmailEntity.Email)))
            .Should().BeEmpty(because: ".Anonymize is the non-unique path — no unique index");
    }

    [Fact]
    public void MapEmailAddress_Anonymize_with_token_writes_Template_rule()
    {
        using var ctx = EmailTemplateContext.Build();
        var rule = AnonRule(ctx.Model
            .FindEntityType(typeof(EmailEntity))!
            .FindProperty(nameof(EmailEntity.Email))!);
        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Template);
        rule.Template.Should().Contain("{UserId}");
    }

    [Fact]
    public void MapEmailAddress_Unique_writes_Template_rule_and_unique_index()
    {
        using var ctx = EmailUniqueContext.Build();
        var entityType = ctx.Model.FindEntityType(typeof(EmailEntity))!;
        var prop = entityType.FindProperty(nameof(EmailEntity.Email))!;
        var rule = AnonRule(prop);
        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Template);
        rule.Template.Should().Contain("{UserId}");

        var uniqueIndex = entityType.GetIndexes()
            .FirstOrDefault(i =>
                i.IsUnique && i.Properties.Any(p => p.Name == nameof(EmailEntity.Email)));
        uniqueIndex.Should().NotBeNull(because: ".Unique() should declare a unique index");
    }

    [Fact]
    public void MapEmailAddress_Unique_without_token_throws_ArgumentException()
    {
        var ex = Record.Exception(() =>
        {
            using var ctx = EmailUniqueNoTokenContext.Build();

            // Force model build (OnModelCreating is called lazily on first Model access).
            _ = ctx.Model;
        });
        ex.Should().BeOfType<ArgumentException>(
            because: ".Unique() with a token-free template should throw at map time");
    }

    [Fact]
    public void MapEmailAddress_no_parameterless_Unique_method_exists()
    {
        // Compile-time coupling: .Unique() (no template arg) must not exist on EmailMapping.
        var parameterlessUnique = typeof(EmailMapping)
            .GetMethod("Unique", System.Type.EmptyTypes);
        parameterlessUnique.Should().BeNull(
            because: "the type system must make token-free unique paths unrepresentable");
    }

    [Fact]
    public void MapEmailAddress_converter_round_trips_via_FromTrusted()
    {
        using var ctx = EmailConstantContext.Build();
        var prop = ctx.Model
            .FindEntityType(typeof(EmailEntity))!
            .FindProperty(nameof(EmailEntity.Email))!;
        var conv = prop.GetValueConverter()!;

        var email = EmailAddress.FromTrusted("user@example.com");
        conv.ConvertToProvider(email).Should().Be("user@example.com");
        conv.ConvertFromProvider("user@example.com").Should().Be(email);

        conv.ConvertToProvider(null).Should().BeNull();
        conv.ConvertFromProvider(null).Should().BeNull();
    }

    // =========================================================================
    // MapPhoneNumber — value converter + coupling
    // =========================================================================

    [Fact]
    public void MapPhoneNumber_property_has_PhoneNumber_to_string_converter_and_max_length()
    {
        using var ctx = PhoneConstantContext.Build();
        var prop = ctx.Model
            .FindEntityType(typeof(PhoneEntity))!
            .FindProperty(nameof(PhoneEntity.Phone))!;
        var conv = prop.GetValueConverter();
        conv.Should().NotBeNull(because: "MapPhoneNumber should apply a value converter");
        conv.ProviderClrType.Should().Be<string>();
        prop.GetMaxLength().Should().Be(FieldConstraints.PHONE_E164_MAX);
    }

    [Fact]
    public void MapPhoneNumber_Anonymize_writes_Constant_rule()
    {
        using var ctx = PhoneConstantContext.Build();
        var rule = AnonRule(ctx.Model
            .FindEntityType(typeof(PhoneEntity))!
            .FindProperty(nameof(PhoneEntity.Phone))!);
        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be("10000000000");
    }

    [Fact]
    public void MapPhoneNumber_Anonymize_with_token_writes_Template_rule()
    {
        using var ctx = PhoneTemplateContext.Build();
        var rule = AnonRule(ctx.Model
            .FindEntityType(typeof(PhoneEntity))!
            .FindProperty(nameof(PhoneEntity.Phone))!);
        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Template);
        rule.Template.Should().Contain("{UserId}");
    }

    [Fact]
    public void MapPhoneNumber_Anonymize_adds_no_unique_index()
    {
        using var ctx = PhoneConstantContext.Build();
        var entityType = ctx.Model.FindEntityType(typeof(PhoneEntity))!;
        entityType.GetIndexes()
            .Where(i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(PhoneEntity.Phone)))
            .Should().BeEmpty(because: ".Anonymize is the non-unique path — no unique index");
    }

    [Fact]
    public void MapPhoneNumber_Unique_writes_Template_rule_and_unique_index()
    {
        using var ctx = PhoneUniqueContext.Build();
        var entityType = ctx.Model.FindEntityType(typeof(PhoneEntity))!;
        var rule = AnonRule(entityType.FindProperty(nameof(PhoneEntity.Phone))!);
        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Template);
        rule.Template.Should().Contain("{UserId}");

        var uniqueIndex = entityType.GetIndexes()
            .FirstOrDefault(i =>
                i.IsUnique && i.Properties.Any(p => p.Name == nameof(PhoneEntity.Phone)));
        uniqueIndex.Should().NotBeNull(because: ".Unique() should declare a unique index");
    }

    [Fact]
    public void MapPhoneNumber_Unique_without_token_throws_ArgumentException()
    {
        var ex = Record.Exception(() =>
        {
            using var ctx = PhoneUniqueNoTokenContext.Build();

            // Force model build (OnModelCreating is called lazily on first Model access).
            _ = ctx.Model;
        });
        ex.Should().BeOfType<ArgumentException>(
            because: ".Unique() with a token-free template should throw at map time");
    }

    [Fact]
    public void MapPhoneNumber_no_parameterless_Unique_method_exists()
    {
        var parameterlessUnique = typeof(PhoneMapping)
            .GetMethod("Unique", System.Type.EmptyTypes);
        parameterlessUnique.Should().BeNull(
            because: "the type system must make token-free unique paths unrepresentable");
    }

    [Fact]
    public void MapPhoneNumber_converter_round_trips_via_FromTrusted()
    {
        using var ctx = PhoneConstantContext.Build();
        var prop = ctx.Model
            .FindEntityType(typeof(PhoneEntity))!
            .FindProperty(nameof(PhoneEntity.Phone))!;
        var conv = prop.GetValueConverter()!;

        var phone = PhoneNumber.FromTrusted("+12025551234");
        conv.ConvertToProvider(phone).Should().Be("+12025551234");
        conv.ConvertFromProvider("+12025551234").Should().Be(phone);

        conv.ConvertToProvider(null).Should().BeNull();
        conv.ConvertFromProvider(null).Should().BeNull();
    }

    // =========================================================================
    // HasToken (internal helper)
    // =========================================================================

    [Fact]
    public void HasToken_returns_true_when_token_present()
    {
        EmailPhoneMappingExtensions.HasToken("foo{UserId}@bar.com").Should().BeTrue();
    }

    [Fact]
    public void HasToken_returns_false_when_no_token_present()
    {
        EmailPhoneMappingExtensions.HasToken("deleted@deleted.user.dcsv.io").Should().BeFalse();
    }

    [Fact]
    public void HasToken_returns_false_for_empty_braces()
    {
        // The regex \{[^}]+\} requires at least one character inside the braces.
        // An empty brace pair "{}" must NOT be treated as a template token.
        EmailPhoneMappingExtensions.HasToken("{}").Should().BeFalse();
    }

    [Fact]
    public void MapEmailAddress_Anonymize_empty_braces_writes_Constant_rule()
    {
        // "{}" contains no token (HasToken returns false), so Anonymize must
        // categorize it as Constant — not Template.
        using var ctx = EmailEmptyBracesContext.Build();
        var rule = AnonRule(ctx.Model
            .FindEntityType(typeof(EmailEntity))!
            .FindProperty(nameof(EmailEntity.Email))!);
        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be("{}");
    }

    // =========================================================================
    // Dropped-coupling hazard (no .Anonymize / .Unique chained)
    // =========================================================================

    [Fact]
    public void MapEmailAddress_dropped_coupling_object_writes_no_anonymize_annotation()
    {
        // This test pins the DROP-COUPLING hazard: if a host calls
        // b.MapEmailAddress(e => e.Email) but discards the returned EmailMapping
        // struct without chaining .Anonymize() or .Unique(), the anonymize
        // annotation is never written — PII ships with no erasure rule.
        // The assertion documents intentional behavior; a FUTURE test that
        // wrongly expects an annotation here would be incorrect.
        using var ctx = EmailDroppedCouplingContext.Build();
        var prop = ctx.Model
            .FindEntityType(typeof(EmailEntity))!
            .FindProperty(nameof(EmailEntity.Email))!;
        prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE).Should().BeNull(
            because: "dropping the coupling object must produce no anonymize annotation");
    }

    [Fact]
    public void MapPhoneNumber_dropped_coupling_object_writes_no_anonymize_annotation()
    {
        // This test pins the DROP-COUPLING hazard: if a host calls
        // b.MapPhoneNumber(e => e.Phone) but discards the returned PhoneMapping
        // struct without chaining .Anonymize() or .Unique(), the anonymize
        // annotation is never written — PII ships with no erasure rule.
        // The assertion documents intentional behavior; a FUTURE test that
        // wrongly expects an annotation here would be incorrect.
        using var ctx = PhoneDroppedCouplingContext.Build();
        var prop = ctx.Model
            .FindEntityType(typeof(PhoneEntity))!
            .FindProperty(nameof(PhoneEntity.Phone))!;
        prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE).Should().BeNull(
            because: "dropping the coupling object must produce no anonymize annotation");
    }

    // =========================================================================
    // Null-arg guards
    // =========================================================================

    [Fact]
    public void MapEmailAddress_null_selector_throws_ArgumentNullException()
    {
        var mb = new ModelBuilder();
        mb.Entity<EmailEntity>(b =>
        {
            var ex = Record.Exception(
                () => b.MapEmailAddress(null!));
            ex.Should().BeOfType<ArgumentNullException>();
        });
    }

    [Fact]
    public void MapPhoneNumber_null_selector_throws_ArgumentNullException()
    {
        var mb = new ModelBuilder();
        mb.Entity<PhoneEntity>(b =>
        {
            var ex = Record.Exception(
                () => b.MapPhoneNumber(null!));
            ex.Should().BeOfType<ArgumentNullException>();
        });
    }

    [Fact]
    public void EmailMapping_Anonymize_null_throws_ArgumentNullException()
    {
        // Build a valid mapping then immediately call Anonymize(null).
        Exception? caught = null;
        var mb = new ModelBuilder();
        mb.Entity<EmailEntity>(b =>
        {
            var mapping = b.MapEmailAddress(e => e.Email);
            caught = Record.Exception(() => mapping.Anonymize(null!));
        });
        caught.Should().BeOfType<ArgumentNullException>();
    }

    [Fact]
    public void EmailMapping_Unique_null_throws_ArgumentNullException()
    {
        Exception? caught = null;
        var mb = new ModelBuilder();
        mb.Entity<EmailEntity>(b =>
        {
            var mapping = b.MapEmailAddress(e => e.Email);
            caught = Record.Exception(() => mapping.Unique(null!));
        });
        caught.Should().BeOfType<ArgumentNullException>();
    }

    [Fact]
    public void PhoneMapping_Anonymize_null_throws_ArgumentNullException()
    {
        Exception? caught = null;
        var mb = new ModelBuilder();
        mb.Entity<PhoneEntity>(b =>
        {
            var mapping = b.MapPhoneNumber(e => e.Phone);
            caught = Record.Exception(() => mapping.Anonymize(null!));
        });
        caught.Should().BeOfType<ArgumentNullException>();
    }

    [Fact]
    public void PhoneMapping_Unique_null_throws_ArgumentNullException()
    {
        Exception? caught = null;
        var mb = new ModelBuilder();
        mb.Entity<PhoneEntity>(b =>
        {
            var mapping = b.MapPhoneNumber(e => e.Phone);
            caught = Record.Exception(() => mapping.Unique(null!));
        });
        caught.Should().BeOfType<ArgumentNullException>();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static IProperty ComplexProp<TEntity>(
        IModel model,
        string complexPropName,
        string memberName)
    {
        var entityType = model.FindEntityType(typeof(TEntity))!;
        var complexProp = entityType.FindComplexProperty(complexPropName)!;
        return complexProp.ComplexType.FindProperty(memberName)!;
    }

    private static AnonymizationRule? AnonRule(IProperty prop) =>
        prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

    // =========================================================================
    // Test entities
    // =========================================================================

    private sealed class PersonalEntity
    {
        public int Id { get; set; }

        public Personal Name { get; set; } = default!;
    }

    private sealed class AffixEntity
    {
        public int Id { get; set; }

        public NameAffixes Affixes { get; set; } = default!;
    }

    private sealed class DemoEntity
    {
        public int Id { get; set; }

        public Demographics Bio { get; set; } = default!;
    }

    private sealed class ProfessionalEntity
    {
        public int Id { get; set; }

        public Professional Work { get; set; } = default!;
    }

    private sealed class TwoPersonalEntity
    {
        public int Id { get; set; }

        public Personal LegalName { get; set; } = default!;

        public Personal MaidenName { get; set; } = default!;
    }

    private sealed class OptionalAffixEntity
    {
        public int Id { get; set; }

        public NameAffixes? Affixes { get; set; }
    }

    private sealed class OptionalDemoEntity
    {
        public int Id { get; set; }

        public Demographics? Bio { get; set; }
    }

    private sealed class EmailEntity
    {
        public int Id { get; set; }

        public Guid UserId { get; set; }

        public EmailAddress? Email { get; set; }
    }

    private sealed class PhoneEntity
    {
        public int Id { get; set; }

        public Guid UserId { get; set; }

        public PhoneNumber? Phone { get; set; }
    }

    // =========================================================================
    // DbContexts — model-build-only (connection never opened)
    // =========================================================================

    private sealed class PersonalContext : DbContext
    {
        private PersonalContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static PersonalContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost")
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<PersonalEntity>(b =>
                b.ComplexProperty(e => e.Name, cp => cp.MapPersonal()));
        }
    }

    private sealed class AffixContext : DbContext
    {
        private AffixContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static AffixContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost")
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<AffixEntity>(b =>
                b.ComplexProperty(e => e.Affixes, cp => cp.MapNameAffixes()));
        }
    }

    private sealed class DemoContext : DbContext
    {
        private DemoContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static DemoContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost", o => o.AddD2NodaTime())
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<DemoEntity>(b =>
                b.ComplexProperty(e => e.Bio, cp => cp.MapDemographics()));
        }
    }

    private sealed class ProfessionalContext : DbContext
    {
        private ProfessionalContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static ProfessionalContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost")
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<ProfessionalEntity>(b =>
                b.ComplexProperty(e => e.Work, cp => cp.MapProfessional()));
        }
    }

    private sealed class TwoPersonalContext : DbContext
    {
        private TwoPersonalContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static TwoPersonalContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost")
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<TwoPersonalEntity>(b =>
            {
                b.ComplexProperty(e => e.LegalName, cp => cp.MapPersonal());
                b.ComplexProperty(e => e.MaidenName, cp => cp.MapPersonal());
            });
        }
    }

    private sealed class OptionalAffixContext : DbContext
    {
        private OptionalAffixContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static OptionalAffixContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost")
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<OptionalAffixEntity>(b =>
                b.ComplexProperty(e => e.Affixes, cp => cp.MapNameAffixes()));
        }
    }

    private sealed class OptionalDemoContext : DbContext
    {
        private OptionalDemoContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static OptionalDemoContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost", o => o.AddD2NodaTime())
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<OptionalDemoEntity>(b =>
                b.ComplexProperty(e => e.Bio, cp => cp.MapDemographics()));
        }
    }

    private sealed class EmailConstantContext : DbContext
    {
        private EmailConstantContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static EmailConstantContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost")
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<EmailEntity>(b =>
                b.MapEmailAddress(e => e.Email).Anonymize("deleted@deleted.user.dcsv.io"));
        }
    }

    private sealed class EmailTemplateContext : DbContext
    {
        private EmailTemplateContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static EmailTemplateContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost")
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<EmailEntity>(b =>
                b.MapEmailAddress(e => e.Email)
                 .Anonymize("deletedUser{UserId}@deleted.user.dcsv.io"));
        }
    }

    private sealed class EmailUniqueContext : DbContext
    {
        private EmailUniqueContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static EmailUniqueContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost")
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<EmailEntity>(b =>
                b.MapEmailAddress(e => e.Email)
                 .Unique("deletedUser{UserId}@deleted.user.dcsv.io"));
        }
    }

    private sealed class EmailUniqueNoTokenContext : DbContext
    {
        private EmailUniqueNoTokenContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static EmailUniqueNoTokenContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost")
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            // token-free template on .Unique() must throw at map time
            mb.Entity<EmailEntity>(b =>
                b.MapEmailAddress(e => e.Email).Unique("deleted@deleted.user.dcsv.io"));
        }
    }

    private sealed class EmailEmptyBracesContext : DbContext
    {
        private EmailEmptyBracesContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static EmailEmptyBracesContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost")
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            // "{}" has no token (HasToken returns false) → Anonymize must write Constant.
            mb.Entity<EmailEntity>(b =>
                b.MapEmailAddress(e => e.Email).Anonymize("{}"));
        }
    }

    private sealed class EmailDroppedCouplingContext : DbContext
    {
        private EmailDroppedCouplingContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static EmailDroppedCouplingContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost")
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<EmailEntity>(b =>
            {
                // Deliberately drop the returned EmailMapping — no .Anonymize() chained.
                // This replicates the dropped-coupling hazard: no anonymize annotation
                // is written when the coupling object is discarded.
                _ = b.MapEmailAddress(e => e.Email);
            });
        }
    }

    private sealed class PhoneDroppedCouplingContext : DbContext
    {
        private PhoneDroppedCouplingContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static PhoneDroppedCouplingContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost")
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<PhoneEntity>(b =>
            {
                // Deliberately drop the returned PhoneMapping — no .Anonymize() chained.
                // This replicates the dropped-coupling hazard: no anonymize annotation
                // is written when the coupling object is discarded.
                _ = b.MapPhoneNumber(e => e.Phone);
            });
        }
    }

    private sealed class PhoneConstantContext : DbContext
    {
        private PhoneConstantContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static PhoneConstantContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost")
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<PhoneEntity>(b =>
                b.MapPhoneNumber(e => e.Phone).Anonymize("10000000000"));
        }
    }

    private sealed class PhoneTemplateContext : DbContext
    {
        private PhoneTemplateContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static PhoneTemplateContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost")
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<PhoneEntity>(b =>
                b.MapPhoneNumber(e => e.Phone).Anonymize("deletedPhone{UserId}"));
        }
    }

    private sealed class PhoneUniqueContext : DbContext
    {
        private PhoneUniqueContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static PhoneUniqueContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost")
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<PhoneEntity>(b =>
                b.MapPhoneNumber(e => e.Phone)
                 .Unique("deletedPhone{UserId}"));
        }
    }

    private sealed class PhoneUniqueNoTokenContext : DbContext
    {
        private PhoneUniqueNoTokenContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static PhoneUniqueNoTokenContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost")
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            // token-free template on .Unique() must throw at map time
            mb.Entity<PhoneEntity>(b =>
                b.MapPhoneNumber(e => e.Phone).Unique("10000000000"));
        }
    }
}
