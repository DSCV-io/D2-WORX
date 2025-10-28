namespace Geo.Domain.Entities;

/// <summary>
/// Represents a locale.
/// </summary>
/// <remarks>
/// Is an entity of the Geography "Geo" Domain. A locale typically combines a
/// <see cref="Language"/> and a <see cref="Country"/> to define regional settings such as date
/// formats, number formats, and cultural conventions.
/// </remarks>
public record Locale
{
    #region Identity - Primary Key

    /// <summary>
    /// The IETF BCP 47 tag of the locale.
    /// </summary>
    /// <example>
    /// es-US
    /// </example>
    /// <remarks>
    /// Must be unique. Follows the IETF BCP 47 standard for language tags.
    /// </remarks>
    public required string IETFBCP47Tag { get; init; }

    #endregion

    #region Properties

    /// <summary>
    /// The display name of the locale.
    /// </summary>
    /// <example>
    /// Spanish (United States)
    /// </example>
    /// <remarks>
    /// This is the name of the locale in English.
    /// </remarks>
    public required string Name { get; init; }

    /// <summary>
    /// The endonym of the locale.
    /// </summary>
    /// <example>
    /// Español (Estados Unidos)
    /// </example>
    /// <remarks>
    /// This is the name of the locale in the language itself.
    /// </remarks>
    public required string Endonym { get; init; }

    #endregion

    #region Foreign Keys

    /// <summary>
    /// The ISO 639-1 code of the language associated with the locale.
    /// </summary>
    /// <example>
    /// es
    /// </example>
    public required string LanguageISO6391Code { get; init; }

    /// <summary>
    /// The ISO 3166-1 alpha-2 code of the country associated with the locale.
    /// </summary>
    /// <example>
    /// US
    /// </example>
    public required string CountryISO31661Alpha2Code { get; init; }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Navigation property to the language associated with the locale.
    /// </summary>
    public Language? Language { get; init; }

    /// <summary>
    /// Navigation property to the country associated with the locale.
    /// </summary>
    public Country? Country { get; init; }

    #endregion
}
