// -----------------------------------------------------------------------
// <copyright file="D2Result.Generic.Factories.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Result;

using System.Net;
using D2.Shared.I18n;

/// <summary>
/// Semantic factory methods on <see cref="D2Result{TData}"/>. The generic factories
/// mirror the non-generic ones (<see cref="D2Result"/>) but additionally carry typed
/// payloads, plus <see cref="BubbleFail"/> / <see cref="Bubble"/> for propagating an
/// upstream <see cref="D2Result"/> into a typed result without re-stating its details.
/// </summary>
public sealed partial class D2Result<TData>
{
    /// <summary>
    /// Creates a successful result with optional <paramref name="data"/>.
    /// </summary>
    /// <param name="data">Optional payload.</param>
    /// <param name="messages">Optional translation messages.</param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A successful <see cref="D2Result{TData}"/>.</returns>
    public static D2Result<TData> Ok(
        TData? data = default,
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
        => new(true, data, messages, traceId: traceId);

    /// <summary>
    /// Creates a successful result with HTTP status <see cref="HttpStatusCode.Created"/>.
    /// </summary>
    /// <param name="data">Optional payload (typically the newly-created resource).</param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A created <see cref="D2Result{TData}"/>.</returns>
    public static D2Result<TData> Created(TData? data = default, string? traceId = null)
        => new(true, data, statusCode: HttpStatusCode.Created, traceId: traceId);

    /// <summary>
    /// Creates a failure result. Use only when no semantic factory matches.
    /// </summary>
    /// <param name="messages">Optional translation messages.</param>
    /// <param name="statusCode">
    /// Optional <see cref="HttpStatusCode"/>; defaults to
    /// <see cref="HttpStatusCode.BadRequest"/>.
    /// </param>
    /// <param name="inputErrors">Optional per-field input errors.</param>
    /// <param name="errorCode">Optional standardized error code.</param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A failure <see cref="D2Result{TData}"/>.</returns>
    public static new D2Result<TData> Fail(
        IReadOnlyList<TKMessage>? messages = null,
        HttpStatusCode? statusCode = null,
        IReadOnlyList<InputError>? inputErrors = null,
        string? errorCode = null,
        string? traceId = null)
        => new(false, default, messages, inputErrors, statusCode, errorCode, traceId);

    /// <summary>
    /// Propagates a failed upstream <see cref="D2Result"/> into a typed
    /// <see cref="D2Result{TData}"/>, preserving the upstream messages, input errors,
    /// status code, error code, and trace ID. <see cref="Data"/> is set to <c>default</c>.
    /// </summary>
    /// <param name="d2Result">The upstream result to propagate.</param>
    /// <returns>A typed <see cref="D2Result{TData}"/> mirroring the upstream failure.</returns>
    public static D2Result<TData> BubbleFail(D2Result d2Result)
        => new(
            false,
            default,
            d2Result.Messages,
            d2Result.InputErrors,
            d2Result.StatusCode,
            d2Result.ErrorCode,
            d2Result.TraceId);

    /// <summary>
    /// Propagates an upstream <see cref="D2Result"/> (success OR failure) into a typed
    /// <see cref="D2Result{TData}"/>, preserving its <see cref="D2Result.Success"/>
    /// flag and all metadata, with the supplied <paramref name="data"/> attached.
    /// </summary>
    /// <param name="d2Result">The upstream result to propagate.</param>
    /// <param name="data">Optional payload to attach.</param>
    /// <returns>A typed <see cref="D2Result{TData}"/> mirroring the upstream.</returns>
    public static D2Result<TData> Bubble(D2Result d2Result, TData? data = default)
        => new(
            d2Result.Success,
            data,
            d2Result.Messages,
            d2Result.InputErrors,
            d2Result.StatusCode,
            d2Result.ErrorCode,
            d2Result.TraceId);

    /// <inheritdoc cref="D2Result.NotFound"/>
    public static new D2Result<TData> NotFound(
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.NOT_FOUND];
        return new(
            false,
            default,
            messages,
            statusCode: HttpStatusCode.NotFound,
            errorCode: ErrorCodes.NOT_FOUND,
            traceId: traceId);
    }

