// -----------------------------------------------------------------------
// <copyright file="CurrencyLookupTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Geo.Default.Lookups;

using AwesomeAssertions;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Geo.Default;
using Xunit;

/// <summary>
/// Per-VALUE pin coverage for <see cref="CurrencyLookup"/> + the
/// <c>Currencies</c> static accessor. Pins per-currency scalar fields
/// AND the reverse-FK <see cref="Currency.AcceptedInCountries"/> nav
/// wired by the two-pass populate coordinator.
/// </summary>
public sealed class CurrencyLookupTests
{
    [Fact]
    public void ByCode_EveryCurrencyCode_HasMatchingRecord()
    {
        foreach (var code in System.Enum.GetValues<CurrencyCode>())
        {
            CurrencyLookup.ByCode.TryGetValue(code, out var record).Should().BeTrue(
                $"every CurrencyCode member must have a record (missing: {code})");
            record!.Iso4217AlphaCode.Should().Be(code);
        }
    }

    [Fact]
    public void ByCode_CardinalityAtLeast175()
    {
        CurrencyLookup.ByCode.Count.Should().BeGreaterThanOrEqualTo(175);
    }

    // §1.2 category: Domain-specific — per-VALUE pins for major currencies.
    [Fact]
    public void Currency_USD_AllFieldsPinned()
    {
        var usd = Currencies.USD;

        usd.Iso4217AlphaCode.Should().Be(CurrencyCode.USD);
        usd.Iso4217NumericCode.Should().Be("840");
        usd.DisplayName.Should().Be("US Dollar");
        usd.DecimalPlaces.Should().Be(2);
        usd.Symbol.Should().Be("$");
        usd.IsSupported.Should().BeTrue();
    }

    [Fact]
    public void Currency_EUR_AllFieldsPinned()
    {
        var eur = Currencies.EUR;

        eur.Iso4217AlphaCode.Should().Be(CurrencyCode.EUR);
        eur.DecimalPlaces.Should().Be(2);
        eur.Symbol.Should().Be("€");
    }

    [Fact]
    public void Currency_JPY_DecimalPlacesZero()
    {
        // JPY is zero-decimal — pin that boundary.
        Currencies.JPY.DecimalPlaces.Should().Be(0);
        Currencies.JPY.Iso4217AlphaCode.Should().Be(CurrencyCode.JPY);
    }

    [Fact]
    public void Currency_GBP_HeadlineFieldsPinned()
    {
        var gbp = Currencies.GBP;

        gbp.Iso4217AlphaCode.Should().Be(CurrencyCode.GBP);
        gbp.Symbol.Should().Be("£");
        gbp.DecimalPlaces.Should().Be(2);
    }

    // §1.2 category: Cross-field — reverse-FK nav populated post wire-nav.
    [Fact]
    public void Currency_USD_AcceptedInCountries_ContainsUS()
    {
        Currencies.USD.AcceptedInCountryIso31661Alpha2Codes.Should()
            .Contain(CountryCode.US);
        Currencies.USD.AcceptedInCountries.Should()
            .Contain(c => c.Iso31661Alpha2Code == CountryCode.US);
    }

    [Fact]
    public void Currency_EUR_AcceptedInCountries_ContainsFRandDE()
    {
        var eur = Currencies.EUR;
        eur.AcceptedInCountryIso31661Alpha2Codes.Should().Contain(CountryCode.FR);
        eur.AcceptedInCountryIso31661Alpha2Codes.Should().Contain(CountryCode.DE);
    }

    [Fact]
    public void Currency_JPY_AcceptedInCountries_ContainsJP()
    {
        Currencies.JPY.AcceptedInCountryIso31661Alpha2Codes.Should()
            .Contain(CountryCode.JP);
    }

    [Fact]
    public void Currency_EveryRecord_AcceptedInCountriesCountMatchesCodes()
    {
        foreach (var currency in CurrencyLookup.All)
        {
            currency.AcceptedInCountries.Count.Should().Be(
                currency.AcceptedInCountryIso31661Alpha2Codes.Count,
                $"{currency.Iso4217AlphaCode}: AcceptedInCountries list count "
                + $"must equal AcceptedInCountryIso31661Alpha2Codes set count");
        }
    }

    [Fact]
    public void Currency_DisplayName_NeverNull()
    {
        foreach (var currency in CurrencyLookup.All)
        {
            currency.DisplayName.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void Currency_Symbol_NeverNull()
    {
        foreach (var currency in CurrencyLookup.All)
        {
            currency.Symbol.Should().NotBeNull();
        }
    }
}
