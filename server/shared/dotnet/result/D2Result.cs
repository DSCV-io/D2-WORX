// -----------------------------------------------------------------------
// <copyright file="D2Result.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Result;

using System.Net;
using D2.Shared.I18n;

/// <summary>
/// Represents the result of an operation, including success status, messages, errors,
/// and related metadata. The non-generic base type for results that do not carry a payload.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Messages"/> entries are <see cref="TKMessage"/> instances — translation
/// keys with optional parameter bindings. The wire format ships them as
/// <c>{ "key": "..." }</c> objects; SvelteKit / browser-side Paraglide translates
/// them on receipt. Server-side translation (for outbound notifications) goes
/// through <see cref="ITranslator"/>.
/// </para>
/// <para>
/// Producers obtain <see cref="TKMessage"/> instances exclusively via the
/// SrcGen-emitted <c>TK</c> constants (e.g. <c>TK.Common.Errors.NOT_FOUND</c>);
/// the type system makes "untranslated literal in <c>Messages</c>" structurally
/// unrepresentable.
/// </para>
/// </remarks>
public partial class D2Result
{
    /// <summary>
    /// Initializes a new instance of the <see cref="D2Result"/> class.
    /// </summary>
    /// <param name="success">
    /// Whether the operation was successful. Required.
    /// </param>
    /// <param name="messages">
    /// Translation messages related to the operation. Optional; defaults to empty.
    /// </param>
    /// <param name="inputErrors">
    /// Per-field input validation errors. Optional; defaults to empty.
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
        IReadOnlyList<TKMessage>? messages = null,
        IReadOnlyList<InputError>? inputErrors = null,
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
    /// Gets the translation messages related to the operation. Each message is a
    /// <see cref="TKMessage"/> (translation key + optional parameter bindings).
    /// </summary>
    public IReadOnlyList<TKMessage> Messages { get; }

    /// <summary>
    /// Gets the per-field input validation errors. Each <see cref="InputError"/>
    /// pairs a field name with one or more <see cref="TKMessage"/> entries
    /// describing what's wrong with that field.
    /// </summary>
    public IReadOnlyList<InputError> InputErrors { get; }

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

    /// <summary>
    /// Returns a new <see cref="D2Result"/> with the same shape (Success,
    /// Messages, InputErrors, StatusCode, ErrorCode) but with the supplied
    /// <paramref name="traceId"/> in place of the original. Used by
    /// <c>BaseHandler.RunCorePipelineAsync</c> to auto-inject the request
    /// trace id on every result that crosses the handler boundary, so
    /// handlers don't have to thread it through every <c>D2Result.Ok(...)</c>
    /// call site.
    /// </summary>
    /// <param name="traceId">The trace id to attach.</param>
    /// <returns>A new <see cref="D2Result"/> with <paramref name="traceId"/> applied.</returns>
    public D2Result WithTraceId(string? traceId)
        => new(Success, Messages, InputErrors, StatusCode, ErrorCode, traceId);
}
