// -----------------------------------------------------------------------
// <copyright file="D2ResultGenericTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Result;

using System.Net;
using AwesomeAssertions;
using D2.Shared.Result;
using Xunit;

public class D2ResultGenericTests
{
    // ----------------------------------------------------------------------
    // Constructor + Data property
    // ----------------------------------------------------------------------

    [Fact]
    public void Ctor_WithData_PreservesData()
    {
        var data = new Payload("hi");

        var result = new D2Result<Payload>(success: true, data: data);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(data);
        result.Data!.Name.Should().Be("hi");
    }

    [Fact]
    public void Ctor_WithoutData_DataIsDefaultForReferenceType()
    {
        var result = new D2Result<Payload>(success: true);

        result.Data.Should().BeNull();
    }

    [Fact]
    public void Ctor_WithoutData_DataIsDefaultForValueType()
    {
        var result = new D2Result<int>(success: true);

        result.Data.Should().Be(0);
    }

    [Fact]
    public void Ctor_NullableValueType_DataDefaultsToNull()
    {
        var result = new D2Result<int?>(success: true);

        result.Data.Should().BeNull();
    }

    [Fact]
    public void Ctor_PropagatesAllBaseFields()
    {
        var result = new D2Result<Payload>(
            success: false,
            data: null,
            messages: ["m1"],
            inputErrors: [["f", "e"]],
            statusCode: HttpStatusCode.Conflict,
            errorCode: ErrorCodes.CONFLICT,
            traceId: "t1");

        result.Success.Should().BeFalse();
        result.Data.Should().BeNull();
        result.Messages.Should().Equal("m1");
        result.InputErrors.Should().HaveCount(1);
        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
        result.ErrorCode.Should().Be(ErrorCodes.CONFLICT);
        result.TraceId.Should().Be("t1");
    }

    // ----------------------------------------------------------------------
    // CheckSuccess / CheckFailure
    // ----------------------------------------------------------------------

    [Fact]
    public void CheckSuccess_OnOk_ReturnsTrueAndPopulatesData()
    {
        var data = new Payload("p");
        var result = D2Result<Payload>.Ok(data);

        var success = result.CheckSuccess(out var unwrapped);

        success.Should().BeTrue();
        unwrapped.Should().Be(data);
    }

    [Fact]
    public void CheckSuccess_OnFailure_ReturnsFalseAndDataIsDefault()
    {
        var result = D2Result<Payload>.NotFound();

        var success = result.CheckSuccess(out var unwrapped);

        success.Should().BeFalse();
        unwrapped.Should().BeNull();
    }

    [Fact]
    public void CheckFailure_OnOk_ReturnsFalseButDataStillExposed()
    {
        var data = new Payload("p");
        var result = D2Result<Payload>.Ok(data);

        var failed = result.CheckFailure(out var unwrapped);

        failed.Should().BeFalse();
        unwrapped.Should().Be(data);
    }

    [Fact]
    public void CheckFailure_OnFailure_ReturnsTrueAndExposesDataIfPresent()
    {
        // Adversarial: SomeFound is a failure that DOES carry data.
        // CheckFailure must expose it — that's the partial-success consumer pattern.
        var data = new Payload("partial");
        var result = D2Result<Payload>.SomeFound(data);

        var failed = result.CheckFailure(out var unwrapped);

        failed.Should().BeTrue();
        unwrapped.Should().Be(data);
    }

    // ----------------------------------------------------------------------
    // Generic Ok / Created
    // ----------------------------------------------------------------------

    [Fact]
    public void Ok_WithData_CarriesData()
    {
        var data = new Payload("x");

        var result = D2Result<Payload>.Ok(data);

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Data.Should().Be(data);
    }

    [Fact]
    public void Ok_WithoutData_DataIsDefault()
    {
        var result = D2Result<Payload>.Ok();

        result.Success.Should().BeTrue();
        result.Data.Should().BeNull();
    }

