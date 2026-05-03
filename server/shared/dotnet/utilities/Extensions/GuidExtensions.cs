// -----------------------------------------------------------------------
// <copyright file="GuidExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Utilities.Extensions;

/// <summary>
/// Extension methods for <see cref="Guid"/> that mirror the
/// <c>Truthy</c> / <c>Falsey</c> semantics used elsewhere in the codebase:
/// a GUID is "truthy" when it is non-null and not equal to
/// <see cref="Guid.Empty"/>.
/// </summary>
public static class GuidExtensions
{
    /// <param name="guid">The nullable GUID being checked.</param>
    extension(Guid? guid)
    {
        /// <summary>
        /// Returns true when the nullable GUID has a value AND that value is not
        /// <see cref="Guid.Empty"/>.
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
}
