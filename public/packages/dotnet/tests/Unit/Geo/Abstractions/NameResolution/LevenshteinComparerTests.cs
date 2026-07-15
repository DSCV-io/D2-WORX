// -----------------------------------------------------------------------
// <copyright file="LevenshteinComparerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Geo.Abstractions.NameResolution;

using AwesomeAssertions;
using D2.Shared.Geo.Abstractions.NameResolution;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="LevenshteinComparer.Compare"/> and
/// <see cref="LevenshteinComparer.IsWithin"/>. Validates: trivial/empty-string
/// cases, single-edit operations (insertion, deletion, substitution),
/// length-difference shortcut, early-termination cap, negative-distance
/// sentinel, null handling, case sensitivity, and boundary behaviour of
/// both public methods.
/// </summary>
public sealed class LevenshteinComparerTests
{
    // ------------------------------------------------------------------
    // Compare — identical strings
    // ------------------------------------------------------------------

    [Fact]
    public void Compare_IdenticalStrings_ReturnsZero()
    {
        LevenshteinComparer.Compare("france", "france", maxDistance: 3).Should().Be(0);
    }

    [Fact]
    public void Compare_BothEmpty_ReturnsZero()
    {
        LevenshteinComparer.Compare(string.Empty, string.Empty, maxDistance: 0).Should().Be(0);
    }

    // ------------------------------------------------------------------
    // Compare — null inputs treated as empty string
    // ------------------------------------------------------------------

    [Fact]
    public void Compare_BothNull_ReturnsZero()
    {
        LevenshteinComparer.Compare(null, null, maxDistance: 0).Should().Be(0);
    }

    [Fact]
    public void Compare_FirstNull_TreatsAsEmpty()
    {
        // null vs "abc" — edit distance 3 (three insertions).
        LevenshteinComparer.Compare(null, "abc", maxDistance: 5).Should().Be(3);
    }

    [Fact]
    public void Compare_SecondNull_TreatsAsEmpty()
    {
        LevenshteinComparer.Compare("abc", null, maxDistance: 5).Should().Be(3);
    }

