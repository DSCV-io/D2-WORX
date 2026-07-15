// -----------------------------------------------------------------------
// <copyright file="Coordinates.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Location.ValueObjects;

using System.Security.Cryptography;
using System.Text;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Location.Encoding;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Immutable geographic point with three universal representations
/// (lat/lon decimal degrees, geohash-10, OLC plus-code-13) and optional
/// accuracy metadata. Every factory normalizes input to the canonical
/// bounding-box center of the geohash-10 cell so inputs in different forms
/// that represent the same physical ~1m grid cell produce byte-identical
/// <see cref="HashId"/> values.
/// </summary>
/// <remarks>
/// <para>
/// <b>Canonical hash input.</b> <see cref="Geohash"/> (10 characters,
/// ~1.2m × 0.6m at equator) is the canonical hash input because it is the
/// shortest of the three representations, has no URL-issue characters,
/// its lexicographic prefix equals spatial proximity, and it is an
/// industry standard. <see cref="HashId"/> is <c>"v1." + Sha256(Geohash)</c>.
/// </para>
/// <para>
/// <b>Accuracy semantics.</b> <see cref="AccuracyMeters"/> is METADATA
/// and is NOT included in <see cref="HashId"/>. Two <see cref="Coordinates"/>
/// instances with the same lat/lon but different accuracy values produce the
/// same hash — accuracy is descriptive, not identity-bearing.
/// </para>
/// <para>
/// <b>Self-redacting PII.</b> <see cref="Latitude"/>, <see cref="Longitude"/>,
/// <see cref="Geohash"/>, <see cref="PlusCode"/>, and <see cref="AccuracyMeters"/>
/// are marked <c>[RedactData(PersonalInformation)]</c> and are masked automatically
/// by the Serilog destructuring policy. <see cref="Geohash"/> is redacted because it
/// is a reversible spatial encoding — decoding a geohash-10 string recovers the
/// original lat/lon to within ~1 m, making it as precise as the raw coordinates.
/// <see cref="AccuracyMeters"/> is redacted because a tight accuracy radius combined
/// with any other logged context can narrow a subject's position precisely enough to
/// re-identify them. <see cref="HashId"/> is left visible because it is a one-way
/// SHA-256 digest of the geohash: opaque, non-reversible, and safe for correlation
/// in logs and traces without leaking position.
/// </para>
/// </remarks>
public sealed record Coordinates
{
    /// <summary>
    /// Gets latitude in decimal degrees, normalized to F6
    /// (~10 cm precision, matches geohash-10 cell-center).
    /// </summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public required double Latitude { get; init; }

    /// <summary>Gets longitude in decimal degrees, normalized to F6.</summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public required double Longitude { get; init; }

    /// <summary>
    /// Gets geohash-10 string (~1.2m × 0.6m at equator) — the canonical hash input.
    /// Base-32 alphabet <c>0123456789bcdefghjkmnpqrstuvwxyz</c>.
    /// Redacted in logs — a geohash is reversible: decoding it recovers the original
    /// lat/lon to within ~1 m, making it equivalent in precision to the raw coordinates.
    /// </summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public required string Geohash { get; init; }

    /// <summary>
    /// Gets OLC plus-code, 13 characters (8 pair digits + '+' + 4 grid digits), ~1m precision.
    /// </summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public required string PlusCode { get; init; }

    /// <summary>
    /// Gets optional accuracy metadata in meters; NOT included in <see cref="HashId"/>.
    /// Redacted in logs — a tight accuracy radius combined with other logged context
    /// can narrow a subject's position precisely enough to re-identify them.
    /// </summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public double? AccuracyMeters { get; init; }

    /// <summary>
    /// Gets stable hash identifier: <c>"v1." + SHA-256(Geohash)</c> as lowercase hex.
    /// Identical across all three input factories for the same canonical cell.
    /// Emitted unredacted in logs — it is a one-way SHA-256 digest (opaque,
    /// non-reversible) and is safe for correlation in logs and traces without
    /// leaking position.
    /// </summary>
    public required string HashId { get; init; }

