// -----------------------------------------------------------------------
// <copyright file="GuidExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Extension methods for <see cref="Guid"/> covering the
/// <c>Truthy</c> / <c>Falsey</c> semantics used elsewhere in the codebase
/// (a GUID is "truthy" when it is non-null and not <see cref="Guid.Empty"/>),
/// plus a string-side <c>TryParseTruthyNull</c> for the common
/// "parse to <see cref="Guid"/>?, collapse missing / unparseable / empty
/// inputs to <c>null</c>" pattern.
/// </summary>
public static class GuidExtensions
{
    /// <param name="guid">The nullable GUID being checked.</param>
    extension(Guid? guid)
    {
        /// <summary>
        /// Returns true when the nullable GUID has a value AND that value is
        /// not <see cref="Guid.Empty"/>.
        /// </summary>
        public bool Truthy() => guid is not null && (Guid)guid != Guid.Empty;

        /// <summary>
        /// Returns true when the nullable GUID is null OR equal to
        /// <see cref="Guid.Empty"/>.
        /// </summary>
        public bool Falsey() => guid is null || (Guid)guid == Guid.Empty;
    }

    /// <param name="guid">The GUID being checked.</param>
    extension(Guid guid)
    {
        /// <summary>
        /// Returns true when the GUID is not <see cref="Guid.Empty"/>.
        /// </summary>
        public bool Truthy() => guid != Guid.Empty;

        /// <summary>
        /// Returns true when the GUID equals <see cref="Guid.Empty"/>.
        /// </summary>
        public bool Falsey() => guid == Guid.Empty;
    }

    /// <param name="input">The string to parse.</param>
    extension(string? input)
    {
        /// <summary>
        /// Tries to parse <paramref name="input"/> as a non-empty
        /// <see cref="Guid"/>. Returns <c>true</c> + the parsed value when the
        /// input is a valid GUID literal AND the parsed value is not
        /// <see cref="Guid.Empty"/>; returns <c>false</c> + <c>null</c> when
        /// the input is null, empty, whitespace, unparseable, OR resolves to
        /// <see cref="Guid.Empty"/>.
        /// </summary>
        /// <param name="result">
        /// The parsed GUID on success; <c>null</c> on failure.
        /// </param>
        /// <returns>
        /// True on parse success of a non-empty GUID; false otherwise.
        /// </returns>
        public bool TryParseTruthyNull(out Guid? result)
        {
            if (Guid.TryParse(input, out var parsed) && parsed.Truthy())
            {
                result = parsed;
                return true;
            }

            result = null;
            return false;
        }
    }
}
