// -----------------------------------------------------------------------
// <copyright file="D2ResultTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit;

using D2.Shared.Result;

/// <summary>
/// Unit tests for the non-generic <see cref="D2Result"/> class.
/// </summary>
public class D2ResultTests
{
    /// <summary>
    /// Tests that the Ok factory method creates a successful result with default values.
    /// </summary>
    [Fact]
    public void Ok_CreatesSuccessResult()
    {
        // Act
        var result = D2Result.Ok();

        // Assert
        Assert.True(result.Success);
        Assert.False(result.Failed);
        Assert.Empty(result.Messages);
        Assert.Empty(result.InputErrors);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.TraceId);
    }

    /// <summary>
    /// Tests that the Ok factory method includes a trace ID when provided.
    /// </summary>
    [Fact]
    public void Ok_WithTraceId_IncludesTraceId()
    {
        // Arrange
        const string trace_id = "trace-123";

        // Act
        var result = D2Result.Ok(trace_id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(trace_id, result.TraceId);
    }

    /// <summary>
    /// Tests that the Fail factory method creates a failure result with messages.
    /// </summary>
    [Fact]
    public void Fail_WithMessages_CreatesFailureResult()
    {
        // Arrange
        List<string> messages = ["Error 1", "Error 2"];

        // Act
        var result = D2Result.Fail(messages);

        // Assert
        Assert.False(result.Success);
        Assert.True(result.Failed);
        Assert.Equal(messages, result.Messages);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Null(result.ErrorCode);
    }

    /// <summary>
    /// Tests that the Fail factory method uses the provided status code.
    /// </summary>
    [Fact]
    public void Fail_WithCustomStatusCode_UsesProvidedStatusCode()
    {
        // Arrange
        const HttpStatusCode status_code = HttpStatusCode.Conflict;

        // Act
        var result = D2Result.Fail(statusCode: status_code);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(status_code, result.StatusCode);
    }

    /// <summary>
    /// Tests that the Fail factory method includes an error code when provided.
    /// </summary>
    [Fact]
    public void Fail_WithErrorCode_IncludesErrorCode()
    {
        // Arrange
        const string error_code = "CUSTOM_ERROR";

        // Act
        var result = D2Result.Fail(errorCode: error_code);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(error_code, result.ErrorCode);
    }

    /// <summary>
    /// Tests that the Fail factory method includes input errors when provided.
    /// </summary>
    [Fact]
    public void Fail_WithInputErrors_IncludesInputErrors()
    {
        // Arrange
        var inputErrors = new List<List<string>>
        {
            new() { "Field1", "Error message 1" },
            new() { "Field2", "Error message 2" },
        };

        // Act
        var result = D2Result.Fail(inputErrors: inputErrors);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(inputErrors, result.InputErrors);
    }

    /// <summary>
    /// Tests that the ValidationFailed factory method creates a validation failure result.
    /// </summary>
    [Fact]
    public void ValidationFailed_CreatesValidationFailureResult()
    {
        // Arrange
        var inputErrors = new List<List<string>>
        {
            new() { "Username", "Username is required" },
            new() { "Email", "Invalid email format" },
        };
        const string trace_id = "trace-456";

        // Act
        var result = D2Result.ValidationFailed(inputErrors: inputErrors, traceId: trace_id);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("common_errors_VALIDATION_FAILED", result.Messages);
        Assert.Equal(inputErrors, result.InputErrors);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(ErrorCodes.VALIDATION_FAILED, result.ErrorCode);
        Assert.Equal(trace_id, result.TraceId);
    }

    /// <summary>
    /// Tests that ValidationFailed accepts an errorCode override for client-side discrimination.
    /// </summary>
    [Fact]
    public void ValidationFailed_AcceptsErrorCodeOverride()
    {
        // Act
        var result = D2Result.ValidationFailed(errorCode: "PHONE_NO_CHANGE");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("PHONE_NO_CHANGE", result.ErrorCode);
        Assert.Contains("common_errors_VALIDATION_FAILED", result.Messages);
    }

    /// <summary>
    /// Tests that TooManyRequests creates a 429 result with the RATE_LIMITED code.
    /// </summary>
    [Fact]
    public void TooManyRequests_CreatesRateLimitedResult()
    {
        // Act
        var result = D2Result.TooManyRequests();

        // Assert
        Assert.False(result.Success);
        Assert.Equal(HttpStatusCode.TooManyRequests, result.StatusCode);
        Assert.Equal(ErrorCodes.RATE_LIMITED, result.ErrorCode);
        Assert.Contains("common_errors_TOO_MANY_REQUESTS", result.Messages);
    }

    /// <summary>
    /// Tests that TooManyRequests accepts an errorCode override (e.g. OTP_RATE_LIMITED).
    /// </summary>
    [Fact]
    public void TooManyRequests_AcceptsErrorCodeOverride()
    {
        // Act
        var result = D2Result.TooManyRequests(errorCode: "OTP_RATE_LIMITED");

        // Assert
        Assert.Equal("OTP_RATE_LIMITED", result.ErrorCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, result.StatusCode);
    }

    /// <summary>
    /// Tests that the constructor defaults to OK status code when success is true.
    /// </summary>
    [Fact]
    public void Constructor_WithSuccessTrue_DefaultsToOkStatusCode()
    {
        // Act
        var result = new D2Result(success: true);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    /// <summary>
    /// Tests that the constructor defaults to BadRequest status code when success is false.
    /// </summary>
    [Fact]
    public void Constructor_WithSuccessFalse_DefaultsToBadRequestStatusCode()
    {
        // Act
        var result = new D2Result(success: false);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    /// <summary>
    /// Tests that the ErrorCodes class contains the expected common error codes.
    /// </summary>
    /// <param name="errorCode">The error code to verify exists.</param>
    [Theory]
    [InlineData("NOT_FOUND")]
    [InlineData("FORBIDDEN")]
    [InlineData("UNAUTHORIZED")]
    [InlineData("VALIDATION_FAILED")]
    [InlineData("CONFLICT")]
    public void CommonErrors_ContainsExpectedErrorCode(string errorCode)
    {
        // Assert
        var field = typeof(ErrorCodes).GetField(errorCode);
        Assert.NotNull(field);
        Assert.Equal(errorCode, field.GetValue(null));
    }
}
