// -----------------------------------------------------------------------
// <copyright file="CountryLookupTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Geo.Default.Lookups;

using AwesomeAssertions;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Geo.Default;
using Xunit;

/// <summary>
/// Per-VALUE pin coverage for <see cref="CountryLookup"/> + the static
/// <c>Countries</c> accessor wall. Hardens the catalog data wired by
/// the two-pass populate coordinator: every <see cref="CountryCode"/>
/// value resolves to a <see cref="Country"/> whose primary-key field
/// matches; sampled records expose pinned scalar + nav-rep values;
/// uninhabited territories carry <c>null</c> primaries; cross-catalog
/// nav refs resolve coherently.
/// </summary>
public sealed class CountryLookupTests
{
    // §1.2 category: State-lifecycle — every enum member maps to a record.
    [Fact]
    public void ByCode_EveryCountryCode_HasMatchingRecord()
    {
        foreach (var code in System.Enum.GetValues<CountryCode>())
        {
            CountryLookup.ByCode.TryGetValue(code, out var record).Should().BeTrue(
                $"every CountryCode member must have a record (missing: {code})");
            record!.Iso31661Alpha2Code.Should().Be(code);
        }
    }

    [Fact]
    public void ByCode_CardinalityAtLeast249()
    {
        // ISO 3166-1 currently lists 249 codes (officially-assigned + reserved).
        CountryLookup.ByCode.Count.Should().BeGreaterThanOrEqualTo(249);
    }

    [Fact]
    public void All_MatchesByCodeCount()
    {
        CountryLookup.All.Count.Should().Be(CountryLookup.ByCode.Count);
    }

    [Fact]
    public void ByIso31661Alpha2_StringIndexedLookup_ReturnsSameRecord()
    {
        var byCode = CountryLookup.ByCode[CountryCode.US];
        var byStr = CountryLookup.ByIso31661Alpha2["US"];
        byStr.Should().BeSameAs(byCode);
    }

    [Fact]
    public void ByIso31661Alpha3_StringIndexedLookup_ReturnsSameRecord()
    {
        var byCode = CountryLookup.ByCode[CountryCode.US];
        var byStr = CountryLookup.ByIso31661Alpha3["USA"];
        byStr.Should().BeSameAs(byCode);
    }

    // §1.2 category: Domain-specific — per-VALUE pins for headline countries.
    [Fact]
    public void Country_US_AllRequiredFieldsPinned()
    {
        var us = Countries.US;

        us.Iso31661Alpha2Code.Should().Be(CountryCode.US);
        us.Iso31661Alpha3Code.Should().Be("USA");
        us.Iso31661NumericCode.Should().Be("840");
        us.DisplayName.Should().Be("United States");
        us.OfficialName.Should().Be("United States of America");
        us.PhoneNumberPrefix.Should().Be("1");
        us.MeasurementSystem.Should().Be(MeasurementSystem.Imperial);
        us.PrimaryLanguageIso6391Code.Should().Be(LanguageCode.En);
        us.PrimaryCurrencyIso4217AlphaCode.Should().Be(CurrencyCode.USD);
    }

    [Fact]
    public void Country_US_PrimaryLanguageNavWired()
    {
        var us = Countries.US;

        us.PrimaryLanguage.Should().NotBeNull();
        us.PrimaryLanguage!.Iso6391Code.Should().Be(LanguageCode.En);
    }

    [Fact]
    public void Country_US_PrimaryCurrencyNavWired()
    {
        var us = Countries.US;

        us.PrimaryCurrency.Should().NotBeNull();
        us.PrimaryCurrency!.Iso4217AlphaCode.Should().Be(CurrencyCode.USD);
    }

    [Fact]
    public void Country_US_PrimaryLocaleNavWired_EnUS()
    {
        var us = Countries.US;

        us.PrimaryLocale.Should().NotBeNull();
        us.PrimaryLocale!.IetfBcp47Tag.Value.Should().Be("en-US");
    }

    [Fact]
    public void Country_US_TerritoryCodes_ContainsPR()
    {
        // Puerto Rico is a US dependent territory.
        Countries.US.TerritoryIso31661Alpha2Codes.Should().Contain(CountryCode.PR);
    }

    [Fact]
    public void Country_US_Territories_NavWired_ContainsPR()
    {
        // Nav-rep mirrors the code-rep set.
        Countries.US.Territories.Should()
            .Contain(t => t.Iso31661Alpha2Code == CountryCode.PR);
    }

