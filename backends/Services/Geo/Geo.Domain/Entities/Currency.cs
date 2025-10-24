namespace Geo.Domain.Entities;

/// <summary>
/// Represents a currency.
/// </summary>
/// <remarks>
/// Is an entity of the Geography "Geo" Domain. A currency can be used by multiple
/// <see cref="Country"/> entities. Countries typically have one primary currency, but may also
/// recognize additional currencies.
/// </remarks>
public record Currency
{
    #region Identity - Primary Key

    /// <summary>
    /// The ISO 4217 alpha code of the currency.
    /// </summary>
    /// <example>
    /// USD
    /// </example>
    /// <remarks>
    /// Must be unique. Always a 3-char string (letters).
    /// </remarks>
    public required string ISO4217AlphaCode { get; init; }

    #endregion

    #region Identity - Unique

    /// <summary>
    /// The ISO 4217 numeric code of the currency.
    /// </summary>
    /// <example>
    /// 840
    /// </example>
    /// <remarks>
    /// Must be unique. Always a 3-char string (numbers). Includes leading zeros where applicable.
    /// </remarks>
    public required string ISO4217NumCode { get; init; }

    #endregion

    #region Properties

    /// <summary>
    /// The display name of the currency.
    /// </summary>
    /// <example>
    /// U.S. dollar
    /// </example>
    /// <remarks>
    /// A short, human-readable name for the currency.
    /// </remarks>
    public required string DisplayName { get; init; }

    /// <summary>
    /// The official name of the currency.
    /// </summary>
    /// <example>
    /// United States dollar
    /// </example>
    public required string OfficialName { get; init; }

    /// <summary>
    /// The number of decimal places generally displayed for amounts in this currency.
    /// </summary>
    /// <example>
    /// 2
    /// </example>
    /// <remarks>
    /// Common values are 0 (e.g., JPY), 2 (e.g ., USD), or 3 (e.g., BHD).
    /// </remarks>
    public required int DecimalPlaces { get; init; }

    /// <summary>
    /// The symbol used to represent the currency.
    /// </summary>
    /// <example>
    /// $
    /// </example>
    /// <remarks>
    /// This may be a single character (e.g., $) or multiple characters (e.g., F.CFA).
    /// </remarks>
    public required string Symbol { get; init; }

    #endregion
}
