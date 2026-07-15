// -----------------------------------------------------------------------
// <copyright file="AnonymizableAttributeTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.DataGovernance.Abstractions;

using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.DataGovernance.Abstractions;
using Xunit;

/// <summary>
/// Tests for <see cref="AnonymizableAttribute"/>. Covers the three public constructors,
/// the <c>AttributeUsage</c> declaration, and adversarial construction inputs.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AnonymizableAttributeTests
{
    // ---- Ctor 1: kind-only (SetNull / SetEmpty) --------------------------------

    [Fact]
    public void Ctor_kind_SetNull_sets_Kind_and_no_payload()
    {
        var sut = new AnonymizableAttribute(AnonymizeKind.SetNull);

        sut.Kind.Should().Be(AnonymizeKind.SetNull);
        sut.ConstantValue.Should().BeNull();
        sut.Template.Should().BeNull();
    }

    [Fact]
    public void Ctor_kind_SetEmpty_sets_Kind_and_no_payload()
    {
        var sut = new AnonymizableAttribute(AnonymizeKind.SetEmpty);

        sut.Kind.Should().Be(AnonymizeKind.SetEmpty);
        sut.ConstantValue.Should().BeNull();
        sut.Template.Should().BeNull();
    }

    [Fact]
    public void Ctor_kind_Constant_without_value_throws_ArgumentException()
    {
        // AnonymizeKind.Constant requires a payload — use the string ctor instead.
        var act = () => new AnonymizableAttribute(AnonymizeKind.Constant);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_kind_Template_without_value_throws_ArgumentException()
    {
        // AnonymizeKind.Template requires a payload — use the template ctor instead.
        var act = () => new AnonymizableAttribute(AnonymizeKind.Template);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_kind_undefined_enum_throws_ArgumentOutOfRangeException()
    {
        const int undefined_value = 999;
        var act = () => new AnonymizableAttribute((AnonymizeKind)undefined_value);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---- Ctor 2: constant string (Constant) -----------------------------------

    [Fact]
    public void Ctor_string_sets_Kind_Constant_and_ConstantValue()
    {
        var sut = new AnonymizableAttribute("Deleted");

        sut.Kind.Should().Be(AnonymizeKind.Constant);
        sut.ConstantValue.Should().Be("Deleted");
        sut.Template.Should().BeNull();
    }

    [Fact]
    public void Ctor_string_empty_constant_is_accepted_as_Kind_Constant()
    {
        // Author intent preserved — not silently rewritten to SetEmpty.
        var sut = new AnonymizableAttribute(string.Empty);

        sut.Kind.Should().Be(AnonymizeKind.Constant);
        sut.ConstantValue.Should().Be(string.Empty);
    }

    [Fact]
    public void Ctor_string_whitespace_constant_is_accepted_as_literal()
    {
        // Whitespace is a valid constant value — author intent is NOT trimmed.
        const string whitespace_constant = " ";
        var sut = new AnonymizableAttribute(whitespace_constant);

        sut.Kind.Should().Be(AnonymizeKind.Constant);
        sut.ConstantValue.Should().Be(whitespace_constant);
    }

    [Fact]
    public void Ctor_string_null_constant_throws_ArgumentNullException()
    {
        // Null is not a valid constant — use AnonymizeKind.SetNull for null overwrites.
        var act = () => new AnonymizableAttribute(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ---- Ctor 3: template (named-arg Form A) ----------------------------------

    [Fact]
    public void Ctor_template_named_arg_sets_Kind_Template_and_Template()
    {
        const string template_value = "deletedUser{UserId}@deleted.user.dcsv.io";

        // Form A: named argument — the preferred call-site shape.
        var sut = new AnonymizableAttribute(template: template_value);

        sut.Kind.Should().Be(AnonymizeKind.Template);
        sut.Template.Should().Be(template_value);
        sut.ConstantValue.Should().BeNull();
    }

    [Fact]
    public void Ctor_template_explicit_discriminator_sets_Kind_Template_and_Template()
    {
        const string template_value = "deleted-{Id}-user";

        // Both positional forms (with explicit marker) and named-arg form produce
        // identical instances.
        var sut = new AnonymizableAttribute(
            template: template_value,
            marker: AnonymizeTemplateMarker.Template);

        sut.Kind.Should().Be(AnonymizeKind.Template);
        sut.Template.Should().Be(template_value);
        sut.ConstantValue.Should().BeNull();
    }

    [Fact]
    public void Ctor_template_null_template_throws_ArgumentException()
    {
        var act = () => new AnonymizableAttribute(
            template: null!,
            marker: AnonymizeTemplateMarker.Template);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_template_empty_template_throws_ArgumentException()
    {
        var act = () => new AnonymizableAttribute(template: string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_template_whitespace_only_template_throws_ArgumentException()
    {
        var act = () => new AnonymizableAttribute(template: "   ");
        act.Should().Throw<ArgumentException>();
    }

    // ---- AttributeUsage -------------------------------------------------------

    [Fact]
    public void AttributeUsage_ValidOn_is_Property()
    {
        var usage = typeof(AnonymizableAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>(inherit: false);

        usage.Should().NotBeNull();
        usage.ValidOn.Should().Be(AttributeTargets.Property);
    }

    [Fact]
    public void AttributeUsage_AllowMultiple_is_false()
    {
        var usage = typeof(AnonymizableAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>(inherit: false);

        usage.Should().NotBeNull();
        usage.AllowMultiple.Should().BeFalse();
    }

    [Fact]
    public void AttributeUsage_Inherited_is_true()
    {
        var usage = typeof(AnonymizableAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>(inherit: false);

        usage.Should().NotBeNull();
        usage.Inherited.Should().BeTrue();
    }

    // ---- Property read-only contract (no public setter / init) ----------------

    [Theory]
    [InlineData(nameof(AnonymizableAttribute.Kind))]
    [InlineData(nameof(AnonymizableAttribute.ConstantValue))]
    [InlineData(nameof(AnonymizableAttribute.Template))]
    public void Surfaced_property_has_no_public_setter(string propertyName)
    {
        var property = typeof(AnonymizableAttribute).GetProperty(propertyName);
        property.Should().NotBeNull();
        property.SetMethod.Should().BeNull(
            because: $"{propertyName} must not be publicly settable "
                + "— contradictory state must be impossible via object initializer.");
    }
}
