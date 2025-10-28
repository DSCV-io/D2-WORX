using System.Security.Cryptography;
using System.Text;
using D2.Contracts.Utilities;
using Geo.Domain.ValueObjects;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Geo.Domain.Entities;

/// <summary>
/// Represents a geographical location with various address components and coordinates.
/// </summary>
/// <remarks>
/// Is an aggregate root of the Geography "Geo" Domain.
///
/// Used by <see cref="WhoIs"/> and <see cref="Contact"/> roots to represent physical locations.
///
/// Contains two value objects: <see cref="Coordinates"/> and <see cref="StreetAddress"/>.
///
/// Its primary key is a content-addressable SHA-256 hash of its properties.
///
/// <see cref="Location"/> records should be immutable once created.
/// </remarks>
public record Location
{
    #region Identity

    /// <summary>
    /// A content-addressable 32-byte SHA-256 hash of the location's content.
    /// </summary>
    public required byte[] HashId { get; init; }

    #endregion

    #region Content Addressible Properties

    /// <summary>
    /// The geographical coordinates (latitude and longitude) of the location.
    /// </summary>
    /// <example>
    /// {
    ///     "latitude": 34.0522,
    ///     "longitude": -118.2437
    /// }
    /// </example>
    public Coordinates? Coordinates { get; init; }

    /// <summary>
    /// The street address (lines) of the location.
    /// </summary>
    /// <example>
    /// {
    ///     "line1": "123 Main St",
    ///     "line2": "Building B",
    ///     "line3": "Suite 400"
    /// }
    /// </example>
    public StreetAddress? Address { get; init; }

    /// <summary>
    /// The city.
    /// </summary>
    /// <example>
    /// Los Angeles
    /// </example>
    public string? City { get; init; }

    /// <summary>
    /// The postal code (or ZIP code) of the location.
    /// </summary>
    /// <example>
    /// 90012
    /// </example>
    public string? PostalCode { get; init; }

    #endregion

    #region Foreign Keys (Content Addressable)

    /// <summary>
    /// The ISO 3166-2 code of the subdivision / region.
    /// </summary>
    /// <example>
    /// US-CA
    /// </example>
    public string? SubdivisionISO31662Code { get; init; }

    /// <summary>
    /// The ISO 3166-1 alpha-2 code of the country.
    /// </summary>
    /// <example>
    /// US
    /// </example>
    public string? CountryISO31661Alpha2Code { get; init; }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Navigation property to the subdivision / region of the location.
    /// </summary>
    public Subdivision? Subdivision { get; init; }

    /// <summary>
    /// Navigation property to the country of the location.
    /// </summary>
    public Country? Country { get; init; }

    #endregion

    #region Functionality

    /// <summary>
    /// Factory method to create a new <see cref="Location"/> instance with a content-addressable
    /// <see cref="HashId"/> based on the provided properties.
    /// </summary>
    ///
    /// <param name="coordinates">
    /// The coordinates. Optional.
    /// </param>
    /// <param name="address">
    /// The street address [lines]. Optional.
    /// </param>
    /// <param name="city">
    /// The city. Optional.
    /// </param>
    /// <param name="postalCode">
    /// The postal code. Optional.
    /// </param>
    /// <param name="subdivisionISO31662Code">
    /// The ISO 3166-2 code of the subdivision / region. Optional.
    /// </param>
    /// <param name="countryISO31661Alpha2Code">
    /// The ISO 3166-1 alpha-2 code of the country.
    /// </param>
    ///
    /// <returns>
    /// A new <see cref="Location"/> instance with a computed <see cref="HashId"/>.
    /// </returns>
    public static Location Create(
        Coordinates? coordinates = null,
        StreetAddress? address = null,
        string? city = null,
        string? postalCode = null,
        string? subdivisionISO31662Code = null,
        string? countryISO31661Alpha2Code = null)
    {
        // Clean up inputs (we don't need to do this for the value objects since they "clean
        // themselves" upon creation).
        var cityClean = city.CleanStr();
        var postalCodeClean = postalCode.CleanStr()?.ToUpperInvariant();
        var subdivisionCodeClean = subdivisionISO31662Code.CleanStr()?.ToUpperInvariant();
        var countryCodeClean = countryISO31661Alpha2Code.CleanStr()?.ToUpperInvariant();

        // Create a normalized string representation of the location's content for hashing.
        var hashInputArr = new[]
        {
            Coordinates.GetParts(coordinates).GetNormalizedStrForHashing(),
            StreetAddress.GetParts(address).GetNormalizedStrForHashing(),
            cityClean,
            postalCodeClean,
            subdivisionCodeClean,
            countryCodeClean
        };
        var input = hashInputArr.GetNormalizedStrForHashing();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashId = SHA256.HashData(inputBytes);

        // Return a new Location instance with the computed HashId and cleaned properties.
        return new Location
        {
            HashId = hashId,
            Coordinates = coordinates,
            Address = address,
            City = cityClean,
            PostalCode = postalCodeClean,
            SubdivisionISO31662Code = subdivisionCodeClean,
            CountryISO31661Alpha2Code = countryCodeClean
        };
    }

    /// <summary>
    /// Factory method to create a new <see cref="Location"/> instance with validation, including
    /// content-addressable <see cref="HashId"/> based on the provided <paramref name="location"/>.
    /// </summary>
    ///
    /// <param name="location">
    /// The location to validate and create a new instance from.
    /// </param>
    ///
    /// <returns>
    /// A new validated <see cref="Location"/> instance.
    /// </returns>
    public static Location Create(Location location)
        => Create(
            location.Coordinates,
            location.Address,
            location.City,
            location.PostalCode,
            location.SubdivisionISO31662Code,
            location.CountryISO31661Alpha2Code);

    #endregion
}
