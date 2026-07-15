// -----------------------------------------------------------------------
// <copyright file="ScopeClaimParser.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.Abstractions;

using System;
using System.Collections.Generic;
using System.Text.Json;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Parses the OAuth <c>scope</c> claim into an <see cref="IReadOnlySet{T}"/>.
/// Per RFC 6749 §3.3 the canonical wire format is a space-separated string;
/// some authorization servers send a JSON array instead. Both shapes are
/// supported defensively.
/// </summary>
/// <remarks>
/// RFC 6749 §3.3 grammar:
/// <code>
/// scope       = scope-token *( SP scope-token )
/// scope-token = 1*( %x21 / %x23-5B / %x5D-7E )
/// </code>
/// The separator is ASCII <c>SP</c> (0x20) only. Tab/CR/LF are NOT in the
/// grammar; accepting them would diverge from issuer-side validation and
/// could let through scope strings the issuer didn't intend. We use SP only.
/// </remarks>
public static class ScopeClaimParser
{
    // RFC 6749 §3.3: SP only.
    private static readonly char[] sr_separators = [' '];

    private static readonly IReadOnlySet<string> sr_empty =
        new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Parses a <see cref="JsonElement"/> representing the <c>scope</c> claim.
    /// String → split on SP; Array → enumerate string elements; anything else → empty set.
    /// </summary>
    /// <param name="element">The <c>scope</c> claim element from a JWT payload.</param>
    /// <returns>The parsed scope set.</returns>
    public static IReadOnlySet<string> Parse(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return ParseString(element.GetString());

        if (element.ValueKind == JsonValueKind.Array)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in element.EnumerateArray())
            {
                // RFC 6749 §3.3: scope-token = 1*( %x21 / %x23-5B / %x5D-7E )
                // — visible non-SP. Whitespace-only tokens are not valid;
                // reject them in both the array path and the string path
                // (the string path already does this via RemoveEmptyEntries +
                // SP split — but a whitespace-only array element would slip
                // past `s.Length > 0`). Use Falsey() for symmetric handling.
                if (item.ValueKind == JsonValueKind.String
                    && item.GetString() is { } s
                    && !s.Falsey())
                {
                    set.Add(s);
                }
            }

            return set;
        }

        return sr_empty;
    }

    /// <summary>
    /// Parses a string-form <c>scope</c> claim by splitting on SP per RFC 6749 §3.3
    /// and returning the unique set of non-empty entries.
    /// </summary>
    /// <param name="value">The space-separated scope string.</param>
    /// <returns>The parsed scope set.</returns>
    public static IReadOnlySet<string> ParseString(string? value)
    {
        if (value.Falsey())
            return sr_empty;

        var parts = value!.Split(sr_separators, StringSplitOptions.RemoveEmptyEntries);
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in parts)
            set.Add(part);

        return set;
    }
}
