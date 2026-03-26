// -----------------------------------------------------------------------
// <copyright file="Country.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Domain.Entities;

/// <summary>
/// Represents a country or nation state.
/// </summary>
/// <remarks>
/// Is an aggregate root of the Geography "Geo" Domain.
/// Relates to <see cref="Subdivisions"/>, <see cref="Currencies"/>, <see cref="Locales"/>, and
/// <see cref="GeopoliticalEntities"/>.
/// </remarks>
public record Country
{
    #region Identity - Primary Key

    /// <summary>
    /// Gets the ISO 3166-1 alpha-2 code of the country.
    /// </summary>
    /// <example>
    /// US.
    /// </example>
    /// <remarks>
    /// Must be unique. Always a 2-char string (letters).
    /// </remarks>
    public required string ISO31661Alpha2Code { get; init; }

    #endregion

    #region Identity - Unique

    /// <summary>
    /// Gets the ISO 3166-1 alpha-3 code of the country.
    /// </summary>
    /// <example>
    /// USA.
    /// </example>
    /// <remarks>
    /// Must be unique. Always a 3-char string (letters).
    /// </remarks>
    public required string ISO31661Alpha3Code { get; init; }

    /// <summary>
    /// Gets the ISO 3166-1 numeric-3 code of the country.
    /// </summary>
    /// <example>
    /// 840.
    /// </example>
    /// <remarks>
    /// Must be unique. Always a 3-char string (numbers).
    /// </remarks>
    public required string ISO31661NumericCode { get; init; }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the display name of the country.
    /// </summary>
    /// <example>
    /// United States.
    /// </example>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the official name of the country.
    /// </summary>
    /// <example>
    /// United States of America.
    /// </example>
    public required string OfficialName { get; init; }

    /// <summary>
    /// Gets the phone number prefix of the country.
    /// </summary>
    /// <example>
    /// 1.
    /// </example>
    /// <remarks>
    /// Does not include the "+".
    /// </remarks>
    public required string PhoneNumberPrefix { get; init; }

    #endregion

    #region Foreign Keys

    /// <summary>
    /// Gets the ISO 3166-1 alpha-2 code of the sovereign country that this country is a territory of,
    /// if applicable.
    /// </summary>
    /// <example>
    /// "US" (for Puerto Rico).
    /// </example>
    public string? SovereignISO31661Alpha2Code { get; init; }

    /// <summary>
    /// Gets the ISO 4217 alpha code of the primary currency used in this country.
    /// </summary>
    /// <example>
    /// "USD" (for United States Dollar in the United States).
    /// </example>
    public string? PrimaryCurrencyISO4217AlphaCode { get; init; }

    /// <summary>
    /// Gets the IETF BCP 47 tag of the primary locale used in this country.
    /// </summary>
    /// <example>
    /// "en-US" (for American English in the United States).
    /// </example>
    public string? PrimaryLocaleIETFBCP47Tag { get; init; }

    #endregion

    #region Navigation Collections

    /// <summary>
    /// Gets navigation property to the sovereign country that this country is a territory of, if
    /// applicable.
    /// </summary>
    /// <example>
    /// United States (for Puerto Rico).
    /// </example>
    public Country? SovereignCountry { get; init; }

    /// <summary>
    /// Gets a collection of territories (other countries) that belong to this country.
    /// </summary>
    /// <example>
    /// Puerto Rico in the United States.
    /// </example>
    public ICollection<Country> Territories { get; init; } = [];

    /// <summary>
    /// Gets a collection of subdivisions that belong to this country.
    /// </summary>
    /// <example>
    /// States in the United States.
    /// </example>
    public ICollection<Subdivision> Subdivisions { get; init; } = [];

    /// <summary>
    /// Gets the primary currency used in this country.
    /// </summary>
    /// <example>
    /// United States Dollar (USD) in the United States.
    /// </example>
    public Currency? PrimaryCurrency { get; init; }

    /// <summary>
    /// Gets a collection of currencies that are used in this country.
    /// </summary>
    /// <remarks>
    /// All accepted currencies including the primary currency should be included in this
    /// collection.
    /// </remarks>
    public ICollection<Currency> Currencies { get; init; } = [];

    /// <summary>
    /// Gets the primary locale used in this country.
    /// </summary>
    /// <example>
    /// American English (en_US) in the United States.
    /// </example>
    public Locale? PrimaryLocale { get; init; }

    /// <summary>
    /// Gets a collection of locales that are present in this country.
    /// </summary>
    /// <example>
    /// American English (en_US) in the United States.
    /// </example>
    /// <remarks>
    /// A country can have multiple locales, but only one primary locale. Locales determine what
    /// language and regional settings are used for formatting dates, times, numbers, and
    /// currencies / numerical values.
    /// </remarks>
    public ICollection<Locale> Locales { get; init; } = [];

    /// <summary>
    /// Gets a collection of IANA timezones associated with this country.
    /// </summary>
    /// <example>
    /// America/New_York, America/Chicago, America/Denver, America/Los_Angeles.
    /// </example>
    /// <remarks>
    /// A country can have multiple timezones. Each timezone has a single primary country.
    /// </remarks>
    public ICollection<Timezone> Timezones { get; init; } = [];

    /// <summary>
    /// Gets a collection of geopolitical entities to which this country belongs.
    /// </summary>
    /// <example>
    /// North America OR USMCA OR NATO.
    /// </example>
    /// <remarks>
    /// This is a many-to-many relationship.
    /// </remarks>
    public ICollection<GeopoliticalEntity> GeopoliticalEntities { get; init; } = [];

    #endregion
}
