// -----------------------------------------------------------------------
// <copyright file="BaseFactoriesEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias ResultErrorCodesSourceGen;

namespace DcsvIo.D2.Tests.Unit.Result.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using Xunit;
using BaseFactoriesEmitter =
    ResultErrorCodesSourceGen::DcsvIo.D2.ErrorCodes.SourceGen.BaseFactoriesEmitter;
using CatalogConfig = ResultErrorCodesSourceGen::DcsvIo.D2.ErrorCodes.SourceGen.CatalogConfig;
using ErrorCodeEntry = ResultErrorCodesSourceGen::DcsvIo.D2.ErrorCodes.SourceGen.ErrorCodeEntry;
using ErrorCodesGenerator =
    ResultErrorCodesSourceGen::DcsvIo.D2.ResultErrorCodes.SourceGen.ErrorCodesGenerator;
using ErrorCodesSpec = ResultErrorCodesSourceGen::DcsvIo.D2.ErrorCodes.SourceGen.ErrorCodesSpec;
using FailuresEmitter = ResultErrorCodesSourceGen::DcsvIo.D2.ErrorCodes.SourceGen.FailuresEmitter;

/// <summary>
/// Pure-logic tests for the generic catalog's CONSTRUCTING factory emission
/// (<c>FactoryHost.Base</c>): the non-generic + generic <c>D2Result</c>
/// factories and the per-code booleans. Drives the shared
/// <see cref="BaseFactoriesEmitter"/> with the real generic catalog config +
/// synthetic specs. Behavioral equivalence with the previously hand-rolled
/// factories is pinned at runtime by the existing <c>D2ResultTests</c> /
/// <c>D2ResultBooleansTests</c> / <c>D2ResultGenericTests</c> (which call the
/// now-generated factories through the unchanged public surface) and the
/// byte-parity golden; this suite pins the emitter's source-shape per
/// <c>factoryShape</c>.
/// </summary>
public sealed class BaseFactoriesEmitterTests
{
    private static CatalogConfig Config => ErrorCodesGenerator.Config;

    [Fact]
    public void EmitFactories_StandardShape_ConstructsUniversalSignature()
    {
        var spec = MakeSpec(Entry(
            "NOT_FOUND",
            404,
            "TK.Common.Errors.NOT_FOUND",
            "NotFound",
            "standard",
            "Doc.",
            category: "not_found"));

        var src = BaseFactoriesEmitter.EmitFactories(spec, Config).GeneratedSource;

        src.Should().Contain("public partial class D2Result");
        src.Should().NotContain("sealed partial class D2Result");

        // The one universal error-factory shape: every factory (incl. the
        // previously-restricted NOT_FOUND) emits the full opts surface.
        src.Should().Contain(
            "public static D2Result NotFound(IReadOnlyList<TKMessage>? messages = null, "
            + "IReadOnlyList<InputError>? inputErrors = null, string? errorCode = null, "
            + "ErrorCategory? category = null, string? traceId = null)");
        src.Should().Contain("messages ??= [TK.Common.Errors.NOT_FOUND];");
        src.Should().Contain("statusCode: HttpStatusCode.NotFound,");
        src.Should().Contain("errorCode: errorCode ?? ErrorCodes.NOT_FOUND,");
        src.Should().Contain("category: category ?? ErrorCategory.NotFound);");

        // inputErrors is passed positionally before statusCode.
        var inputErrorsIdx = src.IndexOf("inputErrors,", System.StringComparison.Ordinal);
        var statusIdx = src.IndexOf(
            "statusCode: HttpStatusCode.NotFound,", System.StringComparison.Ordinal);
        inputErrorsIdx.Should().BeGreaterThan(0);
        inputErrorsIdx.Should().BeLessThan(statusIdx);
    }

    [Fact]
    public void EmitFactories_AnyEntry_EmitsErrorCodeAndCategoryOverrideParams()
    {
        var spec = MakeSpec(Entry(
            "FORBIDDEN",
            403,
            "TK.Common.Errors.FORBIDDEN",
            "Forbidden",
            "standard",
            "Doc.",
            category: "policy_denied"));

        var src = BaseFactoriesEmitter.EmitFactories(spec, Config).GeneratedSource;

        src.Should().Contain(
            "public static D2Result Forbidden(IReadOnlyList<TKMessage>? messages = null, "
            + "IReadOnlyList<InputError>? inputErrors = null, string? errorCode = null, "
            + "ErrorCategory? category = null, string? traceId = null)");
        src.Should().Contain("errorCode: errorCode ?? ErrorCodes.FORBIDDEN,");
        src.Should().Contain("category: category ?? ErrorCategory.PolicyDenied);");
        src.Should().Contain("statusCode: HttpStatusCode.Forbidden,");
    }

