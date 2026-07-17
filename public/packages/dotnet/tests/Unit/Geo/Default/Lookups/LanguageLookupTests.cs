// -----------------------------------------------------------------------
// <copyright file="LanguageLookupTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Geo.Default.Lookups;

using AwesomeAssertions;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Geo.Default;
using Xunit;

/// <summary>
/// Per-VALUE pin coverage for <see cref="LanguageLookup"/> + the
/// <c>Languages</c> static accessor. Pins per-language scalar fields,
/// writing-direction discipline, and the reverse-FK
/// <see cref="Language.SpokenInCountries"/> nav wired in the two-pass
/// populate coordinator.
/// </summary>
public sealed class LanguageLookupTests
{
    [Fact]
    public void ByCode_EveryLanguageCode_HasMatchingRecord()
    {
        foreach (var code in System.Enum.GetValues<LanguageCode>())
        {
            LanguageLookup.ByCode.TryGetValue(code, out var record).Should().BeTrue(
                $"every LanguageCode member must have a record (missing: {code})");
            record!.Iso6391Code.Should().Be(code);
        }
    }

    [Fact]
    public void ByCode_CardinalityAtLeast175()
    {
        LanguageLookup.ByCode.Count.Should().BeGreaterThanOrEqualTo(175);
    }

    [Fact]
    public void ByIso6391_StringIndexedLookup_ReturnsSameRecord()
    {
        var byCode = LanguageLookup.ByCode[LanguageCode.En];
        var byStr = LanguageLookup.ByIso6391["en"];
        byStr.Should().BeSameAs(byCode);
    }

    // §1.2 category: Domain-specific — per-VALUE pins for major languages.
    [Fact]
    public void Language_En_AllFieldsPinned()
    {
        var en = Languages.En;

        en.Iso6391Code.Should().Be(LanguageCode.En);
        en.DisplayName.Should().Be("English");
        en.WritingDirection.Should().Be(WritingDirection.LTR);
        en.IsSupported.Should().BeTrue();
    }

    [Fact]
    public void Language_Es_HeadlineFieldsPinned()
    {
        var es = Languages.Es;
        es.Iso6391Code.Should().Be(LanguageCode.Es);
        es.DisplayName.Should().Contain("Spanish");
        es.WritingDirection.Should().Be(WritingDirection.LTR);
    }

    [Fact]
    public void Language_Fr_HeadlineFieldsPinned()
    {
        var fr = Languages.Fr;
        fr.Iso6391Code.Should().Be(LanguageCode.Fr);
        fr.DisplayName.Should().Be("French");
    }

    [Fact]
    public void Language_Ja_HeadlineFieldsPinned()
    {
        var ja = Languages.Ja;
        ja.Iso6391Code.Should().Be(LanguageCode.Ja);
        ja.WritingDirection.Should().Be(WritingDirection.LTR);
    }

    // §1.2 category: Domain-specific — RTL writing direction sample.
    [Fact]
    public void Language_Ar_WritingDirectionRTL()
    {
        var ar = Languages.Ar;
        ar.Iso6391Code.Should().Be(LanguageCode.Ar);
        ar.WritingDirection.Should().Be(WritingDirection.RTL);
    }

    [Fact]
    public void Language_He_WritingDirectionRTL()
    {
        var he = Languages.He;
        he.WritingDirection.Should().Be(WritingDirection.RTL);
    }

    // §1.2 category: Cross-field — reverse-FK nav populated.
    [Fact]
    public void Language_En_SpokenInCountries_ContainsGB_US_AU()
    {
        var en = Languages.En;
        en.SpokenInCountryIso31661Alpha2Codes.Should().Contain(CountryCode.GB);
        en.SpokenInCountryIso31661Alpha2Codes.Should().Contain(CountryCode.US);
        en.SpokenInCountryIso31661Alpha2Codes.Should().Contain(CountryCode.AU);
        en.SpokenInCountries.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Language_Es_SpokenInCountries_ContainsESandMX()
    {
        var es = Languages.Es;
        es.SpokenInCountryIso31661Alpha2Codes.Should().Contain(CountryCode.ES);
        es.SpokenInCountryIso31661Alpha2Codes.Should().Contain(CountryCode.MX);
    }

    [Fact]
    public void Language_EveryRecord_NavMatchesCodeCount()
    {
        foreach (var lang in LanguageLookup.All)
        {
            lang.SpokenInCountries.Count.Should().Be(
                lang.SpokenInCountryIso31661Alpha2Codes.Count,
                $"{lang.Iso6391Code}: SpokenInCountries list count must equal "
                + $"SpokenInCountryIso31661Alpha2Codes set count");
        }
    }

    [Fact]
    public void Language_EveryRecord_DisplayName_NeverEmpty()
    {
        foreach (var lang in LanguageLookup.All)
        {
            lang.DisplayName.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void Language_EveryRecord_Endonym_NeverNull()
    {
        foreach (var lang in LanguageLookup.All)
        {
            lang.Endonym.Should().NotBeNull();
        }
    }
}
