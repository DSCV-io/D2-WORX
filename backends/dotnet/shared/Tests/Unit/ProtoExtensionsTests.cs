// -----------------------------------------------------------------------
// <copyright file="ProtoExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit;

using D2.Services.Protos.Common.V1;
using D2.Shared.Result;
using D2.Shared.Result.Extensions;

/// <summary>
/// Unit tests for <see cref="ProtoExtensions"/>.
/// </summary>
public class ProtoExtensionsTests
{
    /// <summary>
    /// Tests that ToProto converts a successful D2Result correctly.
    /// </summary>
    [Fact]
    public void ToProto_SuccessResult_ConvertsCorrectly()
    {
        // Arrange
        var result = D2Result.Ok(traceId: "trace-123");

        // Act
        var proto = result.ToProto();

        // Assert
        Assert.True(proto.Success);
        Assert.Equal((int)HttpStatusCode.OK, proto.StatusCode);
        Assert.Equal("trace-123", proto.TraceId);
        Assert.False(proto.HasErrorCode);
        Assert.Empty(proto.Messages);
        Assert.Empty(proto.InputErrors);
    }

    /// <summary>
    /// Tests that ToProto converts a failed D2Result with messages correctly.
    /// </summary>
    [Fact]
    public void ToProto_FailedResult_ConvertsCorrectly()
    {
        // Arrange
        var result = D2Result.Fail(
            messages: ["Error 1", "Error 2"],
            statusCode: HttpStatusCode.BadRequest,
            errorCode: "VALIDATION_FAILED",
            traceId: "trace-456");

        // Act
        var proto = result.ToProto();

        // Assert
        Assert.False(proto.Success);
        Assert.Equal((int)HttpStatusCode.BadRequest, proto.StatusCode);
        Assert.Equal("VALIDATION_FAILED", proto.ErrorCode);
        Assert.Equal("trace-456", proto.TraceId);
        Assert.Equal(2, proto.Messages.Count);
        Assert.Contains("Error 1", proto.Messages);
        Assert.Contains("Error 2", proto.Messages);
    }

    /// <summary>
    /// Tests that ToProto converts input errors correctly.
    /// </summary>
    [Fact]
    public void ToProto_WithInputErrors_ConvertsCorrectly()
    {
        // Arrange
        var inputErrors = new List<List<string>>
        {
            new() { "Field1", "Error 1", "Error 2" },
            new() { "Field2", "Error 3" },
        };
        var result = D2Result.Fail(inputErrors: inputErrors);

        // Act
        var proto = result.ToProto();

        // Assert
        Assert.Equal(2, proto.InputErrors.Count);

        var field1Error = proto.InputErrors.First(e => e.Field == "Field1");
        Assert.Equal(2, field1Error.Errors.Count);
        Assert.Contains("Error 1", field1Error.Errors);
        Assert.Contains("Error 2", field1Error.Errors);

        var field2Error = proto.InputErrors.First(e => e.Field == "Field2");
        Assert.Single(field2Error.Errors);
        Assert.Contains("Error 3", field2Error.Errors);
    }

    /// <summary>
    /// Tests that ToProto skips empty input error entries.
    /// </summary>
    [Fact]
    public void ToProto_WithEmptyInputErrorEntry_SkipsIt()
    {
        // Arrange
        var inputErrors = new List<List<string>>
        {
            new(),
            new() { "Field1", "Error 1" },
        };
        var result = D2Result.Fail(inputErrors: inputErrors);

        // Act
        var proto = result.ToProto();

        // Assert
        Assert.Single(proto.InputErrors);
        Assert.Equal("Field1", proto.InputErrors[0].Field);
    }

    /// <summary>
    /// Tests that ToProto handles null error code and trace ID.
    /// </summary>
    [Fact]
    public void ToProto_NullErrorCodeAndTraceId_FieldsNotSet()
    {
        // Arrange
        var result = D2Result.Fail();

        // Act
        var proto = result.ToProto();

        // Assert
        Assert.False(proto.HasErrorCode);
        Assert.False(proto.HasTraceId);
    }

    /// <summary>
    /// Tests that ToD2Result converts a successful proto correctly.
    /// </summary>
    [Fact]
    public void ToD2Result_SuccessProto_ConvertsCorrectly()
    {
        // Arrange
        var proto = new D2ResultProto
        {
            Success = true,
            StatusCode = (int)HttpStatusCode.OK,
            TraceId = "trace-789",
        };

        // Act
        var result = proto.ToD2Result("test data");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("trace-789", result.TraceId);
        Assert.Equal("test data", result.Data);
        Assert.Null(result.ErrorCode);
    }