    /// <summary>
    /// Creates a <see cref="Coordinates"/> from decimal-degree lat/lon values.
    /// </summary>
    /// <param name="latitude">Latitude (must be finite, in [−90, +90]).</param>
    /// <param name="longitude">Longitude (must be finite, in [−180, +180]).</param>
    /// <param name="accuracyMeters">
    /// Optional accuracy metadata (must be finite and non-negative when supplied).
    /// </param>
    /// <returns>
    /// <c>Ok</c> on success;
    /// <see cref="D2Result{TData}.ValidationFailed"/> on range / finite violations.
    /// </returns>
    public static D2Result<Coordinates> Create(
        double latitude,
        double longitude,
        double? accuracyMeters = null)
    {
        if (!double.IsFinite(latitude))
        {
            return D2Result<Coordinates>.ValidationFailed(
                messages: [TK.Geo.Validation.COORDINATES_FINITE_REQUIRED]);
        }

        if (!double.IsFinite(longitude))
        {
            return D2Result<Coordinates>.ValidationFailed(
                messages: [TK.Geo.Validation.COORDINATES_FINITE_REQUIRED]);
        }

        if (latitude is < -90.0 or > 90.0)
        {
            return D2Result<Coordinates>.ValidationFailed(
                messages: [TK.Geo.Validation.LATITUDE_RANGE]);
        }

        if (longitude is < -180.0 or > 180.0)
        {
            return D2Result<Coordinates>.ValidationFailed(
                messages: [TK.Geo.Validation.LONGITUDE_RANGE]);
        }

        if (accuracyMeters is { } acc && (!double.IsFinite(acc) || acc < 0.0))
        {
            return D2Result<Coordinates>.ValidationFailed(
                messages: [TK.Geo.Validation.COORDINATES_FINITE_REQUIRED]);
        }

        return BuildFromLatLon(latitude, longitude, accuracyMeters);
    }

    /// <summary>
    /// Creates a <see cref="Coordinates"/> from a geohash string, normalizing to the
    /// geohash-10 canonical cell-center.
    /// </summary>
    /// <param name="geohash">
    /// A geohash string using the base-32 alphabet
    /// (<c>0123456789bcdefghjkmnpqrstuvwxyz</c>), length 1–12.
    /// Strings longer than 10 characters are truncated to 10.
    /// Strings shorter than 10 characters are decoded to cell-center and re-encoded at 10.
    /// </param>
    /// <param name="accuracyMeters">Optional accuracy metadata.</param>
    /// <returns>
    /// <c>Ok</c> on success;
    /// <see cref="D2Result{TData}.ValidationFailed"/> when the input is empty or uses
    /// non-geohash characters.
    /// </returns>
    public static D2Result<Coordinates> FromGeohash(
        string geohash,
        double? accuracyMeters = null)
    {
        if (geohash.Falsey())
        {
            return D2Result<Coordinates>.ValidationFailed(
                messages: [TK.Geo.Validation.COORDINATES_GEOHASH_INVALID]);
        }

        // Validate: all characters must be in the geohash base-32 alphabet.
        // Valid: 0-9, b-h, j, k, m, n, p-z (lower-case). Pattern: ^[0-9b-hjkmnp-z]{1,12}$
        if (!IsValidGeohashString(geohash))
        {
            return D2Result<Coordinates>.ValidationFailed(
                messages: [TK.Geo.Validation.COORDINATES_GEOHASH_INVALID]);
        }

        if (accuracyMeters is { } acc && (!double.IsFinite(acc) || acc < 0.0))
        {
            return D2Result<Coordinates>.ValidationFailed(
                messages: [TK.Geo.Validation.COORDINATES_FINITE_REQUIRED]);
        }

        // Normalize: truncate (if > 10 chars) or pad to 10 chars via decode+re-encode.
        var normalized = GeohashEncoder.TruncateOrPad(geohash);
        var (centerLat, centerLon, _, _) = GeohashEncoder.Decode(normalized);

        return BuildFromLatLon(centerLat, centerLon, accuracyMeters);
    }

