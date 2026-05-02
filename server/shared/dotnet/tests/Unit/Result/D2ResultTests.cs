// -----------------------------------------------------------------------
// <copyright file="D2ResultTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Result;

using System.Net;
using AwesomeAssertions;
using D2.Shared.Result;
using Xunit;

public class D2ResultTests
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
        List<string> messages = ["one", "two", "three"];

        var result = new D2Result(success: false, messages: messages);

        result.Messages.Should().Equal("one", "two", "three");
    }

    [Fact]
    public void Ctor_WithMultipleInputErrors_PreservesAll()
    {
        List<List<string>> errors =
        [
            ["email", "Required.", "Must be valid."],
            ["age", "Must be >= 18."],
        ];

        var result = new D2Result(success: false, inputErrors: errors);

        result.InputErrors.Should().HaveCount(2);
        result.InputErrors[0].Should().Equal("email", "Required.", "Must be valid.");
        result.InputErrors[1].Should().Equal("age", "Must be >= 18.");
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
        List<string> messages = ["Resource created."];

        var result = D2Result.Created(messages, traceId: "t1");

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Messages.Should().Equal("Resource created.");
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
        List<string> messages = ["err1"];
        List<List<string>> inputErrors = [["field", "msg"]];

        var result = D2Result.Fail(
            messages: messages,
            statusCode: HttpStatusCode.Forbidden,
            inputErrors: inputErrors,
            errorCode: "CUSTOM_CODE",
            traceId: "t2");

        result.Success.Should().BeFalse();
        result.Messages.Should().Equal("err1");
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
        result.Messages.Should().Equal("common_errors_NOT_FOUND");
    }

    [Fact]
    public void NotFound_WithCustomMessages_UsesCustomNotDefault()
    {
        List<string> custom = ["my custom not-found"];

        var result = D2Result.NotFound(custom);

        result.Messages.Should().Equal("my custom not-found");
    }

    [Fact]
    public void Forbidden_DefaultsToTkMessage()
    {
        var result = D2Result.Forbidden();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.ErrorCode.Should().Be(ErrorCodes.FORBIDDEN);
        result.Messages.Should().Equal("common_errors_FORBIDDEN");
    }

    [Fact]
    public void Unauthorized_DefaultsToTkMessage()
    {
        var result = D2Result.Unauthorized();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.ErrorCode.Should().Be(ErrorCodes.UNAUTHORIZED);
        result.Messages.Should().Equal("common_errors_UNAUTHORIZED");
    }

    [Fact]
    public void ValidationFailed_DefaultsToTkMessage()
    {
        var result = D2Result.ValidationFailed();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(ErrorCodes.VALIDATION_FAILED);
        result.Messages.Should().Equal("common_errors_VALIDATION_FAILED");
    }

    [Fact]
    public void ValidationFailed_WithInputErrors_CarriesThem()
    {
        List<List<string>> errors = [["email", "Required."]];

        var result = D2Result.ValidationFailed(inputErrors: errors);

        result.InputErrors.Should().HaveCount(1);
        result.InputErrors[0].Should().Equal("email", "Required.");
    }

    [Fact]
    public void ValidationFailed_WithCustomErrorCode_OverridesDefault()
    {
        var result = D2Result.ValidationFailed(errorCode: "FILES_INVALID_CONTENT_TYPE");

        result.ErrorCode.Should().Be("FILES_INVALID_CONTENT_TYPE");
        result.Messages.Should().Equal("common_errors_VALIDATION_FAILED");
    }

    [Fact]
    public void Conflict_DefaultsToTkMessage()
    {
        var result = D2Result.Conflict();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
        result.ErrorCode.Should().Be(ErrorCodes.CONFLICT);
        result.Messages.Should().Equal("common_errors_CONFLICT");
    }

    [Fact]
    public void ServiceUnavailable_DefaultsToTkMessage()
    {
        var result = D2Result.ServiceUnavailable();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(ErrorCodes.SERVICE_UNAVAILABLE);
        result.Messages.Should().Equal("common_errors_SERVICE_UNAVAILABLE");
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
        result.Messages.Should().Equal("common_errors_unknown");
    }

    [Fact]
    public void PayloadTooLarge_DefaultsToTkMessage()
    {
        var result = D2Result.PayloadTooLarge();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        result.ErrorCode.Should().Be(ErrorCodes.PAYLOAD_TOO_LARGE);
        result.Messages.Should().Equal("common_errors_PAYLOAD_TOO_LARGE");
    }

    [Fact]
    public void TooManyRequests_DefaultsToRateLimitedErrorCode()
    {
        var result = D2Result.TooManyRequests();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        result.ErrorCode.Should().Be(ErrorCodes.RATE_LIMITED);
        result.Messages.Should().Equal("common_errors_TOO_MANY_REQUESTS");
    }

    [Fact]
    public void TooManyRequests_WithCustomErrorCode_OverridesDefault()
    {
        var result = D2Result.TooManyRequests(errorCode: "OTP_RATE_LIMITED");

        result.ErrorCode.Should().Be("OTP_RATE_LIMITED");
    }

    [Fact]
    public void Cancelled_DefaultsToTkMessage()
    {
        var result = D2Result.Cancelled();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(ErrorCodes.CANCELLED);
        result.Messages.Should().Equal("common_errors_CANCELLED");
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
        result.Messages.Should().Equal("common_errors_SOME_FOUND");
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
        D2Result.Cancelled(traceId: trace_id).TraceId.Should().Be(trace_id);
        D2Result.SomeFound(traceId: trace_id).TraceId.Should().Be(trace_id);
    }
}