    [Fact]
    public void EmitGenericFactories_AnyEntry_EmitsErrorCodeAndCategoryOverrideParams()
    {
        var spec = MakeSpec(Entry(
            "FORBIDDEN",
            403,
            "TK.Common.Errors.FORBIDDEN",
            "Forbidden",
            "standard",
            "Doc.",
            category: "policy_denied"));

        var src = BaseFactoriesEmitter.EmitGenericFactories(spec, Config).GeneratedSource;

        src.Should().Contain("public sealed partial class D2Result<TData>");
        src.Should().Contain(
            "public static new D2Result<TData> Forbidden(IReadOnlyList<TKMessage>? messages = null, "
            + "IReadOnlyList<InputError>? inputErrors = null, string? errorCode = null, "
            + "ErrorCategory? category = null, string? traceId = null)");
        src.Should().Contain("errorCode: errorCode ?? ErrorCodes.FORBIDDEN,");
        src.Should().Contain("category: category ?? ErrorCategory.PolicyDenied);");
        src.Should().Contain("statusCode: HttpStatusCode.Forbidden,");

        // Generic twin carries `default` data and the `new` keyword.
        src.Should().Contain("false,");
        src.Should().Contain("default,");
    }

    [Fact]
    public void EmitFactories_CarriesInputErrorsErrorCodeAndCategoryParams()
    {
        var spec = MakeSpec(Entry(
            "VALIDATION_FAILED",
            400,
            "TK.Common.Errors.VALIDATION_FAILED",
            "ValidationFailed",
            "standard",
            "Doc.",
            category: "validation_failure"));

        var src = BaseFactoriesEmitter.EmitFactories(spec, Config).GeneratedSource;

        src.Should().Contain(
            "public static D2Result ValidationFailed(IReadOnlyList<TKMessage>? messages = null, "
            + "IReadOnlyList<InputError>? inputErrors = null, string? errorCode = null, "
            + "ErrorCategory? category = null, string? traceId = null)");
        src.Should().Contain("statusCode: HttpStatusCode.BadRequest,");
        src.Should().Contain("errorCode: errorCode ?? ErrorCodes.VALIDATION_FAILED,");
        src.Should().Contain("category: category ?? ErrorCategory.ValidationFailure);");

        // inputErrors is passed positionally before statusCode.
        var inputErrorsIdx = src.IndexOf("inputErrors,", System.StringComparison.Ordinal);
        var statusIdx = src.IndexOf("statusCode: HttpStatusCode.BadRequest,", System.StringComparison.Ordinal);
        inputErrorsIdx.Should().BeGreaterThan(0);
        inputErrorsIdx.Should().BeLessThan(statusIdx);
    }

    [Fact]
    public void EmitFactories_NoneShape_EmitsNoFactory()
    {
        var spec = MakeSpec(Entry(
            "SOME_FOUND", 206, "TK.Common.Errors.SOME_FOUND", "SomeFound", "none", "Doc."));

        var src = BaseFactoriesEmitter.EmitFactories(spec, Config).GeneratedSource;

        // none-shape emits the class shell but no factory body for the code.
        src.Should().Contain("public partial class D2Result");
        src.Should().NotContain("SomeFound(");
    }

    [Fact]
    public void EmitGenericFactories_CarryNewKeywordAndDefaultData()
    {
        var spec = MakeSpec(Entry(
            "CONFLICT", 409, "TK.Common.Errors.CONFLICT", "Conflict", "standard", "Doc."));

        var src = BaseFactoriesEmitter.EmitGenericFactories(spec, Config).GeneratedSource;

        src.Should().Contain("public sealed partial class D2Result<TData>");
        src.Should().Contain(
            "public static new D2Result<TData> Conflict(IReadOnlyList<TKMessage>? messages = null, "
            + "IReadOnlyList<InputError>? inputErrors = null, string? errorCode = null, "
            + "ErrorCategory? category = null, string? traceId = null)");

        // Generic twin passes `default` data as the 2nd ctor argument (after the
        // `false` success flag, before `messages`).
        var falseIdx = src.IndexOf("false,", System.StringComparison.Ordinal);
        var defaultIdx = src.IndexOf("default,", System.StringComparison.Ordinal);
        var messagesIdx = src.IndexOf("messages,", System.StringComparison.Ordinal);
        falseIdx.Should().BeLessThan(defaultIdx);
        defaultIdx.Should().BeLessThan(messagesIdx);
    }