    /// <summary>
    /// Creates a <see cref="Coordinates"/> from an OLC plus-code string.
    /// </summary>
    /// <param name="plusCode">
    /// A valid Open Location Code (plus-code) string. Must contain a single '+' separator
    /// and use only OLC-alphabet characters (<c>23456789CFGHJMPQRVWX</c>).
    /// </param>
    /// <param name="accuracyMeters">Optional accuracy metadata.</param>
    /// <returns>
    /// <c>Ok</c> on success;
    /// <see cref="D2Result{TData}.ValidationFailed"/> when the input is empty or not a
    /// valid OLC plus-code.
    /// </returns>
    public static D2Result<Coordinates> FromPlusCode(
        string plusCode,
        double? accuracyMeters = null)
    {
        if (plusCode.Falsey())
        {
            return D2Result<Coordinates>.ValidationFailed(
                messages: [TK.Geo.Validation.COORDINATES_PLUSCODE_INVALID]);
        }

        if (!PlusCodeEncoder.IsValid(plusCode))
        {
            return D2Result<Coordinates>.ValidationFailed(
                messages: [TK.Geo.Validation.COORDINATES_PLUSCODE_INVALID]);
        }

        if (accuracyMeters is { } acc && (!double.IsFinite(acc) || acc < 0.0))
        {
            return D2Result<Coordinates>.ValidationFailed(
                messages: [TK.Geo.Validation.COORDINATES_FINITE_REQUIRED]);
        }

        var (centerLat, centerLon, _, _) = PlusCodeEncoder.Decode(plusCode);

        return BuildFromLatLon(centerLat, centerLon, accuracyMeters);
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Common pipeline: normalize lat/lon to geohash-10 cell-center → compute all
    /// three reps → compute HashId.
    /// </summary>
    private static D2Result<Coordinates> BuildFromLatLon(
        double latitude,
        double longitude,
        double? accuracyMeters)
    {
        // Snap to geohash-10 cell-center (canonical ~1m grid).
        var geohash = GeohashEncoder.Encode(latitude, longitude, precision: 10);
        var (centerLat, centerLon, _, _) = GeohashEncoder.Decode(geohash);

        // Round to F6 (~10 cm precision) to produce a clean stored value.
        centerLat = Math.Round(centerLat, 6, MidpointRounding.AwayFromZero);
        centerLon = Math.Round(centerLon, 6, MidpointRounding.AwayFromZero);

        // Encode plus-code at 12 significant digits (→ 13-char string with '+').
        var plusCode = PlusCodeEncoder.Encode(centerLat, centerLon, codeLength: 12);

        // HashId: "v1." + SHA-256(Geohash) — BCL static one-shot per §15.8.
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(geohash));
        var hashId = "v1." + Convert.ToHexStringLower(hashBytes);

        return D2Result<Coordinates>.Ok(new Coordinates
        {
            Latitude = centerLat,
            Longitude = centerLon,
            Geohash = geohash,
            PlusCode = plusCode,
            AccuracyMeters = accuracyMeters,
            HashId = hashId,
        });
    }

    /// <summary>
    /// Returns <c>true</c> when every character in <paramref name="s"/> belongs to the
    /// geohash base-32 alphabet and the length is between 1 and 12.
    /// </summary>
    private static bool IsValidGeohashString(string s)
    {
        if (s.Length is < 1 or > 12)
            return false;

        foreach (var c in s)
        {
            // Valid geohash characters: 0-9, b-h, j, k, m, n, p-z (lower-case only).
            // Excludes: a, i, l, o (visually ambiguous).
            if (!IsGeohashChar(c))
                return false;
        }

        return true;
    }

    private static bool IsGeohashChar(char c) =>
        c is >= '0' and <= '9'
        or >= 'b' and <= 'h'
        or 'j' or 'k'
        or 'm' or 'n'
        or >= 'p' and <= 'z';
}
