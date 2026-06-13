// -----------------------------------------------------------------------
// <copyright file="ErrorCodesEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.SourceGen;

using System.Collections.Immutable;
using System.IO;
using AwesomeAssertions;
using D2.Shared.Auth.ErrorCodes.SourceGen;
using D2.Shared.ErrorCodes.SourceGen;
using D2.Shared.Tests.Unit.Auth;
using Xunit;

/// <summary>
/// Pure-logic tests for the AuthErrorCodes / AuthFailures emission, driving
/// the shared <see cref="ConstantsEmitter"/> / <see cref="FailuresEmitter"/>
/// with the real auth catalog config + synthetic specs. Asserts both the
/// generated source shape and the diagnostics surfaced for invalid inputs.
/// </summary>
public sealed class ErrorCodesEmitterTests
{
    // The spec-derived closed category set the membership check validates
    // against (mirrors the real build's error-category.spec.json AdditionalFile).
    // Loaded from disk so the test exercises the same FIX-B path the engine
    // runs, rather than a hand-maintained subset.
    private static readonly ImmutableHashSet<string> sr_categoryWireSet =
        CategoryWireSetLoader.LoadWireSet(
            File.ReadAllText(TestPaths.ErrorCategorySpec()));

    private static CatalogConfig Config => ErrorCodesGenerator.Config;

