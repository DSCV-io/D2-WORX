// -----------------------------------------------------------------------
// <copyright file="D2Result.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Result;

using System.Net;

/// <summary>
/// Represents the result of an operation, including success status, messages, errors,
/// and related metadata. The non-generic base type for results that do not carry a payload.
/// </summary>
/// <remarks>
/// Default failure messages are TK translation key strings (e.g. <c>"common_errors_NOT_FOUND"</c>)
/// rather than English prose. The translation middleware resolves these keys to locale-appropriate
/// text before they reach the client. Keys are hardcoded here instead of referencing a TK constants
/// class to keep <c>D2.Shared.Result</c> free of an <c>I18n</c> dependency.
/// </remarks>
public partial class D2Result
{
    /// <summary>
    /// Initializes a new instance of the <see cref="D2Result"/> class.
    /// </summary>
    ///
    /// <param name="success">
    /// Whether the operation was successful. Required.
    /// </param>
    /// <param name="messages">
    /// Messages related to the operation. Optional; defaults to empty list.
    /// </param>
    /// <param name="inputErrors">
    /// Two-dimensional list of input errors. Each inner list begins with the field name
    /// followed by one or more error messages for that field. Optional; defaults to empty list.
    /// </param>
    /// <param name="statusCode">
    /// The <see cref="HttpStatusCode"/> for the operation. Optional; defaults to
    /// <see cref="HttpStatusCode.OK"/> on success and <see cref="HttpStatusCode.BadRequest"/>
    /// on failure.
    /// </param>
    /// <param name="errorCode">
    /// A standardized error code for known failure conditions. Optional.
    /// </param>
    /// <param name="traceId">
    /// Trace identifier for correlating logs and diagnostics. Optional.
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
    /// Gets the list of messages related to the operation.
    /// </summary>
    public List<string> Messages { get; }

    /// <summary>
    /// Gets the two-dimensional list of input errors. Each inner list begins with the
    /// field name, followed by one or more error messages for that field. Allows multiple
    /// errors per field while letting clients group them by field for display.
    /// </summary>
    public List<List<string>> InputErrors { get; }

    /// <summary>
    /// Gets the <see cref="HttpStatusCode"/> for the operation.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Gets the standardized error code, if applicable.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Gets the trace identifier for correlating logs and diagnostics, if available.
    /// </summary>
    public string? TraceId { get; }
}
