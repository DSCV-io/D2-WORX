// -----------------------------------------------------------------------
// <copyright file="LevenshteinComparer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.Abstractions.NameResolution;

/// <summary>
/// Pure helper computing the bounded Levenshtein edit distance between two
/// strings — used by the geo name resolver's fuzzy fallback to score
/// candidate matches after exact lookup misses. Pure / stateless /
/// threadsafe.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cross-language parity:</b> the TypeScript mirror in
/// <c>@dcsv-io/d2-geo-abstractions</c>
/// (<c>src/name-resolution/levenshtein-comparer.ts</c>) implements the
/// same DP algorithm with the same early-termination semantics. The two
/// implementations MUST agree byte-for-byte on <see cref="IsWithin"/>
/// and <see cref="Compare"/> for every input pair the parity fixtures
/// exercise.
/// </para>
/// <para>
/// <b>Algorithm:</b> classic two-row Wagner-Fischer dynamic-programming
/// edit distance with the operations {insert, delete, substitute} all
/// costing 1. Memory is O(min(|a|, |b|)) by shrinking the inner loop to
/// the shorter string; running time is O(|a| × |b|) in the worst case but
/// the <c>maxDistance</c> early-termination cap typically drops it to
/// near-linear when callers use small bounds (≤ 3 is the resolver's usage
/// pattern).
/// </para>
/// <para>
/// <b>Boundary handling:</b>
/// </para>
/// <list type="bullet">
///   <item>
///     <description><c>null</c> on either side is treated as the empty
///     string — the helper never throws on null input.</description>
///   </item>
///   <item>
///     <description>Negative <c>maxDistance</c> is rejected by
///     short-circuiting to <c>maxDistance + 1</c>
///     (<see cref="Compare"/>) / <c>false</c> (<see cref="IsWithin"/>);
///     callers should pass non-negative bounds.</description>
///   </item>
///   <item>
///     <description>Length-difference shortcut: if
///     <c>|len(a) - len(b)| &gt; maxDistance</c> the answer cannot be
///     within the cap, so we return early without allocating the DP
///     buffers.</description>
///   </item>
/// </list>
/// </remarks>
public static class LevenshteinComparer
{
    /// <summary>
    /// Returns <c>true</c> when the Levenshtein edit distance between
    /// <paramref name="a"/> and <paramref name="b"/> is at most
    /// <paramref name="maxDistance"/>. Convenience wrapper over
    /// <see cref="Compare"/>.
    /// </summary>
    /// <param name="a">The first string. May be <c>null</c>.</param>
    /// <param name="b">The second string. May be <c>null</c>.</param>
    /// <param name="maxDistance">
    /// Inclusive distance cap. Must be non-negative; negative values
    /// always return <c>false</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> when the bounded distance is within the cap;
    /// <c>false</c> otherwise (including when either input is rejected
    /// or the early-termination cap is exceeded).
    /// </returns>
    public static bool IsWithin(string? a, string? b, int maxDistance)
    {
        if (maxDistance < 0)
            return false;

        return Compare(a, b, maxDistance) <= maxDistance;
    }

    /// <summary>
    /// Returns the Levenshtein edit distance between <paramref name="a"/>
    /// and <paramref name="b"/>, capped at <paramref name="maxDistance"/>
    /// + 1 via early termination. The capped return value is a sentinel:
    /// callers MUST interpret any value greater than
    /// <paramref name="maxDistance"/> as "exceeds cap" rather than as a
    /// true distance.
    /// </summary>
    /// <param name="a">The first string. May be <c>null</c>.</param>
    /// <param name="b">The second string. May be <c>null</c>.</param>
    /// <param name="maxDistance">
    /// Inclusive distance cap that enables early termination. Must be
    /// non-negative; negative values return <c>maxDistance + 1</c> as
    /// the exceed-cap sentinel.
    /// </param>
    /// <returns>
    /// The edit distance when at most <paramref name="maxDistance"/>;
    /// otherwise <c>maxDistance + 1</c>.
    /// </returns>
    public static int Compare(string? a, string? b, int maxDistance)
    {
        var cap = maxDistance < 0 ? 0 : maxDistance;
        var ceiling = cap + 1;

        // Normalize null to empty and order so b is the shorter side; the
        // DP rows are sized to the shorter string for O(min) memory.
        var first = a ?? string.Empty;
        var second = b ?? string.Empty;
        if (first.Length < second.Length)
            (first, second) = (second, first);

        // Length-difference shortcut — distance is bounded below by the
        // delta and cannot fit inside the cap.
        if (first.Length - second.Length > cap)
            return ceiling;

        // Trivial cases — second is empty, distance equals first.Length.
        if (second.Length == 0)
            return first.Length > cap ? ceiling : first.Length;

        // Two-row DP — previous row + current row over second (the
        // shorter string). Initialize previous row to the empty-prefix
        // distances 0..second.Length.
        var previous = new int[second.Length + 1];
        var current = new int[second.Length + 1];
        for (var j = 0; j <= second.Length; j++)
            previous[j] = j;

        for (var i = 1; i <= first.Length; i++)
        {
            current[0] = i;
            var rowMin = current[0];
            var firstChar = first[i - 1];

            for (var j = 1; j <= second.Length; j++)
            {
                var cost = firstChar == second[j - 1] ? 0 : 1;
                var deletion = previous[j] + 1;
                var insertion = current[j - 1] + 1;
                var substitution = previous[j - 1] + cost;

                var cell = deletion;
                if (insertion < cell)
                    cell = insertion;
                if (substitution < cell)
                    cell = substitution;

                current[j] = cell;
                if (cell < rowMin)
                    rowMin = cell;
            }

            // Early termination — every cell in this row already exceeds
            // the cap, so the final answer cannot drop back below it.
            if (rowMin > cap)
                return ceiling;

            // Swap rows — `current` becomes `previous` for the next i.
            (previous, current) = (current, previous);
        }

        var distance = previous[second.Length];
        return distance > cap ? ceiling : distance;
    }
}
