// -----------------------------------------------------------------------
// <copyright file="LocalAnchoredEvent.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Time;

using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using NodaTime;
using NodaTime.TimeZones;

/// <summary>
/// A Category-3 (future local-anchored) timestamp that represents an event
/// scheduled for a specific wall-clock time in a specific IANA time zone.
/// Examples: "every Tuesday at 9 AM Edmonton", "weekly digest at 8 AM
/// user-local".
/// </summary>
/// <remarks>
/// <para>
/// <b>Category 3 — Future local-anchored event.</b><br />
/// Storage: <c>scheduled_local TIMESTAMP</c> (from
/// <see cref="ScheduledLocal" />) + <c>scheduled_zone TEXT</c> (from
/// <see cref="IANAIdentifier" />) + <c>next_fire_utc TIMESTAMPTZ NULL</c>
/// (from <see cref="NextFireUtc" />).<br />
/// Sort: always use <see cref="NextFireUtc" /> for UTC ordering.
/// </para>
/// <para>
/// <b>DST ambiguity</b>: <see cref="ComputeNextFire"/> encapsulates the
/// resolver strategy — callers must use it rather than hand-rolling
/// <c>ScheduledLocal.InZone(...)</c> calls. Recompute
/// <see cref="NextFireUtc" /> when the tzdb updates or the scheduling
/// parameters change.
/// </para>
/// <para>
/// <b>Construction</b>: smart-constructor pattern via the static
/// <see cref="Create"/> factory. The private positional constructor enforces
/// IANA validation + canonicalization at the entry point.
/// <see cref="ScheduledLocal"/> validation lives upstream — NodaTime's
/// <see cref="LocalDateTime"/> constructor itself throws
/// <see cref="System.ArgumentOutOfRangeException"/> for impossible calendar
/// dates (Feb 30, April 31, Feb 29 in non-leap years, year out of NodaTime
/// range, etc.) before <see cref="Create"/> is reached.
/// </para>
/// <para>
/// <b>JSON serialization</b>: the private constructor makes this type
/// incompatible with default <c>System.Text.Json</c> deserialization. v2
/// D2 persists this type via Npgsql.NodaTime EF Core (column-based,
/// not JSON), and no current consumer round-trips it through JSON. If a
/// future consumer needs JSON support, that consumer writes a
/// <c>JsonConverter&lt;LocalAnchoredEvent&gt;</c> that calls
/// <see cref="Create"/> rather than bypassing it.
/// </para>
/// </remarks>
public sealed record LocalAnchoredEvent
{
    private LocalAnchoredEvent(
        LocalDateTime scheduledLocal,
        string canonicalIANA,
        Instant? nextFireUtc)
    {
        ScheduledLocal = scheduledLocal;
        IANAIdentifier = canonicalIANA;
        NextFireUtc = nextFireUtc;
    }

    /// <summary>
    /// Gets the scheduled local date-time (wall-clock, no zone attached).
    /// <b>Category 3 — Future local-anchored.</b> Meaningless without
    /// <see cref="IANAIdentifier" />. The caller is responsible for
    /// constructing a valid <see cref="LocalDateTime"/>; NodaTime's
    /// <c>LocalDateTime</c> constructor itself throws
    /// <see cref="System.ArgumentOutOfRangeException"/> for impossible dates
    /// — those throws happen at the call site BEFORE <see cref="Create"/>
    /// is invoked.
    /// </summary>
    public LocalDateTime ScheduledLocal { get; }

    /// <summary>
    /// Gets the canonical IANA time-zone identifier in which
    /// <see cref="ScheduledLocal" /> is interpreted. Normalized to canonical
    /// at construction; deprecated aliases are accepted and normalized.
    /// </summary>
    public string IANAIdentifier { get; }

    /// <summary>
    /// Gets the denormalized UTC instant of the next scheduled firing, or
    /// <c>null</c> if not yet computed or the event has been canceled.
    /// <b>Category 2 — Future fixed instant</b> (derived from Category-3
    /// source; recompute via <see cref="ComputeNextFire"/> when tzdb updates
    /// or scheduling changes). Sort/compare on this field.
    /// </summary>
    public Instant? NextFireUtc { get; }

