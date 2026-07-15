// -----------------------------------------------------------------------
// <copyright file="DefaultGeoNameResolver.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.Default.NameResolution;

using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Geo.Abstractions.NameResolution;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Default implementation of <see cref="IGeoNameResolver"/> over the
/// codegen-emitted catalog data. Implements a four-pass fail-closed cascade
/// (exact / startsWith / contains / length-scaled Levenshtein) with
/// cache-aside discipline: the first lookup is O(n) over the catalog and
/// builds a normalized-name → record map; subsequent lookups are O(1)
/// against the cached map. Ambiguity sentinels prevent silent wrong
/// resolution when two records share a normalized name. Sealed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cascade.</b> Each public method runs the same pipeline:
/// </para>
/// <list type="number">
///   <item><description>Predicate 0 (DoS guard) — reject input longer than
///   <c>MAX_NAME_LENGTH</c> (256 chars) before normalization. Without this
///   cap a hostile caller could amplify Pass-4 Levenshtein DP work to
///   billions of cells per call.</description></item>
///   <item><description>Predicate 1 (boundary validation) — return
///   <see cref="D2Result.ValidationFailed"/> for null / empty / whitespace
///   input, normalize otherwise. If normalization collapses to empty,
///   return <see cref="D2Result.ValidationFailed"/>.</description></item>
///   <item><description>Pass 1 (exact match) — always runs, no minimum
///   length. Short-circuits on the first non-ambiguous hit.</description></item>
///   <item><description>Pass 2 (startsWith) — skipped if normalized input
///   shorter than 4 chars; ambiguity at any pass returns
///   <see cref="D2Result.NotFound"/>.</description></item>
///   <item><description>Pass 3 (contains) — skipped if normalized input
///   shorter than 5 chars.</description></item>
///   <item><description>Pass 4 (Levenshtein) — skipped if normalized input
///   shorter than 5 chars; <c>maxDistance = min(2, len / 5)</c>.</description></item>
/// </list>
/// <para>
/// The first matching pass wins; later passes do not run. Ambiguity within
/// a pass (multiple candidate records, OR an ambiguity-sentinel hit in
/// Pass 1) returns <see cref="D2Result.NotFound"/> — never a guess.
/// </para>
/// <para>
/// <b>Ambiguity sentinel.</b> The catalog's emitted data may contain two
/// records that normalize to the same name (e.g. two cities sharing a
/// localized name). The cache-build phase detects these collisions and
/// stores a single <c>CountryCacheEntry</c> with <c>IsAmbiguous = true</c>
/// at that key. Pass-1 lookups hitting the sentinel return
/// <see cref="D2Result.NotFound"/>; Pass-2/3/4 walks exclude ambiguous
/// entries from the candidate pool. The sentinel publishes atomically with
/// the rest of the dictionary because the entry struct (record reference
/// AND <c>IsAmbiguous</c> flag) lives in one dictionary value — a single
/// <see cref="Lazy{T}"/> publish covers both fields, eliminating the
/// narrow race a two-field cache could expose.
/// </para>
/// <para>
/// <b>Cache-aside.</b> The country map is built once per process on first
/// call via <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>.
/// The per-country subdivision maps live in a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> keyed by
/// <see cref="CountryCode"/> — each parent country's map builds lazily on
/// first lookup for that parent. The catalog is immutable post-module-init
/// so the cache never invalidates.
/// </para>
/// <para>
/// <b>Cache-tier choice.</b> The resolver intentionally does NOT use
/// <c>ILocalCache</c>. Rationale: (a) the catalog is build-once-then-read-many,
/// so TTL / eviction / cross-instance coherency are unnecessary;
/// (b) per-get <see cref="D2Result"/> envelope plus dictionary indirection
/// on <c>ILocalCache</c> defeats the O(1) cache-aside design goal;
/// (c) module-scoped <c>Lazy&lt;FrozenDictionary&gt;</c> is the idiomatic
/// .NET pattern for build-once immutable caches over static data.
/// </para>
/// <para>
/// <b>Deterministic iteration order.</b> Catalog iteration during cache
/// build orders records by <c>Iso31661Alpha2Code</c> (or
/// <c>Iso31662Code</c> for subdivisions) using
/// <see cref="StringComparer.Ordinal"/> so two processes building the
/// cache independently agree byte-for-byte on which entries become
/// ambiguity sentinels.
/// </para>
/// <para>
/// <b>Memory profile.</b> Country map: ~250 records × ~6 matchable name
/// fields each = ~1,500 entries. Per-country subdivision maps:
/// ~3,600 subdivisions total × ~5 matchable name fields = ~18,000 entries
/// spread across the per-country dictionaries. Total bounded around
/// 500 KB worst case across all per-country maps combined.
/// </para>
/// <para>
/// <b>Thread safety.</b> All cache fields use single-publish semantics via
/// <see cref="Lazy{T}"/> with <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>.
/// Concurrent first-callers race once on the build factory; only one wins
/// and publishes; later callers read the published map. The resolver
/// methods themselves are pure dictionary reads and are safe for any
/// number of concurrent callers.
/// </para>
/// <para>
/// <b>Resolver is not a CQRS handler.</b> The smart-constructor
/// <c>Domain.Create(input) → D2Result&lt;Domain&gt;</c> pattern does NOT
/// apply here because the resolver returns existing catalog records (not
/// new domain entities); input validation happens at the top of each
/// public method (Predicate 0 length cap + Predicate 1 falsey check).
/// </para>
/// <para>
/// <b>No observability instrumentation.</b> The resolver intentionally
/// ships without <c>ActivitySource</c> / <c>Meter</c> / spans / counters.
/// It runs at every third-party text ingestion point (WhoIs responses,
/// IP-geolocation enrichment, vendor API replies); per-call
/// instrumentation overhead is unacceptable for a hot-path service.
/// </para>
/// <para>
/// <b>PII discipline.</b> The <c>name</c> parameter on every public method
/// is treated as opaque PII. The resolver never logs the input, never
/// attaches it to a <see cref="D2Result"/> reason field, and never
/// emits it as a span tag / metric label. Callers that need an audit
/// trail of the raw upstream string log it at their own layer with
/// appropriate redaction.
/// </para>
/// <para>
/// <b>TraceId flow.</b> The resolver does not accept an
/// <c>IRequestContext</c> parameter and emits results with
/// <c>traceId = null</c>. Callers replay the handler-scoped traceId via
/// <c>D2Result.WithTraceId(context.TraceId)</c> at the call site.
/// </para>
/// </remarks>
public sealed class DefaultGeoNameResolver : IGeoNameResolver
{
    /// <summary>
    /// Maximum accepted input length before normalization (Predicate 0
    /// DoS guard). Pass-4 Levenshtein DP is O(input.Length × key.Length)
    /// per catalog key; without this cap a multi-KB hostile input could
    /// amplify to billions of DP cells per call.
    /// </summary>
    internal const int MAX_NAME_LENGTH = 256;

