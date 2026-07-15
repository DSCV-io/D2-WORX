// -----------------------------------------------------------------------
// <copyright file="CleanEnumEmptyBehavior.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Utilities.Extensions;

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
