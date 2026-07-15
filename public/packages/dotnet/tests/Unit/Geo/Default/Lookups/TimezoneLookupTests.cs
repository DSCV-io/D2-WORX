// -----------------------------------------------------------------------
// <copyright file="TimezoneLookupTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Geo.Default.Lookups;

using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Geo.Default;
using Xunit;

/// <summary>
/// Per-VALUE pin coverage for <see cref="TimezoneLookup"/>. Pins
/// headline IANA zones (<c>America/New_York</c>, <c>Europe/London</c>,
/// <c>Asia/Tokyo</c>) including offset metadata and the
/// <see cref="Timezone.PrimaryCountry"/> + <see cref="Timezone.CoApplicableCountries"/>
/// nav refs wired by the two-pass populate coordinator.
/// </summary>
public sealed class TimezoneLookupTests
{
    [Fact]
    public void ByCode_CardinalityAtLeast250()
    {
        // IANA tzdb has ~450 zones plus aliases; the catalog ships the
        // distinct primary set (~300) — pin a lower bound that captures
        // the meaningful population without churning on tzdb evolution.
        TimezoneLookup.ByCode.Count.Should().BeGreaterThanOrEqualTo(250);
    }

    [Fact]
    public void All_MatchesByCodeCount()
    {
        TimezoneLookup.All.Count.Should().Be(TimezoneLookup.ByCode.Count);
    }

    // §1.2 category: Domain-specific — headline IANA zone pins.
    [Fact]
    public void Timezone_AmericaNewYork_AllFieldsPinned()
    {
        var ny = TimezoneLookup.ByCode[TimezoneCode.FromString("America/New_York")];

        ny.IanaName.Value.Should().Be("America/New_York");
        ny.CurrentStdOffsetMinutes.Should().Be(-300);
        ny.CurrentStdAbbrev.Should().Be("EST");
        ny.PrimaryCountryIso31661Alpha2Code.Should().Be(CountryCode.US);
        ny.PrimaryCountry.Should().NotBeNull();
        ny.PrimaryCountry!.Iso31661Alpha2Code.Should().Be(CountryCode.US);
    }

    [Fact]
    public void Timezone_EuropeLondon_NavWired()
    {
        var london = TimezoneLookup.ByCode[TimezoneCode.FromString("Europe/London")];

        london.PrimaryCountryIso31661Alpha2Code.Should().Be(CountryCode.GB);
        london.PrimaryCountry!.Iso31661Alpha2Code.Should().Be(CountryCode.GB);
    }

    [Fact]
    public void Timezone_AsiaTokyo_NavWired_NoDstOffset()
    {
        var tokyo = TimezoneLookup.ByCode[TimezoneCode.FromString("Asia/Tokyo")];

        tokyo.PrimaryCountryIso31661Alpha2Code.Should().Be(CountryCode.JP);
        tokyo.PrimaryCountry!.Iso31661Alpha2Code.Should().Be(CountryCode.JP);
        tokyo.CurrentStdOffsetMinutes.Should().Be(540);

        // Japan does not observe DST.
        tokyo.CurrentDstOffsetMinutes.Should().BeNull();
        tokyo.CurrentDstAbbrev.Should().BeNull();
    }

    // §1.2 category: Domain-specific — shared zones populate CoApplicable nav.
    [Fact]
    public void Timezone_EveryRecord_CoApplicableNavCountMatchesCodeCount_HasAtLeastOneSharedZone()
    {
        // Shared-zone scenario — at least one IANA zone in the catalog
        // has a non-empty CoApplicableCountryIso31661Alpha2Codes set
        // (Europe shares the same zone across Vatican / San Marino / etc.).
        var sharedZones = TimezoneLookup.All
            .Where(tz => tz.CoApplicableCountryIso31661Alpha2Codes.Count > 0);
        sharedZones.Should().NotBeEmpty(
            "the catalog should carry at least one shared IANA zone");
    }

    [Fact]
    public void Timezone_EveryRecord_PrimaryCountryNavMatchesCode()
    {
        foreach (var tz in TimezoneLookup.All)
        {
            if (tz.PrimaryCountry is { } country)
            {
                tz.PrimaryCountryIso31661Alpha2Code.Should().Be(
                    country.Iso31661Alpha2Code,
                    $"{tz.IanaName.Value}: PrimaryCountry nav code rep mismatch");
            }
        }
    }

    [Fact]
    public void Timezone_EveryRecord_CoApplicableNavCountMatchesCodeCount()
    {
        foreach (var tz in TimezoneLookup.All)
        {
            tz.CoApplicableCountries.Count.Should().Be(
                tz.CoApplicableCountryIso31661Alpha2Codes.Count,
                $"{tz.IanaName.Value}: CoApplicableCountries list count "
                + $"must equal CoApplicableCountryIso31661Alpha2Codes set count");
        }
    }

    [Fact]
    public void Timezone_DisplayName_NeverEmpty()
    {
        foreach (var tz in TimezoneLookup.All)
        {
            tz.DisplayName.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void Timezone_Aliases_NeverNull()
    {
        foreach (var tz in TimezoneLookup.All)
        {
            tz.Aliases.Should().NotBeNull();
        }
    }

    [Fact]
    public void Timezone_LocalizedDisplayNames_IsPresent()
    {
        // The localizedDisplayNames dictionary is per-zone optional in the
        // current catalog; pin only that the field exists (non-null
        // dictionary reference) for every record.
        foreach (var tz in TimezoneLookup.All)
        {
            tz.LocalizedDisplayNames.Should().NotBeNull(
                $"{tz.IanaName.Value}: localizedDisplayNames dictionary must not be null");
        }
    }
}
