// -----------------------------------------------------------------------
// <copyright file="NameNormalizerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Geo.Abstractions.NameResolution;

using AwesomeAssertions;
using DcsvIo.D2.Geo.Abstractions.NameResolution;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="NameNormalizer.Normalize"/>. Validates the
/// full seven-step pipeline (null/falsey short-circuit, NFD decomposition, combining-mark
/// strip, ampersand-token substitution, invariant casefold, trim, whitespace collapse)
/// including idempotency and cross-language parity invariants.
/// </summary>
public sealed class NameNormalizerTests
{
    // ------------------------------------------------------------------
    // Null / empty / whitespace short-circuit (pipeline step 1)
    // ------------------------------------------------------------------

    [Fact]
    public void Normalize_NullInput_ReturnsEmpty()
    {
        NameNormalizer.Normalize(null).Should().Be(string.Empty);
    }

    [Fact]
    public void Normalize_EmptyString_ReturnsEmpty()
    {
        NameNormalizer.Normalize(string.Empty).Should().Be(string.Empty);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("   \t\r\n  ")]
    public void Normalize_WhitespaceOnly_ReturnsEmpty(string input)
    {
        NameNormalizer.Normalize(input).Should().Be(string.Empty);
    }

    // ------------------------------------------------------------------
    // Diacritics / accent stripping (NFD + combining-mark strip)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("São Paulo", "sao paulo")]
    [InlineData("Côte d'Ivoire", "cote d'ivoire")]
    [InlineData("München", "munchen")]
    [InlineData("Zürich", "zurich")]
    [InlineData("Montréal", "montreal")]
    [InlineData("Bogotá", "bogota")]
    [InlineData("Ñoño", "nono")]
    public void Normalize_AccentedInput_StripsAccentsAndCasefolds(string input, string expected)
    {
        NameNormalizer.Normalize(input).Should().Be(expected);
    }

    // ------------------------------------------------------------------
    // Mixed case / invariant casefold (pipeline step 5)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("UNITED STATES", "united states")]
    [InlineData("United States", "united states")]
    [InlineData("uNiTeD sTaTeS", "united states")]
    [InlineData("france", "france")]
    public void Normalize_MixedCase_ProducesLowercase(string input, string expected)
    {
        NameNormalizer.Normalize(input).Should().Be(expected);
    }

    // ------------------------------------------------------------------
    // Leading / trailing whitespace trim (pipeline step 6)
    // ------------------------------------------------------------------

    [Fact]
    public void Normalize_LeadingAndTrailingWhitespace_Trimmed()
    {
        NameNormalizer.Normalize("  France  ").Should().Be("france");
    }

    [Fact]
    public void Normalize_LeadingTabAndTrailingNewline_Trimmed()
    {
        NameNormalizer.Normalize("\tFrance\n").Should().Be("france");
    }

    // ------------------------------------------------------------------
    // Internal whitespace collapse (pipeline step 7)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("United  States", "united states")]
    [InlineData("United   States", "united states")]
    [InlineData("United\tStates", "united states")]
    [InlineData("United\t\t  States", "united states")]
    public void Normalize_InternalWhitespaceRuns_CollapsedToSingleSpace(string input, string expected)
    {
        NameNormalizer.Normalize(input).Should().Be(expected);
    }

    // ------------------------------------------------------------------
    // Ampersand-token substitution (spaced form only — pipeline step 4)
    // ------------------------------------------------------------------

    [Fact]
    public void Normalize_SpacedAmpersand_SubstitutedWithAnd()
    {
        // " & " (with surrounding whitespace) becomes " and ".
        NameNormalizer.Normalize("Trinidad & Tobago").Should().Be("trinidad and tobago");
    }

    [Fact]
    public void Normalize_UnspacedAmpersand_PreservedLiterally()
    {
        // "AT&T" — no surrounding whitespace around "&" → untouched.
        NameNormalizer.Normalize("AT&T").Should().Be("at&t");
    }

    [Fact]
    public void Normalize_AmpersandAtStartOfToken_PreservedLiterally()
    {
        // "&something" — no leading space before "&" → untouched.
        NameNormalizer.Normalize("foo &bar").Should().Be("foo &bar");
    }

    // ------------------------------------------------------------------
    // Unicode — non-Latin scripts (no diacritic strip expected)
    // ------------------------------------------------------------------

    [Fact]
    public void Normalize_ArabicScript_CasefoldedAndTrimmed()
    {
        // Arabic doesn't have combining-mark decompositions in NFD that produce
        // strippable NonSpacingMark characters — the text passes through unchanged
        // (except whitespace collapse / casefold, which is a no-op for Arabic).
        const string arabic = "مصر";
        NameNormalizer.Normalize(arabic).Should().Be(arabic);
    }

    [Fact]
    public void Normalize_CyrillicScript_ReturnedLowercased()
    {
        // Cyrillic uppercase "РОССИЯ" → lowercase "россия".
        NameNormalizer.Normalize("РОССИЯ").Should().Be("россия");
    }

    // ------------------------------------------------------------------
    // Punctuation — preserved (only combining marks stripped)
    // ------------------------------------------------------------------

    [Fact]
    public void Normalize_HyphenatedName_HyphenPreserved()
    {
        NameNormalizer.Normalize("Bosnia-Herzegovina").Should().Be("bosnia-herzegovina");
    }

    [Fact]
    public void Normalize_ApostropheInName_ApostrophePreserved()
    {
        NameNormalizer.Normalize("Cote d'Ivoire").Should().Be("cote d'ivoire");
    }

    // ------------------------------------------------------------------
    // Idempotency — Normalize(Normalize(x)) == Normalize(x)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("São Paulo")]
    [InlineData("Trinidad & Tobago")]
    [InlineData("United   States")]
    [InlineData("  France  ")]
    [InlineData("MÜNCHEN")]
    [InlineData("AT&T")]
    public void Normalize_AppliedTwice_IsIdempotent(string input)
    {
        var once = NameNormalizer.Normalize(input);
        var twice = NameNormalizer.Normalize(once);
        twice.Should().Be(once);
    }

    // ------------------------------------------------------------------
    // Already-normalized input
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("france")]
    [InlineData("united states")]
    [InlineData("trinidad and tobago")]
    [InlineData("at&t")]
    public void Normalize_AlreadyNormalized_ReturnsSameValue(string input)
    {
        NameNormalizer.Normalize(input).Should().Be(input);
    }
}
