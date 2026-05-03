// -----------------------------------------------------------------------
// <copyright file="CleanEnumEmptyBehavior.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Utilities.Extensions;

/// <summary>
/// Behavior options controlling how
/// <see cref="EnumerableExtensions"/> handles a null/empty enumerable, either
/// before or after cleaning.
/// </summary>
public enum CleanEnumEmptyBehavior
{
    /// <summary>
    /// Return an empty enumerable.
    /// </summary>
    ReturnEmpty,

    /// <summary>
    /// Return null.
    /// </summary>
    ReturnNull,

    /// <summary>
    /// Throw an exception.
    /// </summary>
    Throw,
}
