// -----------------------------------------------------------------------
// <copyright file="ConfusablesTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Geo.Default.NameResolver;

using System;
using System.IO;
using System.Text.Json;
using AwesomeAssertions;
using D2.Shared.Geo.Abstractions;
using D2.Shared.Geo.Default.NameResolution;
using D2.Shared.Tests.Unit.Auth;
using Xunit;

/// <summary>
/// Fixture-driven cross-language adversarial test. Loads
/// <c>contracts/geo/fixtures/confusables.fixture.json</c> and walks
/// every row through the resolver. The TS-side
/// <c>@d2/geo-default</c> test loads the same fixture and asserts
/// byte-identical outcomes. Same input MUST resolve identically on
/// both runtimes.
/// </summary>
public sealed class ConfusablesTests
{
    private readonly DefaultGeoNameResolver _resolver = new();

    public static TheoryData<string, string?, string> CountryCases =>
        LoadCountryCases();

    public static TheoryData<string, string, string?, string> SubdivisionCases =>
        LoadSubdivisionCases();

    [Theory]
    [MemberData(nameof(CountryCases))]
    public void Country_FixtureCase_ResolvesAsExpected(
        string input, string? expectedAlpha2, string comment)
    {
        _ = comment;
        var result = _resolver.TryResolveCountryByName(input);

        if (expectedAlpha2 is null)
        {
            result.Success.Should().BeFalse(
                "fixture pins null expected — must NOT silently guess");
        }
        else
        {
            result.Success.Should().BeTrue($"fixture pins {expectedAlpha2}");
            result.Data!.Iso31661Alpha2Code.ToString().Should().Be(expectedAlpha2);
        }
    }

    [Theory]
    [MemberData(nameof(SubdivisionCases))]
    public void Subdivision_FixtureCase_ResolvesAsExpected(
        string input,
        string parentAlpha2,
        string? expectedIso31662,
        string comment)
    {
        _ = comment;
        var parent = D2.Shared.Geo.Default.CountryLookup.ByCode[
            Enum.Parse<CountryCode>(parentAlpha2)];

        var result = _resolver.TryResolveSubdivisionByName(input, parent);

        if (expectedIso31662 is null)
        {
            result.Success.Should().BeFalse(
                "fixture pins null expected — must NOT silently guess");
        }
        else
        {
            result.Success.Should().BeTrue($"fixture pins {expectedIso31662}");
            result.Data!.Iso31662Code.Value.Should().Be(expectedIso31662);
        }
    }

    private static TheoryData<string, string?, string> LoadCountryCases()
    {
        var data = new TheoryData<string, string?, string>();
        var doc = LoadFixtureDoc();
        foreach (var entry in doc.RootElement.GetProperty("countryCases").EnumerateArray())
        {
            var input = entry.GetProperty("input").GetString()!;
            var expected = entry.GetProperty("expectedIso31661Alpha2Code");
            string? expectedAlpha2 = expected.ValueKind == JsonValueKind.Null
                ? null
                : expected.GetString();
            var comment = entry.GetProperty("comment").GetString()!;
            data.Add(input, expectedAlpha2, comment);
        }

        return data;
    }

    private static TheoryData<string, string, string?, string> LoadSubdivisionCases()
    {
        var data = new TheoryData<string, string, string?, string>();
        var doc = LoadFixtureDoc();
        foreach (var entry in doc.RootElement.GetProperty("subdivisionCases").EnumerateArray())
        {
            var input = entry.GetProperty("input").GetString()!;
            var parent = entry.GetProperty("parentCountryIso31661Alpha2Code").GetString()!;
            var expected = entry.GetProperty("expectedIso31662Code");
            string? expectedIso31662 = expected.ValueKind == JsonValueKind.Null
                ? null
                : expected.GetString();
            var comment = entry.GetProperty("comment").GetString()!;
            data.Add(input, parent, expectedIso31662, comment);
        }

        return data;
    }

    private static JsonDocument LoadFixtureDoc()
    {
        var path = Path.Combine(
            TestPaths.RepoRoot(),
            "contracts",
            "geo",
            "fixtures",
            "confusables.fixture.json");
        var json = File.ReadAllText(path);
        return JsonDocument.Parse(json);
    }
}
