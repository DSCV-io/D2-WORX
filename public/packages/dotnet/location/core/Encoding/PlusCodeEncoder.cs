// -----------------------------------------------------------------------
// <copyright file="PlusCodeEncoder.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Location.Encoding;

using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Internal Open Location Code (OLC / plus-code) encoder / decoder.
/// </summary>
/// <remarks>
/// Reference: Google Open Location Code specification.
/// <para>
/// OLC encodes a rectangular bounding box. Format: exactly 8 pair-phase digits
/// (4 lon+lat pairs, base-20 per axis) + '+' separator + grid-refinement digits
/// (each encodes a 5-row × 4-col sub-cell). Selected cell sizes at the equator:
/// <list type="bullet">
/// <item><c>codeLength=10</c> — 2 grid digits → 11-char string → ~14 m lat × ~14 m lon
/// </item>
/// <item><c>codeLength=12</c> — 4 grid digits → 13-char string → ~0.2 m lat × ~1 m lon
/// </item>
/// </list>
/// The pair phase alone (8 chars before '+') covers ~125 m lat × ~250 m lon.
/// Each grid digit subdivides by 5 lat × 4 lon, so two grid digits give ÷25 lat × ÷16 lon
/// and four grid digits give ÷625 lat × ÷256 lon.
/// </para>
/// </remarks>
internal static class PlusCodeEncoder
{
    // OLC base-20 alphabet (digits 2–9 + upper-case letters, 20 chars).
    private const string _ALPHABET = "23456789CFGHJMPQRVWX";
    private const int _ENCODING_BASE = 20;

    // Number of significant digits in the pair phase (4 lon+lat pairs = 8 chars).
    private const int _SEPARATOR_POSITION = 8;

    // Number of complete (lon, lat) pairs = _SEPARATOR_POSITION / 2.
    private const int _FULL_PAIRS = 4;

    // Grid cell dimensions per refinement step.
    private const int _GRID_COLUMNS = 4;
    private const int _GRID_ROWS = 5;

    // OLC coordinate-space offsets.
    private const double _LAT_MAX = 90.0;
    private const double _LON_MAX = 180.0;

    private const char _SEPARATOR = '+';
    private const char _PADDING = '0';

    // Pair-phase cell sizes after FULL_PAIRS iterations of base-20 division.
    // lat domain [0,180) / 20^4 = 0.001125°; lon domain [0,360) / 20^4 = 0.00225°.
    private static readonly double sr_PairLatSize = 180.0 / Math.Pow(_ENCODING_BASE, _FULL_PAIRS);
    private static readonly double sr_PairLonSize = 360.0 / Math.Pow(_ENCODING_BASE, _FULL_PAIRS);

    // Lookup table: ASCII character → OLC digit value (−1 = invalid).
    private static readonly int[] sr_Lookup = new int[128];

    static PlusCodeEncoder()
    {
        for (var i = 0; i < sr_Lookup.Length; i++)
            sr_Lookup[i] = -1;

        for (var i = 0; i < _ALPHABET.Length; i++)
            sr_Lookup[_ALPHABET[i]] = i;
    }

