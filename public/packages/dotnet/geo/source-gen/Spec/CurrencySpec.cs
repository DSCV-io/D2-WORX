// -----------------------------------------------------------------------
// <copyright file="CurrencySpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Spec;

/// <summary>
/// One entry parsed from <c>currencies.spec.json</c>. Matches the JSON
/// shape one-to-one — emitters consume this directly to produce the
/// generated <c>Currency</c> record + per-instance static data.
/// </summary>
/// <param name="Iso4217AlphaCode">ISO 4217 alpha code (e.g. <c>"USD"</c>).</param>
/// <param name="Iso4217NumericCode">
/// ISO 4217 three-digit numeric code; null when the currency lacks a
/// numeric assignment.
/// </param>
/// <param name="DisplayName">English display name.</param>
/// <param name="DecimalPlaces">Number of fraction digits (0..4).</param>
/// <param name="Symbol">Display symbol; null when unavailable.</param>
/// <param name="IsActive">
/// True for currently-circulating currencies; false for historical ones.
/// </param>
/// <param name="IsSupported">
/// Derived flag — true when this currency is referenced by a selectable
/// locale's primary country's primary currency.
/// </param>
internal sealed record CurrencySpec(
    string Iso4217AlphaCode,
    string? Iso4217NumericCode,
    string DisplayName,
    int DecimalPlaces,
    string? Symbol,
    bool IsActive,
    bool IsSupported);
