// -----------------------------------------------------------------------
// <copyright file="DefaultLayerIRequestContextGeoExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Geo.Default.Extensions;

using AwesomeAssertions;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Geo.Default.Extensions;
using Xunit;

/// <summary>
/// Coverage for the Default-layer record-returning
/// <see cref="IRequestContextGeoExtensions"/> — the namespace-shadowed
/// extension that returns the typed record instead of the typed code.
/// </summary>
public sealed class DefaultLayerIRequestContextGeoExtensionsTests
{
    // §1.2 category: Domain-specific — happy path.
    [Fact]
    public void Country_ValidUSRaw_ReturnsCountryRecord()
    {
        var ctx = new MutableRequestContext { CountryIso31661Alpha2Code = "US" };

        var country = ctx.Country();

        country.Should().NotBeNull();
        country.Iso31661Alpha2Code.Should().Be(CountryCode.US);
    }

    [Fact]
    public void Country_ValidJPRaw_ReturnsCountryRecord()
    {
        var ctx = new MutableRequestContext { CountryIso31661Alpha2Code = "JP" };
        ctx.Country().Should().NotBeNull();
        ctx.Country()!.Iso31661Alpha2Code.Should().Be(CountryCode.JP);
    }

    // §1.2 category: Input validation — boundary contract.
    [Fact]
    public void Country_NullRaw_ReturnsNull()
    {
        var ctx = new MutableRequestContext { CountryIso31661Alpha2Code = null };
        ctx.Country().Should().BeNull();
    }

    [Fact]
    public void Country_EmptyRaw_ReturnsNull()
    {
        var ctx = new MutableRequestContext { CountryIso31661Alpha2Code = string.Empty };
        ctx.Country().Should().BeNull();
    }

    [Fact]
    public void Country_WhitespaceRaw_ReturnsNull()
    {
        var ctx = new MutableRequestContext { CountryIso31661Alpha2Code = "   " };
        ctx.Country().Should().BeNull();
    }

    [Fact]
    public void Country_UnknownCodeRaw_ReturnsNull()
    {
        var ctx = new MutableRequestContext { CountryIso31661Alpha2Code = "ZZ" };
        ctx.Country().Should().BeNull();
    }

    [Fact]
    public void Country_LowercaseRaw_ParsesViaIgnoreCase()
    {
        // The Abstractions-layer parser uses TryParseTruthyNull<CountryCode>
        // with ignoreCase: true; the Default-layer wrapper inherits that
        // contract verbatim. Lowercase raw "us" resolves to CountryCode.US.
        var ctx = new MutableRequestContext { CountryIso31661Alpha2Code = "us" };
        var country = ctx.Country();
        country.Should().NotBeNull();
        country.Iso31661Alpha2Code.Should().Be(CountryCode.US);
    }

    [Theory]
    [InlineData("us")]
    [InlineData("US")]
    [InlineData("Us")]
    [InlineData("uS")]
    public void Country_MixedCaseRaw_ResolvesUniformlyViaIgnoreCase(string raw)
    {
        // Cross-language lenient parser contract: any-case input resolves
        // to the canonical uppercase record. JWT claims minted with
        // lowercase / mixed-case codes resolve uniformly across .NET + TS.
        var ctx = new MutableRequestContext { CountryIso31661Alpha2Code = raw };
        var country = ctx.Country();
        country.Should().NotBeNull();
        country.Iso31661Alpha2Code.Should().Be(CountryCode.US);
    }

    // §1.2 category: Domain-specific — AQ has null primaries.
    [Fact]
    public void Country_AQRaw_ReturnsAntarcticaWithNullPrimaries()
    {
        var ctx = new MutableRequestContext { CountryIso31661Alpha2Code = "AQ" };
        var aq = ctx.Country();

        aq.Should().NotBeNull();
        aq.Iso31661Alpha2Code.Should().Be(CountryCode.AQ);
        aq.PrimaryLanguage.Should().BeNull();
        aq.PrimaryCurrency.Should().BeNull();
        aq.PrimaryLocale.Should().BeNull();
    }

    // §1.2 category: Domain-specific — Subdivision happy path.
    [Fact]
    public void Subdivision_ValidUSNYRaw_ReturnsSubdivisionRecord()
    {
        var ctx = new MutableRequestContext { SubdivisionIso31662Code = "US-NY" };

        var sub = ctx.Subdivision();

        sub.Should().NotBeNull();
        sub.Iso31662Code.Value.Should().Be("US-NY");
    }

    [Fact]
    public void Subdivision_ValidCAONRaw_ReturnsSubdivisionRecord()
    {
        var ctx = new MutableRequestContext { SubdivisionIso31662Code = "CA-ON" };
        var sub = ctx.Subdivision();
        sub.Should().NotBeNull();
        sub.Iso31662Code.Value.Should().Be("CA-ON");
    }

    [Fact]
    public void Subdivision_NullRaw_ReturnsNull()
    {
        var ctx = new MutableRequestContext { SubdivisionIso31662Code = null };
        ctx.Subdivision().Should().BeNull();
    }

    [Fact]
    public void Subdivision_EmptyRaw_ReturnsNull()
    {
        var ctx = new MutableRequestContext { SubdivisionIso31662Code = string.Empty };
        ctx.Subdivision().Should().BeNull();
    }

    [Fact]
    public void Subdivision_WhitespaceRaw_ReturnsNull()
    {
        var ctx = new MutableRequestContext { SubdivisionIso31662Code = "   " };
        ctx.Subdivision().Should().BeNull();
    }

    [Fact]
    public void Subdivision_UnknownCodeRaw_ReturnsNull()
    {
        var ctx = new MutableRequestContext { SubdivisionIso31662Code = "ZZ-99" };
        ctx.Subdivision().Should().BeNull();
    }

    [Theory]
    [InlineData("us-ny")]
    [InlineData("US-NY")]
    [InlineData("Us-Ny")]
    [InlineData("uS-nY")]
    public void Subdivision_MixedCaseRaw_ResolvesUniformlyViaIgnoreCase(string raw)
    {
        // Cross-language lenient parser contract: any-case input is
        // uppercased before parser/catalog lookup so JWT claims minted
        // with lowercase / mixed-case codes resolve uniformly across
        // .NET + TS.
        var ctx = new MutableRequestContext { SubdivisionIso31662Code = raw };
        var sub = ctx.Subdivision();
        sub.Should().NotBeNull();
        sub.Iso31662Code.Value.Should().Be("US-NY");
    }

    // §1.2 category: Domain-specific — nested data access pattern.
    [Fact]
    public void Country_NestedDataAccess_PrimaryLanguageDisplayName_NoSecondLookupNeeded()
    {
        var ctx = new MutableRequestContext { CountryIso31661Alpha2Code = "US" };
        var lang = ctx.Country()?.PrimaryLanguage?.DisplayName;
        lang.Should().NotBeNullOrEmpty();
    }
}
