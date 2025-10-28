using FluentAssertions;
using Geo.Domain.Exceptions;
using Geo.Domain.ValueObjects;
using Xunit;

namespace Geo.Tests.Unit.Domain.ValueObjects;

/// <summary>
/// Unit tests for <see cref="Geo.Domain.ValueObjects.Coordinates"/>.
/// </summary>
public class CoordinatesTests
{
    #region Valid Input - Creates Instance

    [Fact]
    public void Create_WithValidCoordinates_Success()
    {
        // Arrange
        const decimal latitude = 34.0522m;
        const decimal longitude = -118.2437m;

        // Act
        var coords = Coordinates.Create(latitude, longitude);

        // Assert
        coords.Should().NotBeNull();
        coords.Latitude.Should().Be(34.0522m);
        coords.Longitude.Should().Be(-118.2437m);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(90, 180)]
    [InlineData(-90, -180)]
    [InlineData(45.5, 90.5)]
    public void Create_WithValidBoundaryValues_Success(double lat, double lon)
    {
        // Act
        var coords = Coordinates.Create((decimal)lat, (decimal)lon);

        // Assert
        coords.Should().NotBeNull();
        coords.Latitude.Should().Be((decimal)lat);
        coords.Longitude.Should().Be((decimal)lon);
    }

    #endregion

    #region Precision - 5 Decimal Places

    [Fact]
    public void Create_QuantizesTo5DecimalPlaces()
    {
        // Arrange
        const decimal lat = 34.052234567m;
        const decimal lon = -118.243745678m;

        // Act
        var coords = Coordinates.Create(lat, lon);

        // Assert
        coords.Latitude.Should().Be(34.05223m);
        coords.Longitude.Should().Be(-118.24375m);
    }

    [Theory]
    [InlineData(34.123456, 34.12346)]
    [InlineData(34.123454, 34.12345)]
    [InlineData(-39.999999, -40.00000)]
    [InlineData(0.000001, 0.00000)]
    public void Create_QuantizesCorrectly(double input, double expected)
    {
        // Act
        var coords = Coordinates.Create((decimal)input, 0);

        // Assert
        coords.Latitude.Should().Be((decimal)expected);
    }

    #endregion

    #region Latitude Out of Range - Throws

    [Theory]
    [InlineData(90.1)]
    [InlineData(91)]
    [InlineData(100)]
    [InlineData(180)]
    public void Create_WithLatitudeTooHigh_ThrowsGeoValidationException(double lat)
    {
        // Act
        var act = () => Coordinates.Create((decimal)lat, 0);

        // Assert
        act.Should().Throw<GeoValidationException>();
    }

    [Theory]
    [InlineData(-90.1)]
    [InlineData(-91)]
    [InlineData(-100)]
    [InlineData(-180)]
    public void Create_WithLatitudeTooLow_ThrowsGeoValidationException(double lat)
    {
        // Act
        var act = () => Coordinates.Create((decimal)lat, 0);

        // Assert
        act.Should().Throw<GeoValidationException>();
    }

    #endregion

    #region Longitude Out of Range - Throws

    [Theory]
    [InlineData(180.1)]
    [InlineData(181)]
    [InlineData(200)]
    [InlineData(360)]
    public void Create_WithLongitudeTooHigh_ThrowsGeoValidationException(double lon)
    {
        // Act
        var act = () => Coordinates.Create(0, (decimal)lon);

        // Assert
        act.Should().Throw<GeoValidationException>();
    }

    [Theory]
    [InlineData(-180.1)]
    [InlineData(-181)]
    [InlineData(-200)]
    [InlineData(-360)]
    public void Create_WithLongitudeTooLow_ThrowsGeoValidationException(double lon)
    {
        // Act
        var act = () => Coordinates.Create(0, (decimal)lon);

        // Assert
        act.Should().Throw<GeoValidationException>();
    }

    #endregion
}
