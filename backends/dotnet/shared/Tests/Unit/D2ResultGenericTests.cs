// -----------------------------------------------------------------------
// <copyright file="D2ResultGenericTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit;

using D2.Shared.Result;

/// <summary>
/// Unit tests for the generic <see cref="D2Result{TData}"/> class.
/// </summary>
public class D2ResultGenericTests
{
    #region Factory Method Tests

    /// <summary>
    /// Tests that the Ok factory method creates a successful result with data.
    /// </summary>
    [Fact]
    public void Ok_WithData_CreatesSuccessResult()
    {
        // Arrange
        const string data = "test data";

        // Act
        var result = D2Result<string>.Ok(data);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.Failed);
        Assert.Equal(data, result.Data);
        Assert.Empty(result.Messages);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Null(result.ErrorCode);
    }

    /// <summary>
    /// Tests that the Ok factory method creates a successful result without data.
    /// </summary>
    [Fact]
    public void Ok_WithoutData_CreatesSuccessResultWithDefaultData()
    {
        // Act
        var result = D2Result<string>.Ok();

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Data);
    }

    /// <summary>
    /// Tests that the Ok factory method includes messages when provided.
    /// </summary>
    [Fact]
    public void Ok_WithMessages_IncludesMessages()
    {
        // Arrange
        const int data = 42;
        List<string> messages = ["Success message"];

        // Act
        var result = D2Result<int>.Ok(data, messages);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(data, result.Data);
        Assert.Equal(messages, result.Messages);
    }

    /// <summary>
    /// Tests that the Ok factory method includes a trace ID when provided.
    /// </summary>
    [Fact]
    public void Ok_WithTraceId_IncludesTraceId()
    {
        // Arrange
        const bool data = true;
        const string trace_id = "trace-789";

        // Act
        var result = D2Result<bool>.Ok(data, traceId: trace_id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(trace_id, result.TraceId);
    }

    /// <summary>
    /// Tests that the Created factory method creates a successful result with Created status.
    /// </summary>
    [Fact]
    public void Created_CreatesSuccessResultWithCreatedStatus()
    {
        // Arrange
        var data = new { Id = 123, Name = "Test" };

        // Act
        var result = D2Result<object>.Created(data);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(data, result.Data);
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
    }

    /// <summary>
    /// Tests that the Fail factory method creates a failure result with messages.
    /// </summary>
    [Fact]
    public void Fail_CreatesFailureResult()
    {
        // Arrange
        List<string> messages = ["Operation failed"];

        // Act
        var result = D2Result<string>.Fail(messages);

        // Assert
        Assert.False(result.Success);
        Assert.True(result.Failed);
        Assert.Null(result.Data);
        Assert.Equal(messages, result.Messages);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    /// <summary>
    /// Tests that the Fail factory method uses the provided status code.
    /// </summary>
    [Fact]
    public void Fail_WithCustomStatusCode_UsesProvidedStatusCode()
    {
        // Arrange
        const HttpStatusCode status_code = HttpStatusCode.InternalServerError;

        // Act
        var result = D2Result<int>.Fail(statusCode: status_code);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(status_code, result.StatusCode);
    }

    /// <summary>
    /// Tests that the NotFound factory method creates a NotFound result.
    /// </summary>
    [Fact]
    public void NotFound_CreatesNotFoundResult()
    {
        // Arrange
        List<string> messages = ["Resource not found"];

        // Act
        var result = D2Result<string>.NotFound(messages);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(messages, result.Messages);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal(ErrorCodes.NOT_FOUND, result.ErrorCode);
    }

    /// <summary>
    /// Tests that the NotFound factory method uses the default message when none are provided.
    /// </summary>
    [Fact]
    public void NotFound_WithoutMessages_UsesDefaultMessage()
    {
        // Act
        var result = D2Result<int>.NotFound();

        // Assert
        Assert.False(result.Success);
        Assert.Contains("common_errors_NOT_FOUND", result.Messages);
    }

    /// <summary>
    /// Tests that the Forbidden factory method creates a Forbidden result.
    /// </summary>
    [Fact]
    public void Forbidden_CreatesForbiddenResult()
    {
        // Arrange
        List<string> messages = ["Access denied"];

        // Act
        var result = D2Result<string>.Forbidden(messages);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Equal(ErrorCodes.FORBIDDEN, result.ErrorCode);
    }

    /// <summary>
    /// Tests that the Forbidden factory method uses the default message when none are provided.
    /// </summary>
    [Fact]
    public void Forbidden_WithoutMessages_UsesDefaultMessage()
    {
        // Act
        var result = D2Result<string>.Forbidden();

        // Assert
        Assert.Contains("common_errors_FORBIDDEN", result.Messages);
    }

    /// <summary>
    /// Tests that the Unauthorized factory method creates an Unauthorized result.
    /// </summary>
    [Fact]
    public void Unauthorized_CreatesUnauthorizedResult()
    {
        // Act
        var result = D2Result<string>.Unauthorized();

        // Assert
        Assert.False(result.Success);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        Assert.Equal(ErrorCodes.UNAUTHORIZED, result.ErrorCode);
        Assert.Contains("common_errors_UNAUTHORIZED", result.Messages);
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
            new() { "Email", "Invalid email format" },
            new() { "Password", "Password too weak" },
        };

        // Act
        var result = D2Result<object>.ValidationFailed(inputErrors: inputErrors);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(ErrorCodes.VALIDATION_FAILED, result.ErrorCode);
        Assert.Equal(inputErrors, result.InputErrors);
        Assert.Contains("common_errors_VALIDATION_FAILED", result.Messages);
    }

    /// <summary>
    /// Tests that the ValidationFailed factory method uses provided messages when given.
    /// </summary>
    [Fact]
    public void ValidationFailed_WithCustomMessages_UsesProvidedMessages()
    {
        // Arrange
        List<string> messages = ["Custom validation message"];
        var inputErrors = new List<List<string>>
        {
            new() { "Field", "Error" },
        };

        // Act
        var result = D2Result<string>.ValidationFailed(messages, inputErrors);

        // Assert
        Assert.Equal(messages, result.Messages);
    }

    /// <summary>
    /// Tests that ValidationFailed accepts an errorCode override (generic variant).
    /// </summary>
    [Fact]
    public void ValidationFailed_AcceptsErrorCodeOverride()
    {
        // Act
        var result = D2Result<string>.ValidationFailed(errorCode: "FILES_INVALID_CONTENT_TYPE");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("FILES_INVALID_CONTENT_TYPE", result.ErrorCode);
    }

    /// <summary>
    /// Tests that TooManyRequests creates a 429 result with the RATE_LIMITED code.
    /// </summary>
    [Fact]
    public void TooManyRequests_CreatesRateLimitedResult()
    {
        // Act
        var result = D2Result<string>.TooManyRequests();

        // Assert
        Assert.False(result.Success);
        Assert.Equal(HttpStatusCode.TooManyRequests, result.StatusCode);
        Assert.Equal(ErrorCodes.RATE_LIMITED, result.ErrorCode);
        Assert.Contains("common_errors_TOO_MANY_REQUESTS", result.Messages);
    }

    /// <summary>
    /// Tests that TooManyRequests accepts an errorCode override.
    /// </summary>
    [Fact]
    public void TooManyRequests_AcceptsErrorCodeOverride()
    {
        // Act
        var result = D2Result<string>.TooManyRequests(errorCode: "OTP_RATE_LIMITED");

        // Assert
        Assert.Equal("OTP_RATE_LIMITED", result.ErrorCode);
    }

    /// <summary>
    /// Tests that the Conflict factory method creates a Conflict result.
    /// </summary>
    [Fact]
    public void Conflict_CreatesConflictResult()
    {
        // Arrange
        List<string> messages = ["Resource already exists"];

        // Act
        var result = D2Result<string>.Conflict(messages);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal(ErrorCodes.CONFLICT, result.ErrorCode);
    }

    /// <summary>
    /// Tests that the Conflict factory method uses the default message when none are provided.
    /// </summary>
    [Fact]
    public void Conflict_WithoutMessages_UsesDefaultMessage()
    {
        // Act
        var result = D2Result<int>.Conflict();

        // Assert
        Assert.Contains("common_errors_CONFLICT", result.Messages);
    }

    /// <summary>
    /// Tests that the UnhandledException factory method creates an InternalServerError result.
    /// </summary>
    [Fact]
    public void UnhandledException_CreatesInternalServerErrorResult()
    {
        // Arrange
        const string trace_id = "trace-error-123";

        // Act
        var result = D2Result<string>.UnhandledException(traceId: trace_id);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Equal("UNHANDLED_EXCEPTION", result.ErrorCode);
        Assert.Equal(trace_id, result.TraceId);
    }

    #endregion

    #region CheckSuccess/CheckFailure Tests

    /// <summary>
    /// Tests that CheckSuccess returns true and outputs data when the result is successful.
    /// </summary>
    [Fact]
    public void CheckSuccess_WhenSuccess_ReturnsTrueWithData()
    {
        // Arrange
        var data = "success data";
        var result = D2Result<string>.Ok(data);

        // Act
        var success = result.CheckSuccess(out var outData);

        // Assert
        Assert.True(success);
        Assert.Equal(data, outData);
    }

    /// <summary>
    /// Tests that CheckSuccess returns false and outputs null when the result is a failure.
    /// </summary>
    [Fact]
    public void CheckSuccess_WhenFailure_ReturnsFalseWithNullData()
    {
        // Arrange
        var result = D2Result<string>.Fail(["Error"]);

        // Act
        var success = result.CheckSuccess(out var outData);

        // Assert
        Assert.False(success);
        Assert.Null(outData);
    }

    /// <summary>
    /// Tests that CheckFailure returns true and outputs null when the result is a failure.
    /// </summary>
    [Fact]
    public void CheckFailure_WhenFailure_ReturnsTrueWithNullData()
    {
        // Arrange
        var result = D2Result<int>.Fail(["Error"]);

        // Act
        var failed = result.CheckFailure(out var outData);

        // Assert
        Assert.True(failed);
        Assert.Equal(0, outData);
    }

    /// <summary>
    /// Tests that CheckFailure returns false and outputs data when the result is successful.
    /// </summary>
    [Fact]
    public void CheckFailure_WhenSuccess_ReturnsFalseWithData()
    {
        // Arrange
        var data = 42;
        var result = D2Result<int>.Ok(data);

        // Act
        var failed = result.CheckFailure(out var outData);

        // Assert
        Assert.False(failed);
        Assert.Equal(data, outData);
    }

    #endregion

    #region BubbleFail Tests

    /// <summary>
    /// Tests that BubbleFail correctly preserves all error details from the original result.
    /// </summary>
    [Fact]
    public void BubbleFail_PreservesAllErrorDetails()
    {
        // Arrange
        List<string> originalMessages = ["Original error"];
        var originalInputErrors = new List<List<string>>
        {
            new() { "Field", "Validation error" },
        };
        const HttpStatusCode original_status_code = HttpStatusCode.BadRequest;
        const string original_error_code = "ORIGINAL_ERROR";
        const string original_trace_id = "trace-bubble-123";

        var original = new D2Result(
            false,
            originalMessages,
            originalInputErrors,
            original_status_code,
            original_error_code,
            original_trace_id);

        // Act
        var bubbled = D2Result<string>.BubbleFail(original);

        // Assert
        Assert.False(bubbled.Success);
        Assert.Null(bubbled.Data);
        Assert.Equal(originalMessages, bubbled.Messages);
        Assert.Equal(originalInputErrors, bubbled.InputErrors);
        Assert.Equal(original_status_code, bubbled.StatusCode);
        Assert.Equal(original_error_code, bubbled.ErrorCode);
        Assert.Equal(original_trace_id, bubbled.TraceId);
    }

    /// <summary>
    /// Tests that BubbleFail preserves validation errors from the original result.
    /// </summary>
    [Fact]
    public void BubbleFail_WithValidationFailure_PreservesValidationErrors()
    {
        // Arrange
        var inputErrors = new List<List<string>>
        {
            new() { "Email", "Required" },
            new() { "Password", "Too short" },
        };
        var original = D2Result.ValidationFailed(inputErrors: inputErrors, traceId: "trace-123");

        // Act
        var bubbled = D2Result<object>.BubbleFail(original);

        // Assert
        Assert.False(bubbled.Success);
        Assert.Equal(ErrorCodes.VALIDATION_FAILED, bubbled.ErrorCode);
        Assert.Equal(inputErrors, bubbled.InputErrors);
    }

    #endregion

    #region Complex Type Tests

    /// <summary>
    /// Tests that the Ok factory method preserves complex data types.
    /// </summary>
    [Fact]
    public void Ok_WithComplexType_PreservesComplexData()
    {
        // Arrange
        var data = new TestDto
        {
            Id = 123,
            Name = "Test",
            Values = [1, 2, 3],
        };

        // Act
        var result = D2Result<TestDto>.Ok(data);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(data.Id, result.Data?.Id);
        Assert.Equal(data.Name, result.Data?.Name);
        Assert.Equal(data.Values, result.Data?.Values);
    }

    /// <summary>
    /// Tests that CheckSuccess works correctly with complex data types.
    /// </summary>
    [Fact]
    public void CheckSuccess_WithComplexType_ReturnsComplexData()
    {
        // Arrange
        var data = new TestDto { Id = 456, Name = "Complex" };
        var result = D2Result<TestDto>.Ok(data);

        // Act
        var success = result.CheckSuccess(out var outData);

        // Assert
        Assert.True(success);
        Assert.NotNull(outData);
        Assert.Equal(data.Id, outData.Id);
        Assert.Equal(data.Name, outData.Name);
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// A test data transfer object (DTO) for complex type testing.
    /// </summary>
    private sealed class TestDto
    {
        /// <summary>
        /// Gets the identifier.
        /// </summary>
        public int Id { get; init; }

        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the list of values.
        /// </summary>
        public List<int> Values { get; init; } = [];
    }

    #endregion
}