    // ------------------------------------------------------------------
    // Compare — one side empty
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("abc", "", 3, 3)]
    [InlineData("", "abc", 3, 3)]
    [InlineData("a", "", 1, 1)]
    public void Compare_OneSideEmpty_ReturnsLengthOfOtherSide(
        string a, string b, int maxDistance, int expected)
    {
        LevenshteinComparer.Compare(a, b, maxDistance).Should().Be(expected);
    }

    // ------------------------------------------------------------------
    // Compare — single edit operations
    // ------------------------------------------------------------------

    [Fact]
    public void Compare_SingleSubstitution_ReturnsOne()
    {
        // "france" vs "fraace" — one substitution (n → a).
        LevenshteinComparer.Compare("france", "fraace", maxDistance: 2).Should().Be(1);
    }

    [Fact]
    public void Compare_SingleInsertion_ReturnsOne()
    {
        // "abc" vs "abbc" — one insertion.
        LevenshteinComparer.Compare("abc", "abbc", maxDistance: 2).Should().Be(1);
    }

    [Fact]
    public void Compare_SingleDeletion_ReturnsOne()
    {
        // "france" vs "frnce" — one deletion.
        LevenshteinComparer.Compare("france", "frnce", maxDistance: 2).Should().Be(1);
    }

    // ------------------------------------------------------------------
    // Compare — transposition (NOT damerau — two ops, not one)
    // ------------------------------------------------------------------

    [Fact]
    public void Compare_Transposition_TwoOps()
    {
        // "ab" vs "ba" — standard Levenshtein counts as 2 ops (no transposition op).
        LevenshteinComparer.Compare("ab", "ba", maxDistance: 3).Should().Be(2);
    }

    // ------------------------------------------------------------------
    // Compare — case sensitivity
    // ------------------------------------------------------------------

    [Fact]
    public void Compare_DifferentCase_CountsAsEdits()
    {
        // The comparer is case-sensitive — callers pre-normalize via NameNormalizer.
        LevenshteinComparer.Compare("France", "france", maxDistance: 10).Should().Be(1);
    }

    // ------------------------------------------------------------------
    // Compare — length-difference shortcut returns ceiling
    // ------------------------------------------------------------------

    [Fact]
    public void Compare_LengthDiffExceedsCap_ReturnsCeiling()
    {
        // |"france".Length - "fr".Length| = 4 > maxDistance 2 → ceiling = 3.
        LevenshteinComparer.Compare("france", "fr", maxDistance: 2).Should().Be(3);
    }

    // ------------------------------------------------------------------
    // Compare — negative maxDistance sentinel
    // ------------------------------------------------------------------

    [Fact]
    public void Compare_NegativeMaxDistance_IdenticalStrings_ReturnsZero()
    {
        // Negative cap is clamped to 0: cap=0, ceiling=1. Identical strings
        // have distance=0; 0 is NOT > cap(0), so the actual distance is returned.
        LevenshteinComparer.Compare("x", "x", maxDistance: -1).Should().Be(0);
    }

    [Fact]
    public void Compare_NegativeMaxDistance_DifferentStrings_ReturnsCeilingOfOne()
    {
        // "abc" vs "xyz" has distance=3; with cap=0, ceiling=1. The row-min
        // early-termination fires, returning ceiling=1.
        LevenshteinComparer.Compare("abc", "xyz", maxDistance: -5).Should().Be(1);
    }

    // ------------------------------------------------------------------
    // Compare — early-termination cap (rowMin exceeds cap)
    // ------------------------------------------------------------------

    [Fact]
    public void Compare_HighlyDifferentStringsWithTightCap_ReturnsCeiling()
    {
        // "abcdef" vs "xyz" — actual distance >= 4 but cap = 1, so ceiling = 2.
        LevenshteinComparer.Compare("abcdef", "xyz", maxDistance: 1).Should().Be(2);
    }

    [Fact]
    public void Compare_DistanceExactlyAtCap_ReturnsExactDistance()
    {
        // "abc" vs "xyz" — distance = 3, cap = 3 → returns 3.
        LevenshteinComparer.Compare("abc", "xyz", maxDistance: 3).Should().Be(3);
    }

    [Fact]
    public void Compare_DistanceOneAboveCap_ReturnsCeiling()
    {
        // "abcd" vs "xyz " — distance = 4, cap = 3 → ceiling = 4.
        LevenshteinComparer.Compare("abcd", "xyz ", maxDistance: 3).Should().Be(4);
    }

    // ------------------------------------------------------------------
    // IsWithin — happy path
    // ------------------------------------------------------------------

    [Fact]
    public void IsWithin_IdenticalStrings_ReturnsTrue()
    {
        LevenshteinComparer.IsWithin("france", "france", maxDistance: 0).Should().BeTrue();
    }

    [Fact]
    public void IsWithin_SingleEditWithinCap_ReturnsTrue()
    {
        LevenshteinComparer.IsWithin("france", "fraace", maxDistance: 1).Should().BeTrue();
    }

    [Fact]
    public void IsWithin_TwoEditsExceedsCapOfOne_ReturnsFalse()
    {
        LevenshteinComparer.IsWithin("france", "fraxxe", maxDistance: 1).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // IsWithin — null inputs
    // ------------------------------------------------------------------

    [Fact]
    public void IsWithin_BothNull_ReturnsTrue()
    {
        LevenshteinComparer.IsWithin(null, null, maxDistance: 0).Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // IsWithin — negative maxDistance always false
    // ------------------------------------------------------------------

    [Fact]
    public void IsWithin_NegativeMaxDistance_IdenticalStrings_ReturnsFalse()
    {
        // Even "x" vs "x" is false when maxDistance < 0 — matches .NET short-circuit.
        LevenshteinComparer.IsWithin("x", "x", maxDistance: -1).Should().BeFalse();
    }

    [Fact]
    public void IsWithin_NegativeMaxDistance_BothEmpty_ReturnsFalse()
    {
        LevenshteinComparer.IsWithin(string.Empty, string.Empty, maxDistance: -1).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // IsWithin — resolver usage pattern (cap = 1, 2, 3)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("france", "france", 0, true)]
    [InlineData("france", "frnce", 1, true)]
    [InlineData("france", "fnce", 2, true)]
    [InlineData("france", "fce", 3, true)]
    [InlineData("france", "fce", 2, false)]
    [InlineData("france", "fnce", 0, false)]
    public void IsWithin_ResolverCapBoundaries_MatchesExpected(
        string a, string b, int maxDistance, bool expected)
    {
        LevenshteinComparer.IsWithin(a, b, maxDistance).Should().Be(expected);
    }
}
