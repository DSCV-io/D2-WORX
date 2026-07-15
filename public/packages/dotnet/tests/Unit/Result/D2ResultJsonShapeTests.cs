// -----------------------------------------------------------------------
// <copyright file="D2ResultJsonShapeTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using DcsvIo.D2.ErrorCodes.Category;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Pins the wire shape of <see cref="D2Result"/> / <see cref="D2Result{TData}"/>
/// against the spec-driven <see cref="D2ResultEnvelopeFieldNames"/> catalog.
/// Asserts:
/// <list type="bullet">
///   <item>Every emitted property uses the camelCase wire name (success /
///     data / messages / inputErrors / errorCode / traceId / statusCode).</item>
///   <item>NONE of the <c>Is*</c> derived boolean helpers leak onto the
///     wire â€” they're <c>[JsonIgnore]</c>'d.</item>
///   <item>The <c>Failed</c> derived helper is also <c>[JsonIgnore]</c>'d.</item>
/// </list>
/// </summary>
public sealed class D2ResultJsonShapeTests
{
    private static readonly JsonSerializerOptions sr_camelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void Serialize_Ok_EmitsCamelCaseEnvelopeFields()
    {
        var result = D2Result.Ok();

        var json = JsonSerializer.Serialize<object>(result);
        var obj = JsonNode.Parse(json)!.AsObject();

        obj.Should().ContainKey(D2ResultEnvelopeFieldNames.SUCCESS);
        obj[D2ResultEnvelopeFieldNames.SUCCESS]!.GetValue<bool>().Should().BeTrue();
        obj.Should().ContainKey(D2ResultEnvelopeFieldNames.MESSAGES);
        obj.Should().ContainKey(D2ResultEnvelopeFieldNames.INPUT_ERRORS);
        obj.Should().ContainKey(D2ResultEnvelopeFieldNames.STATUS_CODE);
        obj[D2ResultEnvelopeFieldNames.STATUS_CODE]!.GetValue<int>().Should().Be(200);
    }

    [Fact]
    public void Serialize_DoesNotLeakIsBooleanHelpers()
    {
        // [JsonIgnore] on every Is* helper prevents the wire from
        // carrying IsOk / IsNotFound / IsCanceled / etc. â€” they're
        // in-process discriminators, not envelope fields.
        var result = D2Result.NotFound();

        var json = JsonSerializer.Serialize<object>(result);
        var obj = JsonNode.Parse(json)!.AsObject();

        var leakedHelpers = new[]
        {
            "IsOk", "isOk", "IsCreated", "isCreated",
            "IsNotFound", "isNotFound", "IsSomeFound", "isSomeFound",
            "IsPartialSuccess",
            "isPartialSuccess",
            "IsConflict", "isConflict", "IsForbidden", "isForbidden",
            "IsUnauthorized",
            "isUnauthorized",
            "IsValidationFailed",
            "isValidationFailed",
            "IsServiceUnavailable",
            "isServiceUnavailable",
            "IsRateLimited",
            "isRateLimited",
            "IsUnhandledException",
            "isUnhandledException",
            "IsPayloadTooLarge",
            "isPayloadTooLarge",
            "IsCanceled",
            "isCanceled",
            "IsIdempotencyInFlight",
            "isIdempotencyInFlight",
            "IsPartialOrMissing",
            "isPartialOrMissing",
            "IsTransientRetryable",
            "isTransientRetryable",
            "Failed",
            "failed",
        };
        foreach (var leak in leakedHelpers)
            obj.Should().NotContainKey(leak, $"derived helper '{leak}' must be [JsonIgnore]'d");
    }

    [Fact]
    public void Serialize_NotFound_EmitsErrorCodeAndStatus()
    {
        var result = D2Result.NotFound();

        var json = JsonSerializer.Serialize<object>(result);
        var obj = JsonNode.Parse(json)!.AsObject();

        obj[D2ResultEnvelopeFieldNames.SUCCESS]!.GetValue<bool>().Should().BeFalse();
        obj[D2ResultEnvelopeFieldNames.STATUS_CODE]!.GetValue<int>().Should().Be(404);
        obj[D2ResultEnvelopeFieldNames.ERROR_CODE]!.GetValue<string>()
            .Should().Be("NOT_FOUND");
    }

    [Fact]
    public void Serialize_ValidationFailed_EmitsInputErrors()
    {
        var result = D2Result.ValidationFailed(
            inputErrors:
            [
                new InputError(
                    "email",
                    [new TKMessage("common_validation_EMAIL_INVALID")]),
            ]);

        var json = JsonSerializer.Serialize<object>(result);
        var obj = JsonNode.Parse(json)!.AsObject();

        obj[D2ResultEnvelopeFieldNames.SUCCESS]!.GetValue<bool>().Should().BeFalse();
        obj[D2ResultEnvelopeFieldNames.ERROR_CODE]!.GetValue<string>()
            .Should().Be("VALIDATION_FAILED");
        var inputErrors = obj[D2ResultEnvelopeFieldNames.INPUT_ERRORS]!.AsArray();
        inputErrors.Should().HaveCount(1);
        inputErrors[0]!.AsObject()["field"]!.GetValue<string>().Should().Be("email");
    }

    [Fact]
    public void Serialize_WithTraceId_EmitsTraceIdField()
    {
        var result = D2Result.Ok().WithTraceId("0123456789abcdef0123456789abcdef");

        var json = JsonSerializer.Serialize<object>(result);
        var obj = JsonNode.Parse(json)!.AsObject();

        obj[D2ResultEnvelopeFieldNames.TRACE_ID]!.GetValue<string>()
            .Should().Be("0123456789abcdef0123456789abcdef");
    }

