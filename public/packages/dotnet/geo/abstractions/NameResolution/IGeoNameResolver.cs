// -----------------------------------------------------------------------
// <copyright file="IGeoNameResolver.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.Abstractions.NameResolution;

using DcsvIo.D2.Result;

/// <summary>
/// Contract for resolving a free-form place-name string (from a 3rd-party
/// source — WhoIs response, IP-geolocation enrichment, vendor API, user
/// input) into the corresponding catalog record. Used at the integration
/// boundary; never called from domain-handler code (typed-handler-input
/// discipline).
/// </summary>
/// <remarks>
/// <para>
/// <b>Fail-closed semantics.</b> Implementations MUST be fail-closed:
/// when input is null / empty / whitespace / ambiguous / too short for a
/// fuzzy pass, OR when multiple entities match at the same score within
/// a pass, the resolver returns <see cref="D2Result.NotFound"/> rather
/// than guessing. The downstream consumer (in the WhoIs enrichment flow)
/// preserves the raw upstream string in the audit trail, so an
/// unresolved outcome is safe — a wrong fuzzy match is not.
/// </para>
/// <para>
/// <b>Cascade.</b> The resolution algorithm is a four-pass cascade with
/// per-pass minimum-input-length guards:
/// </para>
/// <list type="number">
///   <item>
///     <description>Pass 1 (exact) — no minimum input length. Always runs.</description>
///   </item>
///   <item>
///     <description>Pass 2 (startsWith) — skip if <c>q.length &lt; 4</c>.
///     Short prefixes are too promiscuous.</description>
///   </item>
///   <item>
///     <description>Pass 3 (contains) — skip if <c>q.length &lt; 5</c>.
///     Short substrings overmatch.</description>
///   </item>
///   <item>
///     <description>Pass 4 (Levenshtein) — skip if <c>q.length &lt; 5</c>;
///     within-pass <c>maxDistance &lt;= min(2, floor(q.length / 5))</c>.
///     Fuzzy on short inputs collapses distinct entities.</description>
///   </item>
/// </list>
/// <para>
/// The first matching pass wins; later passes do not run. Ambiguity within
/// a pass (multiple matches at the same score) returns
/// <see cref="D2Result.NotFound"/>. Catalog uniqueness is enforced at
/// codegen time via <c>D2GEO010</c>, so the determinism risk of
/// "first match wins" is eliminated at the source.
/// </para>
/// </remarks>
public interface IGeoNameResolver
{
    /// <summary>
    /// Resolves a free-form country-name string to a typed
    /// <see cref="Country"/> identifier via the fail-closed cascade.
    /// </summary>
    /// <param name="name">
    /// The free-form country name. May be null / empty / whitespace —
    /// implementations MUST validate input before performing any lookup and
    /// return <c>D2Result.ValidationFailed</c> for null / empty / whitespace,
    /// or <c>D2Result.NotFound</c> at implementer's discretion; either way
    /// the caller never sees a wrong-Country answer for bad input.
    /// </param>
    /// <returns>
    /// <c>D2Result.Ok(country)</c> on unambiguous match; <c>NotFound</c> on
    /// ambiguity, too-short input for any-pass match, or no-match at all
    /// passes; <c>ValidationFailed</c> on null / empty / whitespace input.
    /// </returns>
    D2Result<Country> TryResolveCountryByName(string name);

    /// <summary>
    /// Resolves a free-form subdivision-name string to the catalog's
    /// <see cref="Subdivision"/> record via the fail-closed cascade,
    /// scoped to <paramref name="parentCountry"/>. The parent-country
    /// scope is mandatory — "Georgia" is BOTH a country (GE) AND a US
    /// state (US-GA), and the resolver MUST NOT silently pick one.
    /// </summary>
    /// <param name="name">The free-form subdivision name.</param>
    /// <param name="parentCountry">The country whose subdivisions to search.</param>
    /// <returns>
    /// <c>D2Result.Ok(subdivision)</c> on unambiguous match;
    /// <c>NotFound</c> on ambiguity, too-short input, no match in that
    /// country's catalog, or coverage gap; <c>ValidationFailed</c> on
    /// null / empty / whitespace input.
    /// </returns>
    D2Result<Subdivision> TryResolveSubdivisionByName(
        string name, Country parentCountry);
}
