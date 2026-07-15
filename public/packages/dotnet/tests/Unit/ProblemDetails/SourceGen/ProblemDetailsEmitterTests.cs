// -----------------------------------------------------------------------
// <copyright file="ProblemDetailsEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.ProblemDetails.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using DcsvIo.D2.ProblemDetails.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the ProblemDetails emitter. Drives the emitter
/// directly with synthetic specs and asserts both the generated source shape
/// (per-VALUE pin) and the diagnostics surfaced for invalid spec inputs.
/// </summary>
public sealed class ProblemDetailsEmitterTests
{
    [Fact]
    public void Emit_ValidSpec_EmitsTypeUriPrefixConstant()
    {
        var spec = MakeSpec(
            "https://problems.d2.dcsv.io/",
            "application/problem+json",
            extensions: [new ExtensionKeyEntry("ERROR_CODE", "d2_error_code", "Doc.")],
            titles: [
                new TitleEntry("UNAUTHORIZED", 401, "Unauthorized", "401 doc."),
                new TitleEntry("REQUEST_FAILED", null, "Request Failed", "Fallback doc."),
            ]);

        var result = ProblemDetailsEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain(
            "public const string TYPE_URI_PREFIX = \"https://problems.d2.dcsv.io/\";");
        result.GeneratedSource.Should().Contain(
            "namespace DcsvIo.D2.ProblemDetails;");
        result.GeneratedSource.Should().Contain(
            "public static class D2ProblemDetailsKeys");
    }

    [Fact]
    public void Emit_ValidSpec_EmitsContentTypeConstant()
    {
        var spec = MakeSpec(
            "https://problems.d2.dcsv.io/",
            "application/problem+json",
            extensions: [],
            titles: [new TitleEntry("REQUEST_FAILED", null, "Request Failed", "Doc.")]);

        var result = ProblemDetailsEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain(
            "public const string CONTENT_TYPE = \"application/problem+json\";");
    }

    [Theory]
    [InlineData("ERROR_CODE", "d2_error_code")]
    [InlineData("MESSAGES", "d2_messages")]
    [InlineData("INPUT_ERRORS", "d2_input_errors")]
    [InlineData("TRACE_ID", "traceId")]
    [InlineData("CORRELATION_ID", "correlationId")]
    public void Emit_ExtensionKey_EmitsConstantWithPrefixAndValue(
        string constName,
        string wireValue)
    {
        var spec = MakeSpec(
            "https://problems.d2.dcsv.io/",
            "application/problem+json",
            extensions: [new ExtensionKeyEntry(constName, wireValue, "Doc.")],
            titles: []);

        var result = ProblemDetailsEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain(
            $"public const string EXTENSION_{constName} = \"{wireValue}\";");
    }

    [Theory]
    [InlineData("BAD_REQUEST", 400, "Bad Request")]
    [InlineData("UNAUTHORIZED", 401, "Unauthorized")]
    [InlineData("FORBIDDEN", 403, "Forbidden")]
    [InlineData("NOT_FOUND", 404, "Not Found")]
    [InlineData("CONFLICT", 409, "Conflict")]
    [InlineData("PAYLOAD_TOO_LARGE", 413, "Payload Too Large")]
    [InlineData("TOO_MANY_REQUESTS", 429, "Too Many Requests")]
    [InlineData("INTERNAL_SERVER_ERROR", 500, "Internal Server Error")]
    [InlineData("SERVICE_UNAVAILABLE", 503, "Service Unavailable")]
    public void Emit_TitleWithHttpStatus_EmitsConstantAndSwitchArm(
        string constName,
        int status,
        string wireValue)
    {
        var spec = MakeSpec(
            "https://problems.d2.dcsv.io/",
            "application/problem+json",
            extensions: [],
            titles: [new TitleEntry(constName, status, wireValue, "Doc.")]);

        var result = ProblemDetailsEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain(
            $"public const string TITLE_{constName} = \"{wireValue}\";");
        result.GeneratedSource.Should().Contain($"{status} => TITLE_{constName},");
    }

    [Fact]
    public void Emit_FallbackTitle_EmitsConstantAndDefaultSwitchArm()
    {
        var spec = MakeSpec(
            "https://problems.d2.dcsv.io/",
            "application/problem+json",
            extensions: [],
            titles: [new TitleEntry("REQUEST_FAILED", null, "Request Failed", "Fallback doc.")]);

        var result = ProblemDetailsEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain(
            "public const string TITLE_REQUEST_FAILED = \"Request Failed\";");
        result.GeneratedSource.Should().Contain("_ => TITLE_REQUEST_FAILED,");
    }

    [Fact]
    public void Emit_NoFallbackTitle_DefaultsToStringEmpty()
    {
        var spec = MakeSpec(
            "https://problems.d2.dcsv.io/",
            "application/problem+json",
            extensions: [],
            titles: [new TitleEntry("UNAUTHORIZED", 401, "Unauthorized", "Doc.")]);

        var result = ProblemDetailsEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("_ => string.Empty,");
    }

    [Fact]
    public void Emit_TypeUriPrefixMissingTrailingSlash_EmitsDiagnostic()
    {
        var spec = MakeSpec(
            "https://problems.d2.dcsv.io",
            "application/problem+json",
            extensions: [],
            titles: []);

        var result = ProblemDetailsEmitter.Emit(spec);

        result.Diagnostics.Should().ContainSingle(d =>
            d.DescriptorId == DiagnosticIds.TypeUriPrefixMissingTrailingSlash);
    }

