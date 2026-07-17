// -----------------------------------------------------------------------
// <copyright file="SupportedLocalesTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.I18n;

using System.Collections.Generic;
using AwesomeAssertions;
using DcsvIo.D2.I18n;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class SupportedLocalesTests
{
    // ----------------------------------------------------------------------
    // ToBcp47 — pure normalizer
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("en-US", "en-US")]
    [InlineData("en-us", "en-US")]
    [InlineData("EN-us", "en-US")]
    [InlineData("EN-US", "en-US")]
    [InlineData("FR-CA", "fr-CA")]
    [InlineData("fr-ca", "fr-CA")]
    [InlineData("en", "en")]
    [InlineData("EN", "en")]
    public void ToBcp47_NormalizesToCanonicalCasing(string input, string expected)
    {
        SupportedLocales.ToBcp47(input).Should().Be(expected);
    }

    [Fact]
    public void ToBcp47_NoHyphen_LowercasesEntireString()
    {
        SupportedLocales.ToBcp47("ZH").Should().Be("zh");
    }

    [Fact]
    public void ToBcp47_LeadingHyphen_LowercasesEmptyLanguageAndUppercasesRegion()
    {
        // Adversarial: degenerate input "-CA" — pin behavior even though no
        // real BCP 47 tag looks like this.
        SupportedLocales.ToBcp47("-CA").Should().Be("-CA");
    }

    [Fact]
    public void ToBcp47_NullArgument_Throws()
    {
        var act = () => SupportedLocales.ToBcp47(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ----------------------------------------------------------------------
    // Construction — defaults when env vars absent
    // ----------------------------------------------------------------------

    [Fact]
    public void Ctor_NoConfiguration_DefaultsToEnUsOnly()
    {
        var sl = new SupportedLocales(EmptyConfig());

        sl.Base.Should().Be("en-US");
        sl.All.Should().Equal("en-US");
    }

    [Fact]
    public void Ctor_NullConfiguration_Throws()
    {
        var act = () => new SupportedLocales(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_EmptyEnabledLocalesSection_DefaultsToEnUsOnly()
    {
        var sl = new SupportedLocales(ConfigWith(("PUBLIC_ENABLED_LOCALES__0", string.Empty)));

        sl.All.Should().Equal("en-US");
    }

    // ----------------------------------------------------------------------
    // Construction — PUBLIC_DEFAULT_LOCALE
    // ----------------------------------------------------------------------

    [Fact]
    public void Ctor_DefaultLocaleConfigured_UsesIt()
    {
        var sl = new SupportedLocales(ConfigWith(("PUBLIC_DEFAULT_LOCALE", "fr-FR")));

        sl.Base.Should().Be("fr-FR");
    }

    [Fact]
    public void Ctor_DefaultLocaleHasMixedCase_NormalizedToCanonical()
    {
        var sl = new SupportedLocales(ConfigWith(("PUBLIC_DEFAULT_LOCALE", "EN-us")));

        sl.Base.Should().Be("en-US");
    }

    [Fact]
    public void Ctor_DefaultLocaleHasSurroundingWhitespace_Trimmed()
    {
        var sl = new SupportedLocales(ConfigWith(("PUBLIC_DEFAULT_LOCALE", "  fr-FR  ")));

        sl.Base.Should().Be("fr-FR");
    }

    [Fact]
    public void Ctor_DefaultLocaleEmpty_DefaultsToEnUs()
    {
        var sl = new SupportedLocales(ConfigWith(("PUBLIC_DEFAULT_LOCALE", string.Empty)));

        sl.Base.Should().Be("en-US");
    }

    // ----------------------------------------------------------------------
    // Construction — PUBLIC_ENABLED_LOCALES indexed section
    // ----------------------------------------------------------------------

    [Fact]
    public void Ctor_MultipleEnabledLocales_AllPreserved()
    {
        var sl = new SupportedLocales(ConfigWith(
            ("PUBLIC_ENABLED_LOCALES:0", "en-US"),
            ("PUBLIC_ENABLED_LOCALES:1", "fr-FR"),
            ("PUBLIC_ENABLED_LOCALES:2", "de-DE")));

        sl.All.Should().Equal("en-US", "fr-FR", "de-DE");
    }

    [Fact]
    public void Ctor_EnabledLocalesMixedCase_AllNormalized()
    {
        var sl = new SupportedLocales(ConfigWith(
            ("PUBLIC_ENABLED_LOCALES:0", "EN-us"),
            ("PUBLIC_ENABLED_LOCALES:1", "fr-fr")));

        sl.All.Should().Equal("en-US", "fr-FR");
    }

    [Fact]
    public void Ctor_EnabledLocalesWithEmptyEntries_EmptiesSkipped()
    {
        var sl = new SupportedLocales(ConfigWith(
            ("PUBLIC_ENABLED_LOCALES:0", "en-US"),
            ("PUBLIC_ENABLED_LOCALES:1", string.Empty),
            ("PUBLIC_ENABLED_LOCALES:2", "  "),
            ("PUBLIC_ENABLED_LOCALES:3", "fr-FR")));

        sl.All.Should().Equal("en-US", "fr-FR");
    }

    [Fact]
    public void Ctor_DuplicateLocalesAfterNormalization_KeepsBoth()
    {
        // Pin behavior: two entries that normalize to the same canonical
        // string are NOT deduped at config time. Callers / ops are expected
        // to keep PUBLIC_ENABLED_LOCALES clean.
        var sl = new SupportedLocales(ConfigWith(
            ("PUBLIC_ENABLED_LOCALES:0", "EN-us"),
            ("PUBLIC_ENABLED_LOCALES:1", "en-US")));

        sl.All.Should().Equal("en-US", "en-US");
    }

    // ----------------------------------------------------------------------
    // LanguageDefaults — first-locale-per-language wins
    // ----------------------------------------------------------------------

    [Fact]
    public void LanguageDefaults_FirstLocalePerLanguageWins()
    {
        var sl = new SupportedLocales(ConfigWith(
            ("PUBLIC_ENABLED_LOCALES:0", "en-CA"),
            ("PUBLIC_ENABLED_LOCALES:1", "en-US"),
            ("PUBLIC_ENABLED_LOCALES:2", "en-GB"),
            ("PUBLIC_ENABLED_LOCALES:3", "fr-FR"),
            ("PUBLIC_ENABLED_LOCALES:4", "fr-CA")));

        sl.LanguageDefaults["en"].Should().Be("en-CA");
        sl.LanguageDefaults["fr"].Should().Be("fr-FR");
    }

    [Fact]
    public void LanguageDefaults_DefaultConfig_HasOnlyEn()
    {
        var sl = new SupportedLocales(EmptyConfig());

        sl.LanguageDefaults.Should().ContainKey("en");
        sl.LanguageDefaults["en"].Should().Be("en-US");
        sl.LanguageDefaults.Count.Should().Be(1);
    }

    // ----------------------------------------------------------------------
    // IsValid
    // ----------------------------------------------------------------------

    [Fact]
    public void IsValid_KnownLocale_ReturnsTrue()
    {
        var sl = new SupportedLocales(ConfigWith(
            ("PUBLIC_ENABLED_LOCALES:0", "en-US"),
            ("PUBLIC_ENABLED_LOCALES:1", "fr-FR")));

        sl.IsValid("en-US").Should().BeTrue();
        sl.IsValid("fr-FR").Should().BeTrue();
    }

    [Fact]
    public void IsValid_CaseInsensitive_NormalizesBeforeChecking()
    {
        var sl = new SupportedLocales(ConfigWith(("PUBLIC_ENABLED_LOCALES:0", "en-US")));

        sl.IsValid("EN-us").Should().BeTrue();
    }

    [Fact]
    public void IsValid_UnknownLocale_ReturnsFalse()
    {
        var sl = new SupportedLocales(EmptyConfig());

        sl.IsValid("zh-CN").Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // Resolve
    // ----------------------------------------------------------------------

    [Fact]
    public void Resolve_Null_ReturnsBase()
    {
        var sl = new SupportedLocales(EmptyConfig());

        sl.Resolve(null).Should().Be("en-US");
    }

    [Fact]
    public void Resolve_Empty_ReturnsBase()
    {
        var sl = new SupportedLocales(EmptyConfig());

        sl.Resolve(string.Empty).Should().Be("en-US");
    }

    [Fact]
    public void Resolve_Whitespace_ReturnsBase()
    {
        var sl = new SupportedLocales(EmptyConfig());

        sl.Resolve("   ").Should().Be("en-US");
    }

    [Fact]
    public void Resolve_KnownCanonical_ReturnsAsIs()
    {
        var sl = new SupportedLocales(ConfigWith(
            ("PUBLIC_ENABLED_LOCALES:0", "en-US"),
            ("PUBLIC_ENABLED_LOCALES:1", "fr-FR")));

        sl.Resolve("fr-FR").Should().Be("fr-FR");
    }

    [Fact]
    public void Resolve_KnownButMixedCase_NormalizedAndReturned()
    {
        var sl = new SupportedLocales(ConfigWith(
            ("PUBLIC_ENABLED_LOCALES:0", "fr-FR")));

        sl.Resolve("FR-fr").Should().Be("fr-FR");
    }

    [Fact]
    public void Resolve_UnknownRegionWithKnownLanguage_FallsBackToFirstLocaleOfThatLanguage()
    {
        var sl = new SupportedLocales(ConfigWith(
            ("PUBLIC_ENABLED_LOCALES:0", "fr-FR"),
            ("PUBLIC_ENABLED_LOCALES:1", "fr-CA")));

        // fr-CH is not enabled, but fr-* fallback finds fr-FR (first fr-*).
        sl.Resolve("fr-CH").Should().Be("fr-FR");
    }

    [Fact]
    public void Resolve_BareLanguageCodeKnown_ResolvesToFirstLocaleOfThatLanguage()
    {
        var sl = new SupportedLocales(ConfigWith(
            ("PUBLIC_ENABLED_LOCALES:0", "en-US"),
            ("PUBLIC_ENABLED_LOCALES:1", "fr-FR")));

        sl.Resolve("fr").Should().Be("fr-FR");
    }

    [Fact]
    public void Resolve_UnknownLanguage_FallsBackToBase()
    {
        var sl = new SupportedLocales(ConfigWith(("PUBLIC_ENABLED_LOCALES:0", "en-US")));

        sl.Resolve("zh-CN").Should().Be("en-US");
        sl.Resolve("zh").Should().Be("en-US");
    }

    [Fact]
    public void Resolve_SurroundingWhitespace_TrimmedBeforeResolution()
    {
        var sl = new SupportedLocales(ConfigWith(("PUBLIC_ENABLED_LOCALES:0", "fr-FR")));

        sl.Resolve("  fr-FR  ").Should().Be("fr-FR");
    }

    // ----------------------------------------------------------------------
    // No xUnit collection annotation needed — instance state means parallel
    // tests do NOT share state. (v1 had this whole class as static and required
    // [Collection("I18n")] on every test class touching it.)
    // ----------------------------------------------------------------------

    [Fact]
    public void Instances_AreIndependent_NotSharedAcrossTests()
    {
        // Adversarial: two SupportedLocales constructed with different configs
        // must NOT share state. Pins the v2 instance-class shape.
        var a = new SupportedLocales(ConfigWith(("PUBLIC_DEFAULT_LOCALE", "fr-FR")));
        var b = new SupportedLocales(ConfigWith(("PUBLIC_DEFAULT_LOCALE", "de-DE")));

        a.Base.Should().Be("fr-FR");
        b.Base.Should().Be("de-DE");
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static IConfiguration EmptyConfig()
        => new ConfigurationBuilder().Build();

    private static IConfiguration ConfigWith(params (string Key, string Value)[] pairs)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (key, value) in pairs)
        {
            dict[key] = value;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();
    }
}
