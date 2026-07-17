// -----------------------------------------------------------------------
// <copyright file="GeoSpecContext.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Spec;

/// <summary>
/// Aggregate view of all geo spec files parsed in a single generator run.
/// Each property exposes one parsed catalog (or <c>null</c> when the
/// corresponding spec file was absent / failed to parse — the loader
/// surfaces the failure as a diagnostic, and downstream emitters degrade
/// gracefully on a null catalog).
/// </summary>
/// <param name="Countries">Parsed <c>countries.spec.json</c>, or <c>null</c>.</param>
/// <param name="Subdivisions">Parsed <c>subdivisions.spec.json</c>, or <c>null</c>.</param>
/// <param name="Currencies">Parsed <c>currencies.spec.json</c>, or <c>null</c>.</param>
/// <param name="Languages">Parsed <c>languages.spec.json</c>, or <c>null</c>.</param>
/// <param name="Locales">Parsed <c>locales.spec.json</c>, or <c>null</c>.</param>
/// <param name="Timezones">Parsed <c>timezones.spec.json</c>, or <c>null</c>.</param>
/// <param name="GeopoliticalEntities">
/// Parsed <c>geopolitical-entities.spec.json</c>, or <c>null</c>.
/// </param>
internal sealed record GeoSpecContext(
    SpecEnvelope<CountrySpec>? Countries,
    SpecEnvelope<SubdivisionSpec>? Subdivisions,
    SpecEnvelope<CurrencySpec>? Currencies,
    SpecEnvelope<LanguageSpec>? Languages,
    SpecEnvelope<LocaleSpec>? Locales,
    SpecEnvelope<TimezoneSpec>? Timezones,
    SpecEnvelope<GeopoliticalEntitySpec>? GeopoliticalEntities);
