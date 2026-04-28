// -----------------------------------------------------------------------
// <copyright file="GuidExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Utilities.Extensions;

/// <summary>
/// Extension methods for GUIDs.
/// </summary>
public static class GuidExtensions
{
    /// <summary>
    /// Extension methods for GUIDs.
    /// </summary>
    ///
    /// <param name="guid">
    /// The GUID.
    /// </param>
    extension(Guid? guid)
    {
        /// <summary>
        /// Checks if a nullable GUID is "truthy" (has value and is not empty).
        /// </summary>
        ///
        /// <returns>
        /// Whether the nullable GUID is truthy.
        /// </returns>
        public bool Truthy() => guid.HasValue && guid.Value != Guid.Empty;

        /// <summary>
        /// Checks if a nullable GUID is "falsey" (no value or is empty).
        /// </summary>
        ///
        /// <returns>
        /// Whether the nullable GUID is falsey.
        /// </returns>
        public bool Falsey() => !guid.HasValue || guid.Value == Guid.Empty;
    }

    /// <summary>
    /// Extension methods for GUIDs.
    /// </summary>
    ///
    /// <param name="guid">
    /// The GUID.
    /// </param>
    extension(Guid guid)
    {
        /// <summary>
        /// Checks if a GUID is "truthy" (not empty).
        /// </summary>
        ///
        /// <returns>
        /// Whether the GUID is truthy.
        /// </returns>
        public bool Truthy() => guid != Guid.Empty;

        /// <summary>
        /// Checks if a GUID is "falsey" (is empty).
        /// </summary>
        ///
        /// <returns>
        /// Whether the GUID is falsey.
        /// </returns>
        public bool Falsey() => guid == Guid.Empty;
    }
}
