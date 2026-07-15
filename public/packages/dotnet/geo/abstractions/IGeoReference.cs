// -----------------------------------------------------------------------
// <copyright file="IGeoReference.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.Abstractions;

/// <summary>
/// Strongly-typed lookup contract for the seven reference-data catalogs.
/// Every method takes a typed identifier (real enum or wrapper struct)
/// and returns the matching non-nullable single-shape record — the type
/// system enforces the absence of a NotFound branch because the typed
/// identifier IS the catalog (every enum member maps to exactly one
/// entity; every wrapper struct value comes through closed-set validation
/// at the deserialization boundary).
/// </summary>
/// <remarks>
/// <para>
/// Inputs use the <c>*Code</c> suffix
/// (<see cref="CountryCode"/>, <see cref="CurrencyCode"/>,
/// <see cref="LanguageCode"/>, <see cref="GeopoliticalEntityCode"/>) for
/// closed-set enums and the wrapper struct names
/// (<see cref="SubdivisionCode"/>, <see cref="LocaleCode"/>,
/// <see cref="TimezoneCode"/>) for open-set codes.
/// </para>
/// <para>
/// String-input cousins (e.g. <c>TryGetCountry(string alpha2)</c>) and
/// free-form name resolution (<see cref="NameResolution.IGeoNameResolver"/>)
/// live outside this interface — they are boundary-code concerns and
/// MUST NOT be called from domain handlers.
/// </para>
/// </remarks>
public interface IGeoReference
{
    /// <summary>
    /// Resolves the full denormalized record for <paramref name="code"/>.
    /// </summary>
    /// <param name="code">The typed ISO 3166-1 alpha-2 identifier.</param>
    /// <returns>The country record (always non-null).</returns>
    Country GetCountry(CountryCode code);

    /// <summary>
    /// Resolves the full denormalized record for <paramref name="code"/>.
    /// </summary>
    /// <param name="code">The wrapped ISO 3166-2 subdivision code.</param>
    /// <returns>The subdivision record (always non-null).</returns>
    Subdivision GetSubdivision(SubdivisionCode code);

    /// <summary>
    /// Resolves the full denormalized record for <paramref name="code"/>.
    /// </summary>
    /// <param name="code">The wrapped IANA timezone identifier.</param>
    /// <returns>The timezone record (always non-null).</returns>
    Timezone GetTimezone(TimezoneCode code);

    /// <summary>
    /// Resolves the full denormalized record for <paramref name="code"/>.
    /// </summary>
    /// <param name="code">The wrapped IETF BCP 47 locale tag.</param>
    /// <returns>The locale record (always non-null).</returns>
    Locale GetLocale(LocaleCode code);

    /// <summary>
    /// Resolves the full denormalized record for <paramref name="code"/>.
    /// </summary>
    /// <param name="code">The typed ISO 4217 alpha identifier.</param>
    /// <returns>The currency record (always non-null).</returns>
    Currency GetCurrency(CurrencyCode code);

    /// <summary>
    /// Resolves the full denormalized record for <paramref name="code"/>.
    /// </summary>
    /// <param name="code">The typed ISO 639-1 identifier.</param>
    /// <returns>The language record (always non-null).</returns>
    Language GetLanguage(LanguageCode code);

    /// <summary>
    /// Resolves the full denormalized record for <paramref name="code"/>.
    /// </summary>
    /// <param name="code">The typed geopolitical-entity identifier.</param>
    /// <returns>The geopolitical-entity record (always non-null).</returns>
    GeopoliticalEntity GetGeopoliticalEntity(GeopoliticalEntityCode code);
}