    [Fact]
    public void Ok_WithMessagesAndTraceId_CarriesBoth()
    {
        var data = new Payload("y");
        List<string> messages = ["ok"];

        var result = D2Result<Payload>.Ok(data, messages, traceId: "t");

        result.Data.Should().Be(data);
        result.Messages.Should().Equal("ok");
        result.TraceId.Should().Be("t");
    }

    [Fact]
    public void Created_WithData_CarriesDataAndStatusCreated()
    {
        var data = new Payload("new");

        var result = D2Result<Payload>.Created(data);

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Data.Should().Be(data);
    }

    // ----------------------------------------------------------------------
    // BubbleFail / Bubble
    // ----------------------------------------------------------------------

    [Fact]
    public void BubbleFail_CopiesAllFieldsFromUpstreamFailure()
    {
        var upstream = D2Result.NotFound(messages: ["custom"], traceId: "t-upstream");

        var bubbled = D2Result<Payload>.BubbleFail(upstream);

        bubbled.Success.Should().BeFalse();
        bubbled.Data.Should().BeNull();
        bubbled.Messages.Should().Equal("custom");
        bubbled.StatusCode.Should().Be(HttpStatusCode.NotFound);
        bubbled.ErrorCode.Should().Be(ErrorCodes.NOT_FOUND);
        bubbled.TraceId.Should().Be("t-upstream");
    }

    [Fact]
    public void BubbleFail_CopiesInputErrors()
    {
        var upstream = D2Result.ValidationFailed(inputErrors: [["email", "Required."]]);

        var bubbled = D2Result<Payload>.BubbleFail(upstream);

        bubbled.InputErrors.Should().HaveCount(1);
        bubbled.InputErrors[0].Should().Equal("email", "Required.");
    }

    [Fact]
    public void BubbleFail_AcrossDifferentGenericArgs_PreservesMetadata()
    {
        // Adversarial: BubbleFail goes from ANY upstream type to ANY downstream type.
        // The generic argument of upstream doesn't constrain the bubble target.
        var upstream = D2Result<int>.Conflict(messages: ["m"]);

        var bubbled = D2Result<Payload>.BubbleFail(upstream);

        bubbled.Success.Should().BeFalse();
        bubbled.Data.Should().BeNull();
        bubbled.ErrorCode.Should().Be(ErrorCodes.CONFLICT);
        bubbled.Messages.Should().Equal("m");
    }

    [Fact]
    public void Bubble_FromSuccess_PreservesSuccessAndAttachesData()
    {
        var upstream = D2Result.Ok(traceId: "t");
        var data = new Payload("attached");

        var result = D2Result<Payload>.Bubble(upstream, data);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(data);
        result.TraceId.Should().Be("t");
    }

    [Fact]
    public void Bubble_FromFailure_PreservesFailureAndAttachesData()
    {
        // Adversarial: Bubble carries data even when the upstream failed.
        // Useful for SomeFound-flavored partial-success propagation.
        var upstream = D2Result.SomeFound();
        var partial = new Payload("partial");

        var result = D2Result<Payload>.Bubble(upstream, partial);

        result.Success.Should().BeFalse();
        result.Data.Should().Be(partial);
        result.ErrorCode.Should().Be(ErrorCodes.SOME_FOUND);
        result.StatusCode.Should().Be(HttpStatusCode.PartialContent);
    }

    [Fact]
    public void Bubble_WithoutData_DataIsDefault()
    {
        var upstream = D2Result.Ok();

        var result = D2Result<Payload>.Bubble(upstream);

        result.Success.Should().BeTrue();
        result.Data.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // Generic semantic factories — each one shapes the result correctly + Data is default
    // ----------------------------------------------------------------------

    [Fact]
    public void NotFound_Generic_ProducesNotFoundFailureWithDefaultData()
    {
        var result = D2Result<Payload>.NotFound();

        result.Success.Should().BeFalse();
        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.NOT_FOUND);
        result.Messages.Should().Equal("common_errors_NOT_FOUND");
    }

    [Fact]
    public void Forbidden_Generic_ProducesForbiddenFailureWithDefaultData()
    {
        var result = D2Result<Payload>.Forbidden();

        result.Success.Should().BeFalse();
        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.ErrorCode.Should().Be(ErrorCodes.FORBIDDEN);
    }

