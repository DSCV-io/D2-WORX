namespace D2.Contracts.Utilities;

/// <summary>
/// Extension methods for GUIDs.
/// </summary>
public static class GuidExtensions
{
    /// <summary>
    /// Checks if a GUID is "truthy" (not null and not an empty GUID).
    /// </summary>
    ///
    /// <param name="guid">
    /// The GUID.
    /// </param>
    ///
    /// <returns>
    /// Whether the GUID is truthy.
    /// </returns>
    public static bool Truthy(this Guid? guid) => guid.HasValue && guid.Value != Guid.Empty;

    /// <summary>
    /// Checks if a GUID is "truthy" (not an empty GUID).
    /// </summary>
    ///
    /// <param name="guid">
    /// The GUID.
    /// </param>
    ///
    /// <returns>
    /// Whether the GUID is truthy.
    /// </returns>
    public static bool Truthy(this Guid guid) => guid != Guid.Empty;

    /// <summary>
    /// Checks if a GUID is "falsey" (null or an empty GUID).
    /// </summary>
    ///
    /// <param name="guid">
    /// The GUID.
    /// </param>
    ///
    /// <returns>
    /// Whether the GUID is falsey.
    /// </returns>
    public static bool Falsey(this Guid? guid) => !guid.Truthy();

    /// <summary>
    /// Checks if a GUID is "falsey" (an empty GUID).
    /// </summary>
    ///
    /// <param name="guid">
    /// The GUID.
    /// </param>
    ///
    /// <returns>
    /// Whether the GUID is falsey.
    /// </returns>
    public static bool Falsey(this Guid guid) => !guid.Truthy();
}