    /// <summary>
    /// Encodes a coordinate pair to an Open Location Code (plus-code) string.
    /// </summary>
    /// <param name="latitude">Latitude (−90 to +90).</param>
    /// <param name="longitude">Longitude (−180 to +180).</param>
    /// <param name="codeLength">
    /// Number of significant code digits (excluding '+' separator and padding '0').
    /// The default of 10 produces an 11-character string (8 pair digits + '+' + 2 grid digits),
    /// giving a cell of ~14 m × 14 m at the equator. For ~1 m precision use 12, which produces
    /// a 13-character string (8 pair digits + '+' + 4 grid digits).
    /// Valid range is 2–15.
    /// </param>
    /// <returns>OLC plus-code string (e.g., <c>"87G7MQ8V+RG"</c>).</returns>
    public static string Encode(double latitude, double longitude, int codeLength = 10)
    {
        // Clip latitude to avoid overflow at exact +90.
        var lat = Math.Min(latitude, _LAT_MAX - 1e-9);

        // Normalize longitude to [−180, +180).
        var lon = longitude;
        while (lon < -_LON_MAX)
            lon += 360.0;
        while (lon >= _LON_MAX)
            lon -= 360.0;

        // Convert to OLC coordinate space: lat ∈ [0, 180), lon ∈ [0, 360).
        lat += _LAT_MAX;
        lon += _LON_MAX;

        var gridDigits = Math.Max(0, codeLength - _SEPARATOR_POSITION);

        // Buffer: 8 pair chars + '+' + grid chars.
        Span<char> buf = stackalloc char[_SEPARATOR_POSITION + 1 + gridDigits];
        var pos = 0;

        // Pair phase — 4 complete (lon, lat) pairs in base-20.
        // Step sizes for the first pair: lat-step = 180/20, lon-step = 360/20.
        var latRem = lat;
        var lonRem = lon;
        var latStep = 180.0 / _ENCODING_BASE;
        var lonStep = 360.0 / _ENCODING_BASE;

        for (var i = 0; i < _FULL_PAIRS; i++)
        {
            var lonDigit = (int)(lonRem / lonStep);
            lonRem -= lonDigit * lonStep;

            var latDigit = (int)(latRem / latStep);
            latRem -= latDigit * latStep;

            buf[pos++] = _ALPHABET[lonDigit];
            buf[pos++] = _ALPHABET[latDigit];

            lonStep /= _ENCODING_BASE;
            latStep /= _ENCODING_BASE;
        }

        // Separator always at position 8.
        buf[pos++] = _SEPARATOR;

        // Grid phase — subdivide the pair cell into GRID_ROWS × GRID_COLUMNS.
        // After the pair loop, latRem and lonRem are the residuals within the pair cell.
        // The pair cell dimensions are sr_PairLatSize × sr_PairLonSize.
        var gridLatSize = sr_PairLatSize / _GRID_ROWS;
        var gridLonSize = sr_PairLonSize / _GRID_COLUMNS;

        for (var i = 0; i < gridDigits; i++)
        {
            var row = (int)(latRem / gridLatSize);
            var col = (int)(lonRem / gridLonSize);

            // Clamp to guard against floating-point drift.
            row = Math.Min(row, _GRID_ROWS - 1);
            col = Math.Min(col, _GRID_COLUMNS - 1);

            latRem -= row * gridLatSize;
            lonRem -= col * gridLonSize;
            gridLatSize /= _GRID_ROWS;
            gridLonSize /= _GRID_COLUMNS;

            buf[pos++] = _ALPHABET[(row * _GRID_COLUMNS) + col];
        }

        return new string(buf[..pos]);
    }