    [Fact]
    public void EmitGenericFactories_PassesInputErrorsAfterDefaultData()
    {
        var spec = MakeSpec(Entry(
            "VALIDATION_FAILED",
            400,
            "TK.Common.Errors.VALIDATION_FAILED",
            "ValidationFailed",
            "standard",
            "Doc."));

        var src = BaseFactoriesEmitter.EmitGenericFactories(spec, Config).GeneratedSource;

        src.Should().Contain("public static new D2Result<TData> ValidationFailed(");

        // false, default, messages, inputErrors — the 4th positional ctor arg.
        var defaultIdx = src.IndexOf("default,", System.StringComparison.Ordinal);
        var inputErrorsIdx = src.IndexOf("inputErrors,", System.StringComparison.Ordinal);
        defaultIdx.Should().BeLessThan(inputErrorsIdx);
    }

    [Fact]
    public void EmitBooleans_NonNoneCode_EmitsErrorCodeKeyedBoolean()
    {
        var spec = MakeSpec(Entry(
            code: "RATE_LIMITED",
            httpStatus: 429,
            userMessageKey: "TK.Common.Errors.TOO_MANY_REQUESTS",
            factoryName: "TooManyRequests",
            factoryShape: "standard",
            doc: "Doc."));

        var src = BaseFactoriesEmitter.EmitBooleans(spec, Config).GeneratedSource;

        src.Should().Contain("public partial class D2Result");
        src.Should().Contain("[JsonIgnore]");
        src.Should().Contain("public bool IsRateLimited => ErrorCode == ErrorCodes.RATE_LIMITED;");
    }

    [Fact]
    public void EmitBooleans_BooleanKeyedNoneCode_StillEmitsBoolean()
    {
        // IDEMPOTENCY_IN_FLIGHT / SOME_FOUND / PARTIAL_SUCCESS are none-shape but
        // DO key a boolean (matching the hand-rolled surface).
        var spec = MakeSpec(Entry(
            code: "IDEMPOTENCY_IN_FLIGHT",
            httpStatus: 409,
            userMessageKey: "TK.Common.Errors.IDEMPOTENCY_IN_FLIGHT",
            factoryName: "IdempotencyInFlight",
            factoryShape: "none",
            doc: "Doc."));

        var src = BaseFactoriesEmitter.EmitBooleans(spec, Config).GeneratedSource;

        src.Should().Contain(
            "public bool IsIdempotencyInFlight => ErrorCode == ErrorCodes.IDEMPOTENCY_IN_FLIGHT;");
    }

    [Fact]
    public void EmitBooleans_SerializationCode_EmitsNoBoolean()
    {
        // COULD_NOT_BE_* are none-shape AND not boolean-keyed — no boolean.
        var spec = MakeSpec(Entry(
            code: "COULD_NOT_BE_SERIALIZED",
            httpStatus: 500,
            userMessageKey: "TK.Common.Errors.COULD_NOT_BE_SERIALIZED",
            factoryName: "CouldNotBeSerialized",
            factoryShape: "none",
            doc: "Doc."));

        var src = BaseFactoriesEmitter.EmitBooleans(spec, Config).GeneratedSource;

        src.Should().NotContain("IsCouldNotBeSerialized");
    }

    [Theory]
    [InlineData("UNHANDLED_EXCEPTION", "TK.Common.Errors.UNKNOWN", "UnhandledException")]
    [InlineData("RATE_LIMITED", "TK.Common.Errors.TOO_MANY_REQUESTS", "TooManyRequests")]
    public void EmitFactories_NameMismatchQuirks_UseSpecUserMessageKey(
        string code, string userMessageKey, string factoryName)
    {
        // The code and its default TK legitimately differ for two entries —
        // UNHANDLED_EXCEPTION -> UNKNOWN and RATE_LIMITED -> TOO_MANY_REQUESTS.
        // Both are the universal standard shape so a delegating 500/429 path can
        // stamp a specific domain code on the base status.
        var spec = MakeSpec(Entry(
            code: code,
            httpStatus: code == "RATE_LIMITED" ? 429 : 500,
            userMessageKey: userMessageKey,
            factoryName: factoryName,
            factoryShape: "standard",
            doc: "Doc."));

        var src = BaseFactoriesEmitter.EmitFactories(spec, Config).GeneratedSource;

        src.Should().Contain($"messages ??= [{userMessageKey}];");
    }