    /// <summary>
    /// Creates a <see cref="LocalAnchoredEvent"/> after validating +
    /// normalizing the IANA identifier. <see cref="ScheduledLocal"/> is
    /// NOT validated here — invalid calendar dates throw at the
    /// <see cref="LocalDateTime"/> constructor call site, before
    /// <see cref="Create"/> is reached.
    /// </summary>
    /// <param name="scheduledLocal">The local (wall-clock) scheduled time.</param>
    /// <param name="ianaIdentifier">
    /// The IANA time-zone identifier (canonical or deprecated alias).
    /// </param>
    /// <param name="nextFireUtc">
    /// Optional denormalized UTC fire time. Omit on first creation; populate
    /// via <see cref="ComputeNextFire"/> + a <c>with</c>-expression.
    /// </param>
    /// <returns>
    /// <c>D2Result&lt;LocalAnchoredEvent&gt;.Ok(...)</c> with normalized IANA on success;
    /// <see cref="D2Result{TData}.ValidationFailed"/> on IANA failure.
    /// </returns>
    public static D2Result<LocalAnchoredEvent> Create(
        LocalDateTime scheduledLocal,
        string? ianaIdentifier,
        Instant? nextFireUtc = null)
    {
        if (ianaIdentifier.Falsey())
        {
            return D2Result<LocalAnchoredEvent>.ValidationFailed(
                inputErrors: [new InputError(
                    nameof(ianaIdentifier),
                    [TK.Common.Errors.NOT_NULL_VIOLATION])]);
        }

        var canonical = TimeZoneIdNormalizer.Normalize(ianaIdentifier!);
        if (canonical is null)
        {
            return D2Result<LocalAnchoredEvent>.ValidationFailed(
                inputErrors: [new InputError(
                    nameof(ianaIdentifier),
                    [TK.Common.Time.INVALID_IANA_IDENTIFIER])]);
        }

        return D2Result<LocalAnchoredEvent>.Ok(
            new LocalAnchoredEvent(scheduledLocal, canonical, nextFireUtc));
    }

    /// <summary>
    /// Computes the UTC instant at which this scheduled local event next
    /// fires, applying NodaTime's <see cref="Resolvers.LenientResolver"/>
    /// to resolve DST-skipped (spring-forward) and DST-ambiguous
    /// (fall-back) local times deterministically:
    /// <list type="bullet">
    ///   <item>Skipped local times → map FORWARD to the next valid instant
    ///     after the gap (e.g., 2:30 AM on US spring-forward day resolves
    ///     to 3:30 AM clock time, i.e. 2:30 AM + DST offset).</item>
    ///   <item>Ambiguous local times → pick the FIRST occurrence (the
    ///     pre-transition / earlier instant; e.g., 1:30 AM on US fall-back
    ///     day resolves to the 1:30 AM that occurred before the clock
    ///     turned back, not the second 1:30 AM after).</item>
    /// </list>
    /// </summary>
    /// <returns>
    /// <c>D2Result&lt;Instant&gt;.Ok(...)</c> with the computed <see cref="Instant"/>.
    /// In practice this method cannot fail (IANA was pre-validated at
    /// <see cref="Create"/>; <see cref="Resolvers.LenientResolver"/>
    /// handles every DST case without throwing) — the
    /// <see cref="D2Result{TData}"/> return shape is preserved for API
    /// consistency with the rest of the codebase's error-as-value pattern
    /// and to allow future failure modes (e.g., tzdb removed the zone
    /// between construction and computation) to surface without an API
    /// break.
    /// </returns>
    public D2Result<Instant> ComputeNextFire()
    {
        var zone = DateTimeZoneProviders.Tzdb[IANAIdentifier];
        return D2Result<Instant>.Ok(
            ScheduledLocal.InZone(zone, Resolvers.LenientResolver).ToInstant());
    }
}
