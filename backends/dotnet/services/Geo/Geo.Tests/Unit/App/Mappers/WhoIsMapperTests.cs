// -----------------------------------------------------------------------
// <copyright file="WhoIsMapperTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Tests.Unit.App.Mappers;

using D2.Geo.App.Mappers;
using D2.Geo.Domain.Entities;
using D2.Services.Protos.Geo.V1;
using FluentAssertions;
using Xunit;

/// <summary>
/// Unit tests for <see cref="WhoIsMapper"/>.
/// </summary>
public class WhoIsMapperTests
{
    #region ToDTO Tests

    /// <summary>
    /// Tests that ToDTO maps all properties correctly.
    /// </summary>
    [Fact]
    public void ToDTO_WithFullWhoIs_MapsAllProperties()
    {
        // Arrange
        var whoIs = WhoIs.Create(
            ipAddress: "192.168.1.1",
            year: 2025,
            month: 6,
            asn: 852,
            asName: "TELUS Communications",
            asDomain: "telus.com",
            asType: "ISP",
            carrierName: "TELUS",
            mcc: "302",
            mnc: "220",
            asChanged: new DateOnly(2024, 1, 15),
            geoChanged: new DateOnly(2024, 6, 1),
            isAnonymous: false,
            isAnycast: false,
            isHosting: false,
            isMobile: true,
            isSatellite: false,
            isProxy: false,
            isRelay: false,
            isTor: false,
            isVpn: false,
            privacyName: null,
            locationHashId: "abc123");

        // Act
        var dto = whoIs.ToDTO();

        // Assert
        dto.HashId.Should().Be(whoIs.HashId);
        dto.IpAddress.Should().Be("192.168.1.1");
        dto.Year.Should().Be(2025);
        dto.Month.Should().Be(6);
        dto.Asn.Should().Be(852);
        dto.AsName.Should().Be("TELUS Communications");
        dto.AsDomain.Should().Be("telus.com");
        dto.AsType.Should().Be("ISP");
        dto.CarrierName.Should().Be("TELUS");
        dto.Mcc.Should().Be("302");
        dto.Mnc.Should().Be("220");
        dto.AsChanged.Should().NotBeEmpty();
        dto.GeoChanged.Should().NotBeEmpty();
        dto.IsAnonymous.Should().BeFalse();
        dto.IsMobile.Should().BeTrue();
        dto.Location.Should().BeNull(); // No location passed to ToDTO
    }

    /// <summary>
    /// Tests that ToDTO handles null optional properties.
    /// </summary>
    [Fact]
    public void ToDTO_WithMinimalWhoIs_HandlesNulls()
    {
        // Arrange
        var whoIs = WhoIs.Create(ipAddress: "10.0.0.1", year: 2025, month: 1);

        // Act
        var dto = whoIs.ToDTO();

        // Assert
        dto.HashId.Should().NotBeNullOrEmpty();
        dto.IpAddress.Should().Be("10.0.0.1");
        dto.Year.Should().Be(2025);
        dto.Month.Should().Be(1);
        dto.Asn.Should().Be(0);
        dto.HasAsName.Should().BeFalse();
        dto.IsAnonymous.Should().BeFalse(); // null defaults to false in proto
        dto.Location.Should().BeNull(); // No location passed to ToDTO
    }

    #endregion

    #region ToDomain Tests

    /// <summary>
    /// Tests that ToDomain maps all properties correctly.
    /// </summary>
    [Fact]
    public void ToDomain_WithFullDTO_MapsAllProperties()
    {
        // Arrange
        var dto = new WhoIsDTO
        {
            HashId = "ignored_hash",
            IpAddress = "192.168.1.100",
            Year = 2025,
            Month = 7,
            Asn = 12345,
            AsName = "Test ISP",
            AsDomain = "test.com",
            AsType = "Business",
            CarrierName = "Test Carrier",
            Mcc = "310",
            Mnc = "260",
            AsChanged = "2024-03-15",
            GeoChanged = "2024-04-20",
            IsAnonymous = true,
            IsAnycast = false,
            IsHosting = true,
            IsMobile = false,
            IsSatellite = false,
            IsProxy = true,
            IsRelay = false,
            IsTor = false,
            IsVpn = true,
            PrivacyName = "TestVPN",
            Location = new LocationDTO { HashId = "location123" },
        };

        // Act
        var domain = dto.ToDomain();

        // Assert
        domain.HashId.Should().HaveLength(64);
        domain.IPAddress.Should().Be("192.168.1.100");
        domain.Year.Should().Be(2025);
        domain.Month.Should().Be(7);
        domain.ASN.Should().Be(12345);
        domain.ASName.Should().Be("Test ISP");
        domain.ASDomain.Should().Be("test.com");
        domain.ASType.Should().Be("Business");
        domain.CarrierName.Should().Be("Test Carrier");
        domain.MCC.Should().Be("310");
        domain.MNC.Should().Be("260");
        domain.ASChanged.Should().Be(new DateOnly(2024, 3, 15));
        domain.GeoChanged.Should().Be(new DateOnly(2024, 4, 20));
        domain.IsAnonymous.Should().BeTrue();
        domain.IsHosting.Should().BeTrue();
        domain.IsProxy.Should().BeTrue();
        domain.IsVPN.Should().BeTrue();
        domain.PrivacyName.Should().Be("TestVPN");
        domain.LocationHashId.Should().Be("location123");
    }

