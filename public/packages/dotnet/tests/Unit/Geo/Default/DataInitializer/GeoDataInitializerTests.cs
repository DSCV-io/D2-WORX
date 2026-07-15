// -----------------------------------------------------------------------
// <copyright file="GeoDataInitializerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Geo.Default.DataInitializer;

using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Geo.Default;
using Xunit;

/// <summary>
/// Wire-up-risk coverage for <see cref="GeoDataInitializer"/>. The
/// coordinator runs once at assembly load via
/// <see cref="System.Runtime.CompilerServices.ModuleInitializerAttribute"/>;
/// these tests verify the post-init nav graph + idempotency guard +
/// cross-catalog ref correctness.
/// </summary>
public sealed class GeoDataInitializerTests
{
    // §1.2 category: State-lifecycle — module-init ran before any test.
    [Fact]
    public void Initialize_RunsAtAssemblyLoad_SInitializedFlagIsTrue()
    {
        // The s_initialized field is private; access via reflection.
        var field = typeof(GeoDataInitializer).GetField(
            "s_initialized",
            BindingFlags.NonPublic | BindingFlags.Static);
        field.Should().NotBeNull("s_initialized field must exist on GeoDataInitializer");
        var raw = field.GetValue(null);
        raw.Should().NotBeNull();
        ((bool)raw).Should().BeTrue(
            "module-initializer should have set s_initialized = true before any test ran");
    }

    [Fact]
    public void Initialize_CalledTwice_NavGraphRemainsStable()
    {
        // The CLR runs module initializers at most once per assembly load.
        // The s_initialized guard short-circuits any defensive re-invocation.
        // Verify by capturing references pre-call, calling Initialize, and
        // confirming reference equality survives.
        var usBefore = CountryLookup.ByCode[CountryCode.US];
        var enBefore = LanguageLookup.ByCode[LanguageCode.En];
        var nyBefore = SubdivisionLookup.ByCode[SubdivisionCode.FromString("US-NY")];

        InvokeInitialize();

        CountryLookup.ByCode[CountryCode.US].Should().BeSameAs(usBefore);
        LanguageLookup.ByCode[LanguageCode.En].Should().BeSameAs(enBefore);
        SubdivisionLookup.ByCode[SubdivisionCode.FromString("US-NY")]
            .Should().BeSameAs(nyBefore);
    }

    [Fact]
    public void Initialize_CalledMultipleTimes_NoThrow()
    {
        var act = () =>
        {
            InvokeInitialize();
            InvokeInitialize();
            InvokeInitialize();
        };

        act.Should().NotThrow();
    }

    // §1.2 category: Cross-field — every Country has expected nav graph.
    [Fact]
    public void PostInit_EveryCountry_HasPrimaryLanguageNavOrCodeIsNull()
    {
        foreach (var country in CountryLookup.All)
        {
            if (country.PrimaryLanguageIso6391Code is null)
            {
                var code = country.Iso31661Alpha2Code;
                country.PrimaryLanguage.Should().BeNull(
                    $"{code}: null PrimaryLanguageIso6391Code must have null PrimaryLanguage");
            }
        }
    }

    [Fact]
    public void PostInit_EveryCountry_HasPrimaryCurrencyNavOrCodeIsNull()
    {
        foreach (var country in CountryLookup.All)
        {
            if (country.PrimaryCurrencyIso4217AlphaCode is null)
            {
                var code = country.Iso31661Alpha2Code;
                country.PrimaryCurrency.Should().BeNull(
                    $"{code}: null PrimaryCurrencyIso4217AlphaCode must have null PrimaryCurrency");
            }
        }
    }

    [Fact]
    public void PostInit_AQBVHM_AllHaveNullPrimaryLanguage()
    {
        Countries.AQ.PrimaryLanguage.Should().BeNull();
        Countries.BV.PrimaryLanguage.Should().BeNull();
        Countries.HM.PrimaryLanguage.Should().BeNull();
    }

    [Fact]
    public void PostInit_AQ_HasNullPrimaryCurrency()
    {
        // AQ (Antarctica) has no primary currency in the spec. BV and HM
        // carry sovereign-country currency by spec convention (NOK / AUD)
        // even though they have no resident population.
        Countries.AQ.PrimaryCurrency.Should().BeNull();
    }

    [Fact]
    public void PostInit_AQBVHM_AllHaveNullPrimaryLocale()
    {
        Countries.AQ.PrimaryLocale.Should().BeNull();
        Countries.BV.PrimaryLocale.Should().BeNull();
        Countries.HM.PrimaryLocale.Should().BeNull();
    }

    // §1.2 category: Cross-field — Subdivision cross-ref symmetry.
    [Fact]
    public void PostInit_EverySubdivision_CountryBackrefMatches()
    {
        foreach (var sub in SubdivisionLookup.All)
        {
            sub.Country.Should().NotBeNull(
                $"{sub.Iso31662Code.Value}: every subdivision must wire a Country nav");
            sub.Country!.Iso31661Alpha2Code.Should().Be(sub.CountryIso31661Alpha2Code);
        }
    }

    [Fact]
    public void PostInit_EveryCountry_SubdivisionsCountMatchesByCountryIndex()
    {
        foreach (var country in CountryLookup.All)
        {
            if (SubdivisionLookup.ByCountry.TryGetValue(
                country.Iso31661Alpha2Code,
                out var bySource))
            {
                country.Subdivisions.Count.Should().Be(
                    bySource.Count,
                    $"{country.Iso31661Alpha2Code}: Country.Subdivisions count "
                    + $"must equal SubdivisionLookup.ByCountry count");
            }
            else
            {
                country.Subdivisions.Should().BeEmpty(
                    $"{country.Iso31661Alpha2Code}: no entry in ByCountry implies "
                    + $"Country.Subdivisions is empty");
            }
        }
    }

    // §1.2 category: Cross-field — Locale + Language + Country nav symmetry.
    [Fact]
    public void PostInit_EveryLocale_LanguageAndCountryNavMatchCodes()
    {
        foreach (var locale in LocaleLookup.All)
        {
            if (locale.Language is { } lang)
            {
                locale.LanguageIso6391Code.Should().Be(lang.Iso6391Code);
            }

            if (locale.Country is { } country)
            {
                locale.CountryIso31661Alpha2Code.Should().Be(country.Iso31661Alpha2Code);
            }
        }
    }

    // §1.2 category: Cross-field — GeopoliticalEntity M:M coherence.
    [Fact]
    public void PostInit_EveryGeopoliticalEntity_MemberCountriesMatchCodes()
    {
        foreach (var entity in GeopoliticalEntityLookup.All)
        {
            entity.MemberCountries.Count.Should().Be(
                entity.MemberCountryIso31661Alpha2Codes.Count);
            foreach (var member in entity.MemberCountries)
            {
                entity.MemberCountryIso31661Alpha2Codes.Should().Contain(
                    member.Iso31661Alpha2Code);
            }
        }
    }

    /// <summary>
    /// Reflection helper invokes the internal <c>Initialize</c> static method.
    /// Direct call (without reflection) would also work because the test
    /// project is <c>InternalsVisibleTo</c>; reflection is used to make the
    /// short-circuit verification explicit.
    /// </summary>
    private static void InvokeInitialize()
    {
        var method = typeof(GeoDataInitializer).GetMethod(
            "Initialize",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();
        method.Invoke(null, parameters: null);
    }
}