    [Fact]
    public void Emit_ValidSingleEntry_EmitsConstantAndAllCodesAndKebabCaseHelper()
    {
        var spec = MakeSpec(AuthEntry("AUTH_X", 401, "validation_failure", "TK.A.B", "X", "X doc."));

        var result = ConstantsEmitter.Emit(spec, Config, sr_categoryWireSet);

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

        var result = ConstantsEmitter.Emit(spec, Config, sr_categoryWireSet);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateCode);
    }

    [Fact]
    public void Emit_DuplicateFactoryName_EmitsDuplicateFactoryNameDiagnostic()
    {
        var spec = MakeSpec(
            AuthEntry("AUTH_X", 401, "validation_failure", "TK", "Same", "X"),
            AuthEntry("AUTH_Y", 401, "validation_failure", "TK", "Same", "Y"));

        var result = ConstantsEmitter.Emit(spec, Config, sr_categoryWireSet);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateFactoryName);
    }

    [Fact]
    public void Emit_UnknownCategory_EmitsUnknownCategoryEnumDiagnostic()
    {
        var spec = MakeSpec(AuthEntry("AUTH_X", 401, "bogus_category", "TK", "X", "X"));

        var result = ConstantsEmitter.Emit(spec, Config, sr_categoryWireSet);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.UnknownCategoryEnum);
    }

    [Fact]
    public void Emit_SpecDerivedCategory_PreviouslyRejected_NowAccepted()
    {
        // FIX-B widening pin: `not_found` is one of the 9 declared categories but
        // was NOT in the old hand-maintained 4-value subset. The spec-derived set
        // accepts it — no unknown-category diagnostic fires.
        var spec = MakeSpec(AuthEntry("AUTH_X", 401, "not_found", "TK", "X", "X"));

        var result = ConstantsEmitter.Emit(spec, Config, sr_categoryWireSet);

        result.Diagnostics.Should()
            .NotContain(d => d.DescriptorId == DiagnosticIds.UnknownCategoryEnum);
    }

    [Fact]
    public void Emit_GenuinelyUnknownCategory_StillRejected()
    {
        // FIX-B narrowing pin: a category outside the 9 declared values still
        // fires — the spec-derived set widens to EXACTLY the 9, no more.
        var spec = MakeSpec(AuthEntry("AUTH_X", 401, "nonsense", "TK", "X", "X"));

        var result = ConstantsEmitter.Emit(spec, Config, sr_categoryWireSet);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.UnknownCategoryEnum);
    }

    [Fact]
    public void Emit_EmptyCategoryWireSet_SkipsMembershipCheck()
    {
        // Degradation pin: when error-category.spec.json is absent / malformed the
        // wire set is empty and the membership check is skipped (no false
        // positive) — mirrors the en-US.json TK cross-check's empty-degrades path.
        var spec = MakeSpec(AuthEntry("AUTH_X", 401, "nonsense", "TK", "X", "X"));

        var result = ConstantsEmitter.Emit(
            spec, Config, System.Collections.Immutable.ImmutableHashSet<string>.Empty);

        result.Diagnostics.Should()
            .NotContain(d => d.DescriptorId == DiagnosticIds.UnknownCategoryEnum);
    }

    [Fact]
    public void Emit_InvalidHttpStatus_EmitsInvalidHttpStatusDiagnostic()
    {
        var spec = MakeSpec(AuthEntry("AUTH_X", 418, "validation_failure", "TK", "X", "X"));

        var result = ConstantsEmitter.Emit(spec, Config, sr_categoryWireSet);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidHttpStatus);
    }

    [Fact]
    public void Emit_NonAuthPrefixedCode_EmitsDomainPrefixViolationDiagnostic()
    {
        // The enforced AUTH_ domain prefix: a non-prefixed code fires D2ERC001.
        var spec = MakeSpec(AuthEntry("BEARER_MISSING", 401, "validation_failure", "TK", "X", "X"));

        var result = ConstantsEmitter.Emit(spec, Config, sr_categoryWireSet);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == EngineDiagnosticIds.DomainPrefixViolation);
    }

    [Fact]
    public void Emit_RealAuthCodes_DoNotFireDomainPrefixViolation()
    {
        var spec = MakeSpec(
            AuthEntry("AUTH_BEARER_MISSING", 401, "validation_failure", "TK", "X", "X"),
            AuthEntry("AUTH_JWKS_UNAVAILABLE", 503, "infrastructure_unavailable", "TK", "Y", "Y"));

        var result = ConstantsEmitter.Emit(spec, Config, sr_categoryWireSet);

        result.Diagnostics.Should()
            .NotContain(d => d.DescriptorId == EngineDiagnosticIds.DomainPrefixViolation);
    }

    [Fact]
    public void Emit_KebabCaseHelperShape_RoundTripsAuthBearerMissingToAuthBearerMissing()
    {
        var spec = MakeSpec(
            AuthEntry("AUTH_BEARER_MISSING", 401, "validation_failure", "TK", "X", "X"));

        var result = ConstantsEmitter.Emit(spec, Config, sr_categoryWireSet);

        result.GeneratedSource.Should().Contain("c == '_' ? '-' : char.ToLowerInvariant(c)");
    }

    [Fact]
    public void Emit_FailureFactories401Entry_EmitsUnauthorizedFactoryNoTypedOverload()
    {
        var spec = MakeSpec(
            AuthEntry("AUTH_X", 401, "validation_failure", "TK.U", "X", "X doc."));

        var result = FailuresEmitter.Emit(spec, Config);

        result.GeneratedSource.Should().Contain("D2Result.Unauthorized");

        // The optional messages override defaults to the spec TK; the threaded
        // `messages` param flows through (the `messages ??= [TK.U]` idiom).
        result.GeneratedSource.Should().Contain("messages ??= [TK.U];");
        result.GeneratedSource.Should().Contain("messages: messages,");
        result.GeneratedSource.Should().Contain("errorCode: AuthErrorCodes.AUTH_X");

        // The auth code's OWN category is stamped onto the base factory
        // (validation_failure here, overriding Unauthorized's policy_denied).
        result.GeneratedSource.Should().Contain(
            "category: ErrorCategory.ValidationFailure");
        result.GeneratedSource.Should().Contain(
            "public static D2Result X(IReadOnlyList<TKMessage>? messages = null)");

        // 401 entries do NOT get a typed <T> overload (only 503 do).
        result.GeneratedSource.Should().NotContain(
            "public static D2Result<T> X<T>(IReadOnlyList<TKMessage>? messages = null)");
    }

    [Fact]
    public void
    Emit_FailureFactories503Entry_EmitsServiceUnavailableAndTypedOverload()
    {
        var spec = MakeSpec(
            AuthEntry("AUTH_X", 503, "infrastructure_unavailable", "TK.T", "X", "X doc."));

        var result = FailuresEmitter.Emit(spec, Config);

        result.GeneratedSource.Should().Contain("D2Result.ServiceUnavailable");
        result.GeneratedSource.Should().Contain(
            "public static D2Result X(IReadOnlyList<TKMessage>? messages = null)");
        result.GeneratedSource.Should().Contain(
            "public static D2Result<T> X<T>(IReadOnlyList<TKMessage>? messages = null)");
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
        result.GeneratedSource.Should().NotContain(
            "public static D2Result<T> X<T>(IReadOnlyList<TKMessage>? messages = null)");
    }

    [Fact]
    public void Emit_FailureFactory_EmitsOptionalMessagesOverrideWithDefaultFallback()
    {
        // The delegating factory takes an optional `messages` override: when the
        // caller omits it the `messages ??= [<TK>]` idiom restores the spec
        // default; when supplied it threads straight through to the base factory
        // so a call site can bind the offending argument via TKMessage.With(...).
        var spec = MakeSpec(
            AuthEntry("AUTH_X", 401, "validation_failure", "TK.U", "X", "X doc."));

        var nonGeneric = FailuresEmitter.Emit(spec, Config).GeneratedSource;
        var generic = FailuresEmitter.EmitGeneric(spec, Config).GeneratedSource;

        // Non-generic: optional param + default-fallback + threaded pass-through.
        nonGeneric.Should().Contain(
            "public static D2Result X(IReadOnlyList<TKMessage>? messages = null)");
        nonGeneric.Should().Contain("messages ??= [TK.U];");
        nonGeneric.Should().Contain("messages: messages,");
        nonGeneric.Should().NotContain("messages: [TK.U],");

        // Generic twin carries the IDENTICAL override shape.
        generic.Should().Contain(
            "public static D2Result<T> X(IReadOnlyList<TKMessage>? messages = null)");
        generic.Should().Contain("messages ??= [TK.U];");
        generic.Should().Contain("messages: messages,");
    }

    [Fact]
    public void Emit_FailureFactory503TypedOverload_AlsoCarriesMessagesOverride()
    {
        // The legacy typed <T> overload emitted on 503 entries gains the same
        // optional `messages` override so it stays parity with the others.
        var spec = MakeSpec(
            AuthEntry("AUTH_X", 503, "infrastructure_unavailable", "TK.T", "X", "X doc."));

        var result = FailuresEmitter.Emit(spec, Config).GeneratedSource;

        result.Should().Contain(
            "public static D2Result<T> X<T>(IReadOnlyList<TKMessage>? messages = null)");
        result.Should().Contain("messages ??= [TK.T];");
        result.Should().Contain("messages: messages,");
    }

    [Fact]
    public void Emit_RunsTwiceWithIdenticalInput_ProducesIdenticalSource()
    {
        var spec = MakeSpec(AuthEntry("AUTH_X", 401, "validation_failure", "TK", "X", "X"));

        var first = ConstantsEmitter.Emit(spec, Config, sr_categoryWireSet);
        var second = ConstantsEmitter.Emit(spec, Config, sr_categoryWireSet);

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
        result.GeneratedSource.Should().Contain(
            "public static D2Result<T> X(IReadOnlyList<TKMessage>? messages = null)");
        result.GeneratedSource.Should().Contain("D2Result<T>.Unauthorized(");
        result.GeneratedSource.Should().Contain("messages ??= [TK.U];");
        result.GeneratedSource.Should().Contain("messages: messages,");
        result.GeneratedSource.Should().Contain("errorCode: AuthErrorCodes.AUTH_X");

        // The generic class does NOT carry a per-method <T> overload (the class
        // itself is generic) — distinct from the non-generic class's 503 twin.
        result.GeneratedSource.Should().NotContain(
            "public static D2Result<T> X<T>(IReadOnlyList<TKMessage>? messages = null)");
    }

    [Fact]
    public void EmitGeneric_503Entry_DelegatesToTypedServiceUnavailable()
    {
        var spec = MakeSpec(
            AuthEntry("AUTH_X", 503, "infrastructure_unavailable", "TK.T", "X", "X doc."));

        var result = FailuresEmitter.EmitGeneric(spec, Config);

        result.GeneratedSource.Should().Contain(
            "public static D2Result<T> X(IReadOnlyList<TKMessage>? messages = null)");
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
        result.GeneratedSource.Should().NotContain(
            "public static D2Result<T> X(IReadOnlyList<TKMessage>? messages = null)");
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
            FactoryShape: "standard");

    private static ErrorCodesSpec MakeSpec(params ErrorCodeEntry[] entries) =>
        new(entries.ToImmutableArray());
}
