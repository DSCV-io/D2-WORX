// -----------------------------------------------------------------------
// <copyright file="D2Result.Factories.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Result;

using System.Net;
using D2.Shared.I18n;

/// <summary>
/// Semantic factory methods on <see cref="D2Result"/>. Always prefer a
/// semantic factory (e.g. <see cref="NotFound"/>) over the raw <see cref="Fail"/>
/// when one matches the failure mode — semantic factories carry the canonical
/// status code, error code, and default <see cref="TKMessage"/> together.
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
    /// Creates a not-found failure (HTTP 404, error code
    /// <see cref="ErrorCodes.NOT_FOUND"/>).
    /// </summary>
    /// <param name="messages">
    /// Optional translation messages; defaults to <c>[TK.Common.Errors.NOT_FOUND]</c>.
    /// </param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A not-found <see cref="D2Result"/>.</returns>
    public static D2Result NotFound(
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.NOT_FOUND];
        return new(
            false,
            messages,
            statusCode: HttpStatusCode.NotFound,
            errorCode: ErrorCodes.NOT_FOUND,
            traceId: traceId);
    }

    /// <summary>
    /// Creates a forbidden failure (HTTP 403, error code
    /// <see cref="ErrorCodes.FORBIDDEN"/>).
    /// </summary>
    /// <param name="messages">
    /// Optional translation messages; defaults to <c>[TK.Common.Errors.FORBIDDEN]</c>.
    /// </param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A forbidden <see cref="D2Result"/>.</returns>
    public static D2Result Forbidden(
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.FORBIDDEN];
        return new(
            false,
            messages,
            statusCode: HttpStatusCode.Forbidden,
            errorCode: ErrorCodes.FORBIDDEN,
            traceId: traceId);
    }

    /// <summary>
    /// Creates an unauthorized failure (HTTP 401, error code
    /// <see cref="ErrorCodes.UNAUTHORIZED"/>).
    /// </summary>
    /// <param name="messages">
    /// Optional translation messages; defaults to <c>[TK.Common.Errors.UNAUTHORIZED]</c>.
    /// </param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>An unauthorized <see cref="D2Result"/>.</returns>
    public static D2Result Unauthorized(
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.UNAUTHORIZED];
        return new(
            false,
            messages,
            statusCode: HttpStatusCode.Unauthorized,
            errorCode: ErrorCodes.UNAUTHORIZED,
            traceId: traceId);
    }

    /// <summary>
    /// Creates a validation-failed result (HTTP 400, error code
    /// <see cref="ErrorCodes.VALIDATION_FAILED"/> by default — overridable for
    /// domain-specific discrimination).
    /// </summary>
    /// <param name="messages">
    /// Optional translation messages; defaults to
    /// <c>[TK.Common.Errors.VALIDATION_FAILED]</c>.
    /// </param>
    /// <param name="inputErrors">Optional per-field input errors.</param>
    /// <param name="errorCode">
    /// Optional override for the default <see cref="ErrorCodes.VALIDATION_FAILED"/>
    /// code so callers can attach a more specific code (e.g.
    /// <c>"FILES_INVALID_CONTENT_TYPE"</c>) for client-side discrimination
    /// without dropping back to raw <see cref="Fail"/>.
    /// </param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A validation-failed <see cref="D2Result"/>.</returns>
    public static D2Result ValidationFailed(
        IReadOnlyList<TKMessage>? messages = null,
        IReadOnlyList<InputError>? inputErrors = null,
        string? errorCode = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.VALIDATION_FAILED];
        return new(
            false,
            messages,
            inputErrors,
            statusCode: HttpStatusCode.BadRequest,
            errorCode: errorCode ?? ErrorCodes.VALIDATION_FAILED,
            traceId: traceId);
    }

    /// <summary>
    /// Creates a conflict failure (HTTP 409, error code
    /// <see cref="ErrorCodes.CONFLICT"/>).
    /// </summary>
    /// <param name="messages">
    /// Optional translation messages; defaults to <c>[TK.Common.Errors.CONFLICT]</c>.
    /// </param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A conflict <see cref="D2Result"/>.</returns>
    public static D2Result Conflict(
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.CONFLICT];
        return new(
            false,
            messages,
            statusCode: HttpStatusCode.Conflict,
            errorCode: ErrorCodes.CONFLICT,
            traceId: traceId);
    }

    /// <summary>
    /// Creates a service-unavailable failure (HTTP 503, error code
    /// <see cref="ErrorCodes.SERVICE_UNAVAILABLE"/> by default — overridable
    /// for downstream discrimination).
    /// </summary>
    /// <param name="messages">
    /// Optional translation messages; defaults to
    /// <c>[TK.Common.Errors.SERVICE_UNAVAILABLE]</c>.
    /// </param>
    /// <param name="errorCode">
    /// Optional override for the default <see cref="ErrorCodes.SERVICE_UNAVAILABLE"/>
    /// code so callers can attach a more specific retry signal — e.g. message
    /// consumers that branch on the error code to decide between retry and
    /// dead-letter — without dropping back to raw <see cref="Fail"/>.
    /// </param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A service-unavailable <see cref="D2Result"/>.</returns>
    public static D2Result ServiceUnavailable(
        IReadOnlyList<TKMessage>? messages = null,
        string? errorCode = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.SERVICE_UNAVAILABLE];
        return new(
            false,
            messages,
            statusCode: HttpStatusCode.ServiceUnavailable,
            errorCode: errorCode ?? ErrorCodes.SERVICE_UNAVAILABLE,
            traceId: traceId);
    }

    /// <summary>
    /// Creates an unhandled-exception failure (HTTP 500, error code
    /// <see cref="ErrorCodes.UNHANDLED_EXCEPTION"/>).
    /// </summary>
    /// <param name="messages">
    /// Optional translation messages; defaults to <c>[TK.Common.Errors.UNKNOWN]</c>.
    /// </param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>An unhandled-exception <see cref="D2Result"/>.</returns>
    public static D2Result UnhandledException(
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.UNKNOWN];
        return new(
            false,
            messages,
            statusCode: HttpStatusCode.InternalServerError,
            errorCode: ErrorCodes.UNHANDLED_EXCEPTION,
            traceId: traceId);
    }

    /// <summary>
    /// Creates a payload-too-large failure (HTTP 413, error code
    /// <see cref="ErrorCodes.PAYLOAD_TOO_LARGE"/>).
    /// </summary>
    /// <param name="messages">
    /// Optional translation messages; defaults to
    /// <c>[TK.Common.Errors.PAYLOAD_TOO_LARGE]</c>.
    /// </param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A payload-too-large <see cref="D2Result"/>.</returns>
    public static D2Result PayloadTooLarge(
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.PAYLOAD_TOO_LARGE];
        return new(
            false,
            messages,
            statusCode: HttpStatusCode.RequestEntityTooLarge,
            errorCode: ErrorCodes.PAYLOAD_TOO_LARGE,
            traceId: traceId);
    }

    /// <summary>
    /// Creates a too-many-requests / rate-limited failure (HTTP 429, error code
    /// <see cref="ErrorCodes.RATE_LIMITED"/> by default — overridable for
    /// client-side discrimination).
    /// </summary>
    /// <param name="messages">
    /// Optional translation messages; defaults to
    /// <c>[TK.Common.Errors.TOO_MANY_REQUESTS]</c>.
    /// </param>
    /// <param name="errorCode">
    /// Optional override for the default <see cref="ErrorCodes.RATE_LIMITED"/>
    /// code so callers can attach a more specific code
    /// (e.g. <c>"OTP_RATE_LIMITED"</c>) for client-side discrimination.
    /// </param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A too-many-requests <see cref="D2Result"/>.</returns>
    public static D2Result TooManyRequests(
        IReadOnlyList<TKMessage>? messages = null,
        string? errorCode = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.TOO_MANY_REQUESTS];
        return new(
            false,
            messages,
            statusCode: HttpStatusCode.TooManyRequests,
            errorCode: errorCode ?? ErrorCodes.RATE_LIMITED,
            traceId: traceId);
    }

    /// <summary>
    /// Creates a canceled failure (HTTP 400, error code
    /// <see cref="ErrorCodes.CANCELED"/>).
    /// </summary>
    /// <param name="messages">
    /// Optional translation messages; defaults to <c>[TK.Common.Errors.CANCELED]</c>.
    /// </param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A canceled <see cref="D2Result"/>.</returns>
    public static D2Result Canceled(
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.CANCELED];
        return new(
            false,
            messages,
            statusCode: HttpStatusCode.BadRequest,
            errorCode: ErrorCodes.CANCELED,
            traceId: traceId);
    }

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
            traceId: traceId);
    }
}
