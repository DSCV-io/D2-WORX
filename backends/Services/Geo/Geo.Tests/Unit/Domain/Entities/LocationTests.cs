using FluentAssertions;
using Geo.Domain.Entities;
using Geo.Domain.ValueObjects;
using Xunit;

namespace Geo.Tests.Unit.Domain.Entities;

/// <summary>
/// Unit tests for the <see cref="Geo.Domain.Entities.Location"/> entity.
/// </summary>
public class LocationTests
{
    #region Valid Creation - Minimal

    [Fact]
    public void Create_WithNoParameters_Success()
    {
        // Act
        var location = Location.Create();

        // Assert
        location.Should().NotBeNull();
        location.HashId.Should().NotBeNull();
        location.HashId.Should().HaveCount(32); // SHA-256 = 32 bytes
        location.Coordinates.Should().BeNull();
        location.Address.Should().BeNull();
        location.City.Should().BeNull();
        location.PostalCode.Should().BeNull();
        location.SubdivisionISO31662Code.Should().BeNull();
        location.CountryISO31661Alpha2Code.Should().BeNull();
    }

    #endregion

    #region Valid Creation - Individual Properties

    [Fact]
    public void Create_WithCoordinatesOnly_Success()
    {
        // Arrange
        var coordinates = Coordinates.Create(34.0522m, -118.2437m);

        // Act
        var location = Location.Create(coordinates: coordinates);

        // Assert
        location.Should().NotBeNull();
        location.HashId.Should().HaveCount(32);
        location.Coordinates.Should().Be(coordinates);
        location.Address.Should().BeNull();
        location.City.Should().BeNull();
    }

    [Fact]
    public void Create_WithAddressOnly_Success()
    {
        // Arrange
        var address = StreetAddress.Create("123 Main St");

        // Act
        var location = Location.Create(address: address);

        // Assert
        location.Should().NotBeNull();
        location.HashId.Should().HaveCount(32);
        location.Coordinates.Should().BeNull();
        location.Address.Should().Be(address);
    }

    [Fact]
    public void Create_WithCityOnly_Success()
    {
        // Arrange
        const string city = "Los Angeles";

        // Act
        var location = Location.Create(city: city);

        // Assert
        location.City.Should().Be("Los Angeles");
    }

    [Fact]
    public void Create_WithPostalCodeOnly_Success()
    {
        // Arrange
        const string postal_code = "90012";

        // Act
        var location = Location.Create(postalCode: postal_code);

        // Assert
        location.PostalCode.Should().Be("90012");
    }

    [Fact]
    public void Create_WithSubdivisionCodeOnly_Success()
    {
        // Arrange
        const string subdivision_code = "US-CA";

        // Act
        var location = Location.Create(subdivisionISO31662Code: subdivision_code);

        // Assert
        location.SubdivisionISO31662Code.Should().Be("US-CA");
    }

    [Fact]
    public void Create_WithCountryCodeOnly_Success()
    {
        // Arrange
        const string country_code = "US";

        // Act
        var location = Location.Create(countryISO31661Alpha2Code: country_code);

        // Assert
        location.CountryISO31661Alpha2Code.Should().Be("US");
    }

    #endregion

    #region Valid Creation - Full Location

    [Fact]
    public void Create_WithAllProperties_Success()
    {
        // Arrange
        var coordinates = Coordinates.Create(34.0522m, -118.2437m);
        var address = StreetAddress.Create("123 Main St", "Building B", "Suite 400");
        const string city = "Los Angeles";
        const string postal_code = "90012";
        const string subdivision_code = "US-CA";
        const string country_code = "US";

        // Act
        var location = Location.Create(
            coordinates,
            address,
            city,
            postal_code,
            subdivision_code,
            country_code);

        // Assert
        location.Should().NotBeNull();
        location.HashId.Should().HaveCount(32);
        location.Coordinates.Should().Be(coordinates);
        location.Address.Should().Be(address);
        location.City.Should().Be("Los Angeles");
        location.PostalCode.Should().Be("90012");
        location.SubdivisionISO31662Code.Should().Be("US-CA");
        location.CountryISO31661Alpha2Code.Should().Be("US");
    }

    #endregion

    #region String Cleanup - City

