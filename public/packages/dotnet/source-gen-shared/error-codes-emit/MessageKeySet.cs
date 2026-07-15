// -----------------------------------------------------------------------
// <copyright file="MessageKeySet.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Value-equatable wrapper over the top-level key set of
/// <c>contracts/messages/en-US.json</c>, used as an IIncrementalGenerator
/// pipeline boundary. Reducing en-US.json to JUST its sorted key set (not its
/// content) at the earliest <c>Select</c> means a translation-VALUE edit
/// (keys unchanged) does not invalidate the error-codes generator's cache —
/// only a key ADD/REMOVE re-runs the TK-existence cross-check.
/// </summary>
internal sealed class MessageKeySet : IEquatable<MessageKeySet>
{
    private readonly ImmutableArray<string> r_sortedKeys;

    private MessageKeySet(ImmutableArray<string> sortedKeys) => r_sortedKeys = sortedKeys;

    /// <summary>Gets an empty key set (no en-US.json was surfaced, or it was unparseable).</summary>
    public static MessageKeySet Empty { get; } = new(ImmutableArray<string>.Empty);

    /// <summary>Gets a value indicating whether the set has no keys (no cross-check is possible).</summary>
    public bool IsEmpty => r_sortedKeys.IsEmpty;

    /// <summary>
    /// Parses the top-level object keys of en-US.json into a sorted,
    /// value-equatable set. Returns <see cref="Empty"/> on any parse failure
    /// (the generator then skips the TK cross-check rather than fire a false
    /// diagnostic on malformed message JSON — that is the i18n generator's
    /// concern, not this one's).
    /// </summary>
    /// <param name="json">Raw en-US.json content.</param>
    /// <returns>The parsed key set, or <see cref="Empty"/> on failure.</returns>
    public static MessageKeySet Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return Empty;

            var keys = ImmutableArray.CreateBuilder<string>();
            foreach (var prop in doc.RootElement.EnumerateObject())
                keys.Add(prop.Name);

            keys.Sort(StringComparer.Ordinal);
            return new MessageKeySet(keys.ToImmutable());
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    /// <summary>
    /// Unions multiple key sets (public∪private en-US AdditionalFiles).
    /// </summary>
    /// <param name="sets">Key sets to merge.</param>
    /// <returns>The union set (empty when none provided).</returns>
    public static MessageKeySet Union(IEnumerable<MessageKeySet> sets)
    {
        var keys = ImmutableArray.CreateBuilder<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var set in sets)
        {
            if (set is null || set.IsEmpty)
            {
                continue;
            }

            foreach (var key in set.r_sortedKeys)
            {
                if (seen.Add(key))
                {
                    keys.Add(key);
                }
            }
        }

        if (keys.Count == 0)
        {
            return Empty;
        }

        keys.Sort(StringComparer.Ordinal);
        return new MessageKeySet(keys.ToImmutable());
    }

    /// <summary>Returns whether the set contains <paramref name="key"/>.</summary>
    /// <param name="key">The snake_case key to test.</param>
    /// <returns><c>true</c> when present.</returns>
    public bool Contains(string key) => r_sortedKeys.Contains(key, StringComparer.Ordinal);

    /// <inheritdoc/>
    public bool Equals(MessageKeySet? other) =>
        other is not null && r_sortedKeys.SequenceEqual(other.r_sortedKeys, StringComparer.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as MessageKeySet);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var key in r_sortedKeys)
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(key);

        return hash;
    }
}