    /// <summary>
    /// Decodes a plus-code to the bounding-box center and half-span error.
    /// </summary>
    /// <param name="plusCode">A valid OLC plus-code string.</param>
    /// <returns>
    /// Center latitude, center longitude, and half-span error in each axis.
    /// </returns>
    public static (double Latitude, double Longitude, double LatError, double LonError)
        Decode(string plusCode)
    {
        var upper = plusCode.ToUpperInvariant();
        var sepIdx = upper.IndexOf(_SEPARATOR);

        // Split into prefix (pair digits) and suffix (grid digits).
        var prefix = sepIdx >= 0
            ? upper[..sepIdx].TrimEnd(_PADDING) // strip padding zeros
            : upper;
        var suffix = (sepIdx >= 0 && sepIdx + 1 < upper.Length)
            ? upper[(sepIdx + 1)..]
            : string.Empty;

        // Each pair contributes 2 chars (lon digit, lat digit).
        var fullPairs = prefix.Length / 2;

        // Pair phase — reconstruct lat and lon from base-20 digit pairs.
        var lat = 0.0;
        var lon = 0.0;
        var latStep = 180.0 / _ENCODING_BASE;
        var lonStep = 360.0 / _ENCODING_BASE;

        for (var i = 0; i < fullPairs; i++)
        {
            var lonDigit = sr_Lookup[prefix[i * 2]];
            var latDigit = sr_Lookup[prefix[(i * 2) + 1]];

            lon += lonDigit * lonStep;
            lat += latDigit * latStep;

            lonStep /= _ENCODING_BASE;
            latStep /= _ENCODING_BASE;
        }

        // After the pair loop the decoded (lat, lon) is the cell lower-left in OLC space.
        // Error at pair level = half the pair-cell size.
        var latError = sr_PairLatSize / 2.0;
        var lonError = sr_PairLonSize / 2.0;

        // Grid phase — refine within the pair cell.
        var gridLatSize = sr_PairLatSize / _GRID_ROWS;
        var gridLonSize = sr_PairLonSize / _GRID_COLUMNS;

        for (var i = 0; i < suffix.Length; i++)
        {
            var digit = sr_Lookup[suffix[i]];
            var row = digit / _GRID_COLUMNS;
            var col = digit % _GRID_COLUMNS;

            lat += row * gridLatSize;
            lon += col * gridLonSize;
            gridLatSize /= _GRID_ROWS;
            gridLonSize /= _GRID_COLUMNS;
        }

        // Error at end of grid phase = half the final grid-cell size.
        // After the loop, gridLatSize was divided one extra time; the actual
        // row height = gridLatSize * _GRID_ROWS, so the half-cell error is half of that.
        if (suffix.Length > 0)
        {
            latError = (gridLatSize * _GRID_ROWS) / 2.0;
            lonError = (gridLonSize * _GRID_COLUMNS) / 2.0;
        }

        // Center = cell lower-left + half the cell size (error).
        lat += latError;
        lon += lonError;

        // Convert from OLC coordinate space back to degrees.
        lat -= _LAT_MAX;
        lon -= _LON_MAX;

        return (lat, lon, latError, lonError);
    }

    /// <summary>
    /// Validates whether <paramref name="plusCode"/> is a syntactically valid OLC plus-code.
    /// </summary>
    /// <param name="plusCode">The string to validate.</param>
    /// <returns><c>true</c> if the string is a valid OLC plus-code; otherwise <c>false</c>.
    /// </returns>
    public static bool IsValid(string? plusCode)
    {
        if (plusCode.Falsey())
            return false;

        var upper = plusCode!.ToUpperInvariant();

        // Must contain exactly one '+'.
        var sepIdx = upper.IndexOf(_SEPARATOR);
        if (sepIdx < 0 || upper.IndexOf(_SEPARATOR, sepIdx + 1) >= 0)
            return false;

        // Separator must be between positions 2 and 8 (inclusive).
        if (sepIdx < 2 || sepIdx > _SEPARATOR_POSITION)
            return false;

        // Prefix: all non-padding chars must be in the OLC alphabet;
        // once a '0' padding char appears, only '0' is allowed.
        var prefix = upper[..sepIdx];
        var seenPad = false;

        foreach (var c in prefix)
        {
            if (c == _PADDING)
            {
                seenPad = true;
                continue;
            }

            if (seenPad)
                return false;

            if (c >= 128 || sr_Lookup[c] < 0)
                return false;
        }

        // Suffix: must be non-empty and all chars in the OLC alphabet.
        var suffix = upper[(sepIdx + 1)..];
        if (suffix.Length == 0)
            return false;

        foreach (var c in suffix)
        {
            if (c >= 128 || sr_Lookup[c] < 0)
                return false;
        }

        // At least 2 total significant digits.
        var sigPrefix = prefix.TrimEnd(_PADDING);
        var totalSig = sigPrefix.Length + suffix.Length;
        return totalSig >= 2;
    }
}
