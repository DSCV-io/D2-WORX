using Geo.Domain.Enums;

namespace Geo.Domain.Entities;

/// <summary>
/// Represents a geopolitical entity (e.g., union, confederation, commonwealth).
/// </summary>
/// <remarks>
/// Is an aggregate root of the Geography "Geo" Domain.
/// Relates to multiple <see cref="Country"/> entities that are members of this geopolitical entity.
/// Countries can belong to many geopolitical entities.
/// </remarks>
public record GeopoliticalEntity
{
    #region Identity - Primary Key

    /// <summary>
    /// A short code identifying the geopolitical entity.
    /// </summary>
    /// <example>
    /// USMCA
    /// </example>
    /// <remarks>
    /// Must be unique. Not an official standard, but typically uppercase and short in length
    /// (2-8 characters).
    /// </remarks>
    public required string ShortCode { get; init; }

    #endregion

    #region Properties

    /// <summary>
    /// The display name of the geopolitical entity.
    /// </summary>
    /// <example>
    /// United States-Mexico-Canada Agreement
    /// </example>
    public required string Name { get; init; }

    /// <summary>
    /// The type of geopolitical entity.
    /// </summary>
    /// <example>
    /// GeopoliticalEntityType.FreeTradeAgreement
    /// </example>
    public required GeopoliticalEntityType Type { get; init; }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// A collection of countries that are members of this geopolitical entity.
    /// </summary>
    /// <example>
    /// The US, Canada, and Mexico are members of the USMCA.
    /// </example>
    /// <remarks>
    /// Many-to-many relationship: A country can belong to multiple geopolitical entities, and a
    /// geopolitical entity can include multiple countries.
    /// </remarks>
    public ICollection<Country> Countries { get; init; } = [];

    #endregion
}
