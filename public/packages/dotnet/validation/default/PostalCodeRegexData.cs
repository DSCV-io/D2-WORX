// -----------------------------------------------------------------------
// <copyright file="PostalCodeRegexData.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Validation;

using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Loads the per-country postal-code regex map from the embedded
/// <c>PostalCodeRegexData.json</c> resource and exposes it as a
/// <see cref="FrozenDictionary{TKey,TValue}"/> keyed by ISO 3166-1 alpha-2
/// country code (upper-case). Built once at class initialization; thread-safe.
/// </summary>
internal static class PostalCodeRegexData
{
    /// <summary>
    /// Per-country regex map. Keys are ISO 3166-1 alpha-2 strings (e.g. <c>"US"</c>,
    /// <c>"GB"</c>). Each <see cref="Regex"/> is anchored, compiled, and carries a
    /// 50 ms match timeout.
    /// </summary>
    internal static readonly FrozenDictionary<string, Regex> SR_Map = BuildMap();

    private const double _MATCH_TIMEOUT_MS = 50;

    private static FrozenDictionary<string, Regex> BuildMap()
    {
        // A const (compile-time literal) timeout — NOT a static readonly field —
        // so there is no static-field initialization-order hazard. (A static
        // readonly TimeSpan declared AFTER Map would still be its default
        // TimeSpan.Zero when this initializer-invoked method runs, and
        // `new Regex(..., TimeSpan.Zero)` throws ArgumentOutOfRangeException.)
        var matchTimeout = TimeSpan.FromMilliseconds(_MATCH_TIMEOUT_MS);

        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "DcsvIo.D2.Validation.PostalCodeRegexData.json";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found in assembly '{assembly.FullName}'.");

        var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException(
                "Failed to deserialize PostalCodeRegexData.json — result was null.");

        var builder = new Dictionary<string, Regex>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, pattern) in raw)
        {
            // Skip the JSON $comment entry.
            if (key.StartsWith('$'))
                continue;

            builder[key] = new Regex(
                pattern,
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                matchTimeout);
        }

        return builder.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
