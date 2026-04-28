// -----------------------------------------------------------------------
// <copyright file="LocationMapperTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Tests.Unit.App.Mappers;

using D2.Geo.App.Mappers;
using D2.Geo.Domain.Entities;
using D2.Geo.Domain.ValueObjects;
using D2.Services.Protos.Geo.V1;
using FluentAssertions;
using Xunit;

/// <summary>
/// Unit tests for <see cref="LocationMapper"/>.
/// </summary>
public class LocationMapperTests
{
    #region ToDTO Tests

    /// <summary>
    /// Tests that ToDTO maps all properties correctly.
    /// </summary>
    [Fact]
    public void ToDTO_WithFullLocation_MapsAllProperties()
    {
        // Arrange
        var location = Location.Create(
            coordinates: Coordinates.Create(34.0522, -118.2437),
            address: StreetAddress.Create("123 Main St", "Suite 100", "Building A"),
            city: "Los Angeles",
            postalCode: "90012",
            subdivisionISO31662Code: "US-CA",
            countryISO31661Alpha2Code: "US");

        // Act
        var dto = location.ToDTO();

        // Assert
        dto.HashId.Should().Be(location.HashId);
        dto.Coordinates.Should().NotBeNull();
        dto.Coordinates!.Latitude.Should().Be(34.0522);
        dto.Coordinates.Longitude.Should().Be(-118.2437);
        dto.Address.Should().NotBeNull();
        dto.Address!.Line1.Should().Be("123 Main St");
        dto.Address.Line2.Should().Be("Suite 100");
        dto.Address.Line3.Should().Be("Building A");
        dto.City.Should().Be("Los Angeles");
        dto.PostalCode.Should().Be("90012");
        dto.SubdivisionIso31662Code.Should().Be("US-CA");
        dto.CountryIso31661Alpha2Code.Should().Be("US");
    }

    /// <summary>
    /// Tests that ToDTO handles null optional properties.
    /// </summary>
    [Fact]
    public void ToDTO_WithMinimalLocation_HandlesNulls()
    {
        // Arrange
        var location = Location.Create();

        // Act
        var dto = location.ToDTO();

        // Assert
        dto.HashId.Should().NotBeNullOrEmpty();
        dto.Coordinates.Should().BeNull();
        dto.Address.Should().BeNull();
        dto.HasCity.Should().BeFalse();
        dto.HasPostalCode.Should().BeFalse();
        dto.HasSubdivisionIso31662Code.Should().BeFalse();
        dto.HasCountryIso31661Alpha2Code.Should().BeFalse();
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
        var dto = new LocationDTO
        {
            HashId = "ignored_hash_gets_recomputed",
            Coordinates = new CoordinatesDTO { Latitude = 34.0522, Longitude = -118.2437 },
            Address = new StreetAddressDTO { Line1 = "123 Main St", Line2 = "Suite 100", Line3 = "Building A" },
            City = "Los Angeles",
            PostalCode = "90012",
            SubdivisionIso31662Code = "US-CA",
            CountryIso31661Alpha2Code = "US",
        };

        // Act
        var domain = dto.ToDomain();

        // Assert
        domain.HashId.Should().HaveLength(64);
        domain.Coordinates.Should().NotBeNull();
        domain.Coordinates!.Latitude.Should().Be(34.0522);
        domain.Coordinates.Longitude.Should().Be(-118.2437);
        domain.Address.Should().NotBeNull();
        domain.Address!.Line1.Should().Be("123 Main St");
        domain.Address.Line2.Should().Be("Suite 100");
        domain.Address.Line3.Should().Be("Building A");
        domain.City.Should().Be("Los Angeles");
        domain.PostalCode.Should().Be("90012");
        domain.SubdivisionISO31662Code.Should().Be("US-CA");
        domain.CountryISO31661Alpha2Code.Should().Be("US");
    }

    /// <summary>
    /// Tests that ToDomain handles empty strings as null.
    /// </summary>
    [Fact]
    public void ToDomain_WithEmptyStrings_CreatesValidLocation()
    {
        // Arrange
        var dto = new LocationDTO
        {
            HashId = string.Empty,
            City = string.Empty,
            PostalCode = string.Empty,
            SubdivisionIso31662Code = string.Empty,
            CountryIso31661Alpha2Code = string.Empty,
        };

        // Act
        var domain = dto.ToDomain();

        // Assert
        domain.HashId.Should().HaveLength(64);
        domain.City.Should().BeNull();
        domain.PostalCode.Should().BeNull();
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
        var original = Location.Create(
            coordinates: Coordinates.Create(34.0522, -118.2437),
            city: "Los Angeles",
            countryISO31661Alpha2Code: "US");

        // Act
        var dto = original.ToDTO();
        var roundTripped = dto.ToDomain();

        // Assert
        roundTripped.HashId.Should().Be(original.HashId);
        roundTripped.Coordinates!.Latitude.Should().Be(original.Coordinates!.Latitude);
        roundTripped.Coordinates.Longitude.Should().Be(original.Coordinates.Longitude);
        roundTripped.City.Should().Be(original.City);
        roundTripped.CountryISO31661Alpha2Code.Should().Be(original.CountryISO31661Alpha2Code);
    }

    #endregion
}
