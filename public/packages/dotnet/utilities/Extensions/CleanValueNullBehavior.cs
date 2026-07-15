// -----------------------------------------------------------------------
// <copyright file="CleanValueNullBehavior.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Behavior options controlling how <see cref="EnumerableExtensions"/> handles
/// a null value produced by the cleaner function.
/// </summary>
public enum CleanValueNullBehavior
{
    /// <summary>
    /// Skip null values (default).
    /// </summary>
    RemoveNulls,

    /// <summary>
    /// Throw an <see cref="InvalidOperationException"/> when the cleaner
    /// returns null for any element.
    /// </summary>
    ThrowOnNull,
}
