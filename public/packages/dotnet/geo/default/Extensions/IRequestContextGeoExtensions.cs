// -----------------------------------------------------------------------
// <copyright file="IRequestContextGeoExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.Default.Extensions;

using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Geo.Abstractions;
using AbsExt = DcsvIo.D2.Geo.Abstractions.Extensions.IRequestContextGeoExtensions;

/// <summary>
/// Default-layer record-returning wrappers over <see cref="IRequestContext"/>
/// geo fields. Returns the full typed record (e.g. <see cref="Country"/>),
/// not the typed code, so callers can read nested data
/// (<c>request.Country()?.PrimaryLanguage?.DisplayName</c>) without a second
/// catalog lookup. Consumers wanting only the typed code without catalog
/// access use the Abstractions-layer extension under
/// <c>DcsvIo.D2.Geo.Abstractions.Extensions</c>.
/// </summary>
/// <remarks>
/// <para>
/// The Abstractions-layer and Default-layer extensions share the same
/// method names (<c>Country()</c>, <c>Subdivision()</c>) on the same
/// receiver type (<see cref="IRequestContext"/>). Pick exactly one
/// namespace per call site via <c>using</c>: importing both produces
/// CS0121 (ambiguous reference) at compile time. The PATTERNS.md
/// namespace-disambiguated-extension entry documents this idiom.
/// </para>
/// <para>
/// <b>Logging guidance for callers.</b> The fields these accessors return
/// derive from upstream IP-geolocation enrichment. Display strings such
/// as <c>DisplayName</c> are not PII themselves but their context (a
/// session resolving to a specific country) can be. When logging from a
/// request context prefer the canonical <c>Iso31661Alpha2Code</c> /
/// <c>Iso31662Code</c> (short, stable, audit-friendly) over the
/// free-form <c>DisplayName</c> to keep log shapes stable and reduce
/// locale coupling.
/// </para>
/// <para>
/// <b>Boundary contract.</b> Both methods are pure wrappers over the
/// Abstractions-layer parser. They never normalize the raw string
/// themselves (no trim / lowercase / sanitize); the parser owns the
/// boundary contract.
/// </para>
/// </remarks>
public static class IRequestContextGeoExtensions
{
    extension(IRequestContext context)
    {
        /// <summary>
        /// Returns the full <see cref="Country"/> record for the
        /// <see cref="IRequestContext"/>'s <c>CountryIso31661Alpha2Code</c>
        /// raw string, or <c>null</c> if the raw string is absent / empty /
        /// whitespace / unparseable / unknown to the catalog.
        /// </summary>
        /// <returns>The matched <see cref="Country"/> record, or <c>null</c>.</returns>
        public Country? Country()
        {
            var code = AbsExt.Country(context);
            return code.HasValue && CountryLookup.ByCode.TryGetValue(code.Value, out var country)
                ? country
                : null;
        }

        /// <summary>
        /// Returns the full <see cref="Subdivision"/> record for the
        /// <see cref="IRequestContext"/>'s <c>SubdivisionIso31662Code</c>
        /// raw string, or <c>null</c> if the raw string is absent / empty /
        /// whitespace / unparseable / unknown to the catalog.
        /// </summary>
        /// <returns>The matched <see cref="Subdivision"/> record, or <c>null</c>.</returns>
        public Subdivision? Subdivision()
        {
            var code = AbsExt.Subdivision(context);
            return code.HasValue && SubdivisionLookup.ByCode.TryGetValue(code.Value, out var sub)
                ? sub
                : null;
        }
    }
}