    [Fact]
    public void Create_WithCleanCity_NoChanges()
    {
        // Arrange
        const string clean_city = "Los Angeles";

        // Act
        var location = Location.Create(city: clean_city);

        // Assert
        location.City.Should().Be(clean_city);
    }

    [Fact]
    public void Create_WithDirtyCity_CleansWhitespace()
    {
        // Arrange
        const string dirty_city = "  Los   Angeles  ";

        // Act
        var location = Location.Create(city: dirty_city);

        // Assert
        location.City.Should().Be("Los Angeles");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Create_WithWhitespaceOnlyCity_SetsToNull(string city)
    {
        // Act
        var location = Location.Create(city: city);

        // Assert
        location.City.Should().BeNull();
    }

    #endregion

    #region String Cleanup - PostalCode (Uppercase)

    [Fact]
    public void Create_WithCleanPostalCode_NoChanges()
    {
        // Arrange
        const string clean_postal = "90012";

        // Act
        var location = Location.Create(postalCode: clean_postal);

        // Assert
        location.PostalCode.Should().Be(clean_postal);
    }

    [Fact]
    public void Create_WithDirtyPostalCode_CleansAndUppercases()
    {
        // Arrange
        const string dirty_postal = "  k1a 0b1  ";

        // Act
        var location = Location.Create(postalCode: dirty_postal);

        // Assert
        location.PostalCode.Should().Be("K1A 0B1");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Create_WithWhitespaceOnlyPostalCode_SetsToNull(string postalCode)
    {
        // Act
        var location = Location.Create(postalCode: postalCode);

        // Assert
        location.PostalCode.Should().BeNull();
    }

    #endregion

    #region String Cleanup - SubdivisionCode (Uppercase)

    [Fact]
    public void Create_WithCleanSubdivisionCode_NoChanges()
    {
        // Arrange
        const string clean_subdivision = "US-CA";

        // Act
        var location = Location.Create(subdivisionISO31662Code: clean_subdivision);

        // Assert
        location.SubdivisionISO31662Code.Should().Be(clean_subdivision);
    }

    [Fact]
    public void Create_WithDirtySubdivisionCode_CleansAndUppercases()
    {
        // Arrange
        const string dirty_subdivision = "  us-ca  ";

        // Act
        var location = Location.Create(subdivisionISO31662Code: dirty_subdivision);

        // Assert
        location.SubdivisionISO31662Code.Should().Be("US-CA");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Create_WithWhitespaceOnlySubdivisionCode_SetsToNull(string subdivisionCode)
    {
        // Act
        var location = Location.Create(subdivisionISO31662Code: subdivisionCode);

        // Assert
        location.SubdivisionISO31662Code.Should().BeNull();
    }

    #endregion

    #region String Cleanup - CountryCode (Uppercase)

    [Fact]
    public void Create_WithCleanCountryCode_NoChanges()
    {
        // Arrange
        const string clean_country = "US";

        // Act
        var location = Location.Create(countryISO31661Alpha2Code: clean_country);

        // Assert
        location.CountryISO31661Alpha2Code.Should().Be(clean_country);
    }

    [Fact]
    public void Create_WithDirtyCountryCode_CleansAndUppercases()
    {
        // Arrange
        const string dirty_country = "  us  ";

        // Act
        var location = Location.Create(countryISO31661Alpha2Code: dirty_country);

        // Assert
        location.CountryISO31661Alpha2Code.Should().Be("US");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Create_WithWhitespaceOnlyCountryCode_SetsToNull(string countryCode)
    {
        // Act
        var location = Location.Create(countryISO31661Alpha2Code: countryCode);

        // Assert
        location.CountryISO31661Alpha2Code.Should().BeNull();
    }

    #endregion

    #region Content-Addressable - Same Input = Same Hash

    [Fact]
    public void Create_WithSameContent_GeneratesSameHash()
    {
        // Arrange
        var coordinates = Coordinates.Create(34.0522m, -118.2437m);
        var address = StreetAddress.Create("123 Main St");
        const string city = "Los Angeles";
        const string postal_code = "90012";
        const string subdivision_code = "US-CA";
        const string country_code = "US";

        // Act
        var location1 = Location.Create(coordinates, address, city, postal_code, subdivision_code, country_code);
        var location2 = Location.Create(coordinates, address, city, postal_code, subdivision_code, country_code);

        // Assert
        location1.HashId.Should().BeEquivalentTo(location2.HashId);
    }

    [Fact]
    public void Create_WithSameContentDifferentWhitespace_GeneratesSameHash()
    {
        // Arrange
        const string city1 = "Los Angeles";
        const string city2 = "  Los   Angeles  ";

        // Act
        var location1 = Location.Create(city: city1);
        var location2 = Location.Create(city: city2);

        // Assert
        location1.HashId.Should().BeEquivalentTo(location2.HashId);
    }

    [Fact]
    public void Create_WithSameContentDifferentCase_GeneratesSameHash()
    {
        // Arrange
        const string country1 = "US";
        const string country2 = "us";

        // Act
        var location1 = Location.Create(countryISO31661Alpha2Code: country1);
        var location2 = Location.Create(countryISO31661Alpha2Code: country2);

        // Assert
        location1.HashId.Should().BeEquivalentTo(location2.HashId);
    }

    #endregion

    #region Content-Addressable - Different Input = Different Hash

    [Fact]
    public void Create_WithDifferentCity_GeneratesDifferentHash()
    {
        // Arrange
        const string city1 = "Los Angeles";
        const string city2 = "San Francisco";

        // Act
        var location1 = Location.Create(city: city1);
        var location2 = Location.Create(city: city2);

        // Assert
        location1.HashId.Should().NotBeEquivalentTo(location2.HashId);
        location1.Should().NotBe(location2);
    }

    [Fact]
    public void Create_WithDifferentCoordinates_GeneratesDifferentHash()
    {
        // Arrange
        var coords1 = Coordinates.Create(34.0522m, -118.2437m);
        var coords2 = Coordinates.Create(37.7749m, -122.4194m);

        // Act
        var location1 = Location.Create(coordinates: coords1);
        var location2 = Location.Create(coordinates: coords2);

        // Assert
        location1.HashId.Should().NotBeEquivalentTo(location2.HashId);
    }

    [Fact]
    public void Create_WithDifferentAddress_GeneratesDifferentHash()
    {
        // Arrange
        var address1 = StreetAddress.Create("123 Main St");
        var address2 = StreetAddress.Create("456 Oak Ave");

        // Act
        var location1 = Location.Create(address: address1);
        var location2 = Location.Create(address: address2);

        // Assert
        location1.HashId.Should().NotBeEquivalentTo(location2.HashId);
    }

    #endregion

    #region Create Overload Tests

    [Fact]
    public void Create_WithExistingLocation_CreatesNewInstanceWithSameHash()
    {
        // Arrange
        var coordinates = Coordinates.Create(34.0522m, -118.2437m);
        var address = StreetAddress.Create("123 Main St");
        var original = Location.Create(
            coordinates,
            address,
            "Los Angeles",
            "90012",
            "US-CA",
            "US");

        // Act
        var copy = Location.Create(original);

        // Assert
        copy.Should().NotBeNull();
        copy.HashId.Should().BeEquivalentTo(original.HashId);
        copy.Coordinates.Should().Be(original.Coordinates);
        copy.Address.Should().Be(original.Address);
        copy.City.Should().Be(original.City);
        copy.PostalCode.Should().Be(original.PostalCode);
        copy.SubdivisionISO31662Code.Should().Be(original.SubdivisionISO31662Code);
        copy.CountryISO31661Alpha2Code.Should().Be(original.CountryISO31661Alpha2Code);
    }

    #endregion

    #region Hash Determinism

    [Fact]
    public void Create_CalledMultipleTimes_GeneratesConsistentHash()
    {
        // Arrange
        var coordinates = Coordinates.Create(34.0522m, -118.2437m);
        var address = StreetAddress.Create("123 Main St");
        const string city = "Los Angeles";

        // Act - Create same location 5 times
        var locations = Enumerable.Range(0, 5)
            .Select(_ => Location.Create(coordinates, address, city))
            .ToList();

        // Assert - All should have identical hashes
        var firstHash = locations[0].HashId;
        locations.Should().AllSatisfy(loc => loc.HashId.Should().BeEquivalentTo(firstHash));
    }

    #endregion
}
