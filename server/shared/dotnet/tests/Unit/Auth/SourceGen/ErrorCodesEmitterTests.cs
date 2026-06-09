// -----------------------------------------------------------------------
// <copyright file="ErrorCodesEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using D2.Shared.Auth.ErrorCodes.SourceGen;
using D2.Shared.ErrorCodes.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the AuthErrorCodes / AuthFailures emission, driving
/// the shared <see cref="ConstantsEmitter"/> / <see cref="FailuresEmitter"/>
/// with the real auth catalog config + synthetic specs. Asserts both the
/// generated source shape and the diagnostics surfaced for invalid inputs.
/// </summary>
public sealed class ErrorCodesEmitterTests
{
    private static CatalogConfig Config => ErrorCodesGenerator.Config;

    [Fact]
    public void Emit_ValidSingleEntry_EmitsConstantAndAllCodesAndKebabCaseHelper()
    {
        var spec = MakeSpec(AuthEntry("AUTH_X", 401, "validation_failure", "TK.A.B", "X", "X doc."));

        var result = ConstantsEmitter.Emit(spec, Config);

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
            AuthEntry("AUTH_X", 401, "validation_failure", "TK", "X", "X"),
            AuthEntry("AUTH_X", 401, "validation_failure", "TK", "Y", "Y"));