    [Fact]
    public void Unauthorized_Generic_ProducesUnauthorizedFailureWithDefaultData()
    {
        var result = D2Result<Payload>.Unauthorized();

        result.Success.Should().BeFalse();
        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.ErrorCode.Should().Be(ErrorCodes.UNAUTHORIZED);
    }

    [Fact]
    public void ValidationFailed_Generic_CarriesInputErrors()
    {
        var result = D2Result<Payload>.ValidationFailed(inputErrors: [["email", "Required."]]);

        result.Success.Should().BeFalse();
        result.Data.Should().BeNull();
        result.ErrorCode.Should().Be(ErrorCodes.VALIDATION_FAILED);
        result.InputErrors.Should().HaveCount(1);
    }

    [Fact]
    public void ValidationFailed_Generic_ErrorCodeOverrideWorks()
    {
        var result = D2Result<Payload>.ValidationFailed(errorCode: "CUSTOM");

        result.ErrorCode.Should().Be("CUSTOM");
    }

    [Fact]
    public void Conflict_Generic_ProducesConflictFailureWithDefaultData()
    {
        var result = D2Result<Payload>.Conflict();

        result.Success.Should().BeFalse();
        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
        result.ErrorCode.Should().Be(ErrorCodes.CONFLICT);
    }

    [Fact]
    public void ServiceUnavailable_Generic_DefaultsToSemanticErrorCode()
    {
        var result = D2Result<Payload>.ServiceUnavailable();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(ErrorCodes.SERVICE_UNAVAILABLE);
        result.Messages.Should().Equal("common_errors_SERVICE_UNAVAILABLE");
    }

    [Fact]
    public void ServiceUnavailable_Generic_ErrorCodeOverrideWorks()
    {
        var result = D2Result<Payload>.ServiceUnavailable(errorCode: "DOMAIN");

        result.ErrorCode.Should().Be("DOMAIN");
    }

    [Fact]
    public void UnhandledException_Generic_Produces500()
    {
        var result = D2Result<Payload>.UnhandledException();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorCode.Should().Be(ErrorCodes.UNHANDLED_EXCEPTION);
    }

    [Fact]
    public void PayloadTooLarge_Generic_Produces413()
    {
        var result = D2Result<Payload>.PayloadTooLarge();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        result.ErrorCode.Should().Be(ErrorCodes.PAYLOAD_TOO_LARGE);
    }

    [Fact]
    public void TooManyRequests_Generic_DefaultsToRateLimited()
    {
        var result = D2Result<Payload>.TooManyRequests();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        result.ErrorCode.Should().Be(ErrorCodes.RATE_LIMITED);
    }

    [Fact]
    public void Cancelled_Generic_Produces400()
    {
        var result = D2Result<Payload>.Cancelled();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(ErrorCodes.CANCELLED);
    }

    [Fact]
    public void SomeFound_Generic_CarriesPartialDataAndIsFailure()
    {
        var partial = new Payload("part");

        var result = D2Result<Payload>.SomeFound(partial);

        result.Success.Should().BeFalse();
        result.Data.Should().Be(partial);
        result.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        result.ErrorCode.Should().Be(ErrorCodes.SOME_FOUND);
    }

    [Fact]
    public void SomeFound_Generic_WithoutData_DataIsDefault()
    {
        var result = D2Result<Payload>.SomeFound();

        result.Success.Should().BeFalse();
        result.Data.Should().BeNull();
    }

    [Fact]
    public void Fail_Generic_CarriesAllFieldsAndDataIsDefault()
    {
        var result = D2Result<Payload>.Fail(
            messages: ["err"],
            statusCode: HttpStatusCode.Forbidden,
            inputErrors: [["f", "e"]],
            errorCode: "X",
            traceId: "t");

        result.Success.Should().BeFalse();
        result.Data.Should().BeNull();
        result.Messages.Should().Equal("err");
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.ErrorCode.Should().Be("X");
        result.TraceId.Should().Be("t");
    }

    private sealed record Payload(string Name);
}
