// -----------------------------------------------------------------------
// <copyright file="CountryCurrencyAcceptance.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Spec;

/// <summary>
/// Nested record under <see cref="CountrySpec.Currencies"/>. Pairs a
/// currency alpha code with its acceptance level inside the country.
/// </summary>
/// <param name="Iso4217AlphaCode">ISO 4217 alpha code (e.g. <c>"USD"</c>).</param>
/// <param name="Level">
/// One of <c>"LegalTender"</c> / <c>"WidelyAccepted"</c> /
/// <c>"Tourist"</c>; emitters map to the matching enum value.
/// </param>
internal sealed record CountryCurrencyAcceptance(
    string Iso4217AlphaCode,
    string Level);
