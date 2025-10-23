using D2.Contracts.Utilities;
using Geo.Domain.Entities;
using Geo.Domain.Exceptions;

namespace Geo.Domain.ValueObjects;

/// <summary>
/// Represents the street address [lines] of a <see cref="Location"/>.
/// </summary>
/// <remarks>
/// Is a value object of the Geography "Geo" Domain, used by the <see cref="Location"/> entity.
/// </remarks>
public record StreetAddress
{
    /// <summary>
    /// The first line of the street address, typically containing the primary street number and
    /// name.
    /// </summary>
    /// <example>
    /// 123 Main St
    /// </example>
    public required string Line1 { get; init; }

    /// <summary>
    /// The second line of the street address, often used for building, apartment, suite, unit.
    /// </summary>
    /// <example>
    /// Building B
    /// </example>
    public string? Line2 { get; init; }


    /// <summary>
    /// The third line of the street address, sometimes used for additional information like
    /// floor or room number or for apartment, suite when a building is specified in Line 2.
    /// </summary>
    /// <example>
    /// Suite 400
    /// </example>
    public string? Line3 { get; init; }

    #region Functionality

    /// <summary>
    /// Factory method to create a new <see cref="StreetAddress"/> instance with validation.
    /// </summary>
    ///
    /// <param name="line1">
    /// The street address first line. Required.
    /// </param>
    /// <param name="line2">
    /// The street address second line. Optional.
    /// </param>
    /// <param name="line3">
    /// The street address third line. Optional.
    /// </param>
    ///
    /// <returns>
    /// A new <see cref="StreetAddress"/> instance with clean lines.
    /// </returns>
    ///
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="line1"/> is null, empty, or whitespace.
    /// Or if <paramref name="line3"/> is provided but <paramref name="line2"/> is null, empty, or
    /// whitespace.
    /// </exception>
    public static StreetAddress Create(
        string line1,
        string? line2 = null,
        string? line3 = null)
    {
        var line1Clean = line1.CleanStr();

        if (line1Clean.Falsey())
            throw new GeoValidationException(
                nameof(StreetAddress),
                nameof(Line1),
                line1,
                "is required.");

        var line2Clean = line2.CleanStr();
        var line3Clean = line3.CleanStr();

        if (line2Clean.Falsey() && line3Clean.Truthy())
            throw new GeoValidationException(
                nameof(StreetAddress),
                nameof(Line2),
                line2,
                $"cannot be empty if {nameof(line3)} is provided.");

        var address = new StreetAddress
        {
            Line1 = line1Clean!,
            Line2 = line2Clean.Falsey() ? null : line2Clean,
            Line3 = line3Clean.Falsey() ? null : line3Clean
        };

        return address;
    }

    /// <summary>
    /// Factory method to create a new <see cref="StreetAddress"/> instance with validation.
    /// </summary>
    ///
    /// <param name="streetAddress">
    /// The street address to validate and create a new instance from.
    /// </param>
    ///
    /// <returns>
    /// A new validated <see cref="StreetAddress"/> instance.
    /// </returns>
    ///
    /// <exception cref="ArgumentException">
    /// Thrown if <see cref="Line1"/> is null, empty, or whitespace.
    /// Or if <see cref="Line2"/> is provided but <see cref="Line3"/> is null, empty, or whitespace.
    /// </exception>
    public static StreetAddress Create(StreetAddress streetAddress)
        => Create(streetAddress.Line1, streetAddress.Line2, streetAddress.Line3);

    /// <summary>
    /// Gets an array of the street address lines, in order.
    /// </summary>
    ///
    /// <param name="address">
    /// The address.
    /// </param>
    ///
    /// <returns>
    /// A string array of street lines.
    /// </returns>
    public static string?[] GetParts(StreetAddress? address)
    {
        return [address?.Line1, address?.Line2, address?.Line3];
    }

    #endregion
}
