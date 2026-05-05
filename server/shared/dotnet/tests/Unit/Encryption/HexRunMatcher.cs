// -----------------------------------------------------------------------
// <copyright file="HexRunMatcher.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Encryption;

using System.Text.RegularExpressions;

/// <summary>
/// Source-generated regex used by encryption tests to detect long hex runs
/// in strings (a heuristic for accidental byte-dump leaks).
/// </summary>
internal static partial class HexRunMatcher
{
    /// <summary>
    /// Matches any unbroken run of 16 or more hex characters. Bucket 1
    /// (no backtracking) — single greedy quantifier with no following
    /// pattern, so no timeout needed.
    /// </summary>
    /// <returns>The compiled regex.</returns>
    [GeneratedRegex("[0-9a-fA-F]{16,}")]
    internal static partial Regex LongHexRun();
}