    /// <summary>
    /// Tests that ToDomain handles empty strings as null.
    /// </summary>
    [Fact]
    public void ToDomain_WithEmptyStrings_TreatsAsNull()
    {
        // Arrange
        var dto = new WhoIsDTO
        {
            IpAddress = "10.0.0.1",
            Year = 2025,
            Month = 1,
            Asn = 0,
            AsName = string.Empty,
            AsDomain = string.Empty,
            AsChanged = string.Empty,
        };

        // Act
        var domain = dto.ToDomain();

        // Assert
        domain.ASN.Should().BeNull();
        domain.ASName.Should().BeNull();
        domain.ASDomain.Should().BeNull();
        domain.ASChanged.Should().BeNull();
        domain.LocationHashId.Should().BeNull();
    }

    /// <summary>
    /// Tests that ToDomain handles invalid date strings gracefully.
    /// </summary>
    [Fact]
    public void ToDomain_WithInvalidDateStrings_TreatsAsNull()
    {
        // Arrange
        var dto = new WhoIsDTO
        {
            IpAddress = "10.0.0.1",
            Year = 2025,
            Month = 1,
            AsChanged = "not-a-date",
            GeoChanged = "invalid",
        };

        // Act
        var domain = dto.ToDomain();

        // Assert
        domain.ASChanged.Should().BeNull();
        domain.GeoChanged.Should().BeNull();
    }

    #endregion

    #region Round-Trip Tests

    /// <summary>
    /// Tests that domain to DTO to domain produces consistent hash.
    /// </summary>
    [Fact]
    public void RoundTrip_DomainToDTOToDomain_ProducesConsistentHash()
    {
        // Arrange
        var original = WhoIs.Create(
            ipAddress: "203.0.113.50",
            year: 2025,
            month: 8,
            asn: 99999,
            asName: "Example ISP");

        // Act
        var dto = original.ToDTO();
        var roundTripped = dto.ToDomain();

        // Assert
        roundTripped.HashId.Should().Be(original.HashId);
        roundTripped.IPAddress.Should().Be(original.IPAddress);
        roundTripped.Year.Should().Be(original.Year);
        roundTripped.Month.Should().Be(original.Month);
    }

    /// <summary>
    /// Tests that IP address normalization is preserved through round-trip.
    /// </summary>
    [Fact]
    public void RoundTrip_WithMixedCaseIP_NormalizesToLowercase()
    {
        // Arrange - IPv6 with mixed case
        var original = WhoIs.Create(
            ipAddress: "2001:0DB8:85A3:0000:0000:8A2E:0370:7334",
            year: 2025,
            month: 1);

        // Act
        var dto = original.ToDTO();
        var roundTripped = dto.ToDomain();

        // Assert
        roundTripped.IPAddress.Should().Be(original.IPAddress);
        roundTripped.IPAddress.Should().Be(roundTripped.IPAddress.ToLowerInvariant());
    }

    #endregion

    #region Hash Consistency Tests

    /// <summary>
    /// Tests that same content produces same hash across mappings.
    /// </summary>
    [Fact]
    public void Hash_SameContent_ProducesSameHash()
    {
        // Arrange
        var dto1 = new WhoIsDTO
        {
            IpAddress = "192.168.1.1",
            Year = 2025,
            Month = 6,
        };

        var dto2 = new WhoIsDTO
        {
            IpAddress = "192.168.1.1",
            Year = 2025,
            Month = 6,
        };

        // Act
        var domain1 = dto1.ToDomain();
        var domain2 = dto2.ToDomain();

        // Assert
        domain1.HashId.Should().Be(domain2.HashId);
    }

    #endregion
}