    [Fact]
    public void Serialize_GenericOkWithData_EmitsDataField()
    {
        var result = D2Result<Dictionary<string, string>>.Ok(
            new Dictionary<string, string> { ["k"] = "v" });

        var json = JsonSerializer.Serialize<object>(result);
        var obj = JsonNode.Parse(json)!.AsObject();

        obj.Should().ContainKey(D2ResultEnvelopeFieldNames.DATA);
        obj[D2ResultEnvelopeFieldNames.DATA]!.AsObject()["k"]!.GetValue<string>()
            .Should().Be("v");
    }

    [Fact]
    public void Serialize_IsImmuneToCamelCaseOptions()
    {
        // The [JsonPropertyName] attributes pin the wire keys regardless
        // of the calling JsonSerializerOptions.PropertyNamingPolicy.
        var result = D2Result.Ok();

        var json = JsonSerializer.Serialize<object>(result, sr_camelCaseOptions);
        var obj = JsonNode.Parse(json)!.AsObject();

        obj.Should().ContainKey(D2ResultEnvelopeFieldNames.SUCCESS);
        obj.Should().NotContainKey("Success", "PascalCase regression");
    }

    [Fact]
    public void Serialize_IsImmuneToDefaultOptions()
    {
        // Default JsonSerializerOptions has NO PropertyNamingPolicy set, so
        // a typical record would round-trip as PascalCase. The
        // [JsonPropertyName] attributes pin camelCase even under default
        // options â€” confirms the envelope wire shape is per-property
        // explicit and does not depend on the caller setting
        // JsonNamingPolicy.CamelCase.
        var result = D2Result.Ok();

        var json = JsonSerializer.Serialize<object>(result);
        var obj = JsonNode.Parse(json)!.AsObject();

        obj.Should().ContainKey(D2ResultEnvelopeFieldNames.SUCCESS);
        obj.Should().NotContainKey("Success", "PascalCase regression");
    }

    [Fact]
    public void Serialize_NotFound_EmitsCategoryAsSnakeWireString()
    {
        // The NotFound factory stamps ErrorCategory.NotFound; it serializes via
        // ErrorCategoryJsonConverter as the snake_case wire string.
        var result = D2Result.NotFound();

        var json = JsonSerializer.Serialize<object>(result);
        var obj = JsonNode.Parse(json)!.AsObject();

        obj.Should().ContainKey(D2ResultEnvelopeFieldNames.CATEGORY);
        obj[D2ResultEnvelopeFieldNames.CATEGORY]!.GetValue<string>()
            .Should().Be("not_found");
    }

    [Fact]
    public void Serialize_Ok_OmitsCategoryWhenNull()
    {
        // [JsonIgnore(WhenWritingNull)] on Category â†’ the key is ABSENT (not
        // null / not "") for a result with no category.
        var result = D2Result.Ok();

        var json = JsonSerializer.Serialize<object>(result);
        var obj = JsonNode.Parse(json)!.AsObject();

        obj.Should().NotContainKey(
            D2ResultEnvelopeFieldNames.CATEGORY,
            "a success result carries no category and the field is null-omitted");
    }

    [Fact]
    public void Serialize_Fail_WithoutCategory_OmitsCategory()
    {
        // A hand-rolled Fail() with no category supplied â†’ omitted, no crash.
        var result = D2Result.Fail();

        var json = JsonSerializer.Serialize<object>(result);
        var obj = JsonNode.Parse(json)!.AsObject();

        obj.Should().NotContainKey(D2ResultEnvelopeFieldNames.CATEGORY);
    }

    [Fact]
    public void Category_WireString_RehydratesViaConverter()
    {
        // The .NET D2Result envelope is serialize-only (the TS @dcsv-io/d2-result parser
        // owns deserialization â€” see the cross-runtime fixtures). The wireâ†’typed
        // direction for the category field itself is the ErrorCategoryJsonConverter
        // round-trip: the snake string parses back to the typed ErrorCategory,
        // which is what the consumer-side reconstruction relies on.
        ErrorCategoryWire.TryFromWire("validation_failure", out var category)
            .Should().BeTrue();
        category.Should().Be(ErrorCategory.ValidationFailure);

        var json = JsonSerializer.Serialize(ErrorCategory.NotFound);
        json.Should().Be("\"not_found\"");
        JsonSerializer.Deserialize<ErrorCategory>(json)
            .Should().Be(ErrorCategory.NotFound);
    }

    [Fact]
    public void Serialize_WireKeysSubsetOfCatalog()
    {
        // Every wire key emitted by a fully-populated D2Result MUST be in
        // the D2ResultEnvelopeFieldNames catalog â€” pins against accidental
        // future leakage (e.g. new public auto-property added without
        // [JsonIgnore]).
        var result = D2Result<string>.Ok(
            data: "payload",
            messages: [new TKMessage("k")])
            .WithTraceId("trace");

        var json = JsonSerializer.Serialize<object>(result);
        var obj = JsonNode.Parse(json)!.AsObject();

        foreach (var prop in obj)
        {
            D2ResultEnvelopeFieldNames.AllFields
                .Should()
                .Contain(prop.Key, $"wire field '{prop.Key}' is not in the catalog");
        }
    }
}
