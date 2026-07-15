// -----------------------------------------------------------------------
// <copyright file="GeopoliticalEntityLookupTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Geo.Default.Lookups;

using AwesomeAssertions;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Geo.Default;
using Xunit;

/// <summary>
/// Per-VALUE pin coverage for <see cref="GeopoliticalEntityLookup"/>
/// + the <c>GeopoliticalEntities</c> static accessor. Pins headline
/// supranational groupings (EU, NATO, OECD, UN) including the M:M
/// <see cref="GeopoliticalEntity.MemberCountries"/> set wired by the
/// two-pass populate coordinator.
/// </summary>
public sealed class GeopoliticalEntityLookupTests
{
    [Fact]
    public void ByCode_EveryGeopoliticalEntityCode_HasMatchingRecord()
    {
        foreach (var code in System.Enum.GetValues<GeopoliticalEntityCode>())
        {
            GeopoliticalEntityLookup.ByCode.TryGetValue(code, out var record).Should()
                .BeTrue(
                    $"every GeopoliticalEntityCode member must have a record (missing: {code})");
            record!.ShortCode.Should().Be(code);
        }
    }

    [Fact]
    public void ByCode_CardinalityAtLeast50()
    {
        GeopoliticalEntityLookup.ByCode.Count.Should().BeGreaterThanOrEqualTo(50);
    }

    // §1.2 category: Domain-specific — per-VALUE pins for headline groupings.
    [Fact]
    public void GeopoliticalEntity_EU_HeadlineFieldsPinned()
    {
        var eu = GeopoliticalEntities.EU;

        eu.ShortCode.Should().Be(GeopoliticalEntityCode.EU);
        eu.DisplayName.Should().NotBeNullOrEmpty();

        // EU continent / continental-Europe member set (per current spec
        // hand-rolled catalog) — pinned lower bound covers core members.
        eu.MemberCountryIso31661Alpha2Codes.Count.Should().BeGreaterThanOrEqualTo(27);
    }

    [Fact]
    public void GeopoliticalEntity_EU_MembersContainFrance()
    {
        var eu = GeopoliticalEntities.EU;

        eu.MemberCountryIso31661Alpha2Codes.Should().Contain(CountryCode.FR);
        eu.MemberCountryIso31661Alpha2Codes.Should().Contain(CountryCode.DE);
    }

    [Fact]
    public void GeopoliticalEntity_EU_MembersNavWired()
    {
        var eu = GeopoliticalEntities.EU;
        eu.MemberCountries.Should().Contain(c => c.Iso31661Alpha2Code == CountryCode.FR);
        eu.MemberCountries.Count.Should().Be(eu.MemberCountryIso31661Alpha2Codes.Count);
    }

    [Fact]
    public void GeopoliticalEntity_NATO_HeadlineFieldsPinned()
    {
        var nato = GeopoliticalEntities.NATO;

        nato.ShortCode.Should().Be(GeopoliticalEntityCode.NATO);
        nato.MemberCountryIso31661Alpha2Codes.Should().Contain(CountryCode.US);
        nato.MemberCountryIso31661Alpha2Codes.Should().Contain(CountryCode.GB);
    }

    [Fact]
    public void GeopoliticalEntity_OECD_ContainsCoreMembers()
    {
        var oecd = GeopoliticalEntities.OECD;

        oecd.ShortCode.Should().Be(GeopoliticalEntityCode.OECD);
        oecd.MemberCountryIso31661Alpha2Codes.Should().Contain(CountryCode.US);
        oecd.MemberCountryIso31661Alpha2Codes.Should().Contain(CountryCode.JP);
    }

    [Fact]
    public void GeopoliticalEntity_UN_FullMemberRoster()
    {
        var un = GeopoliticalEntities.UN;

        un.ShortCode.Should().Be(GeopoliticalEntityCode.UN);

        // UN currently has 193 member states.
        un.MemberCountryIso31661Alpha2Codes.Count.Should().Be(193);
    }

    // §1.2 category: Cross-field — every entity's nav matches code count.
    [Fact]
    public void GeopoliticalEntity_EveryRecord_NavCountMatchesCodeCount()
    {
        foreach (var entity in GeopoliticalEntityLookup.All)
        {
            entity.MemberCountries.Count.Should().Be(
                entity.MemberCountryIso31661Alpha2Codes.Count,
                $"{entity.ShortCode}: MemberCountries list count must equal "
                + $"MemberCountryIso31661Alpha2Codes set count");
        }
    }

    [Fact]
    public void GeopoliticalEntity_DisplayName_NeverEmpty()
    {
        foreach (var entity in GeopoliticalEntityLookup.All)
        {
            entity.DisplayName.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void GeopoliticalEntity_Type_AlwaysValidEnumMember()
    {
        foreach (var entity in GeopoliticalEntityLookup.All)
        {
            System.Enum.IsDefined(entity.Type).Should()
                .BeTrue($"{entity.ShortCode}: Type {entity.Type} not a defined enum member");
        }
    }
}
