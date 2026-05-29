// -----------------------------------------------------------------------
// <copyright file="ErrorCodesEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using D2.Shared.Auth.ErrorCodes.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the AuthErrorCodes / AuthFailures emitters. Drives
/// the emitter directly with synthetic specs and asserts both the generated
/// source shape and the diagnostics surfaced for invalid spec inputs.
/// </summary>
public sealed class ErrorCodesEmitterTests
{
    [Fact]
    public void Emit_ValidSingleEntry_EmitsConstantAndAllCodesAndKebabCaseHelper()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry(
                Code: "AUTH_X",
                HttpStatus: 401,
                Category: "validation_failure",
                UserMessageKey: "TK.A.B",
                FactoryName: "X",
                Doc: "X doc."));

        var result = ErrorCodesEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public const string AUTH_X = \"AUTH_X\";");
        result.GeneratedSource.Should().Contain("namespace D2.Shared.Auth.Errors;");
        result.GeneratedSource.Should().Contain("public static class AuthErrorCodes");
        result.GeneratedSource.Should().Contain(
            "public static int GetHttpStatus(string errorCode)");
        result.GeneratedSource.Should().Contain(
            "public static string KebabCase(string upperUnderscore)");
        result.GeneratedSource.Should().Contain("\"AUTH_X\" => 401,");
        result.GeneratedSource.Should().Contain(
            "public static IReadOnlyList<string> AllCodes => sr_allCodes;");
    }

    [Fact]
    public void Emit_DuplicateCode_EmitsDuplicateCodeDiagnostic()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry("AUTH_X", 401, "validation_failure", "TK", "X", "X"),
            new ErrorCodeEntry("AUTH_X", 401, "validation_failure", "TK", "Y", "Y"));

        var result = ErrorCodesEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateCode);
    }

    [Fact]
    public void Emit_DuplicateFactoryName_EmitsDuplicateFactoryNameDiagnostic()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry("AUTH_X", 401, "validation_failure", "TK", "Same", "X"),
            new ErrorCodeEntry("AUTH_Y", 401, "validation_failure", "TK", "Same", "Y"));

        var result = ErrorCodesEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateFactoryName);
    }

    [Fact]
    public void Emit_UnknownCategory_EmitsUnknownCategoryEnumDiagnostic()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry("AUTH_X", 401, "bogus_category", "TK", "X", "X"));

        var result = ErrorCodesEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.UnknownCategoryEnum);
    }

    [Fact]
    public void Emit_InvalidHttpStatus_EmitsInvalidHttpStatusDiagnostic()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry("AUTH_X", 418, "validation_failure", "TK", "X", "X"));

        var result = ErrorCodesEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidHttpStatus);
    }

    [Fact]
    public void Emit_KebabCaseHelperShape_RoundTripsAuthBearerMissingToAuthBearerMissing()
    {
        // Independent runtime check that the emitted KebabCase helper logic
        // compiles to the expected string transformation. The actual emitted
        // code is asserted by the post-build AuthErrorCodesGeneratedTests
        // which exercise the generated method against the real spec.
        var spec = MakeSpec(
            new ErrorCodeEntry("AUTH_BEARER_MISSING", 401, "validation_failure", "TK", "X", "X"));

        var result = ErrorCodesEmitter.Emit(spec);

        result.GeneratedSource.Should().Contain("c == '_' ? '-' : char.ToLowerInvariant(c)");
    }

    [Fact]
    public void Emit_FailureFactoriesValidationFailureCategory_EmitsUnauthorizedFactory()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry(
                "AUTH_X", 401, "validation_failure", "TK.U", "X", "X doc."));

        var result = FailureFactoriesEmitter.Emit(spec);

        result.GeneratedSource.Should().Contain("D2Result.Unauthorized");
        result.GeneratedSource.Should().Contain("messages: [TK.U]");
        result.GeneratedSource.Should().Contain("errorCode: AuthErrorCodes.AUTH_X");
        result.GeneratedSource.Should().Contain("public static D2Result X()");

        // Validation_failure entries do NOT get a typed <T> overload.
        result.GeneratedSource.Should().NotContain("public static D2Result<T> X<T>()");
    }

    [Fact]
    public void
    Emit_FailureFactoriesInfrastructureUnavailableCategory_EmitsServiceUnavailableAndTypedOverload()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry(
                "AUTH_X", 503, "infrastructure_unavailable", "TK.T", "X", "X doc."));

        var result = FailureFactoriesEmitter.Emit(spec);

        result.GeneratedSource.Should().Contain("D2Result.ServiceUnavailable");
        result.GeneratedSource.Should().Contain("public static D2Result X()");
        result.GeneratedSource.Should().Contain("public static D2Result<T> X<T>()");
        result.GeneratedSource.Should().Contain("D2Result<T>.ServiceUnavailable");
    }

    [Fact]
    public void Emit_FailureFactoriesPolicyDeniedCategory_EmitsUnauthorizedFactoryNoTypedOverload()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry(
                "AUTH_X", 401, "policy_denied", "TK.U", "X", "X doc."));

        var result = FailureFactoriesEmitter.Emit(spec);

        result.GeneratedSource.Should().Contain("D2Result.Unauthorized");
        result.GeneratedSource.Should().NotContain("public static D2Result<T> X<T>()");
    }

    [Fact]
    public void Emit_RunsTwiceWithIdenticalInput_ProducesIdenticalSource()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry("AUTH_X", 401, "validation_failure", "TK", "X", "X"));

        var first = ErrorCodesEmitter.Emit(spec);
        var second = ErrorCodesEmitter.Emit(spec);

        second.GeneratedSource.Should().Be(first.GeneratedSource);
    }

    private static ErrorCodesSpec MakeSpec(params ErrorCodeEntry[] entries) =>
        new(entries.ToImmutableArray());
}
