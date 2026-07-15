// -----------------------------------------------------------------------
// <copyright file="IRequestContextGeoExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.Abstractions.Extensions;

using System;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Typed geo accessors layered over <see cref="IRequestContext"/>'s raw
/// <c>string?</c> WhoIs fields (<c>CountryIso31661Alpha2Code</c>,
/// <c>SubdivisionIso31662Code</c>, etc.). The context interface keeps the raw
/// strings for wire-serialization fidelity + minimal context-source-gen
/// surface; typed access is opt-in via these extensions.
/// </summary>
/// <remarks>
/// <para>
/// <b>Per-method behavior.</b> Every accessor parses the underlying raw
/// string via the matching typed <c>TryParse</c> (which consults the
/// closed-set validation table); returns <c>null</c> when the underlying
/// string is null / empty / whitespace OR when the value is not in the
/// catalog (defensive: a value could carry an out-of-date code from a
/// session minted before a catalog change). Handlers MUST treat <c>null</c>
/// as "geo signal absent" rather than re-deriving the raw alpha-2.
/// </para>
/// <para>
/// <b>What we expose.</b> The two WhoIs geo fields on
/// <see cref="IRequestContext"/>: <c>CountryIso31661Alpha2Code</c> +
/// <c>SubdivisionIso31662Code</c>.
/// </para>
/// </remarks>
public static class IRequestContextGeoExtensions
{
    extension(IRequestContext context)
    {
        /// <summary>
        /// Parses <see cref="IRequestContext.CountryIso31661Alpha2Code"/> (ISO 3166-1
        /// alpha-2 string from WhoIs enrichment) into the typed
        /// <see cref="CountryCode"/> enum. Returns <c>null</c> when the
        /// underlying string is null / empty / whitespace OR when the value
        /// is not present in the catalog.
        /// </summary>
        /// <returns>The typed country identifier, or <c>null</c>.</returns>
        public CountryCode? Country()
        {
            ArgumentNullException.ThrowIfNull(context);

            var raw = context.CountryIso31661Alpha2Code;
            if (raw.Falsey())
                return null;

            if (raw.TryParseTruthyNull<CountryCode>(out var parsed))
                return parsed;

            return null;
        }

        /// <summary>
        /// Parses <see cref="IRequestContext.SubdivisionIso31662Code"/> (ISO 3166-2
        /// string from WhoIs enrichment) into the typed
        /// <see cref="SubdivisionCode"/> wrapper. Returns <c>null</c> when
        /// the underlying string is null / empty / whitespace OR when the
        /// value is not present in the catalog. The raw string is
        /// uppercased before parsing so lowercase / mixed-case values
        /// (e.g. <c>"us-ny"</c>, <c>"Us-Ny"</c>) resolve to the canonical
        /// record — matching the cross-language lenient parser contract.
        /// </summary>
        /// <returns>The typed subdivision code, or <c>null</c>.</returns>
        public SubdivisionCode? Subdivision()
        {
            ArgumentNullException.ThrowIfNull(context);

            var raw = context.SubdivisionIso31662Code;
            if (raw.Falsey())
                return null;

            var normalized = raw!.ToUpperInvariant();
            return SubdivisionCode.TryParse(normalized, out var code)
                ? code
                : null;
        }
    }
}
