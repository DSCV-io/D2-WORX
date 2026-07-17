// -----------------------------------------------------------------------
// <copyright file="CallPathOps.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.Abstractions;

using System;
using System.Collections.Generic;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Pure, depth-bounded helpers for appending a hop's own identity to the
/// propagated call-path. Each trust boundary appends ITS OWN
/// <see cref="CallPathEntry"/> on receipt; this helper keeps the resulting list
/// bounded so a request cannot grow the call-path without limit (oldest entries
/// beyond the bound are trimmed). Operational telemetry only — an authority
/// decision NEVER reads the call-path.
/// </summary>
public static class CallPathOps
{
    /// <summary>
    /// The maximum number of entries the propagated call-path retains. MUST stay
    /// in lockstep with the depth bound the generated
    /// <see cref="PropagatedContextSerializer"/> enforces in
    /// <c>FieldsWithinBounds</c>: a serialized call-path exceeding that bound causes
    /// the receiver to DROP the entire propagated context, so appends are trimmed to
    /// this depth to keep propagation working. Far above any legitimate fan-out depth.
    /// </summary>
    public const int MAX_CALL_PATH_DEPTH = 16;

    /// <summary>
    /// Returns a NEW depth-bounded call-path with a hop's own identity appended as the
    /// newest (last) entry. When <paramref name="existing"/> is null or empty the
    /// result is a single-entry list (the start of a fresh path). When appending would
    /// exceed <see cref="MAX_CALL_PATH_DEPTH"/>, the oldest entries are trimmed so the
    /// newest entries (including the just-appended hop) are retained.
    /// </summary>
    /// <param name="existing">The inbound call-path (may be null or empty).</param>
    /// <param name="id">The appending hop's OWN service / module id (never
    /// request-controlled).</param>
    /// <param name="kind">The kind of hop being recorded.</param>
    /// <param name="timestamp">When this hop was reached (UTC instant in wire form).</param>
    /// <returns>A new, depth-bounded call-path with the hop appended.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="id"/> is null, empty, or whitespace — a missing
    /// self-identity is a misconfiguration, not a silently-dropped entry.
    /// </exception>
    public static IReadOnlyList<CallPathEntry> Append(
        IReadOnlyList<CallPathEntry>? existing,
        string id,
        CallPathKind kind,
        DateTimeOffset timestamp)
    {
        id.ThrowIfFalsey();

        var entry = new CallPathEntry(id, kind, timestamp);

        if (existing.Falsey())
            return [entry];

        var result = new List<CallPathEntry>(existing!.Count + 1);
        result.AddRange(existing);
        result.Add(entry);

        if (result.Count > MAX_CALL_PATH_DEPTH)
            result.RemoveRange(0, result.Count - MAX_CALL_PATH_DEPTH);

        return result;
    }
}
