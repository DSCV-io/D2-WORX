// -----------------------------------------------------------------------
// <copyright file="Timezone.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Domain.Entities;

/// <summary>
/// Represents a timezone from the IANA Time Zone Database.
/// </summary>
/// <remarks>
/// Is an entity of the Geography "Geo" Domain. Only canonical IANA entries are stored
/// (no links/aliases). Each timezone has a single primary <see cref="Country"/> FK.
/// </remarks>
public record Timezone
{
    #region Identity - Primary Key

    /// <summary>
    /// Gets the IANA timezone identifier.
    /// </summary>
    /// <example>
    /// America/New_York.
    /// </example>
    /// <remarks>
    /// Must be unique. Follows the IANA tz database naming convention (Area/Location).
    /// </remarks>
    public required string IANAIdentifier { get; init; }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the display name of the timezone.
    /// </summary>
    /// <example>
    /// America / New York.
    /// </example>
    /// <remarks>
    /// Human-readable version of the IANA identifier with underscores replaced by spaces
    /// and slashes padded with spaces.
    /// </remarks>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the UTC offset during standard time.
    /// </summary>
    /// <example>
    /// -05:00.
    /// </example>
    public required string UTCOffsetSTD { get; init; }

    /// <summary>
    /// Gets the UTC offset during daylight saving time, or null if DST is not observed.
    /// </summary>
    /// <example>
    /// -04:00.
    /// </example>
    public string? UTCOffsetDST { get; init; }

    /// <summary>
    /// Gets the timezone abbreviation during standard time.
    /// </summary>
    /// <example>
    /// EST.
    /// </example>
    public required string AbbreviationSTD { get; init; }

    /// <summary>
    /// Gets the timezone abbreviation during daylight saving time, or null if DST is not observed.
    /// </summary>
    /// <example>
    /// EDT.
    /// </example>
    public string? AbbreviationDST { get; init; }

    #endregion

    #region Foreign Keys

    /// <summary>
    /// Gets the ISO 3166-1 alpha-2 code of the primary country for this timezone.
    /// </summary>
    /// <example>
    /// US.
    /// </example>
    public required string CountryISO31661Alpha2Code { get; init; }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Gets navigation property to the primary country for this timezone.
    /// </summary>
    public Country? Country { get; init; }

    #endregion
}
