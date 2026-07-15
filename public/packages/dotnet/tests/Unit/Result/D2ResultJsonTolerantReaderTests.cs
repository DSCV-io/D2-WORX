// -----------------------------------------------------------------------
// <copyright file="D2ResultJsonTolerantReaderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Pins the Tolerant Reader property of the D2Result JSON serialization substrate.
/// <para>
/// On the .NET side <see cref="D2Result{TData}"/> is a <em>serialize-only</em> type: the
/// TS-side <c>@dcsv-io/d2-result</c> parser owns envelope deserialization (as documented in
/// <see cref="D2ResultJsonShapeTests"/>). The Tolerant Reader property therefore applies in
/// two directions:
/// </para>
/// <list type="bullet">
///   <item><b>Serialize-side</b> — the serialized envelope contains only the declared catalog
///   keys (<see cref="D2ResultEnvelopeFieldNames.AllFields"/>); extra keys are never emitted.
///   This is already pinned by <see cref="D2ResultJsonShapeTests.Serialize_WireKeysSubsetOfCatalog"/>.
///   </item>
///   <item><b>Deserialize-side (DTO payloads)</b> — the STJ default has no
///   <c>UnmappedMemberHandling = Disallow</c> set anywhere in the repository, so a consumer
///   that deserializes a <c>data</c> payload DTO from a JSON body carrying extra unknown keys
///   does NOT throw. These tests pin that assumption on concrete DTO shapes that a service
///   handler or BFF would receive.
///   </item>
/// </list>
/// </summary>
public sealed class D2ResultJsonTolerantReaderTests
{
    private static readonly JsonSerializerOptions sr_camelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // ---------------------------------------------------------------------------
    // TR-JSON-1: a DTO payload with an extra unknown field is ignored by STJ.
    // ---------------------------------------------------------------------------

    [Fact]
    public void Deserialize_DtoPayload_ExtraField_IgnoredByDefaultStj()
    {
        // Simulate a service consumer receiving a response whose data payload carries
        // a field added by a newer server version.  STJ's default UnmappedMemberHandling
        // is JsonUnmappedMemberHandling.Skip; the extra field is silently discarded and
        // the known fields populate correctly.
        const string json =
            """
            {
              "name": "cog",
              "count": 7,
              "addedInV2": "ignored-extra-field"
            }
            """;

        var dto = JsonSerializer.Deserialize<WidgetData>(json);

        dto.Should().NotBeNull();
        dto.Name.Should().Be("cog");
        dto.Count.Should().Be(7);
    }

    // ---------------------------------------------------------------------------
    // TR-JSON-2: multiple extra unknown keys at the same level are all ignored.
    // ---------------------------------------------------------------------------

    [Fact]
    public void Deserialize_DtoPayload_MultipleExtraFields_AllIgnoredByDefaultStj()
    {
        const string json =
            """
            {
              "name": "sprocket",
              "count": 3,
              "futureScalar": 99,
              "futureObject": { "nested": true },
              "futureArray": [1, 2, 3]
            }
            """;

        var dto = JsonSerializer.Deserialize<WidgetData>(json);

        dto.Should().NotBeNull();
        dto.Name.Should().Be("sprocket");
        dto.Count.Should().Be(3);
    }

    // ---------------------------------------------------------------------------
    // TR-JSON-3: the D2Result envelope wire shape never emits more than the declared
    //            catalog — this pins the serialize-side of the Tolerant Reader contract
    //            (no accidental extra fields get emitted that could surprise a reader).
    // ---------------------------------------------------------------------------

    [Fact]
    public void Serialize_D2ResultEnvelope_EmitsNoMoreThanCatalogKeys()
    {
        // A fully-populated result with data should only carry keys in
        // D2ResultEnvelopeFieldNames.AllFields.  Any property added to D2Result
        // without [JsonIgnore] would appear here as a regression.
        var result = D2Result<WidgetData>.Ok(
            data: new WidgetData { Name = "gear", Count = 5 });

        var json = JsonSerializer.Serialize<object>(result);
        var obj = JsonNode.Parse(json)!.AsObject();

        foreach (var prop in obj)
        {
            D2ResultEnvelopeFieldNames.AllFields
                .Should()
                .Contain(prop.Key, $"envelope wire field '{prop.Key}' is not in the catalog");
        }
    }

    // ---------------------------------------------------------------------------
    // TR-JSON-4: STJ options without explicit UnmappedMemberHandling=Disallow do NOT
    //            disallow unknown members — pins the tolerant default is active.
    // ---------------------------------------------------------------------------

    [Fact]
    public void Deserialize_WithCamelCaseOptions_ExtraField_StillIgnoredByDefault()
    {
        // Even under a custom JsonSerializerOptions (e.g. CamelCase naming policy),
        // the default UnmappedMemberHandling.Skip applies and extra fields are silently
        // dropped.  If any caller had set Disallow this would throw.
        const string json = """{ "name": "widget", "count": 2, "unknownKey": "surprise" }""";

        var dto = JsonSerializer.Deserialize<WidgetData>(json, sr_camelCaseOptions);

        dto.Should().NotBeNull();
        dto.Name.Should().Be("widget");
        dto.Count.Should().Be(2);
    }

    // ---------------------------------------------------------------------------
    // DTO used by tests above.  Placed after the test methods per SA1204 ordering.
    // ---------------------------------------------------------------------------

    private sealed class WidgetData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("count")]
        public int Count { get; set; }
    }
}
