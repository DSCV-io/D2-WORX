// -----------------------------------------------------------------------
// <copyright file="IRequestContextGeoExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Geo.Abstractions.Extensions;

using System;
using D2.Shared.Context.Abstractions;
using D2.Shared.Utilities.Extensions;

/// <summary>
/// Typed geo accessors layered over <see cref="IRequestContext"/>'s raw
/// <c>string?</c> WhoIs fields (<c>CountryCode</c>, <c>SubdivisionCode</c>,
/// etc.). The context interface keeps the raw strings for JWT-claim wire
/// fidelity + minimal context-source-gen surface; typed access is opt-in
/// via these extensions.
/// </summary>
/// <remarks>
/// <para>
/// <b>Per-method behavior.</b> Every accessor parses the underlying raw
/// string via the matching typed <c>TryParse</c> (which consults the
/// closed-set validation table); returns <c>null</c> when the underlying
/// string is null / empty / whitespace OR when the value is not in the
/// catalog (defensive: a JWT-claim could carry an out-of-date code from a
/// session minted before a catalog change). Handlers MUST treat <c>null</c>
/// as "geo signal absent" rather than re-deriving the raw alpha-2.
/// </para>
/// <para>
/// <b>What we expose.</b> Only the two geo fields that
/// <see cref="IRequestContext"/> currently surfaces from WhoIs enrichment:
/// <c>CountryCode</c> + <c>SubdivisionCode</c>. Locale / Timezone /
/// Currency accessors are deferred — the request-context spec does not
/// currently carry those fields (they live in user-profile / session
/// preference territory, not in WhoIs response data); add them here when
/// the spec adds them.
/// </para>
/// </remarks>
public static class IRequestContextGeoExtensions
{
    extension(IRequestContext context)
    {
        /// <summary>
        /// Parses <see cref="IRequestContext.CountryCode"/> (ISO 3166-1
        /// alpha-2 string from WhoIs enrichment) into the typed
        /// <see cref="CountryCode"/> enum. Returns <c>null</c> when the
        /// underlying string is null / empty / whitespace OR when the value
        /// is not present in the catalog.
        /// </summary>
        /// <returns>The typed country identifier, or <c>null</c>.</returns>
        public CountryCode? Country()
        {
            ArgumentNullException.ThrowIfNull(context);

            var raw = context.CountryCode;
            if (raw.Falsey())
                return null;

            if (raw.TryParseTruthyNull<CountryCode>(out var parsed) && parsed.HasValue)
                return parsed;

            return null;
        }

        /// <summary>
        /// Parses <see cref="IRequestContext.SubdivisionCode"/> (ISO 3166-2
        /// string from WhoIs enrichment) into the typed
        /// <see cref="SubdivisionCode"/> wrapper. Returns <c>null</c> when
        /// the underlying string is null / empty / whitespace OR when the
        /// value is not present in the catalog. The raw string is
        /// uppercased before parsing so lowercase / mixed-case JWT claims
        /// (e.g. <c>"us-ny"</c>, <c>"Us-Ny"</c>) resolve to the canonical
        /// record — matching the cross-language lenient parser contract.
        /// </summary>
        /// <returns>The typed subdivision code, or <c>null</c>.</returns>
        public SubdivisionCode? Subdivision()
        {
            ArgumentNullException.ThrowIfNull(context);

            var raw = context.SubdivisionCode;
            if (raw.Falsey())
                return null;

            var normalized = raw!.ToUpperInvariant();
            return SubdivisionCode.TryParse(normalized, out var code)
                ? code
                : null;
        }
    }
}