    [Fact]
    public void Emit_DuplicateExtensionKeyConstName_EmitsDiagnostic()
    {
        var spec = MakeSpec(
            "https://problems.d2.dcsv.io/",
            "application/problem+json",
            extensions:
            [
                new ExtensionKeyEntry("ERROR_CODE", "value_a", "Doc."),
                new ExtensionKeyEntry("ERROR_CODE", "value_b", "Doc."),
            ],
            titles: []);

        var result = ProblemDetailsEmitter.Emit(spec);

        result.Diagnostics.Should().ContainSingle(d =>
            d.DescriptorId == DiagnosticIds.DuplicateExtensionKeyConstName);
    }

    [Fact]
    public void Emit_DuplicateExtensionKeyValue_EmitsDiagnostic()
    {
        var spec = MakeSpec(
            "https://problems.d2.dcsv.io/",
            "application/problem+json",
            extensions:
            [
                new ExtensionKeyEntry("A", "d2_error_code", "Doc."),
                new ExtensionKeyEntry("B", "d2_error_code", "Doc."),
            ],
            titles: []);

        var result = ProblemDetailsEmitter.Emit(spec);

        result.Diagnostics.Should().ContainSingle(d =>
            d.DescriptorId == DiagnosticIds.DuplicateExtensionKeyValue);
    }

    [Fact]
    public void Emit_DuplicateTitleConstName_EmitsDiagnostic()
    {
        var spec = MakeSpec(
            "https://problems.d2.dcsv.io/",
            "application/problem+json",
            extensions: [],
            titles:
            [
                new TitleEntry("UNAUTHORIZED", 401, "Unauthorized", "Doc."),
                new TitleEntry("UNAUTHORIZED", 403, "Forbidden", "Doc."),
            ]);

        var result = ProblemDetailsEmitter.Emit(spec);

        result.Diagnostics.Should().ContainSingle(d =>
            d.DescriptorId == DiagnosticIds.DuplicateTitleConstName);
    }

    [Fact]
    public void Emit_DuplicateTitleHttpStatus_EmitsDiagnostic()
    {
        var spec = MakeSpec(
            "https://problems.d2.dcsv.io/",
            "application/problem+json",
            extensions: [],
            titles:
            [
                new TitleEntry("A", 401, "Unauthorized", "Doc."),
                new TitleEntry("B", 401, "Other", "Doc."),
            ]);

        var result = ProblemDetailsEmitter.Emit(spec);

        result.Diagnostics.Should().ContainSingle(d =>
            d.DescriptorId == DiagnosticIds.DuplicateTitleHttpStatus);
    }

    [Fact]
    public void Emit_DuplicateNullFallback_EmitsDiagnostic()
    {
        var spec = MakeSpec(
            "https://problems.d2.dcsv.io/",
            "application/problem+json",
            extensions: [],
            titles:
            [
                new TitleEntry("A", null, "First", "Doc."),
                new TitleEntry("B", null, "Second", "Doc."),
            ]);

        var result = ProblemDetailsEmitter.Emit(spec);

        result.Diagnostics.Should().ContainSingle(d =>
            d.DescriptorId == DiagnosticIds.DuplicateTitleHttpStatus);
    }

    [Fact]
    public void Emit_RunsTwiceWithIdenticalInput_ProducesIdenticalSource()
    {
        var spec = MakeSpec(
            "https://problems.d2.dcsv.io/",
            "application/problem+json",
            extensions: [new ExtensionKeyEntry("ERROR_CODE", "d2_error_code", "Doc.")],
            titles: [new TitleEntry("UNAUTHORIZED", 401, "Unauthorized", "Doc.")]);

        var first = ProblemDetailsEmitter.Emit(spec);
        var second = ProblemDetailsEmitter.Emit(spec);

        second.GeneratedSource.Should().Be(first.GeneratedSource);
    }

    [Fact]
    public void Emit_PreservesSpecOrderOfEntries()
    {
        // Spec ordering drives emitted ordering — predictable diff on spec
        // edits. Mirrors the auth-error-codes emitter's preserve-order
        // discipline.
        var spec = MakeSpec(
            "https://problems.d2.dcsv.io/",
            "application/problem+json",
            extensions:
            [
                new ExtensionKeyEntry("Z", "z_value", "Doc."),
                new ExtensionKeyEntry("A", "a_value", "Doc."),
            ],
            titles: []);

        var result = ProblemDetailsEmitter.Emit(spec);

        var zPos = result.GeneratedSource.IndexOf("EXTENSION_Z", System.StringComparison.Ordinal);
        var aPos = result.GeneratedSource.IndexOf("EXTENSION_A", System.StringComparison.Ordinal);
        zPos.Should().BeLessThan(aPos);
    }

    private static ProblemDetailsSpec MakeSpec(
        string typeUriPrefix,
        string contentType,
        ExtensionKeyEntry[] extensions,
        TitleEntry[] titles) =>
        new(
            typeUriPrefix,
            contentType,
            extensions.ToImmutableArray(),
            titles.ToImmutableArray());
}
