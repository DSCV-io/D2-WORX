namespace Geo.Domain.Entities;

/// <summary>
/// Represents a language.
/// </summary>
/// <remarks>
/// Is an entity of the Geography "Geo" Domain. A language can be spoken in multiple
/// <see cref="Country"/>(s) via the <see cref="Locale"/>(s) they have. Countries typically have a
/// primary language, but may also recognize additional languages.
/// </remarks>
public record Language
{
    #region Identity - Primary Key

    /// <summary>
    /// The ISO 639-1 code of the language.
    /// </summary>
    /// <example>
    /// es
    /// </example>
    /// <remarks>
    /// Must be unique. Always a 2-char string (letters).
    /// </remarks>
    public required string ISO6391Code { get; init; }

    #endregion

    #region Properties

    /// <summary>
    /// The display name of the language.
    /// </summary>
    /// <example>
    /// Spanish
    /// </example>
    /// <remarks>
    /// This is the name of the language in English.
    /// </remarks>
    public required string Name { get; init; }

    /// <summary>
    /// The endonym of the language.
    /// </summary>
    /// <example>
    /// Español
    /// </example>
    /// <remarks>
    /// This is the name of the language in the language itself.
    /// </remarks>
    public required string Endonym { get; init; }

    #endregion
}
