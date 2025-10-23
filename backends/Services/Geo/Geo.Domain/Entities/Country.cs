namespace Geo.Domain.Entities;

/// <summary>
/// Represents a country or nation state.
/// </summary>
/// <remarks>
/// Is an aggregate root of the Geography "Geo" Domain.
/// Relates to <see cref="Subdivisions"/>, <see cref="Currencies"/>, <see cref="Locales"/>, and
/// <see cref="GeopoliticalEntities"/>.
/// </remarks>
public class Country
{
    #region Identity - Primary Key

    /// <summary>
    /// The ISO 3166-1 alpha-2 code of the country.
    /// </summary>
    /// <example>
    /// US
    /// </example>
    /// <remarks>
    /// Must be unique. Always a 2-char string (letters).
    /// </remarks>
    public required string ISO31661Alpha2Code { get; set; }

    #endregion

    #region Identity - Unique

    /// <summary>
    /// The ISO 3166-1 alpha-3 code of the country.
    /// </summary>
    /// <example>
    /// USA
    /// </example>
    /// <remarks>
    /// Must be unique. Always a 3-char string (letters).
    /// </remarks>
    public required string ISO31661Alpha3Code { get; set; }

    /// <summary>
    /// The ISO 3166-1 numeric-3 code of the country.
    /// </summary>
    /// <example>
    /// 840
    /// </example>
    /// <remarks>
    /// Must be unique. Always a 3-char string (numbers).
    /// </remarks>
    public required string ISO31661NumericCode { get; set; }

    #endregion

    #region Properties

    /// <summary>
    /// The display name of the country.
    /// </summary>
    /// <example>
    /// United States
    /// </example>
    public required string DisplayName { get; set; }

    /// <summary>
    /// The official name of the country.
    /// </summary>
    /// <example>
    /// United States of America
    /// </example>
    public required string OfficialName { get; set; }

    /// <summary>
    /// The phone number prefix of the country.
    /// </summary>
    /// <example>
    /// 1
    /// </example>
    /// <remarks>
    /// Does not include the "+".
    /// </remarks>
    public required string PhoneNumberPrefix { get; set; }

    /// <summary>
    /// The phone number format of the country.
    /// </summary>
    /// <example>
    /// (###) ###-####
    /// </example>
    /// <remarks>
    /// Used to display phone numbers in a localized format.
    /// </remarks>
    public required string PhoneNumberFormat { get; set; }

    #endregion

    #region Navigation Collections

    /// <summary>
    /// A collection of subdivisions that belong to this country.
    /// </summary>
    /// <example>
    /// States in the United States.
    /// </example>
    public required ICollection<Subdivision> Subdivisions { get; set; } = [];

    /// <summary>
    /// A collection of currencies that are used in this country.
    /// </summary>
    /// <example>
    /// United States Dollar (USD) in the United States.
    /// </example>
    /// <remarks>
    /// A country can have multiple currencies, but only one primary currency.
    /// </remarks>
    public required ICollection<Currency> Currencies { get; set; } = [];

    /// <summary>
    /// A collection of locales that are present in this country.
    /// </summary>
    /// <example>
    /// American English (en_US) in the United States.
    /// </example>
    /// <remarks>
    /// A country can have multiple locales, but only one primary locale. Locales determine what
    /// language and regional settings are used for formatting dates, times, numbers, and
    /// currencies / numerical values.
    /// </remarks>
    public required ICollection<Locale> Locales { get; set; } = [];

    /// <summary>
    /// A collection of geopolitical entities to which this country belongs.
    /// </summary>
    /// <example>
    /// North America OR USMCA OR NATO.
    /// </example>
    /// <remarks>
    /// This is a many-to-many relationship.
    /// </remarks>
    public required ICollection<GeopoliticalEntity> GeopoliticalEntities { get; set; } = [];

    #endregion
}
