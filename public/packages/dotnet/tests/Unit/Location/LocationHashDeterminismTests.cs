// -----------------------------------------------------------------------
// <copyright file="LocationHashDeterminismTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Location;

using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AwesomeAssertions;
using D2.Shared.Geo.Abstractions;
using D2.Shared.Location;
using D2.Shared.Location.ValueObjects;
using Xunit;

/// <summary>
/// Loads <c>contracts/location/parity-fixtures.json</c> and asserts the
/// <see cref="Coordinates"/> / <see cref="StreetAddress"/> /
/// <see cref="AdminLocation"/> / <see cref="ComposeLocationHash"/> /
/// <see cref="DefaultPostalCodeValidator"/> implementations produce
/// byte-identical hash output to the reference fixture for every case.
/// </summary>
/// <remarks>
/// Hash-determinism regression pin. A byte divergence here means the hash
/// algorithm changed — content-addressable dedup in the geo pipeline would
/// silently produce duplicate records for entities that were previously
/// considered identical.
/// </remarks>
public sealed class LocationHashDeterminismTests
{
    private static readonly LocationHashFixture sr_Fixture = LoadFixture();

    public static TheoryData<string> AllCaseNames()
    {
        var data = new TheoryData<string>();
        if (sr_Fixture.Cases.Count == 0)
        {
            throw new InvalidOperationException(
                $"Fixture loaded with zero cases — version='{sr_Fixture.Version}'. " +
                $"Check the JSON file path and deserialization shape.");
        }

        foreach (var c in sr_Fixture.Cases)
            data.Add(c.Name);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllCaseNames))]
    public void HashDeterminism(string caseName)
    {
        var c = sr_Fixture.Cases.Single(x => x.Name == caseName);

        switch (c.Kind)
        {
            case "coordinates":
                AssertCoordinatesCase(c);
                break;
            case "street-address":
                AssertStreetAddressCase(c);
                break;
            case "admin-location":
                AssertAdminLocationCase(c);
                break;
            case "compose":
                AssertComposeCase(c);
                break;
            case "postal-code":
                AssertPostalCase(c);
                break;
            default:
                throw new InvalidOperationException($"Unknown kind: {c.Kind}");
        }
    }

    private static void AssertCoordinatesCase(LocationHashFixtureCase c)
    {
        var inputs = c.Inputs;

        var result = c.Factory switch
        {
            "create" => Coordinates.Create(
                inputs.GetProperty("latitude").GetDouble(),
                inputs.GetProperty("longitude").GetDouble(),
                inputs.TryGetProperty("accuracyMeters", out var acc)
                    && acc.ValueKind == JsonValueKind.Number
                    ? acc.GetDouble()
                    : null),
            "fromGeohash" => Coordinates.FromGeohash(inputs.GetProperty("geohash").GetString()!),
            "fromPlusCode" => Coordinates.FromPlusCode(inputs.GetProperty("plusCode").GetString()!),
            _ => throw new InvalidOperationException($"Unknown coordinates factory: {c.Factory}"),
        };

        if (c.ExpectedOutcome == "ValidationFailed")
        {
            result.Success.Should().BeFalse();
            return;
        }

        result.Success.Should().BeTrue($"case '{c.Name}' should succeed");
        result.Data!.HashId.Should().Be(c.ExpectedHashId, $"case '{c.Name}'");
    }

    private static void AssertStreetAddressCase(LocationHashFixtureCase c)
    {
        var inputs = c.Inputs;
        var line1 = inputs.GetProperty("line1").GetString();
        var line2 = inputs.TryGetProperty("line2", out var l2) ? l2.GetString() : null;
        var line3 = inputs.TryGetProperty("line3", out var l3) ? l3.GetString() : null;
        var line4 = inputs.TryGetProperty("line4", out var l4) ? l4.GetString() : null;
        var line5 = inputs.TryGetProperty("line5", out var l5) ? l5.GetString() : null;

        var result = StreetAddress.Create(line1, line2, line3, line4, line5);

        if (c.ExpectedOutcome == "ValidationFailed")
        {
            result.Success.Should().BeFalse();
            return;
        }

        result.Success.Should().BeTrue($"case '{c.Name}' should succeed");
        result.Data!.HashId.Should().Be(c.ExpectedHashId, $"case '{c.Name}'");

        if (c.ExpectedNormalizedForHash is not null)
        {
            StreetAddress.NormalizeForHash(line1).Should().Be(
                c.ExpectedNormalizedForHash,
                $"case '{c.Name}' NormalizeForHash output mismatch");
        }
    }

    private static void AssertAdminLocationCase(LocationHashFixtureCase c)
    {
        var inputs = c.Inputs;
        CountryCode? country = inputs.TryGetProperty("countryCode", out var cc)
            && cc.ValueKind == JsonValueKind.String
            ? Enum.Parse<CountryCode>(cc.GetString()!)
            : null;
        SubdivisionCode? sub = inputs.TryGetProperty("subdivisionCode", out var sc)
            && sc.ValueKind == JsonValueKind.String
            ? SubdivisionCode.FromString(sc.GetString()!)
            : null;
        var city = inputs.TryGetProperty("city", out var ct) ? ct.GetString() : null;
        var postal = inputs.TryGetProperty("postalCode", out var pc) ? pc.GetString() : null;

        var result = AdminLocation.Create(country, sub, city, postal);

        if (c.ExpectedOutcome == "ValidationFailed")
        {
            result.Success.Should().BeFalse($"case '{c.Name}' should fail");
            return;
        }

        result.Success.Should().BeTrue($"case '{c.Name}' should succeed");
        result.Data!.HashId.Should().Be(c.ExpectedHashId, $"case '{c.Name}'");

        if (c.ExpectedCountryCode is { } expectedCC)
        {
            result.Data!.CountryIso31661Alpha2Code.Should().Be(Enum.Parse<CountryCode>(expectedCC));
        }
    }

    private static void AssertComposeCase(LocationHashFixtureCase c)
    {
        var inputs = c.Inputs;

        Coordinates? coord = null;
        if (inputs.TryGetProperty("coordinates", out var coordEl)
            && coordEl.ValueKind == JsonValueKind.Object)
        {
            var factory = coordEl.GetProperty("factory").GetString();
            var args = coordEl.GetProperty("args");
            if (factory == "create")
            {
                var lat = args[0].GetDouble();
                var lon = args[1].GetDouble();
                var r = Coordinates.Create(lat, lon);
                coord = r.Data;
            }
        }

        StreetAddress? street = null;
        if (inputs.TryGetProperty("streetAddress", out var streetEl)
            && streetEl.ValueKind == JsonValueKind.Object)
        {
            var line1 = streetEl.GetProperty("line1").GetString();
            var r = StreetAddress.Create(line1);
            street = r.Data;
        }

        AdminLocation? admin = null;
        if (inputs.TryGetProperty("adminLocation", out var adminEl)
            && adminEl.ValueKind == JsonValueKind.Object)
        {
            CountryCode? country = adminEl.TryGetProperty("countryCode", out var cc)
                && cc.ValueKind == JsonValueKind.String
                ? Enum.Parse<CountryCode>(cc.GetString()!)
                : null;
            var city = adminEl.TryGetProperty("city", out var ct) ? ct.GetString() : null;
            var r = AdminLocation.Create(country, null, city);
            admin = r.Data;
        }

        var composed = ComposeLocationHash.Compose(coord, street, admin);

        var expected = c.ExpectedComposeHash;
        if (expected is null)
            composed.Should().BeNull($"case '{c.Name}' should produce null");
        else
            composed.Should().Be(expected, $"case '{c.Name}'");
    }

    private static void AssertPostalCase(LocationHashFixtureCase c)
    {
        var inputs = c.Inputs;
        var postal = inputs.GetProperty("postalCode").GetString();

        var validator = new DefaultPostalCodeValidator();
        var result = validator.Validate(postal);

        if (c.ExpectedOutcome == "ValidationFailed")
        {
            result.Success.Should().BeFalse($"case '{c.Name}' should fail");
            return;
        }

        result.Success.Should().BeTrue($"case '{c.Name}' should succeed");
        if (c.ExpectedNormalized is { } expected)
            result.Data.Should().Be(expected, $"case '{c.Name}'");
    }

    private static LocationHashFixture LoadFixture()
    {
        // Hermetic fixture-path resolution: derive the path from
        // [CallerFilePath] at compile time (this source file's absolute path
        // is baked into the test binary), then navigate to contracts/location.
        // This avoids the filesystem-walk-up pattern's fragility under hermetic
        // CI sandboxes and unusual AppContext.BaseDirectory layouts.
        var candidate = ResolveFixturePath();
        if (!File.Exists(candidate))
        {
            throw new FileNotFoundException(
                $"Location hash fixture not found at expected path '{candidate}' " +
                "(derived from [CallerFilePath]; should resolve to " +
                "contracts/location/parity-fixtures.json " +
                $"relative to this test source file).");
        }

        var json = File.ReadAllText(candidate);
        return ParseFixture(json);
    }

    /// <summary>
    /// Resolves the absolute path of <c>contracts/location/parity-fixtures.json</c>
    /// using <see cref="CallerFilePathAttribute"/> on this method. The compiler
    /// substitutes the absolute path of THIS source file (LocationHashDeterminismTests.cs)
    /// at the call site, which is then mapped to the repo-root relative fixture path.
    /// </summary>
    private static string ResolveFixturePath([CallerFilePath] string thisSourcePath = "")
    {
        // thisSourcePath: <repo>/public/packages/dotnet/tests/Unit/Location/
        //   LocationHashDeterminismTests.cs
        // walk up 6 levels (Location -> Unit -> tests -> dotnet -> shared -> server)
        // to reach <repo>.
        var dir = Path.GetDirectoryName(thisSourcePath) ?? string.Empty;
        for (var i = 0; i < 6; i++)
            dir = Path.GetDirectoryName(dir) ?? string.Empty;
        return Path.Combine(
            dir,
            "public",
            "contracts",
            "location",
            "parity-fixtures.json");
    }

    private static LocationHashFixture ParseFixture(string json)
    {
        // Manual parse via JsonDocument — STJ's reflection-based deserializer
        // produced empty Cases on a struct-containing record (likely related to
        // JsonElement field handling); manual extraction is robust + explicit.
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("cases", out var casesEl))
            throw new InvalidOperationException("Root JSON does not contain 'cases'.");

        var manual = new LocationHashFixture
        {
            Version = doc.RootElement.TryGetProperty("version", out var verEl)
                ? verEl.GetString() ?? string.Empty
                : string.Empty,
        };
        foreach (var caseEl in casesEl.EnumerateArray())
        {
            var fc = new LocationHashFixtureCase
            {
                Name = caseEl.GetProperty("name").GetString() ?? string.Empty,
                Kind = caseEl.GetProperty("kind").GetString() ?? string.Empty,
                Factory = caseEl.TryGetProperty("factory", out var fEl) ? fEl.GetString() : null,
                Inputs = caseEl.TryGetProperty("inputs", out var iEl) ? iEl.Clone() : default,
                ExpectedHashId = caseEl.TryGetProperty("expectedHashId", out var hEl)
                    ? hEl.GetString() : null,
                ExpectedComposeHash = caseEl.TryGetProperty("expectedComposeHash", out var chEl)
                    ? (chEl.ValueKind == JsonValueKind.Null ? null : chEl.GetString())
                    : null,
                ExpectedOutcome = caseEl.TryGetProperty("expectedOutcome", out var oEl)
                    ? oEl.GetString() : null,
                ExpectedNormalizedForHash = caseEl.TryGetProperty(
                    "expectedNormalizedForHash", out var nEl)
                    ? nEl.GetString() : null,
                ExpectedCountryCode = caseEl.TryGetProperty("expectedCountryCode", out var ccEl)
                    ? ccEl.GetString()
                    : null,
                ExpectedNormalized = caseEl.TryGetProperty("expectedNormalized", out var enEl)
                    ? enEl.GetString()
                    : null,
            };
            manual.Cases.Add(fc);
        }

        if (manual.Cases.Count == 0)
        {
            // Diagnostic: dump the first 200 chars of JSON so the error surfaces in the runner.
            var preview = json.Length > 200 ? json[..200] + "..." : json;
            throw new InvalidOperationException(
                $"Deserialized fixture has zero cases. version='{manual.Version}'. " +
                $"JSON preview: {preview}");
        }

        return manual;
    }
}