        var result = ConstantsEmitter.Emit(spec, Config);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateCode);
    }

    [Fact]
    public void Emit_DuplicateFactoryName_EmitsDuplicateFactoryNameDiagnostic()
    {
        var spec = MakeSpec(
            AuthEntry("AUTH_X", 401, "validation_failure", "TK", "Same", "X"),
            AuthEntry("AUTH_Y", 401, "validation_failure", "TK", "Same", "Y"));

        var result = ConstantsEmitter.Emit(spec, Config);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateFactoryName);
    }

    [Fact]
    public void Emit_UnknownCategory_EmitsUnknownCategoryEnumDiagnostic()
    {
        var spec = MakeSpec(AuthEntry("AUTH_X", 401, "bogus_category", "TK", "X", "X"));

        var result = ConstantsEmitter.Emit(spec, Config);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.UnknownCategoryEnum);
    }

    [Fact]
    public void Emit_InvalidHttpStatus_EmitsInvalidHttpStatusDiagnostic()
    {
        var spec = MakeSpec(AuthEntry("AUTH_X", 418, "validation_failure", "TK", "X", "X"));

        var result = ConstantsEmitter.Emit(spec, Config);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidHttpStatus);
    }

    [Fact]
    public void Emit_NonAuthPrefixedCode_EmitsDomainPrefixViolationDiagnostic()
    {
        // The enforced AUTH_ domain prefix: a non-prefixed code fires D2ERC001.
        var spec = MakeSpec(AuthEntry("BEARER_MISSING", 401, "validation_failure", "TK", "X", "X"));

        var result = ConstantsEmitter.Emit(spec, Config);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == EngineDiagnosticIds.DomainPrefixViolation);
    }

    [Fact]
    public void Emit_RealAuthCodes_DoNotFireDomainPrefixViolation()
    {
        var spec = MakeSpec(
            AuthEntry("AUTH_BEARER_MISSING", 401, "validation_failure", "TK", "X", "X"),
            AuthEntry("AUTH_JWKS_UNAVAILABLE", 503, "infrastructure_unavailable", "TK", "Y", "Y"));

        var result = ConstantsEmitter.Emit(spec, Config);

        result.Diagnostics.Should()
            .NotContain(d => d.DescriptorId == EngineDiagnosticIds.DomainPrefixViolation);
    }

    [Fact]
    public void Emit_KebabCaseHelperShape_RoundTripsAuthBearerMissingToAuthBearerMissing()
    {
        var spec = MakeSpec(
            AuthEntry("AUTH_BEARER_MISSING", 401, "validation_failure", "TK", "X", "X"));

        var result = ConstantsEmitter.Emit(spec, Config);

        result.GeneratedSource.Should().Contain("c == '_' ? '-' : char.ToLowerInvariant(c)");
    }

    [Fact]
    public void Emit_FailureFactories401Entry_EmitsUnauthorizedFactoryNoTypedOverload()
    {
        var spec = MakeSpec(
            AuthEntry("AUTH_X", 401, "validation_failure", "TK.U", "X", "X doc."));

        var result = FailuresEmitter.Emit(spec, Config);

        result.GeneratedSource.Should().Contain("D2Result.Unauthorized");
        result.GeneratedSource.Should().Contain("messages: [TK.U]");
        result.GeneratedSource.Should().Contain("errorCode: AuthErrorCodes.AUTH_X");

        // The auth code's OWN category is stamped onto the base factory
        // (validation_failure here, overriding Unauthorized's policy_denied).
        result.GeneratedSource.Should().Contain(
            "category: ErrorCategory.ValidationFailure");
        result.GeneratedSource.Should().Contain("public static D2Result X()");

        // 401 entries do NOT get a typed <T> overload (only 503 do).
        result.GeneratedSource.Should().NotContain("public static D2Result<T> X<T>()");
    }

    [Fact]
    public void
    Emit_FailureFactories503Entry_EmitsServiceUnavailableAndTypedOverload()
    {
        var spec = MakeSpec(
            AuthEntry("AUTH_X", 503, "infrastructure_unavailable", "TK.T", "X", "X doc."));

        var result = FailuresEmitter.Emit(spec, Config);

        result.GeneratedSource.Should().Contain("D2Result.ServiceUnavailable");
        result.GeneratedSource.Should().Contain("public static D2Result X()");
        result.GeneratedSource.Should().Contain("public static D2Result<T> X<T>()");
        result.GeneratedSource.Should().Contain("D2Result<T>.ServiceUnavailable");
        result.GeneratedSource.Should().Contain(
            "category: ErrorCategory.InfrastructureUnavailable");
    }

    [Fact]
    public void Emit_FailureFactoriesPolicyDenied401Entry_EmitsUnauthorizedFactoryNoTypedOverload()
    {
        var spec = MakeSpec(
            AuthEntry("AUTH_X", 401, "policy_denied", "TK.U", "X", "X doc."));

        var result = FailuresEmitter.Emit(spec, Config);

        result.GeneratedSource.Should().Contain("D2Result.Unauthorized");
        result.GeneratedSource.Should().NotContain("public static D2Result<T> X<T>()");
    }

    [Fact]
    public void Emit_RunsTwiceWithIdenticalInput_ProducesIdenticalSource()
    {
        var spec = MakeSpec(AuthEntry("AUTH_X", 401, "validation_failure", "TK", "X", "X"));

        var first = ConstantsEmitter.Emit(spec, Config);
        var second = ConstantsEmitter.Emit(spec, Config);

        second.GeneratedSource.Should().Be(first.GeneratedSource);
    }

    [Fact]
    public void EmitGeneric_EmitsGenericFailuresClassDelegatingToTypedBaseFactory()
    {
        var spec = MakeSpec(
            AuthEntry("AUTH_X", 401, "validation_failure", "TK.U", "X", "X doc."));

        var result = FailuresEmitter.EmitGeneric(spec, Config);

        // The generic class is `public static class AuthFailures<T>` and each
        // method delegates to the typed D2Result<T> base factory.
        result.GeneratedSource.Should().Contain("public static class AuthFailures<T>");
        result.GeneratedSource.Should().Contain("public static D2Result<T> X() =>");
        result.GeneratedSource.Should().Contain("D2Result<T>.Unauthorized(");
        result.GeneratedSource.Should().Contain("messages: [TK.U]");
        result.GeneratedSource.Should().Contain("errorCode: AuthErrorCodes.AUTH_X");

        // The generic class does NOT carry a per-method <T> overload (the class
        // itself is generic) — distinct from the non-generic class's 503 twin.
        result.GeneratedSource.Should().NotContain("public static D2Result<T> X<T>()");
    }

    [Fact]
    public void EmitGeneric_503Entry_DelegatesToTypedServiceUnavailable()
    {
        var spec = MakeSpec(
            AuthEntry("AUTH_X", 503, "infrastructure_unavailable", "TK.T", "X", "X doc."));

        var result = FailuresEmitter.EmitGeneric(spec, Config);

        result.GeneratedSource.Should().Contain("public static D2Result<T> X() =>");
        result.GeneratedSource.Should().Contain("D2Result<T>.ServiceUnavailable(");
    }

    [Fact]
    public void EmitGeneric_NoneShape_EmitsNoFactory()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry(
                Code: "AUTH_X",
                HttpStatus: 401,
                Doc: "X doc.",
                Category: "validation_failure",
                UserMessageKey: "TK.U",
                FactoryName: "X",
                FactoryShape: "none"));

        var result = FailuresEmitter.EmitGeneric(spec, Config);

        result.GeneratedSource.Should().Contain("public static class AuthFailures<T>");
        result.GeneratedSource.Should().NotContain("public static D2Result<T> X()");
    }

    /// <summary>
    /// Pins the full canonical httpStatus→base-factory delegation map.
    /// Auth exercises only 401/503 today; the map covers all per-domain
    /// statuses so future catalogs reuse it correctly.
    /// </summary>
    /// <param name="httpStatus">HTTP status to look up.</param>
    /// <param name="expectedFactory">Expected <c>D2Result</c> factory name.</param>
    [Theory]
    [InlineData(400, "ValidationFailed")]
    [InlineData(401, "Unauthorized")]
    [InlineData(403, "Forbidden")]
    [InlineData(404, "NotFound")]
    [InlineData(409, "Conflict")]
    [InlineData(413, "PayloadTooLarge")]
    [InlineData(429, "TooManyRequests")]
    [InlineData(500, "UnhandledException")]
    [InlineData(503, "ServiceUnavailable")]
    [InlineData(418, "UnhandledException")]
    public void BaseFactory_ReturnsCorrectFactoryForEveryCanonicalStatus(
        int httpStatus, string expectedFactory)
    {
        FailuresEmitter.BaseFactory(httpStatus).Should().Be(expectedFactory);
    }

    private static ErrorCodeEntry AuthEntry(
        string code,
        int httpStatus,
        string category,
        string userMessageKey,
        string factoryName,
        string doc) =>
        new(
            Code: code,
            HttpStatus: httpStatus,
            Doc: doc,
            Category: category,
            UserMessageKey: userMessageKey,
            FactoryName: factoryName,
            FactoryShape: "with_error_code");

    private static ErrorCodesSpec MakeSpec(params ErrorCodeEntry[] entries) =>
        new(entries.ToImmutableArray());
}
