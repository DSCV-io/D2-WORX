// -----------------------------------------------------------------------
// <copyright file="LocationVoDecorator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Location.EntityFrameworkCore;

using System.Security.Cryptography;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Location.ValueObjects;
using DcsvIo.D2.Validation.Abstractions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Internal single-source per-VO decoration core consumed by the per-VO
/// <c>MapXxx</c> complex-type helpers in <see cref="LocationMappingExtensions"/>.
/// Changing a field rule here updates the helper for every host that uses it.
/// </summary>
/// <remarks>
/// Anonymize defaults are written via the fluent <c>.Anonymize</c> /
/// <c>.AnonymizeNull</c> / <c>.AnonymizeEmpty</c> overloads on
/// <c>ComplexTypePropertyBuilder&lt;T&gt;</c> (the builder returned by
/// <c>cp.Property(lambda)</c>); each writes the <c>D2:Anonymize</c> model annotation read
/// by the anonymization engine on subject erasure.
/// </remarks>
internal static class LocationVoDecorator
{
    // =========================================================================
    // Private constants — caps not in FieldConstraints
    // =========================================================================

    // "v1." (3 chars) + SHA-256 hex (SHA256.HashSizeInBytes * 2 = 64 chars) = 67.
    private const int _HASH_ID_MAX = 3 + (SHA256.HashSizeInBytes * 2);

    // ISO 3166-2 subdivision codes are at most 6 chars (e.g. "GB-ENG"); cap at 8 for headroom.
    private const int _SUBDIVISION_CODE_MAX = 8;

    // ISO 3166-1 alpha-2 = exactly 2 chars.
    private const int _COUNTRY_CODE_MAX = 2;

    // Geohash-10 cap — mirrors the encoder-intrinsic 10-char geohash the Coordinates VO
    // normalizes to (no FieldConstraints catalog entry: this is an encoder invariant, not
    // a validation-policy cap).
    private const int _GEOHASH_MAX = 10;

    // OLC plus-code cap — mirrors the encoder-intrinsic 13-char plus-code the Coordinates VO
    // produces at codeLength 12 (8 pair digits + '+' + 4 grid digits); encoder invariant,
    // not a policy cap.
    private const int _PLUSCODE_MAX = 13;

    // Cleared HashId sentinel written on erasure: "v1." + 64 × '0'.
    private static readonly string sr_hashIdCleared =
        "v1." + new string('0', SHA256.HashSizeInBytes * 2);

    // =========================================================================
    // Exposed constants for test assertions
    // =========================================================================

    /// <summary>Gets the HashId cleared sentinel used on erasure.</summary>
    internal static string HashIdCleared => sr_hashIdCleared;

    /// <summary>
    /// Gets the <c>HasMaxLength</c> cap applied to
    /// <c>AdminLocation.SubdivisionIso31662Code</c> (ISO 3166-2 codes are at most
    /// 6 chars; capped at 8 for headroom).
    /// </summary>
    internal static int SubdivisionCodeMax => _SUBDIVISION_CODE_MAX;

    /// <summary>
    /// Gets the <c>HasMaxLength</c> cap applied to
    /// <c>AdminLocation.CountryIso31661Alpha2Code</c> (ISO 3166-1 alpha-2 = 2 chars).
    /// </summary>
    internal static int CountryCodeMax => _COUNTRY_CODE_MAX;

    /// <summary>
    /// Gets the <c>HasMaxLength</c> cap applied to <c>Coordinates.Geohash</c>
    /// (the encoder-intrinsic geohash-10 length).
    /// </summary>
    internal static int GeohashMax => _GEOHASH_MAX;

    /// <summary>
    /// Gets the <c>HasMaxLength</c> cap applied to <c>Coordinates.PlusCode</c>
    /// (the encoder-intrinsic plus-code length).
    /// </summary>
    internal static int PlusCodeMax => _PLUSCODE_MAX;

    // =========================================================================
    // ComplexProperty-shape decorators
    // =========================================================================