    [Fact]
    public void Country_US_SubdivisionsCount_AtLeast50StatesPlusDC()
    {
        // 50 states + DC + territories — pin lower bound; ISO catalog evolves.
        Countries.US.Subdivisions.Count.Should().BeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public void Country_US_SubdivisionsCount_MatchesByCountryIndex()
    {
        Countries.US.Subdivisions.Count.Should().Be(
            SubdivisionLookup.ByCountry[CountryCode.US].Count);
    }

    [Fact]
    public void Country_JP_AllRequiredFieldsPinned()
    {
        var jp = Countries.JP;

        jp.Iso31661Alpha2Code.Should().Be(CountryCode.JP);
        jp.Iso31661Alpha3Code.Should().Be("JPN");
        jp.PrimaryLanguageIso6391Code.Should().Be(LanguageCode.Ja);
        jp.PrimaryCurrencyIso4217AlphaCode.Should().Be(CurrencyCode.JPY);
        jp.EndonymDisplayName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Country_GB_HeadlineFieldsPinned()
    {
        var gb = Countries.GB;

        gb.Iso31661Alpha2Code.Should().Be(CountryCode.GB);
        gb.Iso31661Alpha3Code.Should().Be("GBR");
        gb.PrimaryCurrencyIso4217AlphaCode.Should().Be(CurrencyCode.GBP);
        gb.PrimaryLanguageIso6391Code.Should().Be(LanguageCode.En);
    }

    // §1.2 category: Domain-specific — uninhabited territories carry null primaries.
    [Fact]
    public void Country_AQ_HasNullPrimaries()
    {
        var aq = Countries.AQ;

        aq.PrimaryLanguageIso6391Code.Should().BeNull();
        aq.PrimaryLanguage.Should().BeNull();
        aq.PrimaryCurrencyIso4217AlphaCode.Should().BeNull();
        aq.PrimaryCurrency.Should().BeNull();
        aq.PrimaryLocaleIetfBcp47Tag.Should().BeNull();
        aq.PrimaryLocale.Should().BeNull();
    }

    [Fact]
    public void Country_BV_HasNullPrimaryLanguageAndLocale()
    {
        // Bouvet Island — uninhabited Norwegian dependency.
        // The spec assigns NOK as a courtesy currency carryover from Norway;
        // language and locale are null because there's no resident population.
        var bv = Countries.BV;

        bv.PrimaryLanguage.Should().BeNull();
        bv.PrimaryLocale.Should().BeNull();
    }

    [Fact]
    public void Country_HM_HasNullPrimaryLanguageAndLocale()
    {
        // Heard Island and McDonald Islands — uninhabited Australian territory.
        // Currency carries over from Australia per the spec; language and
        // locale are null because there's no resident population.
        var hm = Countries.HM;

        hm.PrimaryLanguage.Should().BeNull();
        hm.PrimaryLocale.Should().BeNull();
    }

    [Fact]
    public void Country_AQ_NoSubdivisions()
    {
        Countries.AQ.Subdivisions.Should().BeEmpty();
    }

    // §1.2 category: Cross-field — every inhabited country has a wired language nav.
    [Fact]
    public void Country_EveryInhabitedCountry_HasPrimaryLanguageWiredOrPrimaryCodeMatch()
    {
        // For inhabited countries with PrimaryLanguageIso6391Code set, the nav
        // either wires the matching language OR stays null when the language
        // ISO code isn't in the LanguageCode enum (8 countries use 639-3 codes
        // outside ISO 639-1).
        foreach (var country in CountryLookup.All)
        {
            if (country.PrimaryLanguage is { } lang)
                country.PrimaryLanguageIso6391Code.Should().Be(lang.Iso6391Code);
        }
    }

    [Fact]
    public void Country_EveryCountry_NavAndCodeRepAgreeOnCurrencies()
    {
        foreach (var country in CountryLookup.All)
        {
            var codeSetCount = country.CurrencyIso4217AlphaCodes.Count;
            var navListCount = country.Currencies.Count;
            navListCount.Should().Be(
                codeSetCount,
                $"every country's CurrencyIso4217AlphaCodes set count "
                + $"must equal its Currencies list count ({country.Iso31661Alpha2Code})");
        }
    }

    [Fact]
    public void Country_US_FirstCurrencyAcceptance_USD_DualRepConsistent()
    {
        var us = Countries.US;
        var first = us.Currencies[0];

        first.Iso4217AlphaCode.Should().Be(CurrencyCode.USD);
        first.Currency.Should().NotBeNull();
        first.Currency!.Iso4217AlphaCode.Should().Be(CurrencyCode.USD);
    }

    // §1.2 category: Cross-field — Subdivision nav back-references match country code.
    [Fact]
    public void Country_US_EverySubdivision_CountryNavBackrefMatches()
    {
        foreach (var sub in Countries.US.Subdivisions)
        {
            sub.CountryIso31661Alpha2Code.Should().Be(CountryCode.US);
            sub.Country.Should().NotBeNull();
            sub.Country!.Iso31661Alpha2Code.Should().Be(CountryCode.US);
        }
    }
}
