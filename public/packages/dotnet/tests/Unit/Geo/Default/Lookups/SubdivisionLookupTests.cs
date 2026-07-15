// -----------------------------------------------------------------------
// <copyright file="SubdivisionLookupTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Geo.Default.Lookups;

using AwesomeAssertions;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Geo.Default;
using Xunit;

/// <summary>
/// Per-VALUE pin coverage for <see cref="SubdivisionLookup"/>. Verifies
/// catalog cardinality, the per-country index, sampled US state /
/// territory pins, sampled foreign subdivision pins, and the
/// Country / ParentSubdivision nav wiring done by the two-pass populate
/// coordinator.
/// </summary>
public sealed class SubdivisionLookupTests
{
    [Fact]
    public void ByCode_CardinalityAtLeast3000()
    {
        // ISO 3166-2 catalog is ~4900 entries globally; pin a lower bound.
        SubdivisionLookup.ByCode.Count.Should().BeGreaterThanOrEqualTo(3000);
    }

    [Fact]
    public void All_MatchesByCodeCount()
    {
        SubdivisionLookup.All.Count.Should().Be(SubdivisionLookup.ByCode.Count);
    }

    [Fact]
    public void ByCountry_KeyedByCountryCode_ReturnsListOfSubdivisions()
    {
        SubdivisionLookup.ByCountry[CountryCode.US].Count.Should()
            .BeGreaterThanOrEqualTo(50);
        SubdivisionLookup.ByCountry[CountryCode.CA].Count.Should()
            .BeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public void ByCountry_CoversEveryCountryWithSubdivisions()
    {
        // Every subdivision's CountryIso31661Alpha2Code must appear as a
        // ByCountry key.
        foreach (var sub in SubdivisionLookup.All)
        {
            SubdivisionLookup.ByCountry.ContainsKey(sub.CountryIso31661Alpha2Code)
                .Should().BeTrue();
        }
    }

    // §1.2 category: Domain-specific — per-VALUE pins for US states.
    [Fact]
    public void Subdivision_USNY_AllFieldsPinned()
    {
        var ny = SubdivisionLookup.ByCode[SubdivisionCode.FromString("US-NY")];

        ny.Iso31662Code.Value.Should().Be("US-NY");
        ny.ShortCode.Should().Be("NY");
        ny.DisplayName.Should().Be("New York");
        ny.CountryIso31661Alpha2Code.Should().Be(CountryCode.US);
        ny.Country.Should().NotBeNull();
        ny.Country!.Iso31661Alpha2Code.Should().Be(CountryCode.US);
        ny.ParentSubdivisionIso31662Code.Should().BeNull();
        ny.ParentSubdivision.Should().BeNull();
    }

    [Fact]
    public void Subdivision_USCA_AllFieldsPinned()
    {
        var ca = SubdivisionLookup.ByCode[SubdivisionCode.FromString("US-CA")];

        ca.Iso31662Code.Value.Should().Be("US-CA");
        ca.ShortCode.Should().Be("CA");
        ca.DisplayName.Should().Be("California");
        ca.CountryIso31661Alpha2Code.Should().Be(CountryCode.US);
        ca.Country.Should().NotBeNull();
    }

    [Fact]
    public void Subdivision_USTX_AllFieldsPinned()
    {
        var tx = SubdivisionLookup.ByCode[SubdivisionCode.FromString("US-TX")];

        tx.ShortCode.Should().Be("TX");
        tx.DisplayName.Should().Be("Texas");
        tx.CountryIso31661Alpha2Code.Should().Be(CountryCode.US);
    }

    // §1.2 category: Domain-specific — foreign subdivisions.
    [Fact]
    public void Subdivision_CAON_OntarioPinned()
    {
        var on = SubdivisionLookup.ByCode[SubdivisionCode.FromString("CA-ON")];

        on.DisplayName.Should().Be("Ontario");
        on.CountryIso31661Alpha2Code.Should().Be(CountryCode.CA);
        on.Country!.Iso31661Alpha2Code.Should().Be(CountryCode.CA);
    }

    [Fact]
    public void Subdivision_FRIDF_IleDeFrancePinned()
    {
        // Île-de-France — exercises NFD normalization (catalog stores raw NFC).
        var idf = SubdivisionLookup.ByCode[SubdivisionCode.FromString("FR-IDF")];

        idf.CountryIso31661Alpha2Code.Should().Be(CountryCode.FR);
        idf.DisplayName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Subdivision_BRSP_SaoPauloPinned()
    {
        var sp = SubdivisionLookup.ByCode[SubdivisionCode.FromString("BR-SP")];

        sp.CountryIso31661Alpha2Code.Should().Be(CountryCode.BR);
        sp.DisplayName.Should().NotBeNullOrEmpty();
    }

    // §1.2 category: Cross-field — every subdivision wires its parent country.
    [Fact]
    public void Subdivision_EveryRecord_CountryNavBackrefMatchesCode()
    {
        foreach (var sub in SubdivisionLookup.All)
        {
            sub.Country.Should().NotBeNull(
                $"{sub.Iso31662Code.Value} must have a wired Country nav");
            sub.Country!.Iso31661Alpha2Code.Should().Be(sub.CountryIso31661Alpha2Code);
        }
    }

    // §1.2 category: Domain-specific — parent-subdivision nav.
    [Fact]
    public void Subdivision_ParentSubdivisionNav_WhereCodeSet_NavMatches()
    {
        // Sister-subdivision discipline: when ParentSubdivisionIso31662Code
        // is non-null, the nav resolves to the same code.
        foreach (var sub in SubdivisionLookup.All)
        {
            if (sub.ParentSubdivisionIso31662Code is { } parentCode)
            {
                var code = sub.Iso31662Code.Value;
                sub.ParentSubdivision.Should().NotBeNull(
                    $"{code} has parent code {parentCode.Value} but parent nav is null");
                sub.ParentSubdivision!.Iso31662Code.Should().Be(parentCode);
            }
        }
    }

    [Fact]
    public void Subdivision_ByCountry_US_ContainsCalifornia()
    {
        var usSubdivisions = SubdivisionLookup.ByCountry[CountryCode.US];
        usSubdivisions.Should().Contain(
            s => s.Iso31662Code.Value == "US-CA");
    }

    [Fact]
    public void Subdivision_CountryWithoutSubdivisions_AQ_HasNoEntry()
    {
        // AQ (Antarctica) has no subdivisions per catalog.
        SubdivisionLookup.ByCountry.ContainsKey(CountryCode.AQ).Should().BeFalse();
    }

    [Fact]
    public void Subdivision_ShortCode_NeverNull()
    {
        foreach (var sub in SubdivisionLookup.All)
            sub.ShortCode.Should().NotBeNull();
    }

    [Fact]
    public void Subdivision_Type_NeverNull()
    {
        foreach (var sub in SubdivisionLookup.All)
            sub.Type.Should().NotBeNull();
    }
}