    /// <summary>
    /// Tests that ToD2Result converts a failed proto with all fields correctly.
    /// </summary>
    [Fact]
    public void ToD2Result_FailedProto_ConvertsCorrectly()
    {
        // Arrange
        var proto = new D2ResultProto
        {
            Success = false,
            StatusCode = (int)HttpStatusCode.NotFound,
            ErrorCode = "NOT_FOUND",
            TraceId = "trace-abc",
        };
        proto.Messages.AddRange(["Item not found", "Please check ID"]);

        // Act
        var result = proto.ToD2Result<string>();

        // Assert
        Assert.False(result.Success);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
        Assert.Equal("trace-abc", result.TraceId);
        Assert.Equal(2, result.Messages.Count);
        Assert.Contains("Item not found", result.Messages);
        Assert.Null(result.Data);
    }

    /// <summary>
    /// Tests that ToD2Result converts input errors from proto correctly.
    /// </summary>
    [Fact]
    public void ToD2Result_WithInputErrors_ConvertsCorrectly()
    {
        // Arrange
        var proto = new D2ResultProto { Success = false };
        proto.InputErrors.Add(new InputErrorProto
        {
            Field = "Username",
            Errors = { "Required", "Too short" },
        });
        proto.InputErrors.Add(new InputErrorProto
        {
            Field = "Email",
            Errors = { "Invalid format" },
        });

        // Act
        var result = proto.ToD2Result<string>();

        // Assert
        Assert.Equal(2, result.InputErrors.Count);

        var usernameError = result.InputErrors.First(e => e[0] == "Username");
        Assert.Equal(3, usernameError.Count);
        Assert.Equal("Username", usernameError[0]);
        Assert.Contains("Required", usernameError);
        Assert.Contains("Too short", usernameError);

        var emailError = result.InputErrors.First(e => e[0] == "Email");
        Assert.Equal(2, emailError.Count);
        Assert.Contains("Invalid format", emailError);
    }

    /// <summary>
    /// Tests that ToD2Result handles empty error code and trace ID in proto.
    /// </summary>
    [Fact]
    public void ToD2Result_EmptyErrorCodeAndTraceId_ConvertsToNull()
    {
        // Arrange
        var proto = new D2ResultProto
        {
            Success = true,
            StatusCode = (int)HttpStatusCode.OK,
            ErrorCode = string.Empty,
            TraceId = string.Empty,
        };

        // Act
        var result = proto.ToD2Result<string>();

        // Assert
        Assert.Null(result.ErrorCode);
        Assert.Null(result.TraceId);
    }

    /// <summary>
    /// Tests round-trip conversion preserves all data.
    /// </summary>
    [Fact]
    public void RoundTrip_D2ResultToProtoAndBack_PreservesData()
    {
        // Arrange
        var inputErrors = new List<List<string>>
        {
            new() { "Field1", "Error A", "Error B" },
            new() { "Field2", "Error C" },
        };
        var original = D2Result<string>.Fail(
            messages: ["Message 1", "Message 2"],
            statusCode: HttpStatusCode.UnprocessableEntity,
            errorCode: "CUSTOM_ERROR",
            inputErrors: inputErrors,
            traceId: "trace-roundtrip");

        // Act
        var proto = original.ToProto();
        var restored = proto.ToD2Result<string>();

        // Assert
        Assert.Equal(original.Success, restored.Success);
        Assert.Equal(original.StatusCode, restored.StatusCode);
        Assert.Equal(original.ErrorCode, restored.ErrorCode);
        Assert.Equal(original.TraceId, restored.TraceId);
        Assert.Equal(original.Messages, restored.Messages);
        Assert.Equal(original.InputErrors.Count, restored.InputErrors.Count);

        for (var i = 0; i < original.InputErrors.Count; i++)
        {
            Assert.Equal(original.InputErrors[i], restored.InputErrors[i]);
        }
    }

    /// <summary>
    /// Tests that generic D2Result ToProto works correctly.
    /// </summary>
    [Fact]
    public void ToProto_GenericD2Result_ConvertsCorrectly()
    {
        // Arrange
        var result = D2Result<int>.Ok(42, traceId: "trace-generic");

        // Act
        var proto = result.ToProto();

        // Assert
        Assert.True(proto.Success);
        Assert.Equal((int)HttpStatusCode.OK, proto.StatusCode);
        Assert.Equal("trace-generic", proto.TraceId);
    }
}
