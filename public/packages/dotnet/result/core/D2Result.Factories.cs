// -----------------------------------------------------------------------
// <copyright file="D2Result.Factories.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Result;

using System.Net;
using DcsvIo.D2.ErrorCodes.Category;
using DcsvIo.D2.I18n;

/// <summary>
/// Hand-rolled factory methods on <see cref="D2Result"/> that are NOT derived
/// from the error-code spec — the success / raw / data-carrying factories
/// (<see cref="Ok"/> / <see cref="Created"/> / <see cref="Fail"/> /
/// <see cref="SomeFound"/>). The spec-derived semantic failure factories
/// (<c>NotFound</c> / <c>Forbidden</c> / …) are generated onto the same partial
/// class from <c>contracts/error-codes/error-codes.spec.json</c> — see
/// <c>D2Result.Factories.g.cs</c>. Always prefer a semantic factory over the
/// raw <see cref="Fail"/> when one matches the failure mode.
/// </summary>
public partial class D2Result
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A successful <see cref="D2Result"/>.</returns>
    public static D2Result Ok(string? traceId = null) => new(true, traceId: traceId);

    /// <summary>
    /// Creates a successful result with HTTP status
    /// <see cref="HttpStatusCode.Created"/>. Use when the operation produced a
    /// new resource (POST endpoints, etc.).
    /// </summary>
    /// <param name="messages">Optional translation messages.</param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A created <see cref="D2Result"/>.</returns>
    public static D2Result Created(
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
        => new(true, messages, statusCode: HttpStatusCode.Created, traceId: traceId);

    /// <summary>
    /// Creates a failure result with the supplied details. Use only when no
    /// semantic factory matches the failure mode — semantic factories should
    /// be preferred.
    /// </summary>
    /// <param name="messages">Optional translation messages.</param>
    /// <param name="statusCode">
    /// Optional <see cref="HttpStatusCode"/>; defaults to
    /// <see cref="HttpStatusCode.BadRequest"/>.
    /// </param>
    /// <param name="inputErrors">Optional per-field input errors.</param>
    /// <param name="errorCode">Optional standardized error code.</param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A failure <see cref="D2Result"/>.</returns>
    public static D2Result Fail(
        IReadOnlyList<TKMessage>? messages = null,
        HttpStatusCode? statusCode = null,
        IReadOnlyList<InputError>? inputErrors = null,
        string? errorCode = null,
        string? traceId = null)
        => new(false, messages, inputErrors, statusCode, errorCode, traceId);

    /// <summary>
    /// Creates a partial-success result (HTTP 206, error code
    /// <see cref="ErrorCodes.SOME_FOUND"/>). <see cref="Success"/> is
    /// <c>false</c> on the partial-success ladder
    /// (NOT_FOUND → SOME_FOUND → OK) — only fully-found queries succeed.
    /// </summary>
    /// <param name="messages">
    /// Optional translation messages; defaults to
    /// <c>[TK.Common.Errors.SOME_FOUND]</c>.
    /// </param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A partial-success <see cref="D2Result"/>.</returns>
    public static D2Result SomeFound(
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.SOME_FOUND];
        return new(
            false,
            messages,
            statusCode: HttpStatusCode.PartialContent,
            errorCode: ErrorCodes.SOME_FOUND,
            traceId: traceId,
            category: ErrorCategory.PartialSuccess);
    }
}
