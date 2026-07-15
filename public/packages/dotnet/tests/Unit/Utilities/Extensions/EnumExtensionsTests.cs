// -----------------------------------------------------------------------
// <copyright file="EnumExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Utilities.Extensions;

using System;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Utilities.Extensions;
using Xunit;

public sealed class EnumExtensionsTests
{
    // Test fixtures. Public so xUnit Theory + InlineData(TestColor.Red) can bind
    // them from outside the test class scope (CS0051 otherwise — InlineData
    // arguments compile against the test method's parameter types).

    /// <summary>Test enum with three named members.</summary>
    public enum TestColor
    {
        /// <summary>Red.</summary>
        Red = 0,

        /// <summary>Green.</summary>
        Green = 1,

        /// <summary>Blue.</summary>
        Blue = 2,
    }

    /// <summary>
    /// Test enum with no members — verifies "no defined value can match" handling.
    /// </summary>
    public enum EmptyTest
    {
    }

    /// <summary>Test [Flags] enum for verifying comma-separated parse pass-through.</summary>
    [Flags]
    public enum TestAccess
    {
        /// <summary>None.</summary>
        None = 0,

        /// <summary>Read.</summary>
        Read = 1,

        /// <summary>Write.</summary>
        Write = 2,

        /// <summary>Execute.</summary>
        Execute = 4,
    }

    // ----------------------------------------------------------------------
    // null / empty / whitespace inputs → false + null
    // ----------------------------------------------------------------------

