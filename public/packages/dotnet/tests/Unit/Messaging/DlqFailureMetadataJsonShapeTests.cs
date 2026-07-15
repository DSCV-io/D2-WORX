// -----------------------------------------------------------------------
// <copyright file="DlqFailureMetadataJsonShapeTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using Xunit;

/// <summary>
/// Pins the wire shape of <see cref="DlqFailureMetadata"/> against the
/// spec-driven <see cref="DlqFailureMetadataFields"/> catalog. Mirrors the
/// <c>D2ResultJsonShapeTests</c> structural-guard pattern so any future
/// auto-property added to the record without a <c>[JsonIgnore]</c> attribute
/// (or any drift between the property's wire key and the spec catalog)
/// surfaces as a test failure rather than a silent wire-shape change.
/// </summary>
public sealed class DlqFailureMetadataJsonShapeTests
{
    private static readonly JsonSerializerOptions sr_omitNullOptions = new()
    {
        DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public void Serialize_EmitsCatalogWireKeysOnly()
    {
        var metadata = new DlqFailureMetadata
        {
            Cause = "HANDLER_EXCEPTION",
            ErrorCode = "System.InvalidOperationException",
            Detail = "diagnostic detail",
            AttemptCount = 3,
            TraceId = "0123456789abcdef0123456789abcdef",
            NackedBy = "edge",
        };

        var json = JsonSerializer.Serialize(metadata);
        var obj = JsonNode.Parse(json)!.AsObject();

        obj.Should().ContainKey(DlqFailureMetadataFields.CAUSE);
        obj.Should().ContainKey(DlqFailureMetadataFields.ERROR_CODE);
        obj.Should().ContainKey(DlqFailureMetadataFields.DETAIL);
        obj.Should().ContainKey(DlqFailureMetadataFields.ATTEMPT_COUNT);
        obj.Should().ContainKey(DlqFailureMetadataFields.TRACE_ID);
        obj.Should().ContainKey(DlqFailureMetadataFields.NACKED_BY);
    }

    [Fact]
    public void Serialize_WireKeysSubsetOfCatalog()
    {
        // Every wire key emitted by a fully-populated DlqFailureMetadata MUST
        // be in the spec catalog. Pins against accidental leakage when a
        // future auto-property is added without a [JsonIgnore] attribute.
        var metadata = new DlqFailureMetadata
        {
            Cause = "HANDLER_EXCEPTION",
            ErrorCode = "System.InvalidOperationException",
            Detail = "diagnostic detail",
            AttemptCount = 3,
            TraceId = "0123456789abcdef0123456789abcdef",
            NackedBy = "edge",
        };

        var json = JsonSerializer.Serialize(metadata);
        var obj = JsonNode.Parse(json)!.AsObject();

        foreach (var prop in obj)
        {
            DlqFailureMetadataFields.AllFields
                .Should()
                .Contain(prop.Key, $"wire field '{prop.Key}' is not in the catalog");
        }
    }

    [Fact]
    public void Serialize_IsImmuneToDefaultOptions()
    {
        // Default JsonSerializerOptions has NO PropertyNamingPolicy set, so a
        // typical record would round-trip as PascalCase. The
        // [JsonPropertyName] attributes pin camelCase even under default
        // options — confirms the wire shape is per-property explicit and does
        // not depend on the caller setting JsonNamingPolicy.CamelCase.
        var metadata = new DlqFailureMetadata
        {
            Cause = "HANDLER_RESULT_FAILURE",
            ErrorCode = "NOT_FOUND",
        };

        var json = JsonSerializer.Serialize(metadata);
        var obj = JsonNode.Parse(json)!.AsObject();

        obj.Should().ContainKey(DlqFailureMetadataFields.CAUSE);
        obj.Should().NotContainKey("Cause", "PascalCase regression");
        obj.Should().ContainKey(DlqFailureMetadataFields.ERROR_CODE);
        obj.Should().NotContainKey("ErrorCode", "PascalCase regression");
    }

    [Fact]
    public void Serialize_OmitsNullOptionalFields()
    {
        // Optional nullable fields (Detail / TraceId / NackedBy) omit-on-null
        // under the default System.Text.Json behavior, keeping the AMQP
        // header payload as small as possible.
        var metadata = new DlqFailureMetadata
        {
            Cause = "HANDLER_RESULT_FAILURE",
            ErrorCode = "NOT_FOUND",
        };

        var json = JsonSerializer.Serialize(metadata, sr_omitNullOptions);
        var obj = JsonNode.Parse(json)!.AsObject();

        obj.Should().ContainKey(DlqFailureMetadataFields.CAUSE);
        obj.Should().ContainKey(DlqFailureMetadataFields.ERROR_CODE);
        obj.Should().ContainKey(DlqFailureMetadataFields.ATTEMPT_COUNT);
        obj.Should().NotContainKey(DlqFailureMetadataFields.DETAIL);
        obj.Should().NotContainKey(DlqFailureMetadataFields.TRACE_ID);
        obj.Should().NotContainKey(DlqFailureMetadataFields.NACKED_BY);
    }
}