    /// <summary>Pass 2 (startsWith) minimum normalized input length.</summary>
    internal const int MIN_LENGTH_PASS_2 = 4;

    /// <summary>Pass 3 (contains) minimum normalized input length.</summary>
    internal const int MIN_LENGTH_PASS_3 = 5;

    /// <summary>Pass 4 (Levenshtein) minimum normalized input length.</summary>
    internal const int MIN_LENGTH_PASS_4 = 5;

    /// <summary>Pass 4 Levenshtein distance cap (absolute upper bound).</summary>
    internal const int PASS_4_MAX_DISTANCE = 2;

    /// <summary>Pass 4 Levenshtein distance scale divisor.</summary>
    internal const int PASS_4_DISTANCE_SCALE = 5;

    /// <summary>
    /// Country cache. Single <see cref="Lazy{T}"/> published atomically;
    /// the entry value carries both the record reference and the
    /// ambiguity flag so the publish is one atomic field write.
    /// </summary>
    internal static readonly Lazy<FrozenDictionary<string, CountryCacheEntry>>
        SR_CountryByName = new(
            BuildCountryByNameMap,
            LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Per-country subdivision cache. Keyed by parent
    /// <see cref="CountryCode"/>; each parent's map publishes via its own
    /// <see cref="Lazy{T}"/> with the same single-atomic-publish guarantee.
    /// </summary>
    internal static readonly ConcurrentDictionary<
        CountryCode,
        Lazy<FrozenDictionary<string, SubdivisionCacheEntry>>>
        SR_SubdivisionByNameByCountry = new();

    private enum ScanMode
    {
        StartsWith,
        Contains,
    }

    /// <inheritdoc cref="IGeoNameResolver.TryResolveCountryByName"/>
    /// <param name="name">
    /// Free-form country name from third-party text data. May be null /
    /// empty / whitespace — handled by Predicate 1 falsey validation.
    /// Treated as opaque PII inside the resolver (never logged, never
    /// attached to result reason fields).
    /// </param>
    /// <returns>
    /// <see cref="D2Result.Ok"/> wrapping the matched <see cref="Country"/>
    /// record on unambiguous match; <see cref="D2Result.NotFound"/> when
    /// the cascade exhausts with no match OR any pass surfaces ambiguity;
    /// <see cref="D2Result.ValidationFailed"/> when input is null / empty
    /// / whitespace OR longer than
    /// <see cref="MAX_NAME_LENGTH"/> characters.
    /// </returns>
    public D2Result<Country> TryResolveCountryByName(string name)
    {
        // Predicate 0 — DoS guard.
        if (name is { Length: > MAX_NAME_LENGTH })
        {
            return D2Result<Country>.ValidationFailed(
                messages: [TK.Common.Errors.TOO_LONG]);
        }

        // Predicate 1 — boundary validation.
        if (name.Falsey())
        {
            return D2Result<Country>.ValidationFailed(
                messages: [TK.Common.Errors.NOT_NULL_VIOLATION]);
        }

        var q = NameNormalizer.Normalize(name);
        if (q.Length == 0)
        {
            return D2Result<Country>.ValidationFailed(
                messages: [TK.Common.Errors.NOT_NULL_VIOLATION]);
        }

        var map = SR_CountryByName.Value;
        return RunCascade(q, map, static entry => entry.Record!);
    }

    /// <inheritdoc cref="IGeoNameResolver.TryResolveSubdivisionByName"/>
    /// <param name="name">
    /// Free-form subdivision name from third-party text data. May be null
    /// / empty / whitespace — handled by Predicate 1 falsey validation.
    /// Treated as opaque PII inside the resolver.
    /// </param>
    /// <param name="parentCountry">
    /// The country whose subdivisions to search. Required —
    /// <c>null</c> returns <see cref="D2Result.ValidationFailed"/>.
    /// </param>
    /// <returns>
    /// <see cref="D2Result.Ok"/> wrapping the matched <see cref="Subdivision"/>
    /// record on unambiguous match; <see cref="D2Result.NotFound"/> when
    /// the cascade exhausts with no match in the parent country, the
    /// parent country has no subdivisions, OR any pass surfaces ambiguity;
    /// <see cref="D2Result.ValidationFailed"/> when input is null / empty
    /// / whitespace, longer than <see cref="MAX_NAME_LENGTH"/>
    /// characters, OR <paramref name="parentCountry"/> is <c>null</c>.
    /// </returns>
    public D2Result<Subdivision> TryResolveSubdivisionByName(
        string name, Country parentCountry)
    {
        // Defensive null-check: the interface signature declares
        // parentCountry as non-nullable, but the resolver sits behind a DI
        // boundary where consumers may pass a null parameter despite the
        // contract. Fail-closed with ValidationFailed rather than NRE.
        if (((object?)parentCountry) is null)
        {
            return D2Result<Subdivision>.ValidationFailed(
                messages: [TK.Common.Errors.NOT_NULL_VIOLATION]);
        }

        if (name is { Length: > MAX_NAME_LENGTH })
        {
            return D2Result<Subdivision>.ValidationFailed(
                messages: [TK.Common.Errors.TOO_LONG]);
        }

        if (name.Falsey())
        {
            return D2Result<Subdivision>.ValidationFailed(
                messages: [TK.Common.Errors.NOT_NULL_VIOLATION]);
        }

        var q = NameNormalizer.Normalize(name);
        if (q.Length == 0)
        {
            return D2Result<Subdivision>.ValidationFailed(
                messages: [TK.Common.Errors.NOT_NULL_VIOLATION]);
        }

        var lazy = SR_SubdivisionByNameByCountry.GetOrAdd(
            parentCountry.Iso31661Alpha2Code,
            static key => new Lazy<FrozenDictionary<string, SubdivisionCacheEntry>>(
                () => BuildSubdivisionByNameMap(key),
                LazyThreadSafetyMode.ExecutionAndPublication));

        var map = lazy.Value;
        return RunCascade(q, map, static entry => entry.Record!);
    }

    private static D2Result<TRecord> RunCascade<TEntry, TRecord>(
        string q,
        FrozenDictionary<string, TEntry> map,
        Func<TEntry, TRecord> recordSelector)
        where TEntry : struct, ICacheEntry
        where TRecord : class
    {
        // Pass 1 — exact match (always runs).
        if (map.TryGetValue(q, out var hit))
        {
            if (hit.IsAmbiguous)
            {
                return D2Result<TRecord>.NotFound(
                    messages: [TK.Geo.Errors.NAME_RESOLUTION_AMBIGUOUS]);
            }

            return D2Result<TRecord>.Ok(recordSelector(hit));
        }

        // Pass 2 — startsWith.
        if (q.Length >= MIN_LENGTH_PASS_2)
        {
            var pass2 = ScanCandidates(map, q, recordSelector, ScanMode.StartsWith);
            if (pass2.IsAmbiguous)
            {
                return D2Result<TRecord>.NotFound(
                    messages: [TK.Geo.Errors.NAME_RESOLUTION_AMBIGUOUS]);
            }

            if (pass2.Record is not null)
                return D2Result<TRecord>.Ok(pass2.Record);
        }

        // Pass 3 — contains.
        if (q.Length >= MIN_LENGTH_PASS_3)
        {
            var pass3 = ScanCandidates(map, q, recordSelector, ScanMode.Contains);
            if (pass3.IsAmbiguous)
            {
                return D2Result<TRecord>.NotFound(
                    messages: [TK.Geo.Errors.NAME_RESOLUTION_AMBIGUOUS]);
            }

            if (pass3.Record is not null)
                return D2Result<TRecord>.Ok(pass3.Record);
        }

        // Pass 4 — bounded Levenshtein.
        if (q.Length >= MIN_LENGTH_PASS_4)
        {
            var maxDistance = Math.Min(
                PASS_4_MAX_DISTANCE, q.Length / PASS_4_DISTANCE_SCALE);
            var pass4 = ScanLevenshtein(map, q, recordSelector, maxDistance);
            if (pass4.IsAmbiguous)
            {
                return D2Result<TRecord>.NotFound(
                    messages: [TK.Geo.Errors.NAME_RESOLUTION_AMBIGUOUS]);
            }

            if (pass4.Record is not null)
                return D2Result<TRecord>.Ok(pass4.Record);
        }

        // Cascade exhausted.
        return D2Result<TRecord>.NotFound(
            messages: [TK.Geo.Errors.NAME_RESOLUTION_NOT_FOUND]);
    }

    private static ScanResult<TRecord> ScanCandidates<TEntry, TRecord>(
        FrozenDictionary<string, TEntry> map,
        string q,
        Func<TEntry, TRecord> recordSelector,
        ScanMode mode)
        where TEntry : struct, ICacheEntry
        where TRecord : class
    {
        TRecord? winner = null;

        foreach (var kvp in map)
        {
            var entry = kvp.Value;
            if (entry.IsAmbiguous)
            {
                continue;
            }

            var key = kvp.Key;
            var match = mode switch
            {
                ScanMode.StartsWith => key.StartsWith(q, StringComparison.Ordinal),
                ScanMode.Contains => key.Contains(q, StringComparison.Ordinal),
                _ => false,
            };
            if (!match)
            {
                continue;
            }

            var record = recordSelector(entry);
            if (winner is null)
            {
                winner = record;
                continue;
            }

            // Two distinct candidate records at the same pass = ambiguity.
            if (!ReferenceEquals(winner, record))
            {
                return new ScanResult<TRecord>(null, true);
            }
        }

        return new ScanResult<TRecord>(winner, false);
    }

    private static ScanResult<TRecord> ScanLevenshtein<TEntry, TRecord>(
        FrozenDictionary<string, TEntry> map,
        string q,
        Func<TEntry, TRecord> recordSelector,
        int maxDistance)
        where TEntry : struct, ICacheEntry
        where TRecord : class
    {
        var bestDistance = int.MaxValue;
        TRecord? winner = null;
        var ambiguousAtBest = false;

        foreach (var kvp in map)
        {
            var entry = kvp.Value;
            if (entry.IsAmbiguous)
            {
                continue;
            }

            var distance = LevenshteinComparer.Compare(q, kvp.Key, maxDistance);
            if (distance > maxDistance)
            {
                continue;
            }

            var record = recordSelector(entry);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                winner = record;
                ambiguousAtBest = false;
            }
            else if (distance == bestDistance)
            {
                if (winner is null || !ReferenceEquals(winner, record))
                    ambiguousAtBest = true;
            }
        }

        if (ambiguousAtBest)
            return new ScanResult<TRecord>(null, true);

        return new ScanResult<TRecord>(winner, false);
    }

