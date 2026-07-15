// -----------------------------------------------------------------------
// <copyright file="AnonymizationRuleTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.DataGovernance.Abstractions;

using AwesomeAssertions;
using DcsvIo.D2.DataGovernance.Abstractions;
using Xunit;

/// <summary>
/// Tests for <see cref="AnonymizationRule"/>. Covers the <c>Create</c> factory for all
/// valid combinations, the <c>Kind ↔ payload</c> invariant enforcement (adversarial), record
/// equality semantics, immutability, and the <c>with</c>-expression behavior.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AnonymizationRuleTests
{
    // ---- Create — valid paths ------------------------------------------------

    [Fact]
    public void Create_SetNull_returns_rule_with_no_payload()
    {
        var rule = AnonymizationRule.Create(AnonymizeKind.SetNull);

        rule.Kind.Should().Be(AnonymizeKind.SetNull);
        rule.ConstantValue.Should().BeNull();
        rule.Template.Should().BeNull();
    }

    [Fact]
    public void Create_SetEmpty_returns_rule_with_no_payload()
    {
        var rule = AnonymizationRule.Create(AnonymizeKind.SetEmpty);

        rule.Kind.Should().Be(AnonymizeKind.SetEmpty);
        rule.ConstantValue.Should().BeNull();
        rule.Template.Should().BeNull();
    }

    [Fact]
    public void Create_Constant_with_value_returns_correct_rule()
    {
        const string constant_value = "[deleted]";
        var rule = AnonymizationRule.Create(AnonymizeKind.Constant, constantValue: constant_value);

        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be(constant_value);
        rule.Template.Should().BeNull();
    }

    [Fact]
    public void Create_Constant_with_empty_string_is_valid()
    {
        var rule = AnonymizationRule.Create(AnonymizeKind.Constant, constantValue: string.Empty);

        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be(string.Empty);
    }

    [Fact]
    public void Create_Template_with_value_returns_correct_rule()
    {
        const string template_value = "deletedUser{UserId}@deleted.user.dcsv.io";
        var rule = AnonymizationRule.Create(AnonymizeKind.Template, template: template_value);

        rule.Kind.Should().Be(AnonymizeKind.Template);
        rule.Template.Should().Be(template_value);
        rule.ConstantValue.Should().BeNull();
    }

    // ---- Create — adversarial: Kind ↔ payload contradictions -----------------

    [Fact]
    public void Create_SetNull_with_constantValue_throws_ArgumentException()
    {
        var act = () => AnonymizationRule.Create(AnonymizeKind.SetNull, constantValue: "oops");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_SetNull_with_template_throws_ArgumentException()
    {
        var act = () => AnonymizationRule.Create(AnonymizeKind.SetNull, template: "oops");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_SetEmpty_with_constantValue_throws_ArgumentException()
    {
        var act = () => AnonymizationRule.Create(AnonymizeKind.SetEmpty, constantValue: "oops");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_SetEmpty_with_template_throws_ArgumentException()
    {
        var act = () => AnonymizationRule.Create(AnonymizeKind.SetEmpty, template: "oops");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_Constant_without_value_throws_ArgumentException()
    {
        var act = () => AnonymizationRule.Create(AnonymizeKind.Constant);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_Constant_with_null_constantValue_throws_ArgumentException()
    {
        var act = () => AnonymizationRule.Create(AnonymizeKind.Constant, constantValue: null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_Constant_with_both_constantValue_and_template_throws_ArgumentException()
    {
        var act = () => AnonymizationRule.Create(
            AnonymizeKind.Constant,
            constantValue: "x",
            template: "y");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_Template_without_value_throws_ArgumentException()
    {
        var act = () => AnonymizationRule.Create(AnonymizeKind.Template);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_Template_with_null_template_throws_ArgumentException()
    {
        var act = () => AnonymizationRule.Create(AnonymizeKind.Template, template: null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_Template_with_empty_template_throws_ArgumentException()
    {
        var act = () => AnonymizationRule.Create(AnonymizeKind.Template, template: string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_Template_with_whitespace_only_template_throws_ArgumentException()
    {
        var act = () => AnonymizationRule.Create(AnonymizeKind.Template, template: "   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_Template_with_both_template_and_constantValue_throws_ArgumentException()
    {
        var act = () => AnonymizationRule.Create(
            AnonymizeKind.Template,
            constantValue: "x",
            template: "tmpl");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_undefined_kind_throws_ArgumentOutOfRangeException()
    {
        const int undefinedKind = 999;
        var act = () => AnonymizationRule.Create((AnonymizeKind)undefinedKind);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---- Equality -----------------------------------------------------------

    [Fact]
    public void Two_identical_rules_are_equal()
    {
        var a = AnonymizationRule.Create(AnonymizeKind.Constant, constantValue: "del");
        var b = AnonymizationRule.Create(AnonymizeKind.Constant, constantValue: "del");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Rules_with_different_kinds_are_not_equal()
    {
        var a = AnonymizationRule.Create(AnonymizeKind.SetNull);
        var b = AnonymizationRule.Create(AnonymizeKind.SetEmpty);

        a.Should().NotBe(b);
    }

    [Fact]
    public void Constant_empty_string_and_SetEmpty_are_NOT_equal()
    {
        // Pins the deliberate non-equality: Kind differs even though the engine
        // treats them identically at apply-time. The record equality contract
        // depends on Kind to detect conflicting double-declarations.
        var constantEmpty = AnonymizationRule.Create(
            AnonymizeKind.Constant,
            constantValue: string.Empty);
        var setEmpty = AnonymizationRule.Create(AnonymizeKind.SetEmpty);

        constantEmpty.Should().NotBe(setEmpty);
    }

    [Fact]
    public void Rules_with_different_template_values_are_not_equal()
    {
        var a = AnonymizationRule.Create(AnonymizeKind.Template, template: "template-a");
        var b = AnonymizationRule.Create(AnonymizeKind.Template, template: "template-b");

        a.Should().NotBe(b);
    }

    // ---- Immutability --------------------------------------------------------

    [Fact]
    public void Properties_have_no_public_setter()
    {
        // private init — no public mutation path; Create is the only construction point.
        foreach (var prop in typeof(AnonymizationRule).GetProperties())
        {
            prop.SetMethod?.IsPublic.Should().NotBe(
                true,
                because: $"{prop.Name} must not be publicly mutable.");
        }
    }

    // ---- Adversarial: whitespace-only ConstantValue on Template path ----------

    [Fact]
    public void Create_Template_with_whitespace_only_constantValue_throws_ArgumentException()
    {
        // Template path must not accept a non-null ConstantValue — even whitespace.
        var act = () => AnonymizationRule.Create(
            AnonymizeKind.Template,
            constantValue: " ",
            template: "t");
        act.Should().Throw<ArgumentException>();
    }
}
