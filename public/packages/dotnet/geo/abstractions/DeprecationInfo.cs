// -----------------------------------------------------------------------
// <copyright file="DeprecationInfo.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.Abstractions;

/// <summary>
/// Meta-record describing the deprecation status of a geo reference-data
/// entity (country, subdivision, currency, language, locale, timezone, or
/// geopolitical entity). Applies uniformly across every entity, so it lives
/// in this hand-written abstractions slice rather than being emitted per
/// entity by the source generator. Carried on each record as an optional
/// <c>Deprecation?</c> field — <c>null</c> means the entity is currently
/// active.
/// </summary>
/// <remarks>
/// <para>
/// Deprecation is a real, persistent concern for ISO reference data: codes
/// like <c>YU</c> (Yugoslavia) or <c>SU</c> (Soviet Union) live forever in
/// historical records and MUST remain resolvable for hash citations and
/// audit replay. The lookup APIs therefore include deprecated entries by
/// default; UI / selector code that wants to filter them out opts in
/// explicitly via the <c>activeOnly: true</c> overload.
/// </para>
/// <para>
/// <see cref="SupersededBy"/> is plural to cover splits — when one entity
/// deprecates into multiple successors (e.g. <c>YU</c> → <c>RS, ME, HR, SI,
/// MK, BA, XK</c>). <see cref="SuccessorNote"/> is free-form prose for
/// nuance the structured field can't capture (e.g. "successor list is
/// approximate; political recognition of XK is partial").
/// </para>
/// </remarks>
public sealed record DeprecationInfo
{
    /// <summary>
    /// Gets the calendar date on which the entity was deprecated by the
    /// upstream authority (typically ISO or the IANA TZDB committee).
    /// Stored as <see cref="DateOnly"/> because the deprecation is a
    /// calendar event without a meaningful time-of-day component.
    /// </summary>
    public required DateOnly DeprecatedAt { get; init; }

    /// <summary>
    /// Gets a short human-readable explanation of why the entity was
    /// deprecated. Examples: "country dissolved", "currency replaced by
    /// EUR", "tzdb rule consolidation". Not a translation key — this is
    /// reference-data metadata, not user-facing copy.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Gets the canonical codes of the successor entities, or <c>null</c>
    /// when no successor is recorded. Plural to cover splits (one entity
    /// deprecating into several). The list is in the spec's natural order
    /// — callers MUST NOT assume it is sorted alphabetically or by any
    /// other criterion.
    /// </summary>
    public IReadOnlyList<string>? SupersededBy { get; init; }

    /// <summary>
    /// Gets a free-form note adding context the structured
    /// <see cref="SupersededBy"/> list cannot capture (partial recognition,
    /// approximate mapping, regional politics, etc.), or <c>null</c> when
    /// no additional note is recorded.
    /// </summary>
    public string? SuccessorNote { get; init; }
}
