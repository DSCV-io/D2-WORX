// -----------------------------------------------------------------------
// <copyright file="D2ProblemDetailsCustomizerD2ResultAwareTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AspNetCore.Internal;

using System.Net;
using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using DcsvIo.D2.AspNetCore.Internal;
using DcsvIo.D2.ErrorCodes.Category;
using DcsvIo.D2.I18n;
using DcsvIo.D2.ProblemDetails;
using DcsvIo.D2.Result;
using Microsoft.AspNetCore.Http;
using Xunit;
using MvcProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

/// <summary>
/// Pure-logic tests for the FULL D2Result-aware Customizer path. Drives
/// <see cref="D2ProblemDetailsCustomizer.Apply"/> directly against a
/// <see cref="DefaultHttpContext"/> with / without a stashed
/// <see cref="D2Result"/> and asserts the resulting ProblemDetails shape
/// matches the spec-driven constants exactly.
/// </summary>
public sealed class D2ProblemDetailsCustomizerD2ResultAwareTests
{
    [Fact]
    public void Apply_WithStashedD2Result_PopulatesTypeTitleStatusFromSpec()
    {
        var ctx = MakeContext("/api/x", method: "POST");
        var failure = D2Result.Fail(
            messages: [TK.Auth.Errors.UNAUTHORIZED],
            errorCode: "VALIDATION_FAILED",
            statusCode: HttpStatusCode.BadRequest);
        ctx.HttpContext.SetD2Result(failure);

        D2ProblemDetailsCustomizer.Apply(ctx, new D2ProblemDetailsOptions());

        ctx.ProblemDetails.Status.Should().Be(400);
        ctx.ProblemDetails.Title.Should().Be(D2ProblemDetailsKeys.TITLE_BAD_REQUEST);
        ctx.ProblemDetails.Type
            .Should().Be("https://problems.d2.dcsv.io/validation-failed");
        ctx.ProblemDetails.Instance.Should().Be("POST /api/x");
    }

    [Fact]
    public void Apply_WithStashedD2Result_PopulatesErrorCodeAndMessagesExtensions()
    {
        var ctx = MakeContext("/api/x");
        var failure = D2Result.Fail(
            messages: [TK.Auth.Errors.UNAUTHORIZED],
            errorCode: "OOPS",
            statusCode: HttpStatusCode.InternalServerError);
        ctx.HttpContext.SetD2Result(failure);

        D2ProblemDetailsCustomizer.Apply(ctx, new D2ProblemDetailsOptions());

        ctx.ProblemDetails.Extensions[D2ProblemDetailsKeys.EXTENSION_ERROR_CODE]
            .Should().Be("OOPS");
        ctx.ProblemDetails.Extensions[D2ProblemDetailsKeys.EXTENSION_MESSAGES]
            .Should().BeAssignableTo<IReadOnlyList<TKMessage>>();
    }

    [Fact]
    public void Apply_WithStashedD2Result_EmitsInputErrorsExtensionWhenPresent()
    {
        var ctx = MakeContext("/api/x");
        var inputErrors = new[]
        {
            new InputError("email", [TK.Auth.Errors.UNAUTHORIZED]),
        };
        var failure = D2Result.Fail(
            messages: [TK.Auth.Errors.UNAUTHORIZED],
            inputErrors: inputErrors,
            errorCode: "VALIDATION_FAILED",
            statusCode: HttpStatusCode.BadRequest);
        ctx.HttpContext.SetD2Result(failure);

        D2ProblemDetailsCustomizer.Apply(ctx, new D2ProblemDetailsOptions());

        ctx.ProblemDetails.Extensions[D2ProblemDetailsKeys.EXTENSION_INPUT_ERRORS]
            .Should().BeAssignableTo<IReadOnlyList<InputError>>();
    }

    [Fact]
    public void Apply_WithStashedD2Result_OmitsInputErrorsExtensionWhenEmpty()
    {
        var ctx = MakeContext("/api/x");
        var failure = D2Result.Fail(
            messages: [TK.Auth.Errors.UNAUTHORIZED],
            errorCode: "OOPS",
            statusCode: HttpStatusCode.InternalServerError);
        ctx.HttpContext.SetD2Result(failure);

        D2ProblemDetailsCustomizer.Apply(ctx, new D2ProblemDetailsOptions());

        ctx.ProblemDetails.Extensions
            .Should().NotContainKey(D2ProblemDetailsKeys.EXTENSION_INPUT_ERRORS);
    }

    [Fact]
    public void Apply_WithStashedD2Result_EmitsCategoryExtensionWhenPresent()
    {
        var ctx = MakeContext("/api/x");
        var failure = new D2Result(
            success: false,
            messages: [TK.Auth.Errors.UNAUTHORIZED],
            errorCode: "VALIDATION_FAILED",
            statusCode: HttpStatusCode.BadRequest,
            category: ErrorCategory.ValidationFailure);
        ctx.HttpContext.SetD2Result(failure);

        D2ProblemDetailsCustomizer.Apply(ctx, new D2ProblemDetailsOptions());

        ctx.ProblemDetails.Extensions[D2ProblemDetailsKeys.EXTENSION_CATEGORY]
            .Should().Be(ErrorCategory.ValidationFailure.ToWire());
    }

