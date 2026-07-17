// -----------------------------------------------------------------------
// <copyright file="CacheAsideTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Geo.Default.NameResolver;

using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Geo.Default;
using DcsvIo.D2.Geo.Default.NameResolution;
using Xunit;

/// <summary>
/// Cache-aside discipline coverage — first-call builds, subsequent calls
/// hit cache, concurrent first-callers race but only one build wins. Uses
/// the internal <c>SR_CountryByName</c> Lazy field via the existing
/// InternalsVisibleTo declaration.
/// </summary>
public sealed class CacheAsideTests
{
    // §1.2 category: State-lifecycle — first call triggers Lazy.Value.
    [Fact]
    public void CountryCache_FirstCallBuildsMap()
    {
        // The Lazy is module-scoped — IsValueCreated may already be true
        // from earlier tests. The behavioral pin is that AFTER any call,
        // the cache IS materialized.
        _ = new DefaultGeoNameResolver().TryResolveCountryByName("United States");

        DefaultGeoNameResolver.SR_CountryByName.IsValueCreated.Should().BeTrue();
        DefaultGeoNameResolver.SR_CountryByName.Value.Should().NotBeNull();
        DefaultGeoNameResolver.SR_CountryByName.Value.Count.Should().BeGreaterThan(100);
    }

    // §1.2 category: State-lifecycle — second call reuses same map instance.
    [Fact]
    public void CountryCache_SecondCallReusesMap()
    {
        var resolver = new DefaultGeoNameResolver();
        _ = resolver.TryResolveCountryByName("United States");
        var mapAfterFirst = DefaultGeoNameResolver.SR_CountryByName.Value;

        _ = resolver.TryResolveCountryByName("Australia");
        var mapAfterSecond = DefaultGeoNameResolver.SR_CountryByName.Value;

        // Lazy<T> ExecutionAndPublication semantics — second access returns
        // the same materialized value.
        mapAfterSecond.Should().BeSameAs(mapAfterFirst);
    }

    // §1.2 category: Concurrency — thundering-herd safety.
    [Fact]
    public async Task CountryCache_ConcurrentFirstCallers_AllReceiveSameMap()
    {
        var resolver = new DefaultGeoNameResolver();
        const int parallelism = 32;

        var tasks = Enumerable.Range(0, parallelism)
            .Select(_ => Task.Run(() =>
                resolver.TryResolveCountryByName("United States")))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Every caller resolves to the same Country record reference —
        // proves the cache published atomically (no torn entries) and
        // every caller hits the same materialized map.
        var first = results[0].Data;
        first.Should().NotBeNull();
        foreach (var r in results)
        {
            r.Success.Should().BeTrue();
            r.Data.Should().BeSameAs(first);
        }
    }

    // §1.2 category: Concurrency — per-key build-once for subdivisions.
    [Fact]
    public void SubdivisionCache_DifferentCountries_HaveSeparateMaps()
    {
        var resolver = new DefaultGeoNameResolver();
        var us = CountryLookup.ByCode[CountryCode.US];
        var ca = CountryLookup.ByCode[CountryCode.CA];

        _ = resolver.TryResolveSubdivisionByName("California", us);
        DefaultGeoNameResolver.SR_SubdivisionByNameByCountry
            .ContainsKey(CountryCode.US).Should().BeTrue();

        _ = resolver.TryResolveSubdivisionByName("Ontario", ca);
        DefaultGeoNameResolver.SR_SubdivisionByNameByCountry
            .ContainsKey(CountryCode.CA).Should().BeTrue();

        // The per-country maps must be distinct FrozenDictionary instances.
        var usMap = DefaultGeoNameResolver.SR_SubdivisionByNameByCountry[CountryCode.US].Value;
        var caMap = DefaultGeoNameResolver.SR_SubdivisionByNameByCountry[CountryCode.CA].Value;
        usMap.Should().NotBeSameAs(caMap);
    }

    // §1.2 category: State-lifecycle — empty parent country.
    [Fact]
    public void SubdivisionCache_CountryWithNoSubdivisions_BuildsEmptyMap()
    {
        var resolver = new DefaultGeoNameResolver();
        var aq = CountryLookup.ByCode[CountryCode.AQ];

        _ = resolver.TryResolveSubdivisionByName("Anywhere", aq);

        DefaultGeoNameResolver.SR_SubdivisionByNameByCountry
            .ContainsKey(CountryCode.AQ).Should().BeTrue();
        var aqMap = DefaultGeoNameResolver.SR_SubdivisionByNameByCountry[CountryCode.AQ].Value;
        aqMap.Count.Should().Be(0);
    }
}
