// -----------------------------------------------------------------------
// <copyright file="ICacheEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.Default.NameResolution;

/// <summary>
/// Cache-entry contract — both record-presence and ambiguity-flag fields
/// publish atomically because they live in one dictionary value type.
/// Internal so test code can introspect via the existing
/// <c>InternalsVisibleTo</c> declaration on this assembly.
/// </summary>
internal interface ICacheEntry
{
    /// <summary>
    /// Gets a value indicating whether this entry marks an ambiguous normalized name.
    /// </summary>
    bool IsAmbiguous { get; }
}
