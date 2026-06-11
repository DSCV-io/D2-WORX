// -----------------------------------------------------------------------
// <copyright file="KeyCustodianPreconditionPlaceholderTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.SpecsConsistency;

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using D2.Edge.Tests.Unit.KeyCustodian.SourceGen;

/// <summary>
/// Drift guard for the <c>keycustodian_internal_PRECONDITION_VIOLATED</c>
/// message template. The template is an opaque 500/internal_error message —
/// it MUST NOT carry an <c>{arg}</c> placeholder because that would re-introduce
/// the wire leak that S-1 closed. This test fails loudly if any of the ten
/// locales accidentally re-adds the placeholder.
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
    public void EveryLocale_PreconditionMessage_DoesNotContainArgPlaceholder(string locale)
    {
        var catalog = LoadLocale(locale);

        catalog.Should().ContainKey(
            _KEY, because: $"the {locale} catalog must declare the KC precondition key");
        catalog[_KEY].Should().NotContain(
            _PLACEHOLDER,
            because:
                $"the {locale} '{_KEY}' message is opaque — the {{arg}} placeholder "
                + "must not appear or internal C# parameter names would leak onto the wire");
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
