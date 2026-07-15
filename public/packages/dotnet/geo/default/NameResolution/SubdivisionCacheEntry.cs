// -----------------------------------------------------------------------
// <copyright file="SubdivisionCacheEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.Default.NameResolution;

using DcsvIo.D2.Geo.Abstractions;

/// <summary>
/// Single dictionary value for the per-country subdivision name cache.
/// Carries either a record reference or an ambiguity sentinel; the two
/// fields publish atomically via the single-struct-write guarantee.
/// </summary>
/// <param name="Record">
/// The matched subdivision record, or <c>null</c> on ambiguity.
/// </param>
/// <param name="IsAmbiguous">
/// Gets a value indicating whether this entry marks an ambiguous normalized name.
/// </param>
internal readonly record struct SubdivisionCacheEntry(
    Subdivision? Record,
    bool IsAmbiguous) : ICacheEntry;