    /// <summary>
    /// Decorates a <c>StreetAddress</c> VO mapped as a <c>ComplexProperty</c>.
    /// </summary>
    /// <param name="cp">The complex-property builder for <c>StreetAddress</c>.</param>
    internal static void DecorateStreetAddress(ComplexPropertyBuilder<StreetAddress> cp)
    {
        cp.Property(a => a.Line1).HasMaxLength(FieldConstraints.STREET_LINE_MAX)
          .Anonymize("[deleted]");
        cp.Property(a => a.Line2).HasMaxLength(FieldConstraints.STREET_LINE_MAX)
          .AnonymizeNull();
        cp.Property(a => a.Line3).HasMaxLength(FieldConstraints.STREET_LINE_MAX)
          .AnonymizeNull();
        cp.Property(a => a.Line4).HasMaxLength(FieldConstraints.STREET_LINE_MAX)
          .AnonymizeNull();
        cp.Property(a => a.Line5).HasMaxLength(FieldConstraints.STREET_LINE_MAX)
          .AnonymizeNull();
        cp.Property(a => a.HashId).HasMaxLength(_HASH_ID_MAX)
          .Anonymize(sr_hashIdCleared);
    }

    /// <summary>
    /// Decorates an <c>AdminLocation</c> VO mapped as a <c>ComplexProperty</c>.
    /// <c>SubdivisionCode</c> (struct) and <c>CountryCode</c> (ushort enum) get
    /// string-form value converters. <c>CountryIso31661Alpha2Code</c> is kept — no
    /// anonymize call (coarse-grained, retained on erasure).
    /// </summary>
    /// <param name="cp">The complex-property builder for <c>AdminLocation</c>.</param>
    internal static void DecorateAdminLocation(ComplexPropertyBuilder<AdminLocation> cp)
    {
        cp.Property(a => a.City).HasMaxLength(FieldConstraints.CITY_MAX)
          .AnonymizeNull();
        cp.Property(a => a.PostalCode).HasMaxLength(FieldConstraints.POSTAL_CODE_MAX)
          .AnonymizeNull();

        // SubdivisionCode read side: null/empty → no subdivision; a non-empty value
        // (incl. whitespace) goes through FromString. The (v == null || v.Length == 0) form
        // is the precise null-or-empty test that is legal inside the converter expression
        // tree (an `is` pattern is not) and does NOT null a whitespace-only code.
        cp.Property(a => a.SubdivisionIso31662Code)
          .AnonymizeNull()
          .HasConversion(
              s => s == null ? null : s.Value.Value,
              v => v == null || v.Length == 0 ? null : SubdivisionCode.FromString(v))
          .HasMaxLength(_SUBDIVISION_CODE_MAX);

        // CountryCode: enum → alpha-2 string (human-readable, len 2).
        // Country is kept on erasure — coarse-grained, not anonymized.
        cp.Property(a => a.CountryIso31661Alpha2Code)
          .HasConversion<string>()
          .HasMaxLength(_COUNTRY_CODE_MAX);
        cp.Property(a => a.HashId).HasMaxLength(_HASH_ID_MAX)
          .Anonymize(sr_hashIdCleared);
    }

    /// <summary>
    /// Decorates a <c>Coordinates</c> VO mapped as a <c>ComplexProperty</c>. The required
    /// numeric lat/lon fields take a constant tombstone (<c>"0"</c>, coerced to <c>0.0</c>
    /// at erasure); the required geohash / plus-code strings clear to empty; the nullable
    /// accuracy clears to null; the HashId clears to the cleared sentinel.
    /// </summary>
    /// <param name="cp">The complex-property builder for <c>Coordinates</c>.</param>
    internal static void DecorateCoordinates(ComplexPropertyBuilder<Coordinates> cp)
    {
        cp.Property(c => c.Latitude).Anonymize("0");
        cp.Property(c => c.Longitude).Anonymize("0");
        cp.Property(c => c.Geohash).HasMaxLength(_GEOHASH_MAX)
          .AnonymizeEmpty();
        cp.Property(c => c.PlusCode).HasMaxLength(_PLUSCODE_MAX)
          .AnonymizeEmpty();
        cp.Property(c => c.AccuracyMeters).AnonymizeNull();
        cp.Property(c => c.HashId).HasMaxLength(_HASH_ID_MAX)
          .Anonymize(sr_hashIdCleared);
    }
}
