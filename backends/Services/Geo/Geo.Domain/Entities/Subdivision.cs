namespace Geo.Domain.Entities;

/// <summary>
/// Represents a subdivision (e.g., state, province, region) of a country.
/// </summary>
/// <remarks>
/// Is an entity of the Geography "Geo" Domain. A subdivision always belongs to a single
/// <see cref="Country"/> (which is the aggregate root).
/// </remarks>
public record Subdivision
{
    #region Identity - Primary Key

    /// <summary>
    /// The ISO 3166-2 code of the subdivision.
    /// </summary>
    /// <example>
    /// US-AL
    /// </example>
    /// <remarks>
    /// Must be unique. Between 4 and 6 characters in length, typically formatted as "CC-SSS"
    /// where "CC" is the ISO 3166-1 alpha-2 country code and "SSS" is the subdivision code.
    /// </remarks>
    public required string ISO31662Code { get; init; }

    #endregion

    #region Properties

    /// <summary>
    /// The short code of the subdivision. Usually the same as the second part of the ISO 3166-2
    /// code. 3 characters max.
    /// </summary>
    /// <example>
    /// AL
    /// </example>
    public required string ShortCode { get; init; }

    /// <summary>
    /// The display name of the subdivision.
    /// </summary>
    /// <example>
    /// Alabama
    /// </example>
    public required string DisplayName { get; init; }

    /// <summary>
    /// The official name of the subdivision.
    /// </summary>
    /// <example>
    /// State of Alabama
    /// </example>
    public required string OfficialName { get; init; }

    #endregion

    #region Foreign Keys

    /// <summary>
    /// The ISO 3166-1 alpha-2 code of the country this subdivision belongs to.
    /// </summary>
    /// <example>
    /// US
    /// </example>
    public required string CountryISO31661Alpha2Code { get; init; }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Navigation property to the country this subdivision belongs to.
    /// </summary>
    public Country? Country { get; init; }

    #endregion
}
