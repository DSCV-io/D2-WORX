// -----------------------------------------------------------------------
// <copyright file="StreetAddressTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Location;

using AwesomeAssertions;
using D2.Shared.Location.ValueObjects;
using Xunit;

/// <summary>
/// Adversarial test coverage for <see cref="StreetAddress"/> per §7.1 matrix:
/// 5-line ctor + Line1-required + no-gap rule, two-stage normalization
/// (stored vs hash), Latin diacritic dedup, non-Latin script preservation,
/// emoji / CRLF / NUL / TAB / BiDi-override / ZWJ adversarial inputs,
/// HashId invariants.
/// </summary>
public sealed class StreetAddressTests
{
    // -----------------------------------------------------------------------
    // Happy path
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_AllFiveLines_ReturnsOk_WithCasePreserved()
    {
        var result = StreetAddress.Create("123 Main St", "Apt 4B", "Brooklyn", "NY 11201", "USA");

        result.Success.Should().BeTrue();
        var addr = result.Data!;
        addr.Line1.Should().Be("123 Main St");
        addr.Line2.Should().Be("Apt 4B");
        addr.Line3.Should().Be("Brooklyn");
        addr.Line4.Should().Be("NY 11201");
        addr.Line5.Should().Be("USA");
        addr.HashId.Should().StartWith("v1.");
        addr.HashId.Length.Should().Be(67);
    }

