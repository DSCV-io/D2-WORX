// -----------------------------------------------------------------------
// <copyright file="D2Result.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Result;

using System.Net;

/// <summary>
/// Represents the result of an operation, including success status, messages, errors, and related
/// metadata.
/// </summary>
/// <remarks>
/// Default messages in failure factories are TK translation key strings (e.g.
/// "common_errors_NOT_FOUND") rather than English prose. The translation middleware
/// resolves these keys to locale-appropriate text before they reach the client.
/// Keys are hardcoded here instead of referencing <c>TK</c> to keep
/// <c>D2.Shared.Result</c> free of an <c>I18n</c> dependency.
/// </remarks>
public class D2Result
{
    /// <summary>
    /// Initializes a new instance of the <see cref="D2Result"/> class.
    /// </summary>
    ///
    /// <param name="success">
    /// Indicates whether the operation was successful. Required.
    /// </param>
    /// <param name="messages">
    /// A list of messages related to the operation. Optional.
    /// </param>
    /// <param name="inputErrors">
    /// A two-dimensional list representing input errors, where each inner list contains the name
    /// of the field and each proceeding string is an error message related to that field. Optional.
    /// </param>
    /// <param name="statusCode">
    /// The <see cref="HttpStatusCode"/> representing the outcome of the operation. Optional.
    /// </param>
    /// <param name="errorCode">
    /// A standardized error code representing a known failure condition. Optional.
    /// </param>
    /// <param name="traceId">
    /// The trace identifier to correlate logs and diagnostics for the operation. Optional.
    /// </param>
    public D2Result(
        bool success,
        List<string>? messages = null,
        List<List<string>>? inputErrors = null,
        HttpStatusCode? statusCode = null,
        string? errorCode = null,
        string? traceId = null)
    {
        Success = success;
        Messages = messages ?? [];
        InputErrors = inputErrors ?? [];
        StatusCode = statusCode ?? (success ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
        ErrorCode = errorCode;
        TraceId = traceId;
    }

    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool Failed => !Success;

    /// <summary>
    /// Gets a list of messages related to the operation.
    /// </summary>
    public List<string> Messages { get; }

    /// <summary>
    /// Gets a two-dimensional list representing input errors, where each inner list contains the name
    /// of the field and each proceeding string is an error message related to that field.
    /// </summary>
    /// <remarks>
    /// This structure allows for multiple errors to be associated with a single input field,
    /// allowing clients to easily identify and display all relevant validation issues for each
    /// provided field.
    /// </remarks>
    public List<List<string>> InputErrors { get; }

    /// <summary>
    /// Gets the <see cref="HttpStatusCode"/> representing the outcome of the operation.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Gets a standardized error code representing a known failure condition, if applicable.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Gets the trace identifier to correlate logs and diagnostics for the operation, if available.
    /// </summary>
    public string? TraceId { get; }

    #region Functionality

    /// <summary>
    /// Factory method to create a successful <see cref="D2Result"/> instance.
    /// </summary>
    ///
    /// <param name="traceId">
    /// The trace identifier to correlate logs and diagnostics for the operation. Optional.
    /// </param>
    ///
    /// <returns>
    /// A new successful <see cref="D2Result"/> instance.
    /// </returns>
    public static D2Result Ok(string? traceId = null) => new(true, traceId: traceId);

    /// <summary>
    /// Factory method to create a failed <see cref="D2Result"/> instance.
    /// </summary>
    ///
    /// <param name="messages">
    /// A list of messages related to the failure. Optional.
    /// </param>
    /// <param name="statusCode">
    /// The <see cref="HttpStatusCode"/> representing the outcome of the operation. Optional.
    /// </param>
    /// <param name="inputErrors">
    /// A two-dimensional list representing input errors, where each inner list contains the name
    /// of the field and each proceeding string is an error message related to that field. Optional.
    /// </param>
    /// <param name="errorCode">
    /// A standardized error code representing a known failure condition. Optional.
    /// </param>
    /// <param name="traceId">
    /// The trace identifier to correlate logs and diagnostics for the operation. Optional.
    /// </param>
    ///
    /// <returns>
    /// A new failed <see cref="D2Result"/> instance.
    /// </returns>
    public static D2Result Fail(
        List<string>? messages = null,
        HttpStatusCode? statusCode = null,
        List<List<string>>? inputErrors = null,
        string? errorCode = null,
        string? traceId = null)
        => new(
            false,
            messages,
            inputErrors,
            statusCode,
            errorCode,
            traceId);

    /// <summary>
    /// Factory method to create a validation failed <see cref="D2Result"/> instance.
    /// </summary>
    ///
    /// <param name="messages">
    /// A list of messages related to the validation failure. Optional.
    /// </param>
    /// <param name="inputErrors">
    /// A two-dimensional list representing input errors, where each inner list contains the name
    /// of the field and each proceeding string is an error message related to that field. Optional.
    /// </param>
    /// <param name="errorCode">
    /// Overrides the default <see cref="ErrorCodes.VALIDATION_FAILED"/> code so callers can attach
    /// a more specific code (e.g. "FILES_INVALID_CONTENT_TYPE") for client-side discrimination
    /// without dropping back to raw <see cref="Fail"/>. Optional.
    /// </param>
    /// <param name="traceId">
    /// The trace identifier to correlate logs and diagnostics for the operation. Optional.
    /// </param>
    ///
    /// <returns>
    /// A new validation failed <see cref="D2Result"/> instance.
    /// </returns>
    public static D2Result ValidationFailed(
        List<string>? messages = null,
        List<List<string>>? inputErrors = null,
        string? errorCode = null,
        string? traceId = null)
    {
        messages ??= ["common_errors_VALIDATION_FAILED"];
        return new(
            false,
            messages,
            inputErrors,
            statusCode: HttpStatusCode.BadRequest,
            errorCode: errorCode ?? ErrorCodes.VALIDATION_FAILED,
            traceId: traceId);
    }

    /// <summary>
    /// Factory method to create a service unavailable <see cref="D2Result"/> instance.
    /// </summary>
    ///
    /// <param name="messages">
    /// A list of messages related to the service unavailability. Optional.
    /// </param>
    /// <param name="errorCode">
    /// Overrides the default <see cref="ErrorCodes.SERVICE_UNAVAILABLE"/> code so callers can attach
    /// a more specific code (e.g. a domain-specific retry signal) for downstream discrimination —
    /// e.g. message consumers that branch on the error code to decide between retry and dead-letter
    /// — without dropping back to raw <see cref="Fail"/>. Optional.
    /// </param>
    /// <param name="traceId">
    /// The trace identifier to correlate logs and diagnostics for the operation. Optional.
    /// </param>
    ///
    /// <returns>
    /// A new service unavailable <see cref="D2Result"/> instance.
    /// </returns>
    public static D2Result ServiceUnavailable(
        List<string>? messages = null,
        string? errorCode = null,
        string? traceId = null)
    {
        messages ??= ["common_errors_SERVICE_UNAVAILABLE"];
        return new(
            false,
            messages,
            statusCode: HttpStatusCode.ServiceUnavailable,
            errorCode: errorCode ?? ErrorCodes.SERVICE_UNAVAILABLE,
            traceId: traceId);
    }

    /// <summary>
    /// Factory method to create an unauthorized <see cref="D2Result"/> instance.
    /// </summary>
    ///
    /// <param name="messages">
    /// A list of messages related to the unauthorized access. Optional.
    /// </param>
    /// <param name="traceId">
    /// The trace identifier to correlate logs and diagnostics for the operation. Optional.
    /// </param>
    ///
    /// <returns>
    /// A new unauthorized <see cref="D2Result"/> instance.
    /// </returns>
    public static D2Result Unauthorized(
        List<string>? messages = null,
        string? traceId = null)
    {
        messages ??= ["common_errors_UNAUTHORIZED"];
        return new(
            false,
            messages,
            statusCode: HttpStatusCode.Unauthorized,
            errorCode: ErrorCodes.UNAUTHORIZED,
            traceId: traceId);
    }

    /// <summary>
    /// Factory method to create an unhandled exception <see cref="D2Result"/> instance.
    /// </summary>
    ///
    /// <param name="messages">
    /// A list of messages related to the unhandled exception. Optional.
    /// </param>
    /// <param name="traceId">
    /// The trace identifier to correlate logs and diagnostics for the operation. Optional.
    /// </param>
    ///
    /// <returns>
    /// A new unhandled exception <see cref="D2Result"/> instance.
    /// </returns>
    public static D2Result UnhandledException(
        List<string>? messages = null,
        string? traceId = null)
    {
        messages ??= ["common_errors_unknown"];
        return new(
            false,
            messages,
            statusCode: HttpStatusCode.InternalServerError,
            errorCode: ErrorCodes.UNHANDLED_EXCEPTION,
            traceId: traceId);
    }

    /// <summary>
    /// Factory method to create a payload too large <see cref="D2Result"/> instance.
    /// </summary>
    ///
    /// <param name="messages">
    /// A list of messages related to the operation. Optional.
    /// </param>
    /// <param name="traceId">
    /// The trace identifier to correlate logs and diagnostics for the operation. Optional.
    /// </param>
    ///
    /// <returns>
    /// A new payload too large <see cref="D2Result"/> instance.
    /// </returns>
    public static D2Result PayloadTooLarge(
        List<string>? messages = null,
        string? traceId = null)
    {
        messages ??= ["common_errors_PAYLOAD_TOO_LARGE"];
        return new(
            false,
            messages,
            statusCode: HttpStatusCode.RequestEntityTooLarge,
            errorCode: ErrorCodes.PAYLOAD_TOO_LARGE,
            traceId: traceId);
    }

    /// <summary>
    /// Factory method to create a too many requests (rate limited) <see cref="D2Result"/> instance.
    /// </summary>
    ///
    /// <param name="messages">
    /// A list of messages related to the rate limit. Optional.
    /// </param>
    /// <param name="errorCode">
    /// Overrides the default <see cref="ErrorCodes.RATE_LIMITED"/> code so callers can attach
    /// a more specific code (e.g. "OTP_RATE_LIMITED") for client-side discrimination. Optional.
    /// </param>
    /// <param name="traceId">
    /// The trace identifier to correlate logs and diagnostics for the operation. Optional.
    /// </param>
    ///
    /// <returns>
    /// A new too many requests <see cref="D2Result"/> instance.
    /// </returns>
    public static D2Result TooManyRequests(
        List<string>? messages = null,
        string? errorCode = null,
        string? traceId = null)
    {
        messages ??= ["common_errors_TOO_MANY_REQUESTS"];
        return new(
            false,
            messages,
            statusCode: HttpStatusCode.TooManyRequests,
            errorCode: errorCode ?? ErrorCodes.RATE_LIMITED,
            traceId: traceId);
    }

    /// <summary>
    /// Factory method to create a cancelled <see cref="D2Result"/> instance.
    /// </summary>
    ///
    /// <param name="messages">
    /// A list of messages related to the cancellation. Optional.
    /// </param>
    /// <param name="traceId">
    /// The trace identifier to correlate logs and diagnostics for the operation. Optional.
    /// </param>
    ///
    /// <returns>
    /// A new cancelled <see cref="D2Result"/> instance.
    /// </returns>
    public static D2Result Cancelled(
        List<string>? messages = null,
        string? traceId = null)
    {
        messages ??= ["common_errors_CANCELLED"];
        return new(
            false,
            messages,
            statusCode: HttpStatusCode.BadRequest,
            errorCode: ErrorCodes.CANCELLED,
            traceId: traceId);
    }

    /// <summary>
    /// Factory method to create a not found <see cref="D2Result"/> instance.
    /// </summary>
    ///
    /// <param name="messages">
    /// A list of messages related to the operation. Optional.
    /// </param>
    /// <param name="traceId">
    /// The trace identifier to correlate logs and diagnostics for the operation. Optional.
    /// </param>
    ///
    /// <returns>
    /// A new not found <see cref="D2Result"/> instance.
    /// </returns>
    public static D2Result NotFound(
        List<string>? messages = null,
        string? traceId = null)
    {
        messages ??= ["common_errors_NOT_FOUND"];
        return new(
            false,
            messages,
            statusCode: HttpStatusCode.NotFound,
            errorCode: ErrorCodes.NOT_FOUND,
            traceId: traceId);
    }

    /// <summary>
    /// Factory method to create a forbidden <see cref="D2Result"/> instance.
    /// </summary>
    ///
    /// <param name="messages">
    /// A list of messages related to the operation. Optional.
    /// </param>
    /// <param name="traceId">
    /// The trace identifier to correlate logs and diagnostics for the operation. Optional.
    /// </param>
    ///
    /// <returns>
    /// A new forbidden <see cref="D2Result"/> instance.
    /// </returns>
    public static D2Result Forbidden(
        List<string>? messages = null,
        string? traceId = null)
    {
        messages ??= ["common_errors_FORBIDDEN"];
        return new(
            false,
            messages,
            statusCode: HttpStatusCode.Forbidden,
            errorCode: ErrorCodes.FORBIDDEN,
            traceId: traceId);
    }

    /// <summary>
    /// Factory method to create a conflict <see cref="D2Result"/> instance.
    /// </summary>
    ///
    /// <param name="messages">
    /// A list of messages related to the operation. Optional.
    /// </param>
    /// <param name="traceId">
    /// The trace identifier to correlate logs and diagnostics for the operation. Optional.
    /// </param>
    ///
    /// <returns>
    /// A new conflict <see cref="D2Result"/> instance.
    /// </returns>
    public static D2Result Conflict(
        List<string>? messages = null,
        string? traceId = null)
    {
        messages ??= ["common_errors_CONFLICT"];
        return new(
            false,
            messages,
            statusCode: HttpStatusCode.Conflict,
            errorCode: ErrorCodes.CONFLICT,
            traceId: traceId);
    }

    /// <summary>
    /// Factory method to create a created <see cref="D2Result"/> instance.
    /// </summary>
    ///
    /// <param name="messages">
    /// A list of messages related to the operation. Optional.
    /// </param>
    /// <param name="traceId">
    /// The trace identifier to correlate logs and diagnostics for the operation. Optional.
    /// </param>
    ///
    /// <returns>
    /// A new created <see cref="D2Result"/> instance.
    /// </returns>
    public static D2Result Created(
        List<string>? messages = null,
        string? traceId = null)
    {
        return new(
            true,
            messages,
            statusCode: HttpStatusCode.Created,
            traceId: traceId);
    }

    /// <summary>
    /// Factory method to create a some found <see cref="D2Result"/> instance.
    /// </summary>
    ///
    /// <param name="messages">
    /// A list of messages related to the operation. Optional.
    /// </param>
    /// <param name="traceId">
    /// The trace identifier to correlate logs and diagnostics for the operation. Optional.
    /// </param>
    ///
    /// <remarks>
    /// When this method is used, the <see cref="Success"/> flag is set to <c>false</c> to
    /// indicate that not all requested items were found.
    /// </remarks>
    ///
    /// <returns>
    /// A new some found <see cref="D2Result"/> instance.
    /// </returns>
    public static D2Result SomeFound(
        List<string>? messages = null,
        string? traceId = null)
    {
        messages ??= ["common_errors_SOME_FOUND"];
        return new(
            false,
            messages,
            statusCode: HttpStatusCode.PartialContent,
            errorCode: ErrorCodes.SOME_FOUND,
            traceId: traceId);
    }

    #endregion
}