    [Fact]
    public void EmitFactories_UnhandledException_EmitsErrorCodeAndCategoryOverride()
    {
        // The 500/internal_error UNHANDLED_EXCEPTION entry is the universal
        // standard shape so a delegating per-domain 500 factory can stamp its own
        // code + category on the base InternalServerError status (the mechanism
        // KeyCustodianFailures uses).
        var spec = MakeSpec(Entry(
            "UNHANDLED_EXCEPTION",
            500,
            "TK.Common.Errors.UNKNOWN",
            "UnhandledException",
            "standard",
            "Doc.",
            category: "internal_error"));

        var src = BaseFactoriesEmitter.EmitFactories(spec, Config).GeneratedSource;

        src.Should().Contain(
            "public static D2Result UnhandledException(IReadOnlyList<TKMessage>? messages = null, "
            + "IReadOnlyList<InputError>? inputErrors = null, string? errorCode = null, "
            + "ErrorCategory? category = null, string? traceId = null)");
        src.Should().Contain("statusCode: HttpStatusCode.InternalServerError,");
        src.Should().Contain("errorCode: errorCode ?? ErrorCodes.UNHANDLED_EXCEPTION,");
        src.Should().Contain("category: category ?? ErrorCategory.InternalError);");
    }

    [Fact]
    public void EmitFactories_500Entry_DelegatesViaUnhandledException()
    {
        // A per-domain 500 entry delegates to D2Result.UnhandledException stamping
        // its own code + category — the generator-side proof for the
        // KEYCUSTODIAN_PRECONDITION_VIOLATED path.
        var entry = Entry(
            "DOMAIN_PRECONDITION_VIOLATED",
            500,
            "TK.Common.Errors.UNKNOWN",
            "PreconditionViolated",
            "standard",
            "Doc.",
            category: "internal_error");

        FailuresEmitter.BaseFactory(500).Should().Be("UnhandledException");
        BaseFactoriesEmitter.StatusName(500).Should().Be("InternalServerError");
        entry.FactoryShape.Should().Be("standard");
    }

    [Fact]
    public void StatusName_MapsEverySupportedStatusToBclMember()
    {
        BaseFactoriesEmitter.StatusName(404).Should().Be("NotFound");
        BaseFactoriesEmitter.StatusName(403).Should().Be("Forbidden");
        BaseFactoriesEmitter.StatusName(401).Should().Be("Unauthorized");
        BaseFactoriesEmitter.StatusName(400).Should().Be("BadRequest");
        BaseFactoriesEmitter.StatusName(409).Should().Be("Conflict");
        BaseFactoriesEmitter.StatusName(413).Should().Be("RequestEntityTooLarge");
        BaseFactoriesEmitter.StatusName(429).Should().Be("TooManyRequests");
        BaseFactoriesEmitter.StatusName(500).Should().Be("InternalServerError");
        BaseFactoriesEmitter.StatusName(503).Should().Be("ServiceUnavailable");
        BaseFactoriesEmitter.StatusName(206).Should().Be("PartialContent");
        BaseFactoriesEmitter.StatusName(207).Should().Be("MultiStatus");
    }

    [Fact]
    public void PascalCase_ConvertsScreamingSnakeToPascal()
    {
        BaseFactoriesEmitter.PascalCase("NOT_FOUND").Should().Be("NotFound");
        BaseFactoriesEmitter.PascalCase("RATE_LIMITED").Should().Be("RateLimited");
        BaseFactoriesEmitter.PascalCase("IDEMPOTENCY_IN_FLIGHT").Should().Be("IdempotencyInFlight");
    }

    [Theory]
    [InlineData("not_found", "NotFound")]
    [InlineData("validation_failure", "ValidationFailure")]
    [InlineData("infrastructure_unavailable", "InfrastructureUnavailable")]
    [InlineData("conflict", "Conflict")]
    public void CategoryMemberName_MapsSnakeWireToErrorCategoryMember(
        string wire, string expected)
        => BaseFactoriesEmitter.CategoryMemberName(wire).Should().Be(expected);

    [Fact]
    public void EmitFactories_RunsTwiceWithIdenticalInput_ProducesIdenticalSource()
    {
        var spec = MakeSpec(Entry(
            "NOT_FOUND", 404, "TK.Common.Errors.NOT_FOUND", "NotFound", "standard", "Doc."));

        var first = BaseFactoriesEmitter.EmitFactories(spec, Config).GeneratedSource;
        var second = BaseFactoriesEmitter.EmitFactories(spec, Config).GeneratedSource;

        second.Should().Be(first);
    }

    private static ErrorCodeEntry Entry(
        string code,
        int httpStatus,
        string userMessageKey,
        string factoryName,
        string factoryShape,
        string doc,
        string category = "internal_error") =>
        new(
            Code: code,
            HttpStatus: httpStatus,
            Doc: doc,
            Category: category,
            UserMessageKey: userMessageKey,
            FactoryName: factoryName,
            FactoryShape: factoryShape);

    private static ErrorCodesSpec MakeSpec(params ErrorCodeEntry[] entries) =>
        new(entries.ToImmutableArray());
}