    [Fact]
    public void TryParseTruthyNull_OnNull_ReturnsFalseAndNull()
    {
        string? input = null;

        var ok = input.TryParseTruthyNull<TestColor>(out var value);

        ok.Should().BeFalse();
        value.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    [InlineData("   ")]
    public void TryParseTruthyNull_OnEmptyOrWhitespace_ReturnsFalseAndNull(string input)
    {
        var ok = input.TryParseTruthyNull<TestColor>(out var value);

        ok.Should().BeFalse();
        value.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // valid names (case-insensitive)
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("Red", TestColor.Red)]
    [InlineData("Green", TestColor.Green)]
    [InlineData("Blue", TestColor.Blue)]
    public void TryParseTruthyNull_OnValidName_ReturnsTrueAndValue(string input, TestColor expected)
    {
        var ok = input.TryParseTruthyNull<TestColor>(out var value);

        ok.Should().BeTrue();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("red")] // lowercase
    [InlineData("RED")] // uppercase
    [InlineData("rEd")] // mixed-case
    [InlineData("ReD")] // alternative mixed-case
    public void TryParseTruthyNull_OnCaseVariants_ReturnsTrueAndValue(string input)
    {
        var ok = input.TryParseTruthyNull<TestColor>(out var value);

        ok.Should().BeTrue();
        value.Should().Be(TestColor.Red);
    }

    // ----------------------------------------------------------------------
    // invalid names → false + null
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("Purple")] // not a member
    [InlineData("Reddish")] // partial / suffix
    [InlineData("Re")] // prefix
    [InlineData("'; DROP TABLE colors; --")]
    public void TryParseTruthyNull_OnInvalidName_ReturnsFalseAndNull(string input)
    {
        var ok = input.TryParseTruthyNull<TestColor>(out var value);

        ok.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void TryParseTruthyNull_OnCommaSeparatedNames_NonFlagsEnum_StillSucceeds()
    {
        // Adversarial: Enum.TryParse accepts comma-separated names even on a NON-[Flags]
        // enum — the underlying int values are bitwise-OR'd. "Red,Green" → 0|1 = 1 = Green.
        // The wrapper inherits this. Calling code that wants strict single-name parsing
        // must validate separately (e.g. reject inputs containing ',').
        const string comma_separated = "Red,Green";

        var ok = comma_separated.TryParseTruthyNull<TestColor>(out var value);

        ok.Should().BeTrue();
        value.Should().Be(TestColor.Green); // 0 | 1 = 1
    }

    // ----------------------------------------------------------------------
    // empty enum — no member can match
    // ----------------------------------------------------------------------

    [Fact]
    public void TryParseTruthyNull_OnEmptyEnumWithAnyName_ReturnsFalseAndNull()
    {
        // Adversarial: an enum with no defined members should reject every name.
        const string anything = "Anything";

        var ok = anything.TryParseTruthyNull<EmptyTest>(out var value);

        ok.Should().BeFalse();
        value.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // Numeric-string behavior — Enum.TryParse accepts numeric strings BY DEFAULT,
    // even when the integer is NOT a defined enum member. Document the wrapper's
    // pass-through behavior so a future tightening surfaces here.
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("0", TestColor.Red)]
    [InlineData("1", TestColor.Green)]
    [InlineData("2", TestColor.Blue)]
    public void TryParseTruthyNull_OnNumericStringMatchingDefinedMember_ReturnsTrueAndValue(
        string input,
        TestColor expected)
    {
        // Adversarial: the underlying Enum.TryParse accepts numeric strings; the
        // wrapper passes that through. Calling code that wants name-only parsing
        // must validate separately or use Enum.IsDefined.
        var ok = input.TryParseTruthyNull<TestColor>(out var value);

        ok.Should().BeTrue();
        value.Should().Be(expected);
    }

    [Fact]
    public void TryParseTruthyNull_OnUndefinedNumericValue_ReturnsTrueWithIntegerValue()
    {
        // Adversarial: Enum.TryParse with a numeric string accepts ANY integer
        // representable as the underlying type — even when no enum member matches.
        // The wrapper does NOT call Enum.IsDefined, so "999" parses to a TestColor
        // value of 999 with no error. Document the pass-through.
        const string undefined_int = "999";

        var ok = undefined_int.TryParseTruthyNull<TestColor>(out var value);

        ok.Should().BeTrue();
        value.Should().NotBeNull();
        ((int)value.Value).Should().Be(999);
    }

    [Fact]
    public void TryParseTruthyNull_OnNegativeNumericString_ReturnsTrueWithUndefinedValue()
    {
        // Adversarial: same pass-through for negative ints.
        const string negative = "-1";

        var ok = negative.TryParseTruthyNull<TestColor>(out var value);

        ok.Should().BeTrue();
        ((int)value!.Value).Should().Be(-1);
    }

    [Theory]
    [InlineData("notanumber999")]
    [InlineData("1.5")]
    [InlineData("1e3")]
    [InlineData("0x01")]
    public void TryParseTruthyNull_OnMalformedNumericString_ReturnsFalse(string input)
    {
        // Adversarial: anything that's neither a defined name nor a parseable
        // signed integer of the underlying type must fail.
        var ok = input.TryParseTruthyNull<TestColor>(out var value);

        ok.Should().BeFalse();
        value.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // [Flags] enums — Enum.TryParse natively supports comma-separated names.
    // Document behavior so it's not relied on by accident downstream.
    // ----------------------------------------------------------------------

    [Fact]
    public void TryParseTruthyNull_OnFlagsCommaSeparated_ReturnsTrueWithCombinedFlags()
    {
        // Adversarial: Enum.TryParse handles `[Flags]` comma syntax. The wrapper
        // inherits that.
        const string combined = "Read, Write";

        var ok = combined.TryParseTruthyNull<TestAccess>(out var value);

        ok.Should().BeTrue();
        value.Should().Be(TestAccess.Read | TestAccess.Write);
    }

    // ----------------------------------------------------------------------
    // Adversarial — oversized input
    // ----------------------------------------------------------------------

    [Fact]
    public void TryParseTruthyNull_OnThousandCharacterRandomString_ReturnsFalseNoThrow()
    {
        const int huge_length = 1000;
        var input = new string(Enumerable.Range(0, huge_length)
            .Select(_ => (char)('a' + Random.Shared.Next(26)))
            .ToArray());

        Action act = () => input.TryParseTruthyNull<TestColor>(out _);

        act.Should().NotThrow();
        input.TryParseTruthyNull<TestColor>(out var value).Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void TryParseTruthyNull_OnFullEmojiInput_ReturnsFalseNoThrow()
    {
        // Adversarial: surrogate pairs must not crash.
        const string emoji = "😀😁😂";

        Action act = () => emoji.TryParseTruthyNull<TestColor>(out _);

        act.Should().NotThrow();
        emoji.TryParseTruthyNull<TestColor>(out var value).Should().BeFalse();
        value.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // Generic constraint — TEnum : struct, Enum is enforced at compile time.
    // We can't write a runtime test that invokes the method with a non-enum type
    // (the compiler rejects it), so verify the constraint via reflection.
    // ----------------------------------------------------------------------

    [Fact]
    public void TryParseTruthyNull_GenericConstraint_IsStructAndEnum()
    {
        // The extension is emitted as a static method on a generated host class.
        // Locate it by name + by `string?` first parameter + 1 generic argument.
        var method = typeof(EnumExtensions)
            .GetTypeInfo()
            .DeclaredNestedTypes
            .SelectMany(t => t.GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Concat(typeof(EnumExtensions).GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .FirstOrDefault(m =>
                m.Name == "TryParseTruthyNull"
                && m.IsGenericMethodDefinition
                && m.GetGenericArguments().Length == 1);

        method.Should().NotBeNull(
            "EnumExtensions must expose a generic TryParseTruthyNull<TEnum> method " +
            "(the C# 14 extension-member emit shape varies; if this lookup fails, " +
            "extend the search to cover the new shape).");

        var typeParam = method.GetGenericArguments()[0];
        var attrs = typeParam.GenericParameterAttributes;

        attrs.Should().HaveFlag(
            GenericParameterAttributes.NotNullableValueTypeConstraint,
            "TEnum must be `struct` (not-nullable value type).");

        var constraintTypes = typeParam.GetGenericParameterConstraints();
        constraintTypes.Should().Contain(
            typeof(Enum),
            "TEnum must be constrained to `Enum`.");
    }
}
