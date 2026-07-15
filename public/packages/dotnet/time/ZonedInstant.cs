// -----------------------------------------------------------------------
// <copyright file="ZonedInstant.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Time;

using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using NodaTime;

/// <summary>
/// A Category-1 (past instant) timestamp that additionally preserves the
/// original wall-clock time-zone context. Use this when you need to
/// reconstruct the user's local time at the moment the event occurred — for
/// example, displaying "signed in at 9:42 AM Mountain" on an activity log.
/// </summary>
/// <remarks>
/// <para>
/// <b>Category 1 — Past instant with original context.</b><br />
/// Storage: <c>event_at TIMESTAMPTZ</c> (from <see cref="Instant" />) +
/// <c>event_at_zone TEXT NULL</c> (from <see cref="IANAIdentifier" />).<br />
/// Sort/compare: always use <see cref="Instant" /> — zone-agnostic and
/// unambiguous.
/// </para>
/// <para>
/// If you do not need to reconstruct the original wall-clock time (e.g., a
/// background job timestamp where zone is irrelevant), store a bare
/// <see cref="Instant" /> instead (Category 1 without context).
/// </para>
/// <para>
/// <b>Construction</b>: smart-constructor pattern via the static
/// <see cref="Create"/> factory. The private positional constructor enforces
/// IANA validation + canonicalization at the entry point — deprecated aliases
/// (e.g. <c>"US/Pacific"</c>) are normalized to their canonical TZDB names
/// (<c>"America/Los_Angeles"</c>) on the way in, so record equality and
/// downstream comparisons stay consistent regardless of which alias the
/// caller passed.
/// </para>
/// <para>
/// <b>JSON serialization</b>: the private constructor makes this type
/// incompatible with default <c>System.Text.Json</c> deserialization. v2
/// D2 persists this type via Npgsql.NodaTime EF Core (column-based,
/// not JSON), and no current consumer round-trips it through JSON. If a
/// future consumer needs JSON support, that consumer writes a
/// <c>JsonConverter&lt;ZonedInstant&gt;</c> that calls
/// <see cref="Create"/> (surfacing the validation failure) rather than
/// bypassing it.
/// </para>
/// </remarks>
public sealed record ZonedInstant
{
    private ZonedInstant(Instant instant, string canonicalIANA)
    {
        Instant = instant;
        IANAIdentifier = canonicalIANA;
    }

    /// <summary>
    /// Gets the UTC instant at which the event occurred.
    /// <b>Category 1 — Past instant.</b> Sort and compare on this value.
    /// </summary>
    public Instant Instant { get; }

    /// <summary>
    /// Gets the canonical IANA time-zone identifier in effect when the event
    /// occurred (e.g., <c>"America/Los_Angeles"</c>). Always the canonical
    /// form — deprecated aliases passed to <see cref="Create"/> are
    /// normalized at construction time. Reconstruct the user's local
    /// wall-clock time by combining <see cref="Instant"/> with this
    /// identifier via NodaTime's
    /// <c>DateTimeZoneProviders.Tzdb[IANAIdentifier]</c>.
    /// </summary>
    public string IANAIdentifier { get; }

    /// <summary>
    /// Creates a <see cref="ZonedInstant"/> after validating + normalizing
    /// the IANA time-zone identifier.
    /// </summary>
    /// <param name="instant">The UTC instant (always valid; no validation).</param>
    /// <param name="ianaIdentifier">
    /// The IANA time-zone identifier. May be a canonical name
    /// (<c>"America/Los_Angeles"</c>) OR a deprecated alias / renamed zone
    /// (<c>"US/Pacific"</c>, <c>"Asia/Saigon"</c>) — normalized to canonical
    /// form. Fixed-offset notation (<c>"UTC+5"</c>, <c>"+05:00"</c>) is
    /// REJECTED — IANA-only. <c>"Etc/GMT*"</c> zones ARE accepted because
    /// they are valid IANA TZDB entries.
    /// </param>
    /// <returns>
    /// <c>D2Result&lt;ZonedInstant&gt;.Ok(...)</c> on success with <see cref="IANAIdentifier"/>
    /// normalized to canonical, or <see cref="D2Result{TData}.ValidationFailed"/>
    /// on null/empty/whitespace/invalid IANA.
    /// </returns>
    public static D2Result<ZonedInstant> Create(Instant instant, string? ianaIdentifier)
    {
        if (ianaIdentifier.Falsey())
        {
            return D2Result<ZonedInstant>.ValidationFailed(
                inputErrors: [new InputError(
                    nameof(ianaIdentifier),
                    [TK.Common.Errors.NOT_NULL_VIOLATION])]);
        }

        var canonical = TimeZoneIdNormalizer.Normalize(ianaIdentifier!);
        if (canonical is null)
        {
            return D2Result<ZonedInstant>.ValidationFailed(
                inputErrors: [new InputError(
                    nameof(ianaIdentifier),
                    [TK.Common.Time.INVALID_IANA_IDENTIFIER])]);
        }

        return D2Result<ZonedInstant>.Ok(new ZonedInstant(instant, canonical));
    }
}
