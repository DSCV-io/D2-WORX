// -----------------------------------------------------------------------
// <copyright file="D2ResultTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result;

using System.Net;
using AwesomeAssertions;
using DcsvIo.D2.ErrorCodes.Category;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using Xunit;

public sealed class D2ResultTests
{
    // ----------------------------------------------------------------------
    // Constructor — direct invocation
    // ----------------------------------------------------------------------

    [Fact]
    public void Ctor_WithSuccessTrue_DefaultsStatusCodeToOk()
    {
        var result = new D2Result(success: true);

        result.Success.Should().BeTrue();
        result.Failed.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Messages.Should().BeEmpty();
        result.InputErrors.Should().BeEmpty();
        result.ErrorCode.Should().BeNull();
        result.TraceId.Should().BeNull();
    }

    [Fact]
    public void Ctor_WithSuccessFalse_DefaultsStatusCodeToBadRequest()
    {
        var result = new D2Result(success: false);

        result.Success.Should().BeFalse();
        result.Failed.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public void Ctor_WithExplicitStatusCode_OverridesDefault()
    {
        var result = new D2Result(success: true, statusCode: HttpStatusCode.Created);

        result.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public void Ctor_WithNullMessages_DefaultsToEmptyList()
    {
        var result = new D2Result(success: false, messages: null);

        result.Messages.Should().NotBeNull();
        result.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Ctor_WithNullInputErrors_DefaultsToEmptyList()
    {
        var result = new D2Result(success: false, inputErrors: null);

        result.InputErrors.Should().NotBeNull();
        result.InputErrors.Should().BeEmpty();
    }

    [Fact]
    public void Ctor_WithEmptyMessagesList_PreservesEmpty()
    {
        var result = new D2Result(success: false, messages: []);

        result.Messages.Should().NotBeNull();
        result.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Ctor_WithMultipleMessages_PreservesAll()
    {
        IReadOnlyList<TKMessage> messages =
        [
            TK.Common.Errors.NOT_FOUND,
            TK.Common.Errors.FORBIDDEN,
            TK.Common.Errors.UNAUTHORIZED,
        ];

        var result = new D2Result(success: false, messages: messages);

        result.Messages.Should().Equal(
            TK.Common.Errors.NOT_FOUND,
            TK.Common.Errors.FORBIDDEN,
            TK.Common.Errors.UNAUTHORIZED);
    }

    [Fact]
    public void Ctor_WithMultipleInputErrors_PreservesAll()
    {
        IReadOnlyList<InputError> errors =
        [
            new InputError(
                "email",
                [TK.Common.Validation.EMAIL_INVALID, TK.Common.Validation.NON_EMPTY_LIST]),
            new InputError("age", [TK.Common.Validation.NON_EMPTY_LIST]),
        ];

        var result = new D2Result(success: false, inputErrors: errors);

        result.InputErrors.Should().HaveCount(2);
        result.InputErrors[0].Field.Should().Be("email");
        result.InputErrors[0].Errors.Should().Equal(
            TK.Common.Validation.EMAIL_INVALID,
            TK.Common.Validation.NON_EMPTY_LIST);
        result.InputErrors[1].Field.Should().Be("age");
        result.InputErrors[1].Errors.Should().Equal(TK.Common.Validation.NON_EMPTY_LIST);
    }

    [Fact]
    public void Ctor_WithEmptyTraceId_PreservesEmptyString()
    {
        // Adversarial: empty string vs null are distinct — caller's choice should be respected.
        var result = new D2Result(success: true, traceId: string.Empty);

        result.TraceId.Should().Be(string.Empty);
    }

    [Fact]
    public void Ctor_WithWhitespaceTraceId_PreservesWhitespace()
    {
        // Adversarial: D2Result is value-bag, no normalization applied.
        var result = new D2Result(success: true, traceId: "   ");

        result.TraceId.Should().Be("   ");
    }

    [Fact]
    public void Failed_IsAlwaysOppositeOfSuccess()
    {
        new D2Result(success: true).Failed.Should().BeFalse();
        new D2Result(success: false).Failed.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // Ok / Created
    // ----------------------------------------------------------------------

    [Fact]
    public void Ok_CreatesSuccessResult()
    {
        var result = D2Result.Ok();

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.ErrorCode.Should().BeNull();
        result.Messages.Should().BeEmpty();
        result.TraceId.Should().BeNull();
    }

    [Fact]
    public void Ok_WithTraceId_PreservesTraceId()
    {
        const string trace_id = "trace-abc-123";

        var result = D2Result.Ok(trace_id);

        result.Success.Should().BeTrue();
        result.TraceId.Should().Be(trace_id);
    }

    [Fact]
    public void Created_CreatesSuccessWithCreatedStatus()
    {
        var result = D2Result.Created();

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.Created);
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void Created_WithMessagesAndTraceId_CarriesBoth()
    {
        IReadOnlyList<TKMessage> messages = [TK.Common.Errors.SOME_FOUND];

        var result = D2Result.Created(messages, traceId: "t1");

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Messages.Should().Equal(TK.Common.Errors.SOME_FOUND);
        result.TraceId.Should().Be("t1");
    }

    // ----------------------------------------------------------------------
    // Fail (raw)
    // ----------------------------------------------------------------------

    [Fact]
    public void Fail_WithDefaults_CreatesBadRequestFailure()
    {
        var result = D2Result.Fail();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().BeNull();
        result.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Fail_WithAllFields_CarriesEverything()
    {
        IReadOnlyList<TKMessage> messages = [TK.Common.Errors.UNKNOWN];
        IReadOnlyList<InputError> inputErrors =
        [
            new InputError("field", [TK.Common.Validation.NON_EMPTY_LIST]),
        ];

        var result = D2Result.Fail(
            messages: messages,
            statusCode: HttpStatusCode.Forbidden,
            inputErrors: inputErrors,
            errorCode: "CUSTOM_CODE",
            traceId: "t2");

        result.Success.Should().BeFalse();
        result.Messages.Should().Equal(TK.Common.Errors.UNKNOWN);
        result.InputErrors.Should().HaveCount(1);
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.ErrorCode.Should().Be("CUSTOM_CODE");
        result.TraceId.Should().Be("t2");
    }

    // ----------------------------------------------------------------------
    // Semantic factories — happy path + custom messages override
    // ----------------------------------------------------------------------

    [Fact]
    public void NotFound_DefaultsToTkMessage()
    {
        var result = D2Result.NotFound();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.NOT_FOUND);
        result.Messages.Should().Equal(TK.Common.Errors.NOT_FOUND);
    }

    [Fact]
    public void NotFound_WithCustomMessages_UsesCustomNotDefault()
    {
        // A non-default TKMessage proves the custom argument overrides the
        // TK.Common.Errors.NOT_FOUND default.
        IReadOnlyList<TKMessage> custom = [TK.Common.Errors.FORBIDDEN];

        var result = D2Result.NotFound(custom);

        result.Messages.Should().Equal(TK.Common.Errors.FORBIDDEN);
    }

    [Fact]
    public void NotFound_AcceptsUnifiedOpts_InputErrorsErrorCodeAndCategory()
    {
        // Unified-shape regression pin: NotFound was previously the restricted
        // shape (messages?, traceId? only). After the fold every error factory is
        // the one universal standard shape, so a 404 can carry inputErrors and
        // stamp a domain errorCode + category override.
        IReadOnlyList<InputError> errors =
        [
            new InputError("id", [TK.Common.Validation.ID_INVALID]),
        ];

        var result = D2Result.NotFound(
            inputErrors: errors,
            errorCode: "GEO_SUBDIVISION_NOT_FOUND",
            category: ErrorCategory.Conflict,
            traceId: "trace-9");

        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result.ErrorCode.Should().Be("GEO_SUBDIVISION_NOT_FOUND");
        result.Category.Should().Be(ErrorCategory.Conflict);
        result.TraceId.Should().Be("trace-9");
        result.InputErrors.Should().HaveCount(1);
        result.InputErrors[0].Field.Should().Be("id");
        result.Messages.Should().Equal(TK.Common.Errors.NOT_FOUND);
    }

    [Fact]
    public void Forbidden_DefaultsToTkMessage()
    {
        var result = D2Result.Forbidden();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.ErrorCode.Should().Be(ErrorCodes.FORBIDDEN);
        result.Messages.Should().Equal(TK.Common.Errors.FORBIDDEN);
    }

    [Fact]
    public void Unauthorized_DefaultsToTkMessage()
    {
        var result = D2Result.Unauthorized();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.ErrorCode.Should().Be(ErrorCodes.UNAUTHORIZED);
        result.Messages.Should().Equal(TK.Common.Errors.UNAUTHORIZED);
    }

    [Fact]
    public void ValidationFailed_DefaultsToTkMessage()
    {
        var result = D2Result.ValidationFailed();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(ErrorCodes.VALIDATION_FAILED);
        result.Messages.Should().Equal(TK.Common.Errors.VALIDATION_FAILED);
    }

    [Fact]
    public void ValidationFailed_WithInputErrors_CarriesThem()
    {
        IReadOnlyList<InputError> errors =
        [
            new InputError("email", [TK.Common.Validation.EMAIL_INVALID]),
        ];

        var result = D2Result.ValidationFailed(inputErrors: errors);

        result.InputErrors.Should().HaveCount(1);
        result.InputErrors[0].Field.Should().Be("email");
        result.InputErrors[0].Errors.Should().Equal(TK.Common.Validation.EMAIL_INVALID);
    }

    [Fact]
    public void ValidationFailed_WithCustomErrorCode_OverridesDefault()
    {
        var result = D2Result.ValidationFailed(errorCode: "FILES_INVALID_CONTENT_TYPE");

        result.ErrorCode.Should().Be("FILES_INVALID_CONTENT_TYPE");
        result.Messages.Should().Equal(TK.Common.Errors.VALIDATION_FAILED);
    }

    [Fact]
    public void Conflict_DefaultsToTkMessage()
    {
        var result = D2Result.Conflict();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
        result.ErrorCode.Should().Be(ErrorCodes.CONFLICT);
        result.Messages.Should().Equal(TK.Common.Errors.CONFLICT);
    }

    [Fact]
    public void ServiceUnavailable_DefaultsToTkMessage()
    {
        var result = D2Result.ServiceUnavailable();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(ErrorCodes.SERVICE_UNAVAILABLE);
        result.Messages.Should().Equal(TK.Common.Errors.SERVICE_UNAVAILABLE);
    }

    [Fact]
    public void ServiceUnavailable_WithCustomErrorCode_OverridesDefault()
    {
        var result = D2Result.ServiceUnavailable(errorCode: "DOMAIN_RETRY_LATER");

        result.ErrorCode.Should().Be("DOMAIN_RETRY_LATER");
    }

    [Fact]
    public void UnhandledException_DefaultsToCommonErrorsUnknown()
    {
        var result = D2Result.UnhandledException();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorCode.Should().Be(ErrorCodes.UNHANDLED_EXCEPTION);
        result.Category.Should().Be(ErrorCategory.InternalError);
        result.Messages.Should().Equal(TK.Common.Errors.UNKNOWN);
    }

    [Fact]
    public void UnhandledException_WithCustomErrorCode_OverridesDefault()
    {
        // The 500 base factory accepts an errorCode override so a delegating
        // per-domain 500 factory can stamp a specific code on the base status
        // (the mechanism KeyCustodianFailures.PreconditionViolated uses).
        var result = D2Result.UnhandledException(errorCode: "DOMAIN_PRECONDITION_VIOLATED");

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorCode.Should().Be("DOMAIN_PRECONDITION_VIOLATED");
        result.Category.Should().Be(ErrorCategory.InternalError);
    }

    [Fact]
    public void UnhandledException_WithCustomCategory_OverridesDefault()
    {
        var result = D2Result.UnhandledException(
            errorCode: "DOMAIN_PRECONDITION_VIOLATED",
            category: ErrorCategory.InternalError);

        result.ErrorCode.Should().Be("DOMAIN_PRECONDITION_VIOLATED");
        result.Category.Should().Be(ErrorCategory.InternalError);
    }

    [Fact]
    public void PayloadTooLarge_DefaultsToTkMessage()
    {
        var result = D2Result.PayloadTooLarge();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        result.ErrorCode.Should().Be(ErrorCodes.PAYLOAD_TOO_LARGE);
        result.Messages.Should().Equal(TK.Common.Errors.PAYLOAD_TOO_LARGE);
    }

    [Fact]
    public void TooManyRequests_DefaultsToRateLimitedErrorCode()
    {
        var result = D2Result.TooManyRequests();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        result.ErrorCode.Should().Be(ErrorCodes.RATE_LIMITED);
        result.Messages.Should().Equal(TK.Common.Errors.TOO_MANY_REQUESTS);
    }

    [Fact]
    public void TooManyRequests_WithCustomErrorCode_OverridesDefault()
    {
        var result = D2Result.TooManyRequests(errorCode: "OTP_RATE_LIMITED");

        result.ErrorCode.Should().Be("OTP_RATE_LIMITED");
    }

    [Fact]
    public void Canceled_DefaultsToTkMessage()
    {
        var result = D2Result.Canceled();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(ErrorCodes.CANCELED);
        result.Messages.Should().Equal(TK.Common.Errors.CANCELED);
    }

    [Fact]
    public void SomeFound_IsFailureWithPartialContentStatus()
    {
        // Adversarial: SomeFound is on the partial-success ladder — Success is FALSE
        // even though "we found some" is a partial-success outcome. Only the OK factory
        // sets Success=true on the ladder.
        var result = D2Result.SomeFound();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        result.ErrorCode.Should().Be(ErrorCodes.SOME_FOUND);
        result.Messages.Should().Equal(TK.Common.Errors.SOME_FOUND);
    }

    [Fact]
    public void AllSemanticFailureFactories_PropagateTraceId()
    {
        // Boundary: every factory accepts a traceId; verify each one carries it.
        const string trace_id = "trace-xyz";

        D2Result.NotFound(traceId: trace_id).TraceId.Should().Be(trace_id);
        D2Result.Forbidden(traceId: trace_id).TraceId.Should().Be(trace_id);
        D2Result.Unauthorized(traceId: trace_id).TraceId.Should().Be(trace_id);
        D2Result.ValidationFailed(traceId: trace_id).TraceId.Should().Be(trace_id);
        D2Result.Conflict(traceId: trace_id).TraceId.Should().Be(trace_id);
        D2Result.ServiceUnavailable(traceId: trace_id).TraceId.Should().Be(trace_id);
        D2Result.UnhandledException(traceId: trace_id).TraceId.Should().Be(trace_id);
        D2Result.PayloadTooLarge(traceId: trace_id).TraceId.Should().Be(trace_id);
        D2Result.TooManyRequests(traceId: trace_id).TraceId.Should().Be(trace_id);
        D2Result.Canceled(traceId: trace_id).TraceId.Should().Be(trace_id);
        D2Result.SomeFound(traceId: trace_id).TraceId.Should().Be(trace_id);
    }
}
