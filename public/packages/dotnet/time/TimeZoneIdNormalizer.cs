// -----------------------------------------------------------------------
// <copyright file="TimeZoneIdNormalizer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Time;

using NodaTime;
using NodaTime.TimeZones;

/// <summary>
/// Internal helper centralizing IANA time-zone identifier validation +
/// canonicalization. Used by <see cref="ZonedInstant.Create"/> and
/// <see cref="LocalAnchoredEvent.Create"/> so the rules stay byte-identical
/// across both factory paths.
/// </summary>
/// <remarks>
/// <para>
/// NodaTime's <see cref="DateTimeZoneProviders.Tzdb"/> retains TZDB aliases
/// (e.g. <c>"US/Pacific"</c>, <c>"Asia/Saigon"</c>) as queryable IDs whose
/// <see cref="DateTimeZone.Id"/> equals the input alias rather than the
/// canonical TZDB name. To get the canonical name we resolve through
/// <see cref="TzdbDateTimeZoneSource.CanonicalIdMap"/> which is exactly the
/// alias-to-canonical map maintained by TZDB upstream.
/// </para>
/// </remarks>
internal static class TimeZoneIdNormalizer
{
    /// <summary>
    /// Returns the canonical TZDB identifier for <paramref name="iana"/>, or
    /// <c>null</c> when <paramref name="iana"/> is not a recognized TZDB
    /// zone (invalid name, fixed-offset notation, plain numeric input, etc).
    /// </summary>
    /// <param name="iana">
    /// The IANA time-zone identifier. Must already pass the Falsey()
    /// non-null/non-empty/non-whitespace check at the call site; this
    /// method does not re-validate that contract.
    /// </param>
    public static string? Normalize(string iana)
    {
        var zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(iana);
        if (zone is null)
            return null;

        // CanonicalIdMap maps every TZDB id (canonical AND alias) to its
        // canonical form. Lookup on a canonical name returns the same name;
        // lookup on an alias returns the canonical it points to. Missing
        // from the map = caller passed something exotic (e.g. a custom
        // provider zone); fall back to the resolved zone's Id which is at
        // least a valid TZDB identifier.
        var source = TzdbDateTimeZoneSource.Default;
        if (source.CanonicalIdMap.TryGetValue(zone.Id, out var canonical))
            return canonical;

        return zone.Id;
    }
}
