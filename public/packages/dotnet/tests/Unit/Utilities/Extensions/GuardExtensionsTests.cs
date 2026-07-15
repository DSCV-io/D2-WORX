// -----------------------------------------------------------------------
// <copyright file="GuardExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Utilities.Extensions;

using AwesomeAssertions;
using DcsvIo.D2.Utilities.Extensions;
using Xunit;

public sealed class GuardExtensionsTests
{
    // ----------------------------------------------------------------------
    // String overload (#1)
    // ----------------------------------------------------------------------

    [Fact]
    public void String_Null_ThrowsArgumentNullException()
    {
        string? value = null;

        var act = () => value.ThrowIfFalsey();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void String_Null_ParamName_IsCallerExpression()
    {
        string? myArg = null;

        var act = () => myArg.ThrowIfFalsey();

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("myArg");
    }

    [Fact]
    public void String_Empty_ThrowsArgumentException()
    {
        var value = string.Empty;

        var act = () => value.ThrowIfFalsey();

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void String_Whitespace_ThrowsArgumentException(string whitespace)
    {
        var act = () => whitespace.ThrowIfFalsey();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void String_ArgumentException_NotArgumentNullException_ForEmpty()
    {
        var value = string.Empty;

        var act = () => value.ThrowIfFalsey();

        // Must be exactly ArgumentException, NOT ArgumentNullException (derived).
        act.Should().ThrowExactly<ArgumentException>();
    }

    [Fact]
    public void String_Valid_DoesNotThrow_AndIsUsableAfter()
    {
        const string expected_value = "hello";
        var value = expected_value;

        var act = () => value.ThrowIfFalsey();

        act.Should().NotThrow();

        // Post-[NotNull]: compiler treats value as non-null; Length accessible without !.
        value.Length.Should().Be(expected_value.Length);
    }

    // ----------------------------------------------------------------------
    // Collection overload (#2)
    // ----------------------------------------------------------------------

    [Fact]
    public void Collection_Null_ThrowsArgumentNullException()
    {
        IEnumerable<string>? value = null;

        var act = () => value.ThrowIfFalsey();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Collection_Null_ParamName_IsCallerExpression()
    {
        IEnumerable<int>? myArg = null;

        var act = () => myArg.ThrowIfFalsey();

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("myArg");
    }

    [Fact]
    public void Collection_Empty_ThrowsArgumentException()
    {
        IEnumerable<string> value = Array.Empty<string>();

        var act = () => value.ThrowIfFalsey();

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Overload-resolution pin: a NON-null empty <see cref="List{T}"/> (a concrete
    /// collection type) MUST bind to the <see cref="IEnumerable{T}"/> block and throw
    /// <see cref="ArgumentException"/>. Confirms that the C# 14 block-form correctly
    /// routes a concrete collection through the collection guard now that the generic
    /// <c>T : class</c> overload has been removed.
    /// </summary>
    [Fact]
    public void EmptyList_BindsToEnumerableOverload_ThrowsArgumentException()
    {
        var act = () => new List<string>().ThrowIfFalsey();

        act.Should().ThrowExactly<ArgumentException>();
    }

    /// <summary>
    /// Overload-resolution pin: a <see cref="string"/> variable MUST bind to the
    /// <c>string?</c> block (identity conversion), NOT to the
    /// <see cref="IEnumerable{T}"/> block (reference conversion via
    /// <c>IEnumerable&lt;char&gt;</c>). An empty string therefore throws
    /// <see cref="ArgumentException"/> with empty/whitespace semantics, not silently
    /// passes through collection semantics.
    /// </summary>
    [Fact]
    public void EmptyString_BindsToStringOverload_ThrowsArgumentException()
    {
        var value = string.Empty;

        var act = () => value.ThrowIfFalsey();

        act.Should().ThrowExactly<ArgumentException>();
    }

    [Fact]
    public void Collection_Empty_ParamName_IsCallerExpression()
    {
        // Pins [CallerArgumentExpression] capture on the collection-empty path:
        // the exception must name the LOCAL VARIABLE as written at the call site,
        // not the internal parameter name of the extension method.
        IEnumerable<int> myArg = Array.Empty<int>();

        var act = () => myArg.ThrowIfFalsey();

        act.Should().Throw<ArgumentException>()
            .WithParameterName("myArg");
    }

    [Fact]
    public void Collection_ExplicitParamName_Override_UsedInException()
    {
        // Pins the explicit-paramName-override path used by swept indexed-loop
        // call sites (e.g. additionalScopes[i].ThrowIfFalsey($"...[{i}]")).
        // When a caller supplies a non-null paramName string, that string MUST
        // appear in the exception — NOT the auto-captured caller expression.
        IEnumerable<int>? nullCollection = null;

        const string explicit_param = "additionalScopes[0]";

        var act = () => nullCollection.ThrowIfFalsey(explicit_param);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(explicit_param);
    }

    [Fact]
    public void Collection_NonEmpty_DoesNotThrow()
    {
        IEnumerable<int> value = [1, 2, 3];

        var act = () => value.ThrowIfFalsey();

        act.Should().NotThrow();
    }

    // ----------------------------------------------------------------------
    // Guid? overload (#3)
    // ----------------------------------------------------------------------

    [Fact]
    public void NullableGuid_Null_ThrowsArgumentNullException()
    {
        Guid? value = null;

        var act = () => value.ThrowIfFalsey();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NullableGuid_Empty_ThrowsArgumentException()
    {
        Guid? value = Guid.Empty;

        var act = () => value.ThrowIfFalsey();

        act.Should().ThrowExactly<ArgumentException>();
    }

    [Fact]
    public void NullableGuid_Null_ParamName_IsCallerExpression()
    {
        Guid? myArg = null;

        var act = () => myArg.ThrowIfFalsey();

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("myArg");
    }

    [Fact]
    public void NullableGuid_Valid_DoesNotThrow()
    {
        Guid? value = Guid.NewGuid();

        var act = () => value.ThrowIfFalsey();

        act.Should().NotThrow();
    }

    // ----------------------------------------------------------------------
    // Guid overload (#4)
    // ----------------------------------------------------------------------

    [Fact]
    public void Guid_Empty_ThrowsArgumentException()
    {
        var value = Guid.Empty;

        var act = () => value.ThrowIfFalsey();

        act.Should().ThrowExactly<ArgumentException>();
    }

    [Fact]
    public void Guid_Valid_DoesNotThrow()
    {
        var value = Guid.NewGuid();

        var act = () => value.ThrowIfFalsey();

        act.Should().NotThrow();
    }

    [Fact]
    public void Guid_Empty_ParamName_IsCallerExpression()
    {
        var myArg = Guid.Empty;

        var act = () => myArg.ThrowIfFalsey();

        act.Should().Throw<ArgumentException>()
            .WithParameterName("myArg");
    }
}