    [Fact]
    public void Apply_WithStashedD2Result_OmitsCategoryExtensionWhenNull()
    {
        var ctx = MakeContext("/api/x");
        var failure = D2Result.Fail(
            messages: [TK.Auth.Errors.UNAUTHORIZED],
            errorCode: "OOPS",
            statusCode: HttpStatusCode.InternalServerError);
        ctx.HttpContext.SetD2Result(failure);

        D2ProblemDetailsCustomizer.Apply(ctx, new D2ProblemDetailsOptions());

        ctx.ProblemDetails.Extensions
            .Should().NotContainKey(D2ProblemDetailsKeys.EXTENSION_CATEGORY);
    }

    [Fact]
    public void Apply_WithStashedD2ResultEmptyErrorCode_TypeFallsBackToUnhandledException()
    {
        var ctx = MakeContext("/api/x");
        var failure = D2Result.Fail(
            messages: [TK.Auth.Errors.UNAUTHORIZED],
            errorCode: string.Empty,
            statusCode: HttpStatusCode.InternalServerError);
        ctx.HttpContext.SetD2Result(failure);

        D2ProblemDetailsCustomizer.Apply(ctx, new D2ProblemDetailsOptions());

        ctx.ProblemDetails.Type
            .Should().Be("https://problems.d2.dcsv.io/unhandled-exception");
    }

    [Fact]
    public void Apply_WithoutStashedD2Result_LeavesTypeAndTitleAlone()
    {
        // ProblemDetails comes in with framework-default null Type/Title;
        // customizer SHOULD NOT impose spec values when no D2Result is
        // present.
        var ctx = MakeContext("/api/x");

        D2ProblemDetailsCustomizer.Apply(ctx, new D2ProblemDetailsOptions());

        ctx.ProblemDetails.Type.Should().BeNull();
        ctx.ProblemDetails.Title.Should().BeNull();
    }

    [Fact]
    public void Apply_WithoutStashedD2Result_StillPopulatesTraceIdAndCorrelationId()
    {
        var ctx = MakeContext("/api/x");

        D2ProblemDetailsCustomizer.Apply(ctx, new D2ProblemDetailsOptions());

        ctx.ProblemDetails.Extensions
            .Should().ContainKey(D2ProblemDetailsKeys.EXTENSION_TRACE_ID);
        ctx.ProblemDetails.Extensions
            .Should().ContainKey(D2ProblemDetailsKeys.EXTENSION_CORRELATION_ID);
    }

    [Fact]
    public void Apply_AllUnconditionalExtensionKeysReferencedAreSpecDriven()
    {
        // Reflection regression: assert the Customizer reads constants from
        // D2ProblemDetailsKeys (not string literals) — written + read via the
        // codegen-emitted catalog. Cross-check by asserting the keys appear
        // in the ProblemDetails.Extensions dictionary at their exact wire
        // values (the spec literals).
        // NOTE: d2_category is intentionally absent here — this D2Result
        // carries no Category, so the conditional-emit path omits it. The
        // omission is pinned explicitly via NotContainKey to prevent future
        // regressions where a null category is emitted as a null extension.
        var ctx = MakeContext("/api/x");
        var inputErrors = new[]
        {
            new InputError("f", [TK.Auth.Errors.UNAUTHORIZED]),
        };
        var failure = D2Result.Fail(
            messages: [TK.Auth.Errors.UNAUTHORIZED],
            inputErrors: inputErrors,
            errorCode: "VALIDATION_FAILED",
            statusCode: HttpStatusCode.BadRequest);
        ctx.HttpContext.SetD2Result(failure);

        D2ProblemDetailsCustomizer.Apply(ctx, new D2ProblemDetailsOptions());

        ctx.ProblemDetails.Extensions.Keys.Should()
            .Contain("d2_error_code")
            .And.Contain("d2_messages")
            .And.Contain("d2_input_errors")
            .And.Contain("traceId")
            .And.Contain("correlationId")
            .And.NotContain("d2_category");
    }

    [Fact]
    public void Apply_StatusCodeFromStashedD2ResultOverridesFrameworkDefault()
    {
        var ctx = MakeContext("/api/x");
        ctx.ProblemDetails.Status = 500;   // framework default
        var failure = D2Result.Fail(
            messages: [TK.Auth.Errors.UNAUTHORIZED],
            errorCode: "VALIDATION_FAILED",
            statusCode: HttpStatusCode.BadRequest);
        ctx.HttpContext.SetD2Result(failure);

        D2ProblemDetailsCustomizer.Apply(ctx, new D2ProblemDetailsOptions());

        ctx.ProblemDetails.Status.Should().Be(400);
    }

    private static ProblemDetailsContext MakeContext(string path, string method = "GET")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.Request.Path = path;
        return new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new MvcProblemDetails(),
        };
    }
}