    private static FrozenDictionary<string, CountryCacheEntry> BuildCountryByNameMap()
    {
        var builder = new Dictionary<string, CountryCacheEntry>(StringComparer.Ordinal);

        // Deterministic ordering — cross-process agreement on which entries
        // become ambiguity sentinels.
        var ordered = CountryLookup.ByCode.Values
            .OrderBy(c => c.Iso31661Alpha2Code.ToString(), StringComparer.Ordinal);

        foreach (var country in ordered)
        {
            AddCountryKey(builder, country.DisplayName, country);
            AddCountryKey(builder, country.OfficialName, country);
            AddCountryKey(builder, country.EndonymDisplayName, country);
            AddCountryKey(builder, country.EndonymOfficialName, country);
            AddCountryKey(builder, country.Iso31661Alpha3Code, country);
            AddCountryKey(builder, country.Iso31661NumericCode, country);
        }

        return builder.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static void AddCountryKey(
        Dictionary<string, CountryCacheEntry> builder,
        string? rawName,
        Country country)
    {
        if (rawName.Falsey())
            return;

        var key = NameNormalizer.Normalize(rawName);
        if (key.Length == 0)
            return;

        if (builder.TryGetValue(key, out var existing))
        {
            // Re-mapping the same record via a different name field is a
            // no-op; a different record at the same key is an ambiguity.
            if (existing.IsAmbiguous)
                return;

            if (!ReferenceEquals(existing.Record, country))
                builder[key] = new CountryCacheEntry(null, IsAmbiguous: true);

            return;
        }

        builder[key] = new CountryCacheEntry(country, IsAmbiguous: false);
    }

    private static FrozenDictionary<string, SubdivisionCacheEntry> BuildSubdivisionByNameMap(
        CountryCode parent)
    {
        var builder = new Dictionary<string, SubdivisionCacheEntry>(StringComparer.Ordinal);

        if (!SubdivisionLookup.ByCountry.TryGetValue(parent, out var subdivisions))
            return builder.ToFrozenDictionary(StringComparer.Ordinal);

        var ordered = subdivisions
            .OrderBy(s => s.Iso31662Code.Value, StringComparer.Ordinal);

        foreach (var sub in ordered)
        {
            AddSubdivisionKey(builder, sub.DisplayName, sub);
            AddSubdivisionKey(builder, sub.OfficialName, sub);
            AddSubdivisionKey(builder, sub.EndonymDisplayName, sub);
            AddSubdivisionKey(builder, sub.EndonymOfficialName, sub);
            AddSubdivisionKey(builder, sub.ShortCode, sub);
        }

        return builder.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static void AddSubdivisionKey(
        Dictionary<string, SubdivisionCacheEntry> builder,
        string? rawName,
        Subdivision sub)
    {
        if (rawName.Falsey())
            return;

        var key = NameNormalizer.Normalize(rawName);
        if (key.Length == 0)
            return;

        if (builder.TryGetValue(key, out var existing))
        {
            if (existing.IsAmbiguous)
                return;

            if (!ReferenceEquals(existing.Record, sub))
                builder[key] = new SubdivisionCacheEntry(null, IsAmbiguous: true);

            return;
        }

        builder[key] = new SubdivisionCacheEntry(sub, IsAmbiguous: false);
    }

    private readonly record struct ScanResult<TRecord>(TRecord? Record, bool IsAmbiguous)
        where TRecord : class;
}
