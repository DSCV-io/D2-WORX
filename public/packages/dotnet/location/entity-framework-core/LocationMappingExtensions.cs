// -----------------------------------------------------------------------
// <copyright file="LocationMappingExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Location.EntityFrameworkCore;

using DcsvIo.D2.Location.ValueObjects;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Per-VO complex-type mapping helpers for the DcsvIo.D2.Location value objects
/// (<c>StreetAddress</c>, <c>AdminLocation</c>, <c>Coordinates</c>). Called from the
/// host's <c>IEntityTypeConfiguration&lt;T&gt;</c> inside a <c>b.ComplexProperty</c>
/// callback. Each helper: wires <c>HasMaxLength</c> from <c>FieldConstraints.*</c> (plus
/// the encoder-intrinsic geohash / plus-code caps), applies any necessary value converters
/// (CountryCode, SubdivisionCode), and writes the per-field anonymize defaults via the
/// fluent <c>.Anonymize*</c> API.
/// </summary>
/// <remarks>
/// <para>
/// The host keeps the domain aggregate completely free of EF references — a VO-typed
/// property on a host entity is a plain CLR property; all mapping lives in the infra
/// <c>IEntityTypeConfiguration&lt;T&gt;</c> class.
/// </para>
/// <para>
/// <b>Same-VO-type-twice</b> (e.g. billing + shipping <c>AdminLocation</c>): call the
/// helper twice via two distinct host-property selectors. EF Core 10 prefixes complex
/// columns by the owning-property path automatically, producing distinct column sets
/// (<c>BillingLocation_City</c> vs <c>ShippingLocation_City</c>). The helpers never call
/// <c>HasColumnName</c>, which preserves this default uniquification.
/// </para>
/// </remarks>
public static class LocationMappingExtensions
{
    // =========================================================================
    // ComplexPropertyBuilder<StreetAddress>
    // =========================================================================
    extension(ComplexPropertyBuilder<StreetAddress> builder)
    {
        /// <summary>
        /// Configures a <c>StreetAddress</c> complex-property column set: wires
        /// <c>HasMaxLength</c> on all five address lines and <c>HashId</c>, and
        /// writes the per-field anonymize defaults.
        /// Anonymize defaults: Line1 → <c>"[deleted]"</c> (constant); Line2–5 → SetNull;
        /// HashId → cleared sentinel.
        /// </summary>
        /// <returns>The same builder for fluent chaining.</returns>
        public ComplexPropertyBuilder<StreetAddress> MapStreetAddress()
        {
            LocationVoDecorator.DecorateStreetAddress(builder);
            return builder;
        }
    }

    // =========================================================================
    // ComplexPropertyBuilder<AdminLocation>
    // =========================================================================
    extension(ComplexPropertyBuilder<AdminLocation> builder)
    {
        /// <summary>
        /// Configures an <c>AdminLocation</c> complex-property column set: wires
        /// <c>HasMaxLength</c>, value converters for <c>SubdivisionCode</c> (struct →
        /// string) and <c>CountryCode</c> (enum → alpha-2 string), and the per-field
        /// anonymize defaults.
        /// Anonymize defaults: City/PostalCode/Subdivision → SetNull;
        /// Country → <b>KEPT</b> (coarse-grained, no annotation);
        /// HashId → cleared sentinel.
        /// </summary>
        /// <remarks>
        /// An empty subdivision string read from the database is materialized as
        /// <see langword="null"/>, never as <c>SubdivisionCode("")</c>.
        /// </remarks>
        /// <returns>The same builder for fluent chaining.</returns>
        public ComplexPropertyBuilder<AdminLocation> MapAdminLocation()
        {
            LocationVoDecorator.DecorateAdminLocation(builder);
            return builder;
        }
    }

    // =========================================================================
    // ComplexPropertyBuilder<Coordinates>
    // =========================================================================
    extension(ComplexPropertyBuilder<Coordinates> builder)
    {
        /// <summary>
        /// Configures a <c>Coordinates</c> complex-property column set: wires
        /// <c>HasMaxLength</c> on the <c>Geohash</c>, <c>PlusCode</c>, and <c>HashId</c>
        /// string members, and writes the per-field anonymize defaults.
        /// Anonymize defaults: Latitude/Longitude → constant <c>"0"</c> (coerced to
        /// <c>0.0</c> at erasure); Geohash/PlusCode → SetEmpty; AccuracyMeters → SetNull;
        /// HashId → cleared sentinel.
        /// </summary>
        /// <returns>The same builder for fluent chaining.</returns>
        public ComplexPropertyBuilder<Coordinates> MapCoordinates()
        {
            LocationVoDecorator.DecorateCoordinates(builder);
            return builder;
        }
    }
}