    [Fact]
    public void Create_OnlyLine1_ReturnsOk()
    {
        var result = StreetAddress.Create("Just Line 1");

        result.Success.Should().BeTrue();
        result.Data!.Line1.Should().Be("Just Line 1");
        result.Data!.Line2.Should().BeNull();
        result.Data!.Line3.Should().BeNull();
        result.Data!.Line4.Should().BeNull();
        result.Data!.Line5.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // No-gap rule — Line1 + Line5 with nulls between is valid
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_Line1AndLine5_WithNullsBetween_ReturnsOk_NoGapRule()
    {
        var result = StreetAddress.Create("Line 1", null, null, null, "Line 5");

        result.Success.Should().BeTrue();
        result.Data!.Line1.Should().Be("Line 1");
        result.Data!.Line5.Should().Be("Line 5");
    }

    // -----------------------------------------------------------------------
    // Line1 required
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n\r")]
    public void Create_Line1NullOrWhitespace_ReturnsValidationFailed(string? line1)
    {
        var result = StreetAddress.Create(line1);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be("geo_validation_address_line1_required");
    }

    [Fact]
    public void Create_Line1OnlyPunctuation_ReturnsValidationFailed_AfterStrip()
    {
        // Decorative punctuation strips entirely from stored form.
        var result = StreetAddress.Create("....");
        result.Success.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // Two-stage normalization — stored form
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_DecorativePunctuationInStored_IsStripped_CasePreserved()
    {
        // Periods, commas, semicolons, colons, exclamation, question marks strip from stored form.
        var result = StreetAddress.Create("123 Main St., Apt. 4!");
        result.Success.Should().BeTrue();
        result.Data!.Line1.Should().Be("123 Main St Apt 4");
    }

    [Fact]
    public void Create_SemanticPunctuationInStored_IsPreserved()
    {
        // Hyphens, apostrophes, parentheses, '#', '&' SHOULD survive.
        var result = StreetAddress.Create("O'Connor's #4 & Co (Suite-B)");
        result.Success.Should().BeTrue();
        result.Data!.Line1.Should().Be("O'Connor's #4 & Co (Suite-B)");
    }

    [Fact]
    public void Create_InternalWhitespaceCollapsedInStored()
    {
        var result = StreetAddress.Create("123    Main    St");
        result.Success.Should().BeTrue();
        result.Data!.Line1.Should().Be("123 Main St");
    }

    [Fact]
    public void Create_OuterWhitespaceTrimmedInStored()
    {
        var result = StreetAddress.Create("   123 Main St   ");
        result.Success.Should().BeTrue();
        result.Data!.Line1.Should().Be("123 Main St");
    }

    // -----------------------------------------------------------------------
    // Hash dedup — case
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_DifferentCase_ProducesSameHashId()
    {
        var r1 = StreetAddress.Create("123 Main St");
        var r2 = StreetAddress.Create("123 main st");
        var r3 = StreetAddress.Create("123 MAIN ST");

        r1.Data!.HashId.Should().Be(r2.Data!.HashId);
        r1.Data!.HashId.Should().Be(r3.Data!.HashId);
    }

    // -----------------------------------------------------------------------
    // Hash dedup — Latin diacritics
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_LatinDiacritics_DedupViaNfd_Café_vs_Cafe_SameHashId()
    {
        var r1 = StreetAddress.Create("Café");
        var r2 = StreetAddress.Create("Cafe");

        r1.Data!.HashId.Should().Be(r2.Data!.HashId);
    }

    [Fact]
    public void Create_LatinDiacritics_Zürich_vs_Zurich_SameHashId()
    {
        var r1 = StreetAddress.Create("Zürich");
        var r2 = StreetAddress.Create("Zurich");

        r1.Data!.HashId.Should().Be(r2.Data!.HashId);
    }

    // -----------------------------------------------------------------------
    // HashId invariants
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_HashId_StartsWithV1Prefix()
    {
        var result = StreetAddress.Create("123 Main St");
        result.Data!.HashId.Should().StartWith("v1.");
    }

    [Fact]
    public void Create_HashId_HasCorrectLength()
    {
        var result = StreetAddress.Create("123 Main St");

        // "v1." (3) + 64 hex chars = 67
        result.Data!.HashId.Length.Should().Be(67);
    }

    [Fact]
    public void Create_HashId_IsLowercaseHex()
    {
        var result = StreetAddress.Create("123 Main St");
        var hexPart = result.Data!.HashId[3..];
        hexPart.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Create_HashId_Deterministic_AcrossCalls()
    {
        var r1 = StreetAddress.Create("123 Main St", "Apt 4");
        var r2 = StreetAddress.Create("123 Main St", "Apt 4");

        r1.Data!.HashId.Should().Be(r2.Data!.HashId);
    }

    [Fact]
    public void Create_HashId_AllFiveSlots_Differ_From_FewerSlots()
    {
        var r1 = StreetAddress.Create("Line 1");
        var r2 = StreetAddress.Create("Line 1", "Line 2");

        r1.Data!.HashId.Should().NotBe(r2.Data!.HashId);
    }

    [Fact]
    public void Create_HashId_LinePopulationDistinct_ProducesDistinctHashes()
    {
        // Line5 only with the rest null is a positionally distinct shape vs Line1-only.
        var r1 = StreetAddress.Create("Line 1");
        var r2 = StreetAddress.Create("Line 1", null, null, null, "Line 5");

        r1.Data!.HashId.Should().NotBe(r2.Data!.HashId);
    }

    // -----------------------------------------------------------------------
    // Non-Latin scripts — preserved (Amendment 56)
    // -----------------------------------------------------------------------

    [Fact]
    public void NormalizeForHash_Cyrillic_PreservedAfterUppercase()
    {
        StreetAddress.NormalizeForHash("Москва").Should().Be("МОСКВА");
    }

    [Fact]
    public void NormalizeForHash_CJK_PreservedAsIs_CaselessScript()
    {
        StreetAddress.NormalizeForHash("东京").Should().Be("东京");
    }

    [Fact]
    public void NormalizeForHash_Greek_NfdStripsCombiningMarks_UppercasePreserved()
    {
        StreetAddress.NormalizeForHash("Αθήνα").Should().Be("ΑΘΗΝΑ");
    }

    [Fact]
    public void NormalizeForHash_Arabic_PreservedAsIs_CaselessScript()
    {
        StreetAddress.NormalizeForHash("الرياض").Should().Be("الرياض");
    }

    // -----------------------------------------------------------------------
    // Adversarial — emoji / CRLF / NUL / TAB / BiDi / ZWJ
    // -----------------------------------------------------------------------

    [Fact]
    public void NormalizeForHash_EmojiInInput_Stripped()
    {
        // U+1F4A9 PILE OF POO — surrogate pair — must strip via Rune iteration.
        StreetAddress.NormalizeForHash("💩 Address").Should().Be(" ADDRESS");
    }

    [Fact]
    public void NormalizeForHash_OnlyEmoji_ProducesEmpty()
    {
        StreetAddress.NormalizeForHash("💩🌍🚀").Should().Be(string.Empty);
    }

    [Fact]
    public void NormalizeForHash_AllPunctuation_ProducesEmpty()
    {
        StreetAddress.NormalizeForHash("....,,,;;;:::!!!???").Should().Be(string.Empty);
    }

    [Fact]
    public void NormalizeForHash_CRLF_Stripped()
    {
        // CRLF chars are Control, not Letter/Digit/space → stripped.
        var hashed = StreetAddress.NormalizeForHash("123 Main\r\nINJECTED");
        hashed.Should().NotContain("\r");
        hashed.Should().NotContain("\n");
    }

    [Fact]
    public void NormalizeForHash_NullByte_Stripped()
    {
        var hashed = StreetAddress.NormalizeForHash("123 Main\0INJECTED");
        hashed.Should().NotContain("\0");
    }

    [Fact]
    public void NormalizeForHash_Tab_Stripped()
    {
        var hashed = StreetAddress.NormalizeForHash("123 Main\t\tApt");
        hashed.Should().NotContain("\t");
    }

    [Fact]
    public void NormalizeForHash_ZeroWidthJoiner_Stripped()
    {
        // U+200D ZERO WIDTH JOINER — Format category.
        var hashed = StreetAddress.NormalizeForHash("Main‍St");
        hashed.Should().Be("MAINST");
    }

    [Fact]
    public void NormalizeForHash_BiDiOverride_Stripped()
    {
        // U+202E RIGHT-TO-LEFT OVERRIDE — Format category.
        var hashed = StreetAddress.NormalizeForHash("Hello‮World");
        hashed.Should().Be("HELLOWORLD");
    }

    [Fact]
    public void NormalizeForHash_MixedScriptWithHyphenAndDigits_HyphenStripped()
    {
        // Hyphen is Punctuation, not Letter/Digit/space → stripped.
        var hashed = StreetAddress.NormalizeForHash("123 Москва-Centre");
        hashed.Should().Be("123 МОСКВАCENTRE");
    }

    // -----------------------------------------------------------------------
    // Stored form — CRLF / NUL / TAB collapse to single space or strip
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_CrLfInLine1_CollapsedOrStrippedInStoredForm()
    {
        var result = StreetAddress.Create("123 Main\r\nINJECTED LOG LINE");
        result.Success.Should().BeTrue();
        result.Data!.Line1.Should().NotContain("\r");
        result.Data!.Line1.Should().NotContain("\n");

        // Whitespace + control chars collapse to single space.
        result.Data!.Line1.Should().Be("123 Main INJECTED LOG LINE");
    }

    [Fact]
    public void Create_NullByteInLine1_StrippedFromStoredAndHashForms()
    {
        var result = StreetAddress.Create("123 Main\0INJECTED");
        result.Success.Should().BeTrue();
        result.Data!.Line1.Should().NotContain("\0");
    }

    [Fact]
    public void Create_TabInLine1_CollapsedToSpaceInStoredForm()
    {
        var result = StreetAddress.Create("123 Main\t\tApt");
        result.Success.Should().BeTrue();
        result.Data!.Line1.Should().Be("123 Main Apt");
    }

    // -----------------------------------------------------------------------
    // Boundary — single-char + very-long inputs
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_SingleChar_Ok()
    {
        var result = StreetAddress.Create("A");
        result.Success.Should().BeTrue();
        result.Data!.Line1.Should().Be("A");
    }

    [Fact]
    public void Create_1000CharInput_Ok_NoTimeout()
    {
        var huge = new string('A', 1000);
        var result = StreetAddress.Create(huge);
        result.Success.Should().BeTrue();
        result.Data!.Line1.Length.Should().Be(1000);
    }

    // -----------------------------------------------------------------------
    // NormalizeForHash — single char + oversized
    // -----------------------------------------------------------------------

    [Fact]
    public void NormalizeForHash_SingleLetter_Preserved()
    {
        StreetAddress.NormalizeForHash("a").Should().Be("A");
    }

    [Fact]
    public void NormalizeForHash_1000CharInput_NoTimeout()
    {
        var huge = new string('a', 1000);
        var hashed = StreetAddress.NormalizeForHash(huge);
        hashed.Length.Should().Be(1000);
        hashed.Should().Be(new string('A', 1000));
    }

    [Fact]
    public void NormalizeForHash_TypicalUSAddress_ExpectedOutput()
    {
        StreetAddress.NormalizeForHash("123 Main St").Should().Be("123 MAIN ST");
    }
}
