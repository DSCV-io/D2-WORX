// -----------------------------------------------------------------------
// <copyright file="EnumerableExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Utilities.Extensions;

using AwesomeAssertions;
using DcsvIo.D2.Utilities.Extensions;
using Xunit;

public sealed class EnumerableExtensionsTests
{
    // ----------------------------------------------------------------------
    // Truthy / Falsey
    // ----------------------------------------------------------------------

    // Concrete-array variable typing (vs IEnumerable<T>?) signals to R# that
    // re-enumeration is safe — no PossibleMultipleEnumeration warnings on the
    // symmetric Truthy/Falsey assertions.

    [Fact]
    public void Falsey_OnNull_IsTrue()
    {
        int[]? input = null;

        input.Falsey().Should().BeTrue();
        input.Truthy().Should().BeFalse();
    }

    [Fact]
    public void Falsey_OnEmpty_IsTrue()
    {
        var input = Array.Empty<int>();

        input.Falsey().Should().BeTrue();
        input.Truthy().Should().BeFalse();
    }

    [Fact]
    public void Truthy_OnSingleElement_IsTrue()
    {
        int[] input = [42];

        input.Truthy().Should().BeTrue();
        input.Falsey().Should().BeFalse();
    }

    [Fact]
    public void Truthy_OnMultipleElements_IsTrue()
    {
        int[] input = [1, 2, 3];

        input.Truthy().Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // Clean — empty input handling (3 behaviors × null input)
    //
    // NOTE: Clean's signature `Func<T, T?>` only resolves T? to a nullable
    // form for reference types — for value types T? collapses back to T
    // (unconstrained generic). Tests therefore exercise reference-type T.
    // ----------------------------------------------------------------------

    [Fact]
    public void Clean_OnNullInput_DefaultBehavior_ReturnsNull()
    {
        IEnumerable<string>? input = null;

        var result = input.Clean(s => s);

        result.Should().BeNull();
    }

    [Fact]
    public void Clean_OnNullInput_ReturnEmpty_ReturnsEmpty()
    {
        IEnumerable<string>? input = null;

        var result = input.Clean(s => s, CleanEnumEmptyBehavior.ReturnEmpty);

        result.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Clean_OnNullInput_Throw_ThrowsArgumentException()
    {
        IEnumerable<string>? input = null;

        var act = () => input.Clean(s => s, CleanEnumEmptyBehavior.Throw);

        act.Should().Throw<ArgumentException>()
            .WithMessage("The enumerable is empty after cleaning.*");
    }

    // ----------------------------------------------------------------------
    // Clean — empty input via empty array (covers the path before cleaner)
    // ----------------------------------------------------------------------

    [Fact]
    public void Clean_OnEmptyInput_DefaultBehavior_ReturnsNull()
    {
        IEnumerable<string> input = [];

        var result = input.Clean(s => s);

        result.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // Clean — cleaner-returns-null branch (2 behaviors)
    // ----------------------------------------------------------------------

    [Fact]
    public void Clean_OnCleanerReturningNullForSome_RemoveNulls_SkipsThem()
    {
        IEnumerable<string> input = ["keep1", "drop", "keep2", "drop", "keep3"];

        // Drop the literal "drop" entries by returning null for them.
        var result = input.Clean(s => s == "drop" ? null : s);

        result.Should().Equal("keep1", "keep2", "keep3");
    }

    [Fact]
    public void Clean_OnCleanerReturningNull_ThrowOnNull_ThrowsInvalidOperation()
    {
        IEnumerable<string> input = ["a", "b", "c"];

        var act = () => input.Clean(
            _ => null,
            CleanEnumEmptyBehavior.ReturnNull,
            CleanValueNullBehavior.ThrowOnNull);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("A cleaned value evaluated to null.");
    }

    // ----------------------------------------------------------------------
    // Clean — all values cleaned to null (post-clean empty list, 3 behaviors)
    // ----------------------------------------------------------------------

    [Fact]
    public void Clean_AllCleanedToNull_DefaultBehavior_ReturnsNull()
    {
        IEnumerable<string> input = ["a", "b", "c"];

        var result = input.Clean(_ => null);

        result.Should().BeNull();
    }

    [Fact]
    public void Clean_AllCleanedToNull_ReturnEmpty_ReturnsEmpty()
    {
        IEnumerable<string> input = ["a", "b", "c"];

        var result = input.Clean(
            _ => null,
            CleanEnumEmptyBehavior.ReturnEmpty);

        result.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Clean_AllCleanedToNull_Throw_ThrowsArgumentException()
    {
        IEnumerable<string> input = ["a", "b", "c"];

        var act = () => input.Clean(
            _ => null,
            CleanEnumEmptyBehavior.Throw);

        act.Should().Throw<ArgumentException>()
            .WithMessage("The enumerable is empty after cleaning.*");
    }

    // ----------------------------------------------------------------------
    // Clean — happy path
    // ----------------------------------------------------------------------

    [Fact]
    public void Clean_HappyPath_AppliesCleanerToEachElement()
    {
        IEnumerable<string> input = ["a", "b", "c"];

        var result = input.Clean(s => s.ToUpperInvariant());

        result.Should().Equal("A", "B", "C");
    }

    [Fact]
    public void Clean_MaterializesUpstreamOnce()
    {
        // Adversarial: a generator-backed enumerable with side effects must
        // only be enumerated once (the implementation calls .ToList() upfront).
        var enumerationCount = 0;
        IEnumerable<string> Source()
        {
            enumerationCount++;
            yield return "a";
            yield return "b";
        }

        Source().Clean(s => s);

        enumerationCount.Should().Be(1);
    }
}
