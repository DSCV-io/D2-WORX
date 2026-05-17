// -----------------------------------------------------------------------
// <copyright file="D2ProblemDetailsCustomizerD2ResultAwareTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AspNetCore.Internal;

using System.Net;
using AwesomeAssertions;
using D2.Shared.AspNetCore;
using D2.Shared.AspNetCore.Internal;
using D2.Shared.I18n;
using D2.Shared.ProblemDetails;
using D2.Shared.Result;
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
    public void Apply_AllFiveExtensionKeysReferencedAreSpecDriven()
    {
        // Reflection regression: assert the Customizer reads constants from
        // D2ProblemDetailsKeys (not string literals) — written + read via the
        // codegen-emitted catalog. Cross-check by asserting the keys appear
        // in the ProblemDetails.Extensions dictionary at their exact wire
        // values (the spec literals).
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
            .And.Contain("correlationId");
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