    /// <inheritdoc cref="D2Result.Forbidden"/>
    public static new D2Result<TData> Forbidden(
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.FORBIDDEN];
        return new(
            false,
            default,
            messages,
            statusCode: HttpStatusCode.Forbidden,
            errorCode: ErrorCodes.FORBIDDEN,
            traceId: traceId);
    }

    /// <inheritdoc cref="D2Result.Unauthorized"/>
    public static new D2Result<TData> Unauthorized(
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.UNAUTHORIZED];
        return new(
            false,
            default,
            messages,
            statusCode: HttpStatusCode.Unauthorized,
            errorCode: ErrorCodes.UNAUTHORIZED,
            traceId: traceId);
    }

    /// <inheritdoc cref="D2Result.ValidationFailed"/>
    public static new D2Result<TData> ValidationFailed(
        IReadOnlyList<TKMessage>? messages = null,
        IReadOnlyList<InputError>? inputErrors = null,
        string? errorCode = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.VALIDATION_FAILED];
        return new(
            false,
            default,
            messages,
            inputErrors,
            statusCode: HttpStatusCode.BadRequest,
            errorCode: errorCode ?? ErrorCodes.VALIDATION_FAILED,
            traceId: traceId);
    }

    /// <inheritdoc cref="D2Result.Conflict"/>
    public static new D2Result<TData> Conflict(
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.CONFLICT];
        return new(
            false,
            default,
            messages,
            statusCode: HttpStatusCode.Conflict,
            errorCode: ErrorCodes.CONFLICT,
            traceId: traceId);
    }

    /// <inheritdoc cref="D2Result.ServiceUnavailable"/>
    public static new D2Result<TData> ServiceUnavailable(
        IReadOnlyList<TKMessage>? messages = null,
        string? errorCode = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.SERVICE_UNAVAILABLE];
        return new(
            false,
            default,
            messages,
            statusCode: HttpStatusCode.ServiceUnavailable,
            errorCode: errorCode ?? ErrorCodes.SERVICE_UNAVAILABLE,
            traceId: traceId);
    }

    /// <inheritdoc cref="D2Result.UnhandledException"/>
    public static new D2Result<TData> UnhandledException(
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.UNKNOWN];
        return new(
            false,
            default,
            messages,
            statusCode: HttpStatusCode.InternalServerError,
            errorCode: ErrorCodes.UNHANDLED_EXCEPTION,
            traceId: traceId);
    }

    /// <inheritdoc cref="D2Result.PayloadTooLarge"/>
    public static new D2Result<TData> PayloadTooLarge(
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.PAYLOAD_TOO_LARGE];
        return new(
            false,
            default,
            messages,
            statusCode: HttpStatusCode.RequestEntityTooLarge,
            errorCode: ErrorCodes.PAYLOAD_TOO_LARGE,
            traceId: traceId);
    }

    /// <inheritdoc cref="D2Result.TooManyRequests"/>
    public static new D2Result<TData> TooManyRequests(
        IReadOnlyList<TKMessage>? messages = null,
        string? errorCode = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.TOO_MANY_REQUESTS];
        return new(
            false,
            default,
            messages,
            statusCode: HttpStatusCode.TooManyRequests,
            errorCode: errorCode ?? ErrorCodes.RATE_LIMITED,
            traceId: traceId);
    }

    /// <inheritdoc cref="D2Result.Canceled"/>
    public static new D2Result<TData> Canceled(
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.CANCELED];
        return new(
            false,
            default,
            messages,
            statusCode: HttpStatusCode.BadRequest,
            errorCode: ErrorCodes.CANCELED,
            traceId: traceId);
    }

    /// <summary>
    /// Creates a partial-success result (HTTP 206, error code
    /// <see cref="ErrorCodes.SOME_FOUND"/>) carrying the partial
    /// <paramref name="data"/>. <see cref="D2Result.Success"/> is <c>false</c> on the
    /// partial-success ladder (NOT_FOUND → SOME_FOUND → OK) — only fully-found
    /// queries succeed.
    /// </summary>
    /// <param name="data">Optional partial payload.</param>
    /// <param name="messages">
    /// Optional translation messages; defaults to <c>[TK.Common.Errors.SOME_FOUND]</c>.
    /// </param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A partial-success <see cref="D2Result{TData}"/>.</returns>
    public static D2Result<TData> SomeFound(
        TData? data = default,
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.SOME_FOUND];
        return new(
            false,
            data,
            messages,
            statusCode: HttpStatusCode.PartialContent,
            errorCode: ErrorCodes.SOME_FOUND,
            traceId: traceId);
    }

    /// <summary>
    /// Creates a partial-success result (HTTP 207 Multi-Status, error code
    /// <see cref="ErrorCodes.PARTIAL_SUCCESS"/>) for a multi-target write
    /// where some targets succeeded and others failed.
    /// <see cref="D2Result.Success"/> is <c>true</c> — the operation did
    /// partially succeed. Callers inspect <c>IsPartialSuccess</c> and the
    /// payload to decide on retry / compensation for the failed target(s).
    /// </summary>
    /// <param name="data">Outcome payload describing which targets succeeded.</param>
    /// <param name="messages">
    /// Optional translation messages; defaults to <c>[TK.Common.Errors.PARTIAL_SUCCESS]</c>.
    /// </param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A partial-success <see cref="D2Result{TData}"/>.</returns>
    public static D2Result<TData> PartialSuccess(
        TData? data = default,
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.PARTIAL_SUCCESS];
        return new(
            true,
            data,
            messages,
            statusCode: HttpStatusCode.MultiStatus,
            errorCode: ErrorCodes.PARTIAL_SUCCESS,
            traceId: traceId);
    }
}
