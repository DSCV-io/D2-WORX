// -----------------------------------------------------------------------
// <copyright file="LocaleLookupTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Geo.Default.Lookups;

using AwesomeAssertions;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Geo.Default;
using Xunit;

/// <summary>
/// Per-VALUE pin coverage for <see cref="LocaleLookup"/> and the
/// nested <c>Locales</c> accessor hierarchy. Pins headline locales
/// (<c>en-US</c>, <c>en-GB</c>, <c>fr-FR</c>, <c>pt-BR</c>) including
/// the Language + Country nav refs wired by the two-pass populate
/// coordinator.
/// </summary>
public sealed class LocaleLookupTests
{
    [Fact]
    public void ByCode_CardinalityAtLeast700()
    {
        // CLDR locale catalog runs into the hundreds; lower-bound assertion.
        LocaleLookup.ByCode.Count.Should().BeGreaterThanOrEqualTo(700);
    }

    [Fact]
    public void All_MatchesByCodeCount()
    {
        LocaleLookup.All.Count.Should().Be(LocaleLookup.ByCode.Count);
    }

    // §1.2 category: Domain-specific — headline locale pins.
    [Fact]
    public void Locale_EnUS_AllFieldsPinned()
    {
        var enUS = LocaleLookup.ByCode[LocaleCode.FromString("en-US")];

        enUS.IetfBcp47Tag.Value.Should().Be("en-US");
        enUS.DisplayName.Should().NotBeNullOrEmpty();
        enUS.LanguageIso6391Code.Should().Be(LanguageCode.En);
        enUS.CountryIso31661Alpha2Code.Should().Be(CountryCode.US);
        enUS.Language.Should().NotBeNull();
        enUS.Language!.Iso6391Code.Should().Be(LanguageCode.En);
        enUS.Country.Should().NotBeNull();
        enUS.Country!.Iso31661Alpha2Code.Should().Be(CountryCode.US);
        enUS.DecimalSeparator.Should().Be(".");
        enUS.ThousandsSeparator.Should().Be(",");
        enUS.IsSelectable.Should().BeTrue();
    }

    [Fact]
    public void Locale_EnGB_NavWired()
    {
        var enGB = LocaleLookup.ByCode[LocaleCode.FromString("en-GB")];

        enGB.LanguageIso6391Code.Should().Be(LanguageCode.En);
        enGB.CountryIso31661Alpha2Code.Should().Be(CountryCode.GB);
        enGB.Country!.Iso31661Alpha2Code.Should().Be(CountryCode.GB);
    }

    [Fact]
    public void Locale_FrFR_NavWired()
    {
        var frFR = LocaleLookup.ByCode[LocaleCode.FromString("fr-FR")];

        frFR.LanguageIso6391Code.Should().Be(LanguageCode.Fr);
        frFR.CountryIso31661Alpha2Code.Should().Be(CountryCode.FR);
        frFR.Language!.Iso6391Code.Should().Be(LanguageCode.Fr);
        frFR.Country!.Iso31661Alpha2Code.Should().Be(CountryCode.FR);
    }

    [Fact]
    public void Locale_PtBR_NavWired()
    {
        var ptBR = LocaleLookup.ByCode[LocaleCode.FromString("pt-BR")];

        ptBR.LanguageIso6391Code.Should().Be(LanguageCode.Pt);
        ptBR.CountryIso31661Alpha2Code.Should().Be(CountryCode.BR);
    }

    // §1.2 category: Cross-field — code rep + nav rep must agree.
    [Fact]
    public void Locale_EveryRecord_LanguageNavMatchesCode()
    {
        foreach (var locale in LocaleLookup.All)
        {
            if (locale.Language is { } lang)
            {
                locale.LanguageIso6391Code.Should().Be(
                    lang.Iso6391Code,
                    $"{locale.IetfBcp47Tag.Value}: Language nav code rep mismatch");
            }
        }
    }

    [Fact]
    public void Locale_EveryRecord_CountryNavMatchesCode()
    {
        foreach (var locale in LocaleLookup.All)
        {
            if (locale.Country is { } country)
            {
                locale.CountryIso31661Alpha2Code.Should().Be(
                    country.Iso31661Alpha2Code,
                    $"{locale.IetfBcp47Tag.Value}: Country nav code rep mismatch");
            }
        }
    }

    [Fact]
    public void Locale_EnUS_FirstDayOfWeek_Sunday()
    {
        var enUS = LocaleLookup.ByCode[LocaleCode.FromString("en-US")];
        enUS.FirstDayOfWeek.Should().Be(GeoDayOfWeek.Sunday);
    }

    [Fact]
    public void Locale_DisplayName_NeverEmpty()
    {
        foreach (var locale in LocaleLookup.All)
        {
            locale.DisplayName.Should().NotBeNullOrEmpty();
        }
    }
}
