// -----------------------------------------------------------------------
// <copyright file="StringExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Utilities.Extensions;

using AwesomeAssertions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Location.ValueObjects;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using Xunit;

public sealed class StringExtensionsTests
{
    // ----------------------------------------------------------------------
    // Truthy / Falsey
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("   \r\n  \t  ")]
    public void Falsey_OnNullEmptyOrWhitespaceOnly_IsTrue(string? input)
    {
        input.Falsey().Should().BeTrue();
        input.Truthy().Should().BeFalse();
    }

    [Theory]
    [InlineData("x")]
    [InlineData("hello")]
    [InlineData("  padded  ")]
    [InlineData(" a ")]
    public void Truthy_OnAnyNonWhitespace_IsTrue(string input)
    {
        input.Truthy().Should().BeTrue();
        input.Falsey().Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // ToNullIfEmpty
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void ToNullIfEmpty_OnFalsey_ReturnsNull(string? input)
    {
        input.ToNullIfEmpty().Should().BeNull();
    }

    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("  hello  ", "hello")]
    [InlineData("\thello\n", "hello")]
    public void ToNullIfEmpty_OnTruthy_ReturnsTrimmed(string input, string expected)
    {
        input.ToNullIfEmpty().Should().Be(expected);
    }

    // ----------------------------------------------------------------------
    // CleanStr
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void CleanStr_OnFalsey_ReturnsNull(string? input)
    {
        input.CleanStr().Should().BeNull();
    }

    [Theory]
    [InlineData("hello world", "hello world")]
    [InlineData("  hello   world  ", "hello world")]
    [InlineData("a\t\nb", "a b")]
    [InlineData("a   b   c", "a b c")]
    [InlineData("single", "single")]
    public void CleanStr_OnTruthy_TrimAndCollapseInternalWhitespace(string input, string expected)
    {
        input.CleanStr().Should().Be(expected);
    }

    // ----------------------------------------------------------------------
    // CleanDisplayStr
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CleanDisplayStr_OnFalsey_ReturnsNull(string? input)
    {
        input.CleanDisplayStr().Should().BeNull();
    }

    [Theory]
    [InlineData("John Doe", "John Doe")]
    [InlineData("  John   Doe  ", "John Doe")]
    [InlineData("Mary-Jane O'Neil, Jr.", "Mary-Jane O'Neil, Jr.")]
    [InlineData("Иван Петров", "Иван Петров")] // Cyrillic
    [InlineData("日本語名前", "日本語名前")] // CJK
    [InlineData("José Núñez", "José Núñez")] // Latin diacritics
    public void CleanDisplayStr_PreservesAllowedCharacters(string input, string expected)
    {
        input.CleanDisplayStr().Should().Be(expected);
    }

    [Theory]
    [InlineData("<script>x</script>John", "scriptxscriptJohn")]
    [InlineData("(John) [Doe] {Esq}", "John Doe Esq")]
    [InlineData("John\"Doe\"", "JohnDoe")]
    [InlineData("John`Doe`", "JohnDoe")]
    [InlineData("John+Doe=1", "JohnDoe1")]
    [InlineData("John|Doe", "JohnDoe")]
    [InlineData("John\\Doe", "JohnDoe")]
    public void CleanDisplayStr_StripsDisallowedCharacters(string input, string expected)
    {
        input.CleanDisplayStr().Should().Be(expected);
    }

    [Fact]
    public void CleanDisplayStr_OnAllInvalid_ReturnsNull()
    {
        // After stripping all chars, only empty remains → CleanStr returns null.
        "@@@***".CleanDisplayStr().Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // TryParseEmail — returns D2Result<string>
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("user@example.com", "user@example.com")]
    [InlineData("USER@EXAMPLE.COM", "user@example.com")]
    [InlineData("  user@example.com  ", "user@example.com")]
    [InlineData("First.Last+tag@sub.example.co.uk", "first.last+tag@sub.example.co.uk")]
    public void TryParseEmail_OnValid_LowercasesAndReturnsOk(
        string input,
        string expected)
    {
        var result = input.TryParseEmail();

        result.Success.Should().BeTrue();
        result.Data.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("noatsign")]
    [InlineData("no@dot")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("user@.com")]
    [InlineData("user@example.")]
    [InlineData("user@@example.com")]
    [InlineData("us er@example.com")]
    public void TryParseEmail_OnInvalid_ReturnsValidationFailedWithEmailInvalidKey(string? input)
    {
        var result = input.TryParseEmail();

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.VALIDATION_FAILED);
        result.Messages.Should().Equal(TK.Common.Validation.EMAIL_INVALID);
    }

    // ----------------------------------------------------------------------
    // TryParsePhoneNumber — returns D2Result<string>
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("5551234", "5551234")] // exactly 7
    [InlineData("555-123-4567", "5551234567")]
    [InlineData("(555) 123-4567", "5551234567")]
    [InlineData("+44 20 7946 0958", "442079460958")]
    [InlineData("123456789012345", "123456789012345")] // exactly 15
    public void TryParsePhoneNumber_OnValid_StripsNonDigitsAndReturnsOk(
        string input,
        string expected)
    {
        var result = input.TryParsePhoneNumber();

        result.Success.Should().BeTrue();
        result.Data.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParsePhoneNumber_OnFalsey_ReturnsValidationFailedWithPhoneInvalidKey(
        string? input)
    {
        var result = input.TryParsePhoneNumber();

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.VALIDATION_FAILED);
        result.Messages.Should().Equal(TK.Common.Validation.PHONE_INVALID);
    }

    [Fact]
    public void TryParsePhoneNumber_OnNoDigits_ReturnsValidationFailed()
    {
        var result = "abc-def".TryParsePhoneNumber();

        result.Success.Should().BeFalse();
        result.Messages.Should().Equal(TK.Common.Validation.PHONE_INVALID);
    }

    [Theory]
    [InlineData("123456")] // 6 digits
    [InlineData("1")]
    [InlineData("1234567890123456")] // 16 digits
    public void TryParsePhoneNumber_OnLengthOutOfBounds_ReturnsValidationFailed(string input)
    {
        var result = input.TryParsePhoneNumber();

        result.Success.Should().BeFalse();
        result.Messages.Should().Equal(TK.Common.Validation.PHONE_INVALID);
    }

    // ----------------------------------------------------------------------
    // GetNormalizedStrForHashing
    // ----------------------------------------------------------------------

    [Fact]
    public void GetNormalizedStrForHashing_PreservesPositionsForFalseyParts()
    {
        // Falsey segments collapse to empty, so the pipe positions remain stable
        // — important when caller builds composite hash keys like
        // "city|region|country" where any field may be missing.
        string?[] parts = [" Test One ", "   ", "TEST3"];

        parts.GetNormalizedStrForHashing().Should().Be("test one||test3");
    }

    [Fact]
    public void GetNormalizedStrForHashing_OnEmptyArray_ReturnsEmpty()
    {
        string?[] parts = [];

        parts.GetNormalizedStrForHashing().Should().BeEmpty();
    }

    [Fact]
    public void GetNormalizedStrForHashing_OnAllFalsey_ReturnsPipesOnly()
    {
        string?[] parts = [null, string.Empty, "  "];

        parts.GetNormalizedStrForHashing().Should().Be("||");
    }

    [Fact]
    public void GetNormalizedStrForHashing_LowercasesAndCleansEachPart()
    {
        string?[] parts = ["  HELLO  ", "World"];

        parts.GetNormalizedStrForHashing().Should().Be("hello|world");
    }

    // ----------------------------------------------------------------------
    // NormalizeForHash
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("   \r\n  \t  ")]
    public void NormalizeForHash_OnFalsey_ReturnsEmpty(string? input)
    {
        input.NormalizeForHash().Should().BeEmpty();
    }

    [Theory]
    [InlineData(".,;:!?")]
    [InlineData("@@@***")]
    [InlineData("💥🔥")]
    [InlineData("()[]{}")]
    [InlineData("&^%$#~")]
    public void NormalizeForHash_OnNoLettersOrDigits_ReturnsEmpty(string input)
    {
        // Punctuation/symbols/emoji carry no Letter or Decimal-digit code
        // points → everything stripped → empty result.
        input.NormalizeForHash().Should().BeEmpty();
    }

    // Diacritic collapse, case-fold, multi-script — NFC inputs collapse to same.
    [Theory]
    [InlineData("Café", "CAFE")]
    [InlineData("Cafe", "CAFE")]
    [InlineData("café", "CAFE")]
    [InlineData("Zürich", "ZURICH")]
    [InlineData("naïve", "NAIVE")]
    [InlineData("JOSÉ", "JOSE")]
    [InlineData("josé", "JOSE")]
    [InlineData("José", "JOSE")]
    [InlineData("Иван", "ИВАН")] // Cyrillic — caseless upper
    [InlineData("日本語", "日本語")] // CJK — caseless, kept as-is
    [InlineData("Ελλάδα", "ΕΛΛΑΔΑ")] // Greek — diacritics stripped + uppercased
    [InlineData("مرحبا", "مرحبا")] // Arabic — caseless, kept as-is
    [InlineData("O'Neil-Jr.", "ONEILJR")] // punctuation dropped mid-word
    [InlineData("a&b", "AB")]
    public void NormalizeForHash_UnicodeNormalization_MatchesExpected(
        string input,
        string expected)
    {
        input.NormalizeForHash().Should().Be(expected);
    }

    // Decimal-digit \p{Nd} kept regardless of script; Arabic-Indic '٣' (U+0663) is
    // Rune.IsDigit true.
    [Theory]
    [InlineData("٣", "٣")] // Arabic-Indic digit kept
    [InlineData("123", "123")] // ASCII digits kept
    [InlineData("٣abc", "٣ABC")] // Arabic-Indic + ASCII mixed
    public void NormalizeForHash_DecimalDigitsFromAnyScript_Kept(
        string input,
        string expected)
    {
        input.NormalizeForHash().Should().Be(expected);
    }

    [Fact]
    public void NormalizeForHash_InternalSingleSpace_Preserved()
    {
        // Stage-2 does NOT collapse whitespace; stage-1 is the caller's
        // responsibility. Single internal space passes through as ASCII space.
        "123 Main St".NormalizeForHash().Should().Be("123 MAIN ST");
    }

    [Fact]
    public void NormalizeForHash_MultipleInternalSpaces_NotCollapsed()
    {
        // Pins the stage-2-only contract: multi-space runs survive unchanged.
        // This is intentional — stage-1 collapse is the CALLER's concern.
        "123  Main  St".NormalizeForHash().Should().Be("123  MAIN  ST");
    }

    [Fact]
    public void NormalizeForHash_LeadingAndTrailingSpace_Survives()
    {
        " a ".NormalizeForHash().Should().Be(" A ");
    }

    [Fact]
    public void NormalizeForHash_SupplementaryPlaneCjkLetter_Kept()
    {
        // U+20000 (𠀀) — supplementary-plane CJK letter, requires two UTF-16
        // surrogates. Rune.IsLetter must return true and the pair must NOT be
        // split into two replacement chars (char.IsLetter is surrogate-unsafe).
        var cjk = "\U00020000"; // 𠀀
        var result = cjk.NormalizeForHash();
        result.Should().Be(cjk);
    }

    [Fact]
    public void NormalizeForHash_SupplementaryPlaneEmoji_DroppedWithoutSplitting()
    {
        // 💥 (U+1F4A5) is a supplementary-plane Symbol → dropped.
        // The Rune iteration must not produce two U+FFFD replacement chars —
        // the result must be completely empty (not two garbage chars).
        "💥".NormalizeForHash().Should().BeEmpty();
    }

    // Non-Letter/Digit/ASCII-space chars are stripped — BiDi overrides, zero-width
    // joiners, and non-ASCII spaces (NBSP U+00A0) are all in this class.
    [Theory]
    [InlineData("‮evil", "EVIL")] // BiDi override U+202E
    [InlineData("a‍b", "AB")] // zero-width joiner U+200D
    [InlineData("x y", "XY")] // non-breaking space U+00A0 — not ASCII space
    public void NormalizeForHash_StripsNonLetterNonDigitNonAsciiSpace(
        string input,
        string expected)
    {
        input.NormalizeForHash().Should().Be(expected);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("Café")]
    [InlineData("123 Main St")]
    [InlineData("O'Neil-Jr.")]
    [InlineData("Иван")]
    public void NormalizeForHash_IsIdempotent(string input)
    {
        var once = input.NormalizeForHash();
        var twice = once.NormalizeForHash();
        twice.Should().Be(once);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("Café")]
    [InlineData("123 Main St")]
    public void NormalizeForHash_IsDeterministic(string input)
    {
        var first = input.NormalizeForHash();
        var second = input.NormalizeForHash();
        second.Should().Be(first);
    }

    [Fact]
    public void NormalizeForHash_OnLongInput_DoesNotThrow()
    {
        // Robustness smoke: 10 000-char string — linear algorithm, no regex.
        var longInput = new string('a', 5_000) + new string('é', 5_000);
        var act = () => longInput.NormalizeForHash();
        act.Should().NotThrow();
    }

    // Byte-identical to Location's internal StreetAddress.NormalizeForHash; this parity
    // test guards that identity so any algorithm divergence fails here immediately.
    [Theory]
    [InlineData("123 Main St")]
    [InlineData("123 Main St.")]
    [InlineData("123  Main  St")]
    [InlineData("Café")]
    [InlineData("Cafe")]
    [InlineData("Zürich")]
    [InlineData("JOSÉ")]
    [InlineData("O'Neil")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Иван")] // Cyrillic
    [InlineData("日本語")] // CJK
    [InlineData("💥")] // Emoji (supplementary-plane symbol)
    [InlineData("a b")] // thin space U+2009 — non-ASCII space, dropped → "AB"
    [InlineData("a　b")] // ideographic space U+3000 — non-ASCII space, dropped → "AB"
    [InlineData("𠀀")] // Supplementary-plane CJK letter (U+20000)
    public void NormalizeForHash_ByteIdenticalToStreetAddressNormalizeForHash(string? input)
    {
        // Utilities extension must produce the exact same output as
        // Location's internal algorithm.
        input.NormalizeForHash().Should().Be(StreetAddress.NormalizeForHash(input));
    }
}
