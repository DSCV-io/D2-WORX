// -----------------------------------------------------------------------
// <copyright file="KeyCustodianPreconditionPlaceholderTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.SpecsConsistency;

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AwesomeAssertions;
using D2.Edge.Tests.Unit.KeyCustodian.SourceGen;
using Xunit;

/// <summary>
/// Drift guard for the <c>keycustodian_internal_PRECONDITION_VIOLATED</c>
/// message template. The converted KC domain guards bind the offending argument
/// via <c>TK.Keycustodian.Internal.PRECONDITION_VIOLATED.With("arg", "&lt;name&gt;")</c>,
/// so EVERY locale's message value MUST carry the <c>{arg}</c> placeholder for
/// the binding to render. A locale that drops the token would silently render
/// the bound arg nowhere — this test fails loudly if any of the ten locales
/// loses the placeholder.
/// </summary>
public sealed class KeyCustodianPreconditionPlaceholderTests
{
    private const string _KEY = "keycustodian_internal_PRECONDITION_VIOLATED";
    private const string _PLACEHOLDER = "{arg}";

    private static readonly string[] sr_locales =
    [
        "en-US", "en-GB", "en-CA", "de-DE", "es-ES",
        "es-MX", "fr-CA", "fr-FR", "it-IT", "ja-JP",
    ];

    [Theory]
    [InlineData("en-US")]
    [InlineData("en-GB")]
    [InlineData("en-CA")]
    [InlineData("de-DE")]
    [InlineData("es-ES")]
    [InlineData("es-MX")]
    [InlineData("fr-CA")]
    [InlineData("fr-FR")]
    [InlineData("it-IT")]
    [InlineData("ja-JP")]
    public void EveryLocale_PreconditionMessage_CarriesArgPlaceholder(string locale)
    {
        var catalog = LoadLocale(locale);

        catalog.Should().ContainKey(
            _KEY, because: $"the {locale} catalog must declare the KC precondition key");
        catalog[_KEY].Should().Contain(
            _PLACEHOLDER,
            because:
                $"the {locale} '{_KEY}' value must keep the {{arg}} token so the runtime "
                + "TKMessage.With(\"arg\", ...) binding renders the offending argument");
    }

    [Fact]
    public void AllLocales_CoverTheSameTenSupportedLocales()
    {
        // Pins the locale set the placeholder check walks — a new locale added to
        // contracts/messages would need the same {arg} guarantee.
        sr_locales.Should().HaveCount(10);
    }

    private static Dictionary<string, string> LoadLocale(string locale)
    {
        var dir = Path.GetDirectoryName(TestPaths.EnUsMessages())!;
        var path = Path.Combine(dir, $"{locale}.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
    }
}
