// -----------------------------------------------------------------------
// <copyright file="Coordinates.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Domain.ValueObjects;

using System.Globalization;
using D2.Geo.Domain.Entities;
using D2.Geo.Domain.Exceptions;
using D2.Shared.Utilities.Attributes;
using D2.Shared.Utilities.Enums;

/// <summary>
/// Represents the geographic coordinates (latitude and longitude) of a <see cref="Location"/>.
/// </summary>
/// <remarks>
/// Is a value object of the Geography "Geo" Domain, used by the <see cref="Location"/> entity.
///
/// Latitude and longitude are expressed in decimal degrees.
/// </remarks>
[RedactData(Reason = RedactReason.PersonalInformation)]
public record Coordinates
{
    /// <summary>
    /// Gets latitude in decimal degrees.
    /// </summary>
    /// <example>
    /// 48.42841.
    /// </example>
    public required double Latitude { get; init; }

    /// <summary>
    /// Gets longitude in decimal degrees.
    /// </summary>
    /// <example>
    /// -123.36564.
    /// </example>
    public required double Longitude { get; init; }

    #region Functionality

    /// <summary>
    /// Factory method to create a new <see cref="Coordinates"/> instance with validation.
    /// </summary>
    ///
    /// <param name="latitude">
    /// Latitude in decimal degrees. Must be between -90 and 90 (inclusive). Required.
    /// </param>
    /// <param name="longitude">
    /// Longitude in decimal degrees. Must be between -180 and 180 (inclusive). Required.
    /// </param>
    ///
    /// <returns>
    /// A new validated <see cref="Coordinates"/> instance.
    /// </returns>
    ///
    /// <exception cref="GeoValidationException">
    /// Thrown if latitude or longitude are out of range.
    /// </exception>
    public static Coordinates Create(
        double latitude,
        double longitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw new GeoValidationException(
                nameof(Coordinates),
                nameof(Latitude),
                latitude,
                "must be between -90 and 90.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new GeoValidationException(
                nameof(Coordinates),
                nameof(Longitude),
                longitude,
                "must be between -180 and 180.");
        }

        var quantizedLat = Math.Round(latitude, 5);
        var quantizedLon = Math.Round(longitude, 5);

        return new Coordinates
        {
            Latitude = quantizedLat,
            Longitude = quantizedLon,
        };
    }

    /// <summary>
    /// Factory method to create a new <see cref="Coordinates"/> instance with validation.
    /// </summary>
    ///
    /// <param name="coordinates">
    /// The coordinates to validate and create a new instance from.
    /// </param>
    ///
    /// <returns>
    /// A new validated <see cref="Coordinates"/> instance.
    /// </returns>
    ///
    /// <exception cref="GeoValidationException">
    /// Thrown if latitude or longitude are out of range.
    /// </exception>
    public static Coordinates Create(Coordinates coordinates)
        => Create(coordinates.Latitude, coordinates.Longitude);

    /// <summary>
    /// Gets an array of strings representing the latitude and longitude, formatted to 5 decimal
    /// places, using invariant culture.
    /// </summary>
    ///
    /// <param name="coordinates">
    /// The coordinates.
    /// </param>
    ///
    /// <returns>
    /// A string array of coordinates.
    /// </returns>
    public static string?[] GetParts(Coordinates? coordinates)
    {
        return [
            coordinates?.Latitude.ToString("F5", CultureInfo.InvariantCulture),
            coordinates?.Longitude.ToString("F5", CultureInfo.InvariantCulture)
        ];
    }

    #endregion
}
