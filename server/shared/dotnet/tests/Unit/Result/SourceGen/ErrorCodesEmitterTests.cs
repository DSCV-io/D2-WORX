// -----------------------------------------------------------------------
// <copyright file="ErrorCodesEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Result.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using D2.Shared.ResultErrorCodes.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the generic ErrorCodes emitter. Drives the emitter
/// directly with synthetic specs and asserts both the generated source shape
/// and the diagnostics surfaced for invalid spec inputs. Includes per-VALUE
/// pins for every entry in the shipping spec so a wire-value drift surfaces
/// at the emitter level (the parity test catches cross-language drift; this
/// test catches within-emitter drift).
/// </summary>
public sealed class ErrorCodesEmitterTests
{
    [Fact]
    public void Emit_ValidSingleEntry_EmitsConstantAndAllCodesAndGetHttpStatus()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry(
                Code: "X_THING",
                HttpStatus: 404,
                Doc: "X thing doc."));

        var result = ErrorCodesEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public const string X_THING = \"X_THING\";");
        result.GeneratedSource.Should().Contain("namespace D2.Shared.Result;");
        result.GeneratedSource.Should().Contain("public static class ErrorCodes");
        result.GeneratedSource.Should().Contain(
            "public static int GetHttpStatus(string errorCode)");
        result.GeneratedSource.Should().Contain("\"X_THING\" => 404,");
        result.GeneratedSource.Should().Contain(
            "public static IReadOnlyList<string> AllCodes => sr_allCodes;");
    }

    [Fact]
    public void Emit_DuplicateCode_EmitsDuplicateCodeDiagnostic()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry("DUPE", 400, "X"),
            new ErrorCodeEntry("DUPE", 400, "Y"));

        var result = ErrorCodesEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateCode);
    }

    [Fact]
    public void Emit_InvalidHttpStatus_EmitsInvalidHttpStatusDiagnostic()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry("X", 418, "X"));

        var result = ErrorCodesEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidHttpStatus);
    }

    [Fact]
    public void Emit_InvalidCodeLowercase_EmitsInvalidCodeDiagnostic()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry("lowercase", 400, "X"));

        var result = ErrorCodesEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidCode);
    }

    [Fact]
    public void Emit_InvalidCodeEmpty_EmitsInvalidCodeDiagnostic()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry(string.Empty, 400, "X"));

        var result = ErrorCodesEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidCode);
    }

    [Fact]
    public void Emit_InvalidCodeWhitespace_EmitsInvalidCodeDiagnostic()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry("   ", 400, "X"));

        var result = ErrorCodesEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidCode);
    }

    [Fact]
    public void Emit_InvalidCodeStartingWithDigit_EmitsInvalidCodeDiagnostic()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry("9NOPE", 400, "X"));

        var result = ErrorCodesEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidCode);
    }

    [Fact]
    public void Emit_MissingDoc_EmitsMissingDocDiagnostic()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry("X", 400, string.Empty));

        var result = ErrorCodesEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.MissingDoc);
    }

    [Fact]
    public void Emit_WhitespaceDoc_EmitsMissingDocDiagnostic()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry("X", 400, "   "));

        var result = ErrorCodesEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.MissingDoc);
    }

    [Fact]
    public void Emit_PreservesSpecOrder()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry("ZEBRA", 400, "Z"),
            new ErrorCodeEntry("ALPHA", 400, "A"));

        var result = ErrorCodesEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();

        // ZEBRA must appear before ALPHA in the emitted source — spec order
        // wins; alphabetical sorting would change the diff shape on spec edits.
        var zebraIndex = result.GeneratedSource.IndexOf("ZEBRA", System.StringComparison.Ordinal);
        var alphaIndex = result.GeneratedSource.IndexOf("ALPHA", System.StringComparison.Ordinal);
        zebraIndex.Should().BeLessThan(alphaIndex);
    }

    [Fact]
    public void Emit_RunsTwiceWithIdenticalInput_ProducesIdenticalSource()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry("X", 400, "X doc."));

        var first = ErrorCodesEmitter.Emit(spec);
        var second = ErrorCodesEmitter.Emit(spec);

        second.GeneratedSource.Should().Be(first.GeneratedSource);
    }

    /// <summary>
    /// Per-VALUE pin for every shipping spec entry: the emitter MUST produce
    /// the exact constant declaration AND the exact switch-arm wire-value mapping.
    /// A drift of any single entry's code, httpStatus, or the switch-arm flips
    /// these rows red — the failure message names the specific drifted constant.
    /// </summary>
    /// <param name="code">The wire-format error code expected on the emitted constant.</param>
    /// <param name="httpStatus">
    /// The HTTP status the emitted switch arm must map the code to.
    /// </param>
    [Theory]
    [InlineData("NOT_FOUND", 404)]
    [InlineData("FORBIDDEN", 403)]
    [InlineData("UNAUTHORIZED", 401)]
    [InlineData("VALIDATION_FAILED", 400)]
    [InlineData("CONFLICT", 409)]
    [InlineData("UNHANDLED_EXCEPTION", 500)]
    [InlineData("COULD_NOT_BE_SERIALIZED", 500)]
    [InlineData("COULD_NOT_BE_DESERIALIZED", 500)]
    [InlineData("SERVICE_UNAVAILABLE", 503)]
    [InlineData("SOME_FOUND", 206)]
    [InlineData("PARTIAL_SUCCESS", 207)]
    [InlineData("RATE_LIMITED", 429)]
    [InlineData("IDEMPOTENCY_IN_FLIGHT", 409)]
    [InlineData("PAYLOAD_TOO_LARGE", 413)]
    [InlineData("CANCELED", 400)]
    public void Emit_ShippingSpecEntry_EmitsConstantAndHttpStatusMapping(
        string code, int httpStatus)
    {
        var spec = MakeSpec(
            new ErrorCodeEntry(code, httpStatus, $"{code} doc."));

        var result = ErrorCodesEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain($"public const string {code} = \"{code}\";");
        result.GeneratedSource.Should().Contain($"\"{code}\" => {httpStatus},");
    }

    [Fact]
    public void Emit_XmlDocSpecialChars_AreEscaped()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry("X", 400, "Has <angle> & ampersand."));

        var result = ErrorCodesEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("&lt;angle&gt; &amp; ampersand.");
    }

    [Fact]
    public void Emit_ShortDoc_KeepsCompactSingleLineSummary()
    {
        var spec = MakeSpec(new ErrorCodeEntry("X", 400, "Short doc."));

        var result = ErrorCodesEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("    /// <summary>Short doc.</summary>");
    }

    private static ErrorCodesSpec MakeSpec(params ErrorCodeEntry[] entries) =>
        new(entries.ToImmutableArray());
}
