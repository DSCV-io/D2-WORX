// -----------------------------------------------------------------------
// <copyright file="SubdivisionCacheEntry.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Geo.Default.NameResolution;

using D2.Shared.Geo.Abstractions;

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
