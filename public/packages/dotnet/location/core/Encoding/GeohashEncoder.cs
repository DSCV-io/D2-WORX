// -----------------------------------------------------------------------
// <copyright file="GeohashEncoder.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Location.Encoding;

/// <summary>
/// Internal geohash encoder / decoder using the Niemeyer base-32 alphabet.
/// Algorithm: bit-interleave longitude (even bits) and latitude (odd bits),
/// then map 5-bit chunks to the base-32 alphabet.
/// </summary>
/// <remarks>
/// Reference: https://en.wikipedia.org/wiki/Geohash — Niemeyer 2008.
/// Base-32 alphabet is <c>0123456789bcdefghjkmnpqrstuvwxyz</c>
/// (32 characters: digits + lowercase letters excluding <c>a</c>, <c>i</c>,
/// <c>l</c>, <c>o</c> to prevent visually-ambiguous characters).
/// </remarks>
internal static class GeohashEncoder
{
    // Geohash base-32 alphabet (excludes a, i, l, o).
    private const string _ALPHABET = "0123456789bcdefghjkmnpqrstuvwxyz";

    // Lookup table from base-32 char → 5-bit value (for decode).
    // Initialized in static constructor below.
    private static readonly int[] sr_Lookup = new int[128];

    // Each char encodes exactly 5 bits. Lon gets bits at even positions
    // (counting from MSB of the bit-stream), lat at odd positions.
    static GeohashEncoder()
    {
        for (var i = 0; i < sr_Lookup.Length; i++)
            sr_Lookup[i] = -1;

        for (var i = 0; i < _ALPHABET.Length; i++)
            sr_Lookup[_ALPHABET[i]] = i;
    }

    /// <summary>
    /// Encodes a geographic coordinate pair to a geohash string.
    /// </summary>
    /// <param name="latitude">Latitude in decimal degrees (−90 to +90).</param>
    /// <param name="longitude">Longitude in decimal degrees (−180 to +180).</param>
    /// <param name="precision">Number of geohash characters (1–12). Default 10.</param>
    /// <returns>Lowercase geohash string of <paramref name="precision"/> characters.</returns>
    public static string Encode(double latitude, double longitude, int precision = 10)
    {
        var chars = new char[precision];
        var latMin = -90.0;
        var latMax = 90.0;
        var lonMin = -180.0;
        var lonMax = 180.0;

        var bits = 0;
        var isLon = true;
        var bitIdx = 0;
        var charIdx = 0;

        while (charIdx < precision)
        {
            double mid;
            if (isLon)
            {
                mid = (lonMin + lonMax) / 2.0;
                if (longitude >= mid)
                {
                    bits = (bits << 1) | 1;
                    lonMin = mid;
                }
                else
                {
                    bits <<= 1;
                    lonMax = mid;
                }
            }
            else
            {
                mid = (latMin + latMax) / 2.0;
                if (latitude >= mid)
                {
                    bits = (bits << 1) | 1;
                    latMin = mid;
                }
                else
                {
                    bits <<= 1;
                    latMax = mid;
                }
            }

            isLon = !isLon;
            bitIdx++;

            if (bitIdx == 5)
            {
                chars[charIdx++] = _ALPHABET[bits];
                bits = 0;
                bitIdx = 0;
            }
        }

        return new string(chars);
    }

    /// <summary>
    /// Decodes a geohash string to the bounding-box center and error margins.
    /// </summary>
    /// <param name="geohash">A valid geohash string (base-32 alphabet, length 1–12).</param>
    /// <returns>
    /// Center latitude, center longitude, and the half-span error in each axis.
    /// </returns>
    public static (double Latitude, double Longitude, double LatError, double LonError)
        Decode(string geohash)
    {
        var latMin = -90.0;
        var latMax = 90.0;
        var lonMin = -180.0;
        var lonMax = 180.0;

        var isLon = true;

        foreach (var c in geohash)
        {
            var val = sr_Lookup[c];

            // 5 bits per character, MSB first.
            for (var bit = 4; bit >= 0; bit--)
            {
                var bitVal = (val >> bit) & 1;
                double mid;

                if (isLon)
                {
                    mid = (lonMin + lonMax) / 2.0;
                    if (bitVal == 1)
                        lonMin = mid;
                    else
                        lonMax = mid;
                }
                else
                {
                    mid = (latMin + latMax) / 2.0;
                    if (bitVal == 1)
                        latMin = mid;
                    else
                        latMax = mid;
                }

                isLon = !isLon;
            }
        }

        var centerLat = (latMin + latMax) / 2.0;
        var centerLon = (lonMin + lonMax) / 2.0;
        var latError = (latMax - latMin) / 2.0;
        var lonError = (lonMax - lonMin) / 2.0;

        return (centerLat, centerLon, latError, lonError);
    }

    /// <summary>
    /// Truncates or pads a geohash string to exactly <paramref name="precision"/> characters.
    /// Truncation removes trailing characters. Padding decodes the current center and
    /// re-encodes at the target precision.
    /// </summary>
    /// <param name="geohash">Source geohash (base-32 alphabet).</param>
    /// <param name="precision">Target precision (1–12). Default 10.</param>
    /// <returns>Geohash of exactly <paramref name="precision"/> characters.</returns>
    public static string TruncateOrPad(string geohash, int precision = 10)
    {
        if (geohash.Length == precision)
            return geohash;

        if (geohash.Length > precision)
            return geohash[..precision];

        // Shorter: decode cell center and re-encode at target precision.
        var (lat, lon, _, _) = Decode(geohash);
        return Encode(lat, lon, precision);
    }
}
